using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SmallBusinessUpdater.Services;

internal static class SelfUpdateService
{
    private static bool checking;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static async Task CheckAsync()
    {
        if (checking) return;
        checking = true;
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Small Business Updater");
            if (!string.Equals(Path.GetDirectoryName(Environment.ProcessPath), directory, StringComparison.OrdinalIgnoreCase))
            {
                LogService.Write("self-update.log", "Samoczynna aktualizacja wymaga instalacji dla bieżącego użytkownika.");
                return;
            }
            string cache = Path.Combine(AppPaths.LocalDataDirectory, "SelfUpdate");
            Directory.CreateDirectory(cache);
            using var updateLock = new FileStream(Path.Combine(cache, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/repos/CezaryMal/SmallBusinessUpdater/releases/latest");
            request.Headers.UserAgent.ParseAdd("SmallBusinessUpdater/1.0");
            using var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var release = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = release.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return;
            string tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return;
            var current = Assembly.GetExecutingAssembly().GetName().Version!;
            var normalized = new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
            if (normalized <= current) return;
            string name = $"SmallBusinessUpdater_Setup_v{normalized}.exe";
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != name) continue;
                string url = asset.GetProperty("browser_download_url").GetString() ?? "";
                if (!url.StartsWith("https://github.com/CezaryMal/SmallBusinessUpdater/releases/download/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Nieprawidłowy adres instalatora.");
                if (!asset.TryGetProperty("digest", out var digestValue))
                    throw new InvalidDataException("Brak sumy SHA256 instalatora w GitHub Release.");
                string digest = digestValue.GetString() ?? "";
                if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Brak sumy SHA256 instalatora.");
                long size = asset.GetProperty("size").GetInt64();
                if (size < 1 || size > 250_000_000) throw new InvalidDataException("Nieprawidłowy rozmiar instalatora.");
                byte[] bytes = await Client.GetByteArrayAsync(url);
                if (bytes.LongLength != size || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), digest[7..], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Suma lub rozmiar instalatora nie zgadza się z GitHub Release.");
                string installer = Path.Combine(cache, name);
                await File.WriteAllBytesAsync(installer + ".part", bytes);
                File.Move(installer + ".part", installer, true);
                string script = "$ErrorActionPreference='Stop'; " +
                    $"Wait-Process -Id {Environment.ProcessId} -Timeout 120 -ErrorAction SilentlyContinue; " +
                    $"if (Get-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue) {{ exit 1 }}; " +
                    "$p=Start-Process -FilePath '" + installer.Replace("'", "''") +
                    "' -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /SP- /NORESTART /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS /LOG' -Wait -PassThru; " +
                    "Add-Content -LiteralPath '" + Path.Combine(cache, "install-result.log").Replace("'", "''") + "' -Value ((Get-Date).ToString('o') + ' exit=' + $p.ExitCode); exit $p.ExitCode";
                var helper = Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                    Arguments = "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (helper == null) throw new IOException("Nie udało się uruchomić aktualizacji.");
                helper.Dispose();
                LogService.Write("self-update.log", $"Przekazano instalację wersji {normalized} do procesu w tle.");
                Application.Current.Shutdown();
                return;
            }
            LogService.Write("self-update.log", "Nowsze wydanie nie ma pasującego instalatora.");
        }
        catch (Exception ex) { LogService.Write("self-update.log", ex.ToString()); }
        finally { checking = false; }
    }
}

