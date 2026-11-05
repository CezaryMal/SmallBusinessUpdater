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
            if (!UpdateScheduleService.IsDue(config))
            {
                Log("Pominięto sprawdzanie SB — nie upłynął ustawiony odstęp.");
                if (config.LaunchAfterUpdate) SmallBusinessService.Start(config.SmallBusinessPath);
                return CheckRunResult.Completed;
            }
            Log("Rozpoczynam sprawdzanie aktualizacji w tle.");
			UpdateCheckResult result = await UpdateService.CheckAndDownloadAsync(config);
			if (!result.Success)
			{
				Log("Błąd sprawdzania aktualizacji: " + result.Message);
                BackgroundNotificationService.Show(config, "Nie udało się sprawdzić Small Business", "Sprawdź połączenie i dane licencji. Możesz ponowić sprawdzanie w oknie Updatera.", true);
				return CheckRunResult.Failed;
			}
			if (!result.UpdateAvailable)
			{
				Log($"Brak nowej aktualizacji. Build {result.BuildNumber} jest już zainstalowany.");
                CheckHistoryService.MarkCompleted();
				if (config.LaunchAfterUpdate) SmallBusinessService.Start(config.SmallBusinessPath);
				return CheckRunResult.Completed;
			}
			if (string.IsNullOrWhiteSpace(result.InstallerPath))
			{
				Log("Dostępna jest aktualizacja, ale nie otrzymano ścieżki instalatora.");
                BackgroundNotificationService.Show(config, "Aktualizacja wymaga uwagi", "Nie znaleziono instalatora Small Business. Otwórz Updater i spróbuj ponownie.", true);
				return CheckRunResult.Failed;
			}
			Log($"Dostępna aktualizacja: build {result.BuildNumber}, wersja {result.ProductVersion}.");
			InstallResult installResult = await InstallerService.InstallAsync(result.InstallerPath, config.SmallBusinessPath, config.LaunchAfterUpdate);
			if (!installResult.Success)
			{
				Log("Instalacja nie powiodła się: " + installResult.Message);
                BackgroundNotificationService.Show(config, "Aktualizacja Small Business wymaga uwagi", installResult.Message, true);
				return CheckRunResult.Failed;
			}
			VersionStateService.SaveInstalledBuild(result.BuildNumber, result.ProductVersion ?? "", result.InstallerHash ?? "");
			Log($"Poprawnie zainstalowano build {result.BuildNumber}.");
            CheckHistoryService.MarkCompleted();
            BackgroundNotificationService.Show(config, "Small Business został zaktualizowany", "Nowa wersja programu została pomyślnie zainstalowana.");
			return CheckRunResult.Completed;
		}
		catch (Exception value)
		{
			Log($"Nieobsłużony błąd --check: {value}");
            BackgroundNotificationService.Show(config, "Aktualizacja Small Business wymaga uwagi", "Operacja nie została zakończona. Otwórz Updater i sprawdź logi.", true);
			return CheckRunResult.Failed;
		}
		finally
		{
			await BackgroundNotificationService.FlushAsync();
            await SelfUpdateService.CheckAsync(automatic: true);
            await BackgroundNotificationService.FlushAsync();
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


