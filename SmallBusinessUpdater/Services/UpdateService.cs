using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SmallBusinessUpdater.Models;

namespace SmallBusinessUpdater.Services;

public static class UpdateService
{
	private static readonly HttpClient HttpClient = new HttpClient
	{
		Timeout = TimeSpan.FromMinutes(5L)
	};

	public static async Task<UpdateCheckResult> CheckAndDownloadAsync(AppConfig config, IProgress<string>? progress = null)
	{
		AppPaths.EnsureDirectories();
		progress?.Report("Sprawdzanie aktualizacji...");
		string nip = new string(config.Nip.Where(char.IsDigit).ToArray());
		if (string.IsNullOrWhiteSpace(config.SymplexId))
		{
			return new UpdateCheckResult
			{
				Success = false,
				Message = "Brak ID Symplex."
			};
		}
		if (nip.Length != 10)
		{
			return new UpdateCheckResult
			{
				Success = false,
				Message = "Nieprawidłowy NIP."
			};
		}
		string[] array = new string[4] { "https://www.symplex.eu", "https://symplex.eu", "https://www.symplex.pl", "https://symplex.pl" };
		string[] array2 = array;
		foreach (string value in array2)
		{
			string destination = null;
			try
			{
				progress?.Report("Pobieranie aktualizacji...");
				string requestUri = $"{value}/user/get_program.php?nipu={Uri.EscapeDataString(nip)}&symboll={Uri.EscapeDataString(config.SymplexId)}";
				using HttpResponseMessage response = await HttpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);
				if (!response.IsSuccessStatusCode)
				{
					continue;
				}
				byte[] bytes = await response.Content.ReadAsByteArrayAsync();
				if (bytes.Length < 1000000 || bytes.Length < 2 || bytes[0] != 77 || bytes[1] != 90)
				{
					continue;
				}
				string fileName = GetFileNameFromResponse(response);
				if (string.IsNullOrWhiteSpace(fileName))
				{
					fileName = $"inst_sb_{DateTime.Now:yyyyMMdd_HHmmss}.exe";
				}
				fileName = Path.GetFileName(fileName);
				destination = Path.Combine(AppPaths.DownloadsDirectory, fileName);
				await File.WriteAllBytesAsync(destination, bytes);
				progress?.Report("Weryfikowanie aktualizacji...");
				if (!AuthenticodeHelper.IsSignatureValid(destination))
				{
					DeleteFileSafe(destination);
					return new UpdateCheckResult
					{
						Success = false,
						Message = "Pobrany instalator ma nieprawidłowy podpis cyfrowy."
					};
				}
				X509Certificate2 signerCertificate = AuthenticodeHelper.GetSignerCertificate(destination);
				if (signerCertificate == null)
				{
					DeleteFileSafe(destination);
					return new UpdateCheckResult
					{
						Success = false,
						Message = "Nie udało się odczytać certyfikatu instalatora."
					};
				}
				string text = signerCertificate.Subject ?? "";
				if (!text.Contains("Symplex", StringComparison.OrdinalIgnoreCase))
				{
					DeleteFileSafe(destination);
					return new UpdateCheckResult
					{
						Success = false,
						Message = "Instalator nie jest podpisany przez Symplex. Podpis: " + text
					};
				}
				FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(destination);
				string productVersion = versionInfo.ProductVersion ?? "";
				string fileVersion = versionInfo.FileVersion ?? "";
				string installerHash = Convert.ToHexString(SHA256.HashData(bytes));
				int buildNumber = GetBuildNumber(fileName, productVersion);
				if (buildNumber <= 0)
				{
					DeleteFileSafe(destination);
					return new UpdateCheckResult
					{
						Success = false,
						Message = "Nie udało się rozpoznać numeru buildu instalatora."
					};
				}
				UpdateState updateState = VersionStateService.Load();
				bool flag = updateState.InstalledBuild != buildNumber;
				string message = ((!flag) ? $"Build {buildNumber} jest już zainstalowany." : $"Dostępna aktualizacja: build {buildNumber}.");
				return new UpdateCheckResult
				{
					Success = true,
					UpdateAvailable = flag,
					Message = message,
					InstallerPath = destination,
					InstallerHash = installerHash,
					ProductVersion = productVersion,
					FileVersion = fileVersion,
					SignerName = text,
					BuildNumber = buildNumber,
					InstalledBuild = updateState.InstalledBuild
				};
			}
			catch
			{
				if (!string.IsNullOrWhiteSpace(destination))
				{
					DeleteFileSafe(destination);
				}
			}
		}
		return new UpdateCheckResult
		{
			Success = false,
			UpdateAvailable = false,
			Message = "Nie udało się pobrać aktualizacji z serwera Symplex."
		};
	}

	private static int GetBuildNumber(string fileName, string productVersion)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
		if (fileNameWithoutExtension.StartsWith("inst_sb", StringComparison.OrdinalIgnoreCase) && int.TryParse(new string(fileNameWithoutExtension.Substring("inst_sb".Length).TakeWhile(char.IsDigit).ToArray()), out var result))
		{
			return result;
		}
		if (!string.IsNullOrWhiteSpace(productVersion))
		{
			string[] array = productVersion.Split('.');
			if (array.Length >= 4 && int.TryParse(array[^1], out var result2))
			{
				return result2;
			}
		}
		return 0;
	}

	private static string? GetFileNameFromResponse(HttpResponseMessage response)
	{
		string text = response.Content.Headers.ContentDisposition?.FileNameStar;
		if (text != null)
		{
			return text.Trim('"');
		}
		return (response.Content.Headers.ContentDisposition?.FileName)?.Trim('"');
	}

	private static void DeleteFileSafe(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}
}
