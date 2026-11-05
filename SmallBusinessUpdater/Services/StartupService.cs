using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SmallBusinessUpdater.Models;

namespace SmallBusinessUpdater.Services;

public static class StartupService
{
	public static async Task<StartupRunResult> RunAsync(Action? checkingFinished = null)
	{
		AppPaths.EnsureDirectories();
		bool checkingFinishedCalled = false;
		using (UpdateLockService updateLock = UpdateLockService.TryAcquire())
		{
			if (!updateLock.Acquired)
			{
				Log("Inna operacja aktualizacji jest już uruchomiona.");
				FinishChecking();
				return StartupRunResult.UpdateAlreadyRunning;
			}
			AppConfig config = ConfigService.Load();
			if (!IsConfigValid(config))
			{
				Log("Konfiguracja jest niepełna lub nieprawidłowa.");
				FinishChecking();
				return StartupRunResult.OpenGui;
			}
			if (DateTime.Today >= config.UpdateEndDate.Date)
			{
				Log($"Okres aktualizacji wygasł: {config.UpdateEndDate:yyyy-MM-dd}");
				FinishChecking();
				if (config.LaunchAfterUpdate)
				{
					StartSmallBusiness(config);
				}
				return StartupRunResult.UpdatePeriodExpired;
			}
			try
			{
                if (!UpdateScheduleService.IsDue(config))
                {
                    Log("Pominięto sprawdzanie SB — nie upłynął ustawiony odstęp.");
                    FinishChecking();
                    if (config.LaunchAfterUpdate) StartSmallBusiness(config);
                    return StartupRunResult.Completed;
                }
                Log("Rozpoczynam sprawdzanie aktualizacji.");
				UpdateCheckResult result = await UpdateService.CheckAndDownloadAsync(config);
				FinishChecking();
				if (!result.Success)
				{
					Log("Błąd sprawdzania aktualizacji: " + result.Message);
                    BackgroundNotificationService.Show(config, "Nie udało się sprawdzić Small Business", "Sprawdź połączenie i dane licencji. Możesz ponowić sprawdzanie w oknie Updatera.", true);
					if (config.LaunchAfterUpdate)
					{
						StartSmallBusiness(config);
					}
					return StartupRunResult.Completed;
				}
				if (!result.UpdateAvailable)
				{
					Log($"Brak aktualizacji. Build {result.BuildNumber} jest już zainstalowany.");
                    CheckHistoryService.MarkCompleted();
					if (config.LaunchAfterUpdate)
					{
						StartSmallBusiness(config);
					}
					return StartupRunResult.Completed;
				}
				if (string.IsNullOrWhiteSpace(result.InstallerPath))
				{
					Log("UpdateService zwrócił aktualizację, ale bez ścieżki instalatora.");
                    BackgroundNotificationService.Show(config, "Aktualizacja wymaga uwagi", "Nie znaleziono instalatora Small Business. Otwórz Updater i spróbuj ponownie.", true);
					if (config.LaunchAfterUpdate)
					{
						StartSmallBusiness(config);
					}
					return StartupRunResult.Completed;
				}
				Log($"Dostępna aktualizacja. Build {result.BuildNumber}, wersja {result.ProductVersion}.");
				InstallResult installResult = await InstallerService.InstallAsync(result.InstallerPath, config.SmallBusinessPath, launchAfterUpdate: false);
				if (!installResult.Success)
				{
					Log("Instalacja nie powiodła się: " + installResult.Message);
                    BackgroundNotificationService.Show(config, "Aktualizacja Small Business wymaga uwagi", installResult.Message, true);
					if (config.LaunchAfterUpdate)
					{
						StartSmallBusiness(config);
					}
					return StartupRunResult.Completed;
				}
				VersionStateService.SaveInstalledBuild(result.BuildNumber, result.ProductVersion ?? "", result.InstallerHash ?? "");
				Log($"Zainstalowano build {result.BuildNumber}.");
                CheckHistoryService.MarkCompleted();
                BackgroundNotificationService.Show(config, "Small Business został zaktualizowany", "Nowa wersja programu została pomyślnie zainstalowana.");
				if (config.LaunchAfterUpdate)
				{
					StartSmallBusiness(config);
				}
				return StartupRunResult.Completed;
			}
			catch (Exception value)
			{
				FinishChecking();
				Log($"Nieobsłużony błąd --startup: {value}");
                BackgroundNotificationService.Show(config, "Aktualizacja Small Business wymaga uwagi", "Operacja nie została zakończona. Otwórz Updater i sprawdź logi.", true);
				if (config.LaunchAfterUpdate)
				{
					StartSmallBusiness(config);
				}
				return StartupRunResult.Completed;
			}
			finally
			{
				await BackgroundNotificationService.FlushAsync();
            await SelfUpdateService.CheckAsync(automatic: true);
            await BackgroundNotificationService.FlushAsync();
			}
		}
		void FinishChecking()
		{
			if (checkingFinishedCalled)
			{
				return;
			}
			checkingFinishedCalled = true;
			try
			{
				checkingFinished?.Invoke();
			}
			catch
			{
			}
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

	private static void StartSmallBusiness(AppConfig config)
	{
		if (SmallBusinessService.Start(config.SmallBusinessPath))
		{
			Log("Uruchomiono Small Business.");
		}
		else
		{
			Log("Nie udało się uruchomić Small Business.");
		}
	}

	private static void Log(string message)
	{
		LogService.Write("startup.log", message);
	}
}


