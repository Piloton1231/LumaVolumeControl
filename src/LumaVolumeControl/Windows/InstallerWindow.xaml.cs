using LumaVolumeControl.Infrastructure;
using LumaVolumeControl.Models;
using Localization = LumaVolumeControl.Infrastructure.Localization;

namespace LumaVolumeControl.Windows;

public enum InstallerWindowMode
{
    Install,
    UninstallConfirm,
    FinishUninstall
}

public partial class InstallerWindow : Window
{
    private readonly InstallerWindowMode _mode;
    private readonly string? _targetDirectory;
    private readonly InstallerService _service = new();
    private readonly SettingsService _settingsService = new();
    private AppLanguage _selectedLanguage;
    private bool _completed;

    public InstallerWindow(InstallerWindowMode mode, string? targetDirectory)
    {
        InitializeComponent();
        _mode = mode;
        _targetDirectory = targetDirectory;
        _selectedLanguage = Localization.CurrentLanguage;
        ConfigureMode();
        UpdateLanguageButtons();
        Loaded += OnLoaded;
    }

    private void ConfigureMode()
    {
        if (_mode == InstallerWindowMode.Install)
        {
            ApplyInstallLocalization();
            return;
        }

        TitleText.Text = Localization.T("Installer.UninstallTitle");
        LanguagePanel.Visibility = Visibility.Collapsed;
        WarningPanel.Visibility = Visibility.Collapsed;
        OptionsPanel.Visibility = Visibility.Collapsed;
        LogoGlyph.Text = "−";
        if (_mode == InstallerWindowMode.UninstallConfirm)
        {
            HeadingText.Text = Localization.T("Installer.DeleteHeading");
            DescriptionText.Text = Localization.T("Installer.DeleteDesc");
            SecondaryButton.Content = Localization.T("Common.Cancel");
            PrimaryButton.Content = Localization.T("Common.Delete");
            PrimaryButton.Background = FindResource("Danger") as Brush;
        }
        else
        {
            HeadingText.Text = Localization.T("Installer.RemovingHeading");
            DescriptionText.Text = Localization.T("Installer.RemovingDesc");
            SecondaryButton.Visibility = Visibility.Collapsed;
            PrimaryButton.Visibility = Visibility.Collapsed;
            CloseButton.IsEnabled = false;
            ProgressTrack.Visibility = Visibility.Visible;
        }
    }

    private void ApplyInstallLocalization()
    {
        TitleText.Text = Localization.T("Installer.SetupTitle");
        HeadingText.Text = Localization.T("Installer.InstallHeading");
        DescriptionText.Text = Localization.T("Installer.InstallDesc");
        WarningTitleText.Text = Localization.T("Installer.WarningTitle");
        WarningText.Text = Localization.T("Installer.WarningText");
        LanguageLabel.Text = Localization.T("Installer.Language");
        StartupCheck.Content = Localization.T("Installer.Startup");
        DesktopCheck.Content = Localization.T("Installer.Desktop");
        SecondaryButton.Content = Localization.T("Common.Cancel");
        PrimaryButton.Content = Localization.T("Installer.Install");
    }

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        if (_mode != InstallerWindowMode.Install || _completed || sender is not Button button || !Enum.TryParse<AppLanguage>(button.Tag?.ToString(), out var language)) return;
        _selectedLanguage = language;
        Localization.SetLanguage(language);
        ApplyInstallLocalization();
        UpdateLanguageButtons();
        StatusText.Text = string.Empty;
    }

    private void UpdateLanguageButtons()
    {
        foreach (var button in LanguageButtons.Children.OfType<Button>())
        {
            var selected = Enum.TryParse<AppLanguage>(button.Tag?.ToString(), out var language) && language == _selectedLanguage;
            button.Background = (Brush)FindResource(selected ? "Accent" : "Bg2");
            button.BorderBrush = (Brush)FindResource(selected ? "Accent" : "Stroke");
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_mode == InstallerWindowMode.FinishUninstall) await FinishUninstallAsync();
    }

    private async void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (_completed)
        {
            if (_mode == InstallerWindowMode.Install) _service.StartInstalledApp();
            Close();
            return;
        }
        if (_mode == InstallerWindowMode.Install) await InstallAsync();
        else if (_mode == InstallerWindowMode.UninstallConfirm) BeginUninstall();
    }

    private async Task InstallAsync()
    {
        SetBusy(true, Localization.T("Installer.Preparing"));
        try
        {
            var settings = _settingsService.Load();
            settings.Language = _selectedLanguage;
            settings.RunAtStartup = StartupCheck.IsChecked == true;
            _settingsService.Save(settings);
            await _service.InstallAsync(DesktopCheck.IsChecked == true, StartupCheck.IsChecked == true, new Progress<double>(SetProgress));
            _completed = true;
            HeadingText.Text = Localization.T("Installer.Installed");
            DescriptionText.Text = Localization.T("Installer.InstalledDesc");
            StatusText.Text = _service.InstallDirectory;
            PrimaryButton.Content = Localization.T("Installer.Launch");
            PrimaryButton.Visibility = Visibility.Visible;
            SecondaryButton.Visibility = Visibility.Collapsed;
            CloseButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            HeadingText.Text = Localization.T("Installer.InstallFailed");
            PrimaryButton.Content = Localization.T("Common.Retry");
            SetBusy(false, ex.Message);
        }
    }

    private void BeginUninstall()
    {
        try
        {
            var temporary = _service.CreateTemporaryUninstaller();
            var info = new ProcessStartInfo(temporary) { UseShellExecute = false };
            info.ArgumentList.Add("--finish-uninstall");
            info.ArgumentList.Add(_service.InstallDirectory);
            Process.Start(info);
            Application.Current.Shutdown();
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private async Task FinishUninstallAsync()
    {
        try
        {
            await _service.FinishUninstallAsync(_targetDirectory ?? _service.InstallDirectory, new Progress<double>(SetProgress));
            _service.ScheduleTemporaryUninstallerCleanup();
            _completed = true;
            HeadingText.Text = Localization.T("Installer.Uninstalled");
            DescriptionText.Text = Localization.T("Installer.UninstalledDesc");
            StatusText.Text = Localization.T("Installer.Thanks");
        }
        catch (Exception ex)
        {
            _completed = true;
            HeadingText.Text = Localization.T("Installer.UninstallFailed");
            StatusText.Text = ex.Message;
        }
        PrimaryButton.Content = Localization.T("Common.Close");
        PrimaryButton.Visibility = Visibility.Visible;
        PrimaryButton.IsEnabled = true;
        CloseButton.IsEnabled = true;
    }

    private void SetBusy(bool busy, string status)
    {
        LanguagePanel.IsEnabled = !busy;
        OptionsPanel.IsEnabled = !busy;
        SecondaryButton.IsEnabled = !busy;
        PrimaryButton.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        CloseButton.IsEnabled = !busy;
        ProgressTrack.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = status;
    }

    private void SetProgress(double value)
    {
        ProgressFill.Width = Math.Max(0, (ProgressTrack.ActualWidth > 0 ? ProgressTrack.ActualWidth : 556) * Math.Clamp(value, 0, 1));
        StatusText.Text = $"{Math.Round(value * 100)}%";
    }

    private void Close_Click(object sender, RoutedEventArgs e) { if (CloseButton.IsEnabled) Close(); }
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
