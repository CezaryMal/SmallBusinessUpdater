using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SmallBusinessUpdater.Models;

namespace SmallBusinessUpdater.Services;

public static class CheckService
{
	public static async Task<CheckRunResult> RunAsync()
	{
		AppPaths.EnsureDirectories();
		using UpdateLockService updateLock = UpdateLockService.TryAcquire();
		if (!updateLock.Acquired)
		{
			Log("Inna operacja aktualizacji jest już uruchomiona.");
			return CheckRunResult.AlreadyRunning;
		}
		AppConfig config = ConfigService.Load();
		if (!IsConfigValid(config))
		{
			Log("Konfiguracja jest niepełna lub nieprawidłowa.");
			return CheckRunResult.ConfigurationInvalid;
		}
		if (DateTime.Today >= config.UpdateEndDate.Date)
		{
			Log($"Okres aktualizacji wygasł: {config.UpdateEndDate:yyyy-MM-dd}");
			return CheckRunResult.UpdatePeriodExpired;
		}
		try
		{
			Log("Rozpoczynam ręczne sprawdzanie aktualizacji.");
			UpdateCheckResult result = await UpdateService.CheckAndDownloadAsync(config);
			if (!result.Success)
			{
				Log("Błąd sprawdzania aktualizacji: " + result.Message);
				return CheckRunResult.Failed;
			}
			if (!result.UpdateAvailable)
			{
				Log($"Brak nowej aktualizacji. Build {result.BuildNumber} jest już zainstalowany.");
				if (config.LaunchAfterUpdate) SmallBusinessService.Start(config.SmallBusinessPath);
				return CheckRunResult.Completed;
			}
			if (string.IsNullOrWhiteSpace(result.InstallerPath))
			{
				Log("Dostępna jest aktualizacja, ale nie otrzymano ścieżki instalatora.");
				return CheckRunResult.Failed;
			}
			Log($"Dostępna aktualizacja: build {result.BuildNumber}, wersja {result.ProductVersion}.");
			InstallResult installResult = await InstallerService.InstallAsync(result.InstallerPath, config.SmallBusinessPath, config.LaunchAfterUpdate);
			if (!installResult.Success)
			{
				Log("Instalacja nie powiodła się: " + installResult.Message);
				return CheckRunResult.Failed;
			}
			VersionStateService.SaveInstalledBuild(result.BuildNumber, result.ProductVersion ?? "", result.InstallerHash ?? "");
			Log($"Poprawnie zainstalowano build {result.BuildNumber}.");
			return CheckRunResult.Completed;
		}
		catch (Exception value)
		{
			Log($"Nieobsłużony błąd --check: {value}");
			return CheckRunResult.Failed;
		}
		finally
		{
			await SelfUpdateService.CheckAsync();
		}
	}

	private static bool IsConfigValid(AppConfig config)
	{
		if (config == null)
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(config.SymplexId))
		{
			return false;
		}
		string text = config.Nip.Replace("-", "").Replace(" ", "");
		if (text.Length != 10 || !text.All(char.IsDigit))
		{
			return false;
		}
		if (config.UpdateEndDate == default(DateTime))
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(config.SmallBusinessPath))
		{
			return false;
		}
		if (!File.Exists(config.SmallBusinessPath))
		{
			return false;
		}
		return true;
	}

	private static void Log(string message)
	{
		LogService.Write("check.log", message);
	}
}

