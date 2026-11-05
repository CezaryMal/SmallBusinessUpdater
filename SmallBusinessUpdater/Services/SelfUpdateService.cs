using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SmallBusinessUpdater.Services;

internal sealed record SelfUpdateResult(bool Success, string Message, bool InstallationStarted = false);

internal static class SelfUpdateService
{
    private static bool checking;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static async Task<SelfUpdateResult> CheckAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (checking) return new(false, "Sprawdzanie aktualizacji Updatera już trwa.");
        checking = true;
        string? partialPath = null;
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Small Business Updater");
            if (!string.Equals(Path.GetDirectoryName(Environment.ProcessPath), directory, StringComparison.OrdinalIgnoreCase))
            {
                const string message = "Aktualizacja wymaga zainstalowanej kopii Updatera w folderze bieżącego użytkownika.";
                LogService.Write("self-update.log", message);
                return new(false, message);
            }
            string cache = Path.Combine(AppPaths.LocalDataDirectory, "SelfUpdate");
            Directory.CreateDirectory(cache);
            using var updateLock = new FileStream(Path.Combine(cache, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            SelfUpdateArchiveService.Clean(cache);
            progress?.Report("Sprawdzanie aktualizacji Updatera…");
            byte[]? metadata = await NetworkRetryService.RunAsync(async token =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://api.github.com/repos/CezaryMal/SmallBusinessUpdater/releases/latest");
                request.Headers.UserAgent.ParseAdd("SmallBusinessUpdater/1.0");
                using var response = await Client.SendAsync(request, token);
                if (response.StatusCode == HttpStatusCode.NotFound) return null;
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(token);
            }, progress, cancellationToken);
            if (metadata == null)
                return new(true, "Nie znaleziono opublikowanego wydania Updatera. Możesz nadal używać tej wersji.");
            using var release = JsonDocument.Parse(metadata);
            var root = release.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
                return new(true, "Nie ma nowego stabilnego wydania Updatera.");
            string tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version))
                return new(false, "Wydanie GitHuba ma nieprawidłowy numer wersji.");
            var current = Assembly.GetExecutingAssembly().GetName().Version!;
            var normalized = new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
            if (normalized <= current)
                return new(true, $"Updater jest aktualny. Zainstalowana wersja to {current}.");
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
                if (size < 1 || size > 250_000_000)
                    throw new InvalidDataException("Nieprawidłowy rozmiar instalatora.");
                progress?.Report("Pobieranie aktualizacji Updatera…");
                byte[] bytes = await NetworkRetryService.RunAsync(
                    token => Client.GetByteArrayAsync(url, token), progress, cancellationToken);
                if (bytes.LongLength != size ||
                    !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), digest[7..], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Suma lub rozmiar instalatora nie zgadza się z GitHub Release.");
                string installer = Path.Combine(cache, name);
                partialPath = installer + ".part";
                await File.WriteAllBytesAsync(partialPath, bytes, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(partialPath, installer, true);
                SelfUpdateArchiveService.Clean(cache, installer);
                string script = "$ErrorActionPreference='Stop'; " +
                    $"Wait-Process -Id {Environment.ProcessId} -Timeout 120 -ErrorAction SilentlyContinue; " +
                    $"if (Get-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue) {{ exit 1 }}; " +
                    "$p=Start-Process -FilePath '" + installer.Replace("'", "''") +
                    "' -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /SP- /NORESTART /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS /LOG' -Wait -PassThru; " +
                    "Add-Content -LiteralPath '" + Path.Combine(cache, "install-result.log").Replace("'", "''") +
                    "' -Value ((Get-Date).ToString('o') + ' exit=' + $p.ExitCode); exit $p.ExitCode";
                cancellationToken.ThrowIfCancellationRequested();
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
                return new(true, $"Rozpoczęto instalowanie wersji {normalized}.", true);
            }
            const string missing = "Nowsze wydanie nie ma pasującego instalatora. Spróbuj później.";
            LogService.Write("self-update.log", missing);
            return new(false, missing);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(false, "Sprawdzanie aktualizacji Updatera zostało anulowane.");
        }
        catch (Exception ex)
        {
            LogService.Write("self-update.log", ex.ToString());
            return new(false, "Nie udało się sprawdzić lub pobrać aktualizacji Updatera. Sprawdź połączenie i spróbuj ponownie. Szczegóły znajdziesz w logach.");
        }
        finally
        {
            if (partialPath != null)
            {
                try { File.Delete(partialPath); }
                catch (IOException ex) { LogService.Write("self-update.log", ex.Message); }
                catch (UnauthorizedAccessException ex) { LogService.Write("self-update.log", ex.Message); }
            }
            checking = false;
        }
    }
}
