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
	}

	private void LoadConfigToGui()
	{
		IdTextBox.Text = _config.SymplexId;
		NipTextBox.Text = _config.Nip;
		UpdateEndDatePicker.SelectedDate = ((_config.UpdateEndDate == default(DateTime)) ? DateTime.Today : _config.UpdateEndDate);
		ProgramPathTextBox.Text = (string.IsNullOrWhiteSpace(_config.SmallBusinessPath) ? "C:\\SB4\\ksiegaw.exe" : _config.SmallBusinessPath);
		AutoStartCheckBox.IsChecked = AutoStartService.IsEnabled();
		LaunchAfterUpdateCheckBox.IsChecked = _config.LaunchAfterUpdate;
		SetStatus("Gotowy");
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
		SaveConfig();
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
	{
		if (!SaveConfig())
		{
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
				SetStatus($"Brak nowej aktualizacji. Build {result.BuildNumber} jest już zainstalowany.");
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
			SetStatus($"Zainstalowano build {result.BuildNumber}.");
		}
		catch (Exception ex)
		{
			SetStatus("Wystąpił błąd.");
			MessageBox.Show(ex.Message, "Small Business Updater", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			SetBusy(busy: false);
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
	}
}
