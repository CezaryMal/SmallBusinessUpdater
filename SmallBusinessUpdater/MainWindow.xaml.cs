using System.Diagnostics;
using System.IO;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Win32;
using SmallBusinessUpdater.Models;
using SmallBusinessUpdater.Services;

namespace SmallBusinessUpdater;

public partial class MainWindow : Window, IComponentConnector
{
	private AppConfig _config;

	public MainWindow()
	{
		InitializeComponent();
		_config = ConfigService.Load();
		LoadConfigToGui();
		RefreshSummary();
		Activated += (_, _) => RefreshSummary();
        if (string.IsNullOrWhiteSpace(_config.SymplexId) || string.IsNullOrWhiteSpace(_config.Nip))
            SettingsTab.IsChecked = true;
	}

	private void LoadConfigToGui()
	{
		IdTextBox.Text = _config.SymplexId;
		NipTextBox.Text = _config.Nip;
		UpdateEndDatePicker.SelectedDate = ((_config.UpdateEndDate == default(DateTime)) ? DateTime.Today : _config.UpdateEndDate);
		ProgramPathTextBox.Text = (string.IsNullOrWhiteSpace(_config.SmallBusinessPath) ? "C:\\SB4\\ksiegaw.exe" : _config.SmallBusinessPath);
		AutoStartCheckBox.IsChecked = AutoStartService.IsEnabled();
		LaunchAfterUpdateCheckBox.IsChecked = _config.LaunchAfterUpdate;
		DarkThemeCheckBox.IsChecked = _config.DarkTheme;
		SetStatus("Gotowy do sprawdzenia aktualizacji");
	}

	private bool SaveConfig()
	{
		string text = IdTextBox.Text.Trim();
		string text2 = NipTextBox.Text.Replace("-", "").Replace(" ", "");
		if (string.IsNullOrWhiteSpace(text))
		{
			MessageBox.Show("Podaj ID Symplex.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return false;
		}
		if (text2.Length != 10 || !text2.All(char.IsDigit))
		{
			MessageBox.Show("NIP powinien zawierać 10 cyfr.", "Nieprawidłowy NIP", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return false;
		}
		if (!UpdateEndDatePicker.SelectedDate.HasValue)
		{
			MessageBox.Show("Wybierz datę końca aktualizacji.", "Brak daty", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return false;
		}
		string text3 = ProgramPathTextBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(text3))
		{
			MessageBox.Show("Podaj ścieżkę do ksiegaw.exe.", "Brak ścieżki", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return false;
		}
		bool valueOrDefault = AutoStartCheckBox.IsChecked == true;
		_config = new AppConfig
		{
			SymplexId = text,
			Nip = text2,
			UpdateEndDate = UpdateEndDatePicker.SelectedDate.Value.Date,
			SmallBusinessPath = text3,
			AutoStart = valueOrDefault,
			DarkTheme = DarkThemeCheckBox.IsChecked == true,
			LaunchAfterUpdate = (LaunchAfterUpdateCheckBox.IsChecked == true)
		};
		try
		{
			ConfigService.Save(_config);
			AutoStartService.Apply(valueOrDefault);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Nie udało się zapisać ustawień.\n\n" + ex.Message, "Small Business Updater", MessageBoxButton.OK, MessageBoxImage.Hand);
			return false;
		}
		NipTextBox.Text = text2;
		SetStatus("Ustawienia zapisane");
		return true;
	}

	private void BrowseButton_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Wybierz ksiegaw.exe",
			Filter = "Small Business|ksiegaw.exe|Programy (*.exe)|*.exe"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			ProgramPathTextBox.Text = openFileDialog.FileName;
		}
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		if (SaveConfig())
        {
            RefreshSummary();
            HomeTab.IsChecked = true;
        }
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
	{
		if (!SaveConfig())
		{
            SettingsTab.IsChecked = true;
			return;
		}
		using UpdateLockService updateLock = UpdateLockService.TryAcquire();
		if (!updateLock.Acquired)
		{
			SetStatus("Aktualizacja jest już uruchomiona.");
			MessageBox.Show("Inny proces Small Business Updater już sprawdza lub instaluje aktualizację.", "Aktualizacja już trwa", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		if (DateTime.Today >= _config.UpdateEndDate.Date)
		{
			SetStatus("Okres aktualizacji wygasł.");
			MessageBox.Show($"Okres aktualizacji wygasł {_config.UpdateEndDate:dd.MM.yyyy}.", "Koniec aktualizacji", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		SetBusy(busy: true);
		try
		{
			Progress<string> progress = new Progress<string>((string message) =>
			{
				SetStatus(message);
			});
			UpdateCheckResult result = await UpdateService.CheckAndDownloadAsync(_config, progress);
			if (!result.Success)
			{
				SetStatus(result.Message);
				return;
			}
			if (!result.UpdateAvailable)
			{
				SetStatus("Small Business ma już tę wersję. Możesz korzystać z programu.");
				if (_config.LaunchAfterUpdate) SmallBusinessService.Start(_config.SmallBusinessPath);
				return;
			}
			if (string.IsNullOrWhiteSpace(result.InstallerPath))
			{
				SetStatus("Brak instalatora.");
				return;
			}
			SetStatus("Aktualizowanie Small Business...");
			InstallResult installResult = await InstallerService.InstallAsync(result.InstallerPath, _config.SmallBusinessPath, _config.LaunchAfterUpdate, progress);
			if (!installResult.Success)
			{
				SetStatus(installResult.Message);
				MessageBox.Show(installResult.Message, "Small Business Updater", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			VersionStateService.SaveInstalledBuild(result.BuildNumber, result.ProductVersion ?? "", result.InstallerHash ?? "");
			SetStatus("Aktualizacja Small Business została zakończona.");
		}
		catch (Exception ex)
		{
			SetStatus("Wystąpił błąd.");
			MessageBox.Show(ex.Message, "Small Business Updater", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			SetBusy(busy: false);
			RefreshSummary();
			await SelfUpdateService.CheckAsync();
		}
	}

	private void SetStatus(string text)
	{
		StatusRun.Text = text;
	}

	private void SetBusy(bool busy)
	{
		ProgressBar.Visibility = ((!busy) ? Visibility.Collapsed : Visibility.Visible);
		CheckUpdatesButton.IsEnabled = !busy;
		SaveButton.IsEnabled = !busy;
		BrowseButton.IsEnabled = !busy;
		IdTextBox.IsEnabled = !busy;
		NipTextBox.IsEnabled = !busy;
		UpdateEndDatePicker.IsEnabled = !busy;
		ProgramPathTextBox.IsEnabled = !busy;
		AutoStartCheckBox.IsEnabled = !busy;
		LaunchAfterUpdateCheckBox.IsEnabled = !busy;
		DarkThemeCheckBox.IsEnabled = !busy;
	}

    private void RefreshSummary()
    {
        HomeGuidanceText.Text = string.IsNullOrWhiteSpace(_config.SymplexId)
            ? "Zacznij od wpisania danych w zakładce Ustawienia."
            : "Przed aktualizacją zapisz pracę i zamknij Small Business.";
        LicenseSummaryText.Text = _config.UpdateEndDate == default
            ? "Datę dostępu do aktualizacji ustawisz w Ustawieniach."
            : DateTime.Today >= _config.UpdateEndDate.Date
                ? $"Dostęp do aktualizacji wygasł {_config.UpdateEndDate:dd.MM.yyyy}. Sprawdź dane w Ustawieniach."
                : $"Koniec dostępu do aktualizacji: {_config.UpdateEndDate:dd.MM.yyyy}";
        var state = VersionStateService.Load();
        var history = CheckHistoryService.Load();
        HomeLastCheckText.Text = history.CheckedAt.HasValue
            ? $"Ostatnie sprawdzenie: {history.CheckedAt.Value.ToLocalTime():dd.MM.yyyy, HH:mm}"
            : "Nie sprawdzono jeszcze aktualizacji.";
        InstalledBuildText.Text = state.InstalledBuild > 0
            ? $"Small Business — zainstalowany build {state.InstalledBuild}"
            : "Small Business — brak zapisanego buildu";
        InstalledDateText.Text = state.InstalledAt.HasValue
            ? $"Ostatnia instalacja: {state.InstalledAt:dd.MM.yyyy HH:mm}"
            : "Ostatnia instalacja: brak danych";
        LastCheckText.Text = history.CheckedAt.HasValue
            ? $"Ostatnie sprawdzenie: {history.CheckedAt.Value.ToLocalTime():dd.MM.yyyy HH:mm}"
            : "Ostatnie sprawdzenie: brak danych";
        LastCheckResultText.Text = string.IsNullOrWhiteSpace(history.Result)
            ? "Wynik sprawdzenia: brak danych"
            : "Wynik sprawdzenia: " + history.Result;
    }

    private void DarkTheme_Click(object sender, RoutedEventArgs e)
    {
        bool dark = DarkThemeCheckBox.IsChecked == true;
        try
        {
            var saved = ConfigService.Load();
            saved.DarkTheme = dark;
            ConfigService.Save(saved);
            _config.DarkTheme = dark;
            ThemeService.Apply(dark);
        }
        catch (Exception ex)
        {
            DarkThemeCheckBox.IsChecked = _config.DarkTheme;
            MessageBox.Show("Nie udało się zapisać motywu. " + ex.Message, "Small Business Updater");
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenDirectory(AppPaths.LogsDirectory);
    private void OpenSbInstallers_Click(object sender, RoutedEventArgs e) => OpenDirectory(AppPaths.DownloadsDirectory);
    private void OpenUpdaterInstallers_Click(object sender, RoutedEventArgs e) =>
        OpenDirectory(Path.Combine(AppPaths.LocalDataDirectory, "SelfUpdate"));

    private void OpenDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nie udało się otworzyć folderu. " + ex.Message, "Small Business Updater");
        }
    }

    private void Page_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        HomePage.Visibility = HomeTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = SettingsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DetailsPage.Visibility = DetailsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        CheckUpdatesButton.Visibility = HomeTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.Visibility = SettingsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }
}






