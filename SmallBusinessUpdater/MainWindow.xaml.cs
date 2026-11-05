using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using SmallBusinessUpdater.Models;
using SmallBusinessUpdater.Services;

namespace SmallBusinessUpdater;

public partial class MainWindow : Window, IComponentConnector
{
    private AppConfig _config;
    private EditorState _savedEditorState;
    private bool _busy;
    private bool _installingUpdater;
    private bool _invalidDate;
    private CancellationTokenSource? _downloadCancellation;

    private sealed record EditorState(string Id, string Nip, string Date, string Program,
        bool AutoStart, bool LaunchAfterUpdate, bool AutoUpdateUpdater, string IntervalDays, bool Notifications);

    public MainWindow()
    {
        InitializeComponent();
        _config = ConfigService.Load();
        LoadConfigToGui();
        _savedEditorState = ReadEditorState();
        RefreshSummary();
        Activated += (_, _) => RefreshSummary();
        Closing += Window_Closing;
        if (string.IsNullOrWhiteSpace(_config.SymplexId) || string.IsNullOrWhiteSpace(_config.Nip))
            SettingsTab.IsChecked = true;
    }

    private void LoadConfigToGui()
    {
        IdTextBox.Text = _config.SymplexId;
        NipTextBox.Text = _config.Nip;
        UpdateEndDatePicker.SelectedDate = _config.UpdateEndDate == default ? DateTime.Today : _config.UpdateEndDate;
        ProgramPathTextBox.Text = string.IsNullOrWhiteSpace(_config.SmallBusinessPath) ? @"C:\SB4\ksiegaw.exe" : _config.SmallBusinessPath;
        AutoStartCheckBox.IsChecked = AutoStartService.IsEnabled();
        LaunchAfterUpdateCheckBox.IsChecked = _config.LaunchAfterUpdate;
        DarkThemeCheckBox.IsChecked = _config.DarkTheme;
        AutoUpdateUpdaterCheckBox.IsChecked = _config.AutoUpdateUpdater;
        CheckIntervalDaysTextBox.Text = Math.Clamp(_config.CheckIntervalDays, 1, 365).ToString();
        NotificationsCheckBox.IsChecked = _config.ShowBackgroundNotifications;
        ShowFeedback("Gotowy do sprawdzenia", "Kliknij „Sprawdź aktualizacje”, gdy chcesz sprawdzić nową wersję.", "Info");
    }

    private EditorState ReadEditorState() => new(
        IdTextBox.Text.Trim(),
        NipTextBox.Text.Replace("-", "").Replace(" ", ""),
        UpdateEndDatePicker.Text,
        ProgramPathTextBox.Text.Trim(),
        AutoStartCheckBox.IsChecked == true,
        LaunchAfterUpdateCheckBox.IsChecked == true,
        AutoUpdateUpdaterCheckBox.IsChecked == true,
        CheckIntervalDaysTextBox.Text.Trim(), NotificationsCheckBox.IsChecked == true);

    private bool SaveConfig()
    {
        if (_invalidDate && DateTime.TryParse(UpdateEndDatePicker.Text, out var parsedDate) &&
            UpdateEndDatePicker.SelectedDate?.Date == parsedDate.Date)
            _invalidDate = false;
        ClearValidation();
        Control? firstInvalid = null;
        string id = IdTextBox.Text.Trim();
        string nip = NipTextBox.Text.Replace("-", "").Replace(" ", "");
        string path = ProgramPathTextBox.Text.Trim();

        void Invalid(Control field, TextBlock message, string text)
        {
            message.Text = text;
            message.Visibility = Visibility.Visible;
            field.SetResourceReference(Control.BorderBrushProperty, "ErrorTextBrush");
            firstInvalid ??= field;
        }

        if (string.IsNullOrWhiteSpace(id))
            Invalid(IdTextBox, IdErrorText, "Wpisz ID Symplex z danych licencji.");
        if (nip.Length != 10 || !nip.All(char.IsDigit))
            Invalid(NipTextBox, NipErrorText, "NIP powinien zawierać 10 cyfr. Możesz wpisać go z myślnikami.");
        if (_invalidDate || !UpdateEndDatePicker.SelectedDate.HasValue)
            Invalid(UpdateEndDatePicker, DateErrorText, "Wybierz poprawną datę z kalendarza.");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Invalid(ProgramPathTextBox, PathErrorText, "Nie znaleziono programu. Użyj „Wybierz plik…” i wskaż ksiegaw.exe.");

        bool intervalValid = int.TryParse(CheckIntervalDaysTextBox.Text.Trim(), out int intervalDays) && intervalDays >= 1 && intervalDays <= 365;
        if (!intervalValid)
            Invalid(CheckIntervalDaysTextBox, IntervalErrorText, "Wpisz liczbę dni od 1 do 365.");
        if (firstInvalid != null)
        {
            SettingsTab.IsChecked = true;
            ShowFeedback("Sprawdź ustawienia", "Popraw pola oznaczone komunikatem i zapisz ustawienia.", "Warning");
            firstInvalid.BringIntoView();
            firstInvalid.Focus();
            return false;
        }

        var next = new AppConfig
        {
            SymplexId = id,
            Nip = nip,
            UpdateEndDate = UpdateEndDatePicker.SelectedDate!.Value.Date,
            SmallBusinessPath = path,
            AutoStart = AutoStartCheckBox.IsChecked == true,
            DarkTheme = DarkThemeCheckBox.IsChecked == true,
            AutoUpdateUpdater = AutoUpdateUpdaterCheckBox.IsChecked == true,
            CheckIntervalDays = intervalDays,
            ShowBackgroundNotifications = NotificationsCheckBox.IsChecked == true,
            LaunchAfterUpdate = LaunchAfterUpdateCheckBox.IsChecked == true
        };
        bool previousAutoStart = _savedEditorState.AutoStart;
        try
        {
            AutoStartService.Apply(next.AutoStart);
            ConfigService.Save(next);
        }
        catch (Exception ex)
        {
            try { AutoStartService.Apply(previousAutoStart); }
            catch (Exception rollbackError) { LogService.Write("gui.log", rollbackError.ToString()); }
            LogService.Write("gui.log", ex.ToString());
            ShowFeedback("Nie udało się zapisać ustawień", "Spróbuj ponownie. Szczegóły błędu znajdziesz w logach.", "Error");
            return false;
        }
        _config = next;
        NipTextBox.Text = nip;
        CheckIntervalDaysTextBox.Text = intervalDays.ToString();
        _savedEditorState = ReadEditorState();
        ShowFeedback("Ustawienia zapisane", "Możesz przejść do sprawdzania aktualizacji.", "Success");
        return true;
    }

    private void ClearValidation()
    {
        foreach (var message in new[] { IdErrorText, NipErrorText, DateErrorText, PathErrorText, IntervalErrorText })
            message.Visibility = Visibility.Collapsed;
        foreach (var field in new Control[] { IdTextBox, NipTextBox, UpdateEndDatePicker, ProgramPathTextBox, CheckIntervalDaysTextBox })
            field.ClearValue(Control.BorderBrushProperty);
    }

    private void UpdateDate_Invalid(object sender, DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException = false;
        _invalidDate = true;
        DateErrorText.Text = "Nie rozpoznano daty. Wybierz ją z kalendarza.";
        DateErrorText.Visibility = Visibility.Visible;
        UpdateEndDatePicker.SetResourceReference(Control.BorderBrushProperty, "ErrorTextBrush");
    }

    private void UpdateDate_Changed(object sender, SelectionChangedEventArgs e)
    {
        _invalidDate = false;
        if (DateErrorText == null) return;
        DateErrorText.Visibility = Visibility.Collapsed;
        UpdateEndDatePicker.ClearValue(Control.BorderBrushProperty);
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Wybierz program Small Business",
            Filter = "Small Business|ksiegaw.exe|Programy (*.exe)|*.exe"
        };
        if (dialog.ShowDialog() == true)
            ProgramPathTextBox.Text = dialog.FileName;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveConfig()) return;
        RefreshSummary();
        HomeTab.IsChecked = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_installingUpdater) return;
        if (_busy)
        {
            e.Cancel = true;
            ShowFeedback("Aktualizacja jest w toku", "Poczekaj na zakończenie operacji przed zamknięciem okna.", "Warning");
            return;
        }
        if (ReadEditorState() == _savedEditorState && !_invalidDate) return;
        var answer = MessageBox.Show(this,
            "Masz niezapisane zmiany w ustawieniach. Czy zapisać je przed zamknięciem?\n\nTak — zapisz i zamknij\nNie — zamknij bez zapisywania\nAnuluj — wróć do ustawień",
            "Niezapisane ustawienia", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        e.Cancel = answer == MessageBoxResult.Cancel || (answer == MessageBoxResult.Yes && !SaveConfig());
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !SaveConfig()) return;
        using UpdateLockService updateLock = UpdateLockService.TryAcquire();
        if (!updateLock.Acquired)
        {
            ShowFeedback("Aktualizacja jest już uruchomiona", "Poczekaj, aż drugi proces Updatera zakończy pracę.", "Warning");
            return;
        }
        if (DateTime.Today >= _config.UpdateEndDate.Date)
        {
            ShowFeedback("Dostęp do aktualizacji wygasł",
                $"Okres aktualizacji zakończył się {_config.UpdateEndDate:dd.MM.yyyy}. Po przedłużeniu dostępu wpisz nową datę w Ustawieniach.", "Warning");
            return;
        }

        using var downloadCancellation = new CancellationTokenSource();
        _downloadCancellation = downloadCancellation;
        bool skipSelfUpdate = false;
        SetBusy(true);
        ShowFeedback("1 z 3 · Sprawdzanie i pobieranie", "Łączę się z serwerem Symplex. Pobieranie może potrwać kilka minut.", "Info");
        try
        {
            var progress = new Progress<string>(message =>
            {
                if (_busy && !_installingUpdater && !downloadCancellation.IsCancellationRequested)
                    SetProgress(message);
            });
            UpdateCheckResult result = await UpdateService.CheckAndDownloadAsync(_config, progress, downloadCancellation.Token);
            downloadCancellation.Token.ThrowIfCancellationRequested();
            _downloadCancellation = null;
            UpdateActionButtons();
            if (!result.Success)
            {
                ShowFeedback("Nie udało się sprawdzić aktualizacji",
                    result.Message + " Sprawdź połączenie z internetem i dane licencji, a następnie spróbuj ponownie.", "Error");
                return;
            }
            if (!result.UpdateAvailable)
            {
                CheckHistoryService.MarkCompleted();
                bool opened = _config.LaunchAfterUpdate && SmallBusinessService.Start(_config.SmallBusinessPath);
                ShowFeedback("Small Business jest aktualny",
                    _config.LaunchAfterUpdate
                        ? opened ? "Nie ma nowej aktualizacji. Small Business został uruchomiony."
                                 : "Nie ma nowej aktualizacji. Nie udało się otworzyć Small Business — uruchom go swoim skrótem."
                        : "Nie ma nowej aktualizacji. Możesz korzystać ze Small Business.", "Success");
                return;
            }
            if (string.IsNullOrWhiteSpace(result.InstallerPath))
            {
                ShowFeedback("Nie znaleziono instalatora", "Ponownie sprawdź aktualizacje, aby pobrać plik.", "Error");
                return;
            }
            while (SmallBusinessService.IsRunning(_config.SmallBusinessPath))
            {
                ShowFeedback("Zamknij Small Business", "Zapisz pracę i zamknij program, aby można było zainstalować aktualizację.", "Warning");
                var answer = MessageBox.Show(this,
                    "Small Business jest otwarty. Zapisz pracę i zamknij go samodzielnie. Po zamknięciu kliknij OK, aby kontynuować. Anuluj odłoży instalację.",
                    "Zamknij Small Business", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                if (answer == MessageBoxResult.Cancel)
                {
                    skipSelfUpdate = true;
                    ShowFeedback("Instalacja odłożona", "Po zamknięciu Small Business możesz ponownie sprawdzić aktualizacje.", "Info");
                    return;
                }
            }
            ShowFeedback("2 z 3 · Instalowanie Small Business", "Instalator pracuje. Poczekaj na zakończenie, nie zamykaj okna.", "Info");
            InstallResult installed = await InstallerService.InstallAsync(
                result.InstallerPath, _config.SmallBusinessPath, _config.LaunchAfterUpdate, progress);
            if (!installed.Success)
            {
                LogService.Write("gui.log", installed.Message);
                ShowFeedback("Aktualizacja nie została zakończona",
                    installed.Message + " Szczegóły znajdziesz w zakładce Szczegóły → Otwórz folder z logami.", "Error");
                return;
            }
            VersionStateService.SaveInstalledBuild(result.BuildNumber, result.ProductVersion ?? "", result.InstallerHash ?? "");
            CheckHistoryService.MarkCompleted();
            ShowFeedback("Aktualizacja zakończona", "Nowa wersja Small Business została zainstalowana.", "Success");
        }
        catch (OperationCanceledException) when (downloadCancellation.IsCancellationRequested)
        {
            skipSelfUpdate = true;
            ShowFeedback("Anulowano sprawdzanie", "Pobieranie zostało przerwane. Instalacja nie została rozpoczęta.", "Info");
        }
        catch (Exception ex)
        {
            LogService.Write("gui.log", ex.ToString());
            ShowFeedback("Nie udało się zakończyć operacji", "Spróbuj ponownie. Szczegóły błędu znajdziesz w logach w zakładce Szczegóły.", "Error");
        }
        finally
        {
            _downloadCancellation = null;
            UpdateActionButtons();
            RefreshSummary();
            // Keep editors locked until a possible self-update has handed off installation.
            if (!skipSelfUpdate && _config.AutoUpdateUpdater)
            {
                _installingUpdater = true;
                _downloadCancellation = downloadCancellation;
                UpdateActionButtons();
                StageText.Text = "3 z 3 · Sprawdzanie aktualizacji Updatera w tle…";
                var progress = new Progress<string>(message =>
                {
                    if (_busy && !downloadCancellation.IsCancellationRequested)
                        StageText.Text = message;
                });
                await SelfUpdateService.CheckAsync(progress, downloadCancellation.Token, reopenAfterUpdate: true, automatic: true);
                if (downloadCancellation.IsCancellationRequested)
                    FeedbackDescription.Text += " Sprawdzanie aktualizacji Updatera anulowano.";
            }
            _downloadCancellation = null;
            _installingUpdater = false;
            StageText.Text = "";
            SetBusy(false);
        }
    }

    private void SetProgress(string message)
    {
        StageText.Text = message;
    }

    private void ShowFeedback(string title, string description, string kind)
    {
        FeedbackTitle.Text = title;
        FeedbackDescription.Text = description;
        FeedbackBorder.SetResourceReference(Border.BackgroundProperty, kind + "Brush");
        FeedbackTitle.SetResourceReference(TextBlock.ForegroundProperty, kind + "TextBrush");
        FeedbackDescription.SetResourceReference(TextBlock.ForegroundProperty, kind + "TextBrush");
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        foreach (var field in new Control[] { CheckUpdatesButton, SaveButton, BrowseButton, IdTextBox,
            NipTextBox, UpdateEndDatePicker, ProgramPathTextBox, AutoStartCheckBox,
            LaunchAfterUpdateCheckBox, DarkThemeCheckBox, CloseButton, SelfUpdateButton, OpenSmallBusinessButton, SettingsPage })
            field.IsEnabled = !busy;
        UpdateActionButtons();
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
        int days = Math.Clamp(_config.CheckIntervalDays, 1, 365);
        ScheduleSummaryText.Text = days == 1 ? "Sprawdzanie w tle: codziennie." :
            days == 7 ? "Sprawdzanie w tle: co tydzień." : $"Sprawdzanie w tle: co {days} dni.";
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
            LogService.Write("gui.log", ex.ToString());
            ShowFeedback("Nie udało się zapisać wyglądu", "Spróbuj ponownie. Szczegóły znajdziesz w logach.", "Error");
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
            LogService.Write("gui.log", ex.ToString());
            ShowFeedback("Nie udało się otworzyć folderu", "Sprawdź dostęp do folderu i spróbuj ponownie.", "Error");
        }
    }

    private void Page_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        HomePage.Visibility = HomeTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = SettingsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DetailsPage.Visibility = DetailsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        bool cancellable = _downloadCancellation != null;
        CheckUpdatesButton.Visibility = HomeTab.IsChecked == true && !cancellable ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.Visibility = SettingsTab.IsChecked == true && !cancellable ? Visibility.Visible : Visibility.Collapsed;
        CancelDownloadButton.Visibility = cancellable ? Visibility.Visible : Visibility.Collapsed;
        CancelDownloadButton.IsEnabled = cancellable && !_downloadCancellation!.IsCancellationRequested;
    }

    private void CancelDownload_Click(object sender, RoutedEventArgs e)
    {
        _downloadCancellation?.Cancel();
        CancelDownloadButton.IsEnabled = false;
        StageText.Text = "Anulowanie sprawdzania i pobierania…";
    }

    private async void SelfUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (ReadEditorState() != _savedEditorState || _invalidDate)
        {
            SettingsTab.IsChecked = true;
            ShowFeedback("Masz niezapisane ustawienia", "Zapisz zmiany lub cofnij edycję przed aktualizacją Updatera.", "Warning");
            return;
        }
        using var updateLock = UpdateLockService.TryAcquire();
        if (!updateLock.Acquired)
        {
            ShowFeedback("Aktualizacja jest już uruchomiona", "Poczekaj na zakończenie drugiej operacji.", "Warning");
            return;
        }
        using var cancellation = new CancellationTokenSource();
        _downloadCancellation = cancellation;
        SetBusy(true);
        _installingUpdater = true;
        ShowFeedback("Sprawdzanie wersji Updatera", "Sprawdzam najnowsze wydanie na GitHubie.", "Info");
        try
        {
            var progress = new Progress<string>(message =>
            {
                if (_busy && !cancellation.IsCancellationRequested) StageText.Text = message;
            });
            var result = await SelfUpdateService.CheckAsync(progress, cancellation.Token, reopenAfterUpdate: true);
            ShowFeedback(cancellation.IsCancellationRequested ? "Sprawdzanie anulowane" :
                result.Success ? "Sprawdzanie Updatera zakończone" : "Nie udało się zaktualizować Updatera",
                result.Message, cancellation.IsCancellationRequested ? "Info" : result.Success ? "Success" : "Warning");
        }
        finally
        {
            _downloadCancellation = null;
            _installingUpdater = false;
            StageText.Text = "";
            SetBusy(false);
        }
    }

    private void OpenSmallBusiness_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        string path = ProgramPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            SettingsTab.IsChecked = true;
            ShowFeedback("Wskaż program Small Business", "W Ustawieniach wybierz plik ksiegaw.exe, a następnie spróbuj ponownie.", "Warning");
            ProgramPathTextBox.BringIntoView();
            ProgramPathTextBox.Focus();
            return;
        }
        bool alreadyRunning = SmallBusinessService.IsRunning(path);
        bool started = SmallBusinessService.Start(path);
        ShowFeedback(started ? "Small Business jest gotowy" : "Nie udało się otworzyć Small Business",
            started ? alreadyRunning ? "Program jest już uruchomiony. Przejdź do jego okna." : "Small Business został uruchomiony."
                    : "Sprawdź ścieżkę programu w Ustawieniach i spróbuj ponownie.",
            started ? "Success" : "Warning");
    }

    private void IntervalPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string days)
            CheckIntervalDaysTextBox.Text = days;
    }
}





