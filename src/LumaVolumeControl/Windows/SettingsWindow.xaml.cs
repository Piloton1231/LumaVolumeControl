using LumaVolumeControl.Infrastructure;
using LumaVolumeControl.Models;
using Localization = LumaVolumeControl.Infrastructure.Localization;

namespace LumaVolumeControl.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private AppSettings _settings;
    private string _savedValues = string.Empty;

    public SettingsWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;
        LoadValues();
        ApplyLocalization();
        SelectPanel(GeneralPanel, GeneralNav);
    }

    public event EventHandler<AppSettings>? SettingsSaved;
    public event EventHandler<AppSettings>? PreviewRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<(string Key, string Name, int Volume)>? SavedVolumeChanged;

    public void Reload(AppSettings settings)
    {
        _settings = settings;
        LoadValues();
        ApplyLocalization();
    }

    private void LoadValues()
    {
        AutoVolumeCheck.IsChecked = _settings.AutoVolumeEnabled;
        AppLanguageCombo.SelectedIndex = (int)_settings.Language;
        StartupCheck.IsChecked = _settings.RunAtStartup;
        StartMinimizedCheck.IsChecked = _settings.StartMinimized;
        FirstVolumeSlider.Value = _settings.FirstLaunchVolume;
        ShowOverlayCheck.IsChecked = _settings.ShowOverlay;
        AnimateCheck.IsChecked = _settings.AnimateVolumeChange;
        ShowVersionCheck.IsChecked = _settings.ShowVersionBadge;
        OverlayPositionCombo.SelectedIndex = (int)_settings.OverlayPlacement;
        DurationSlider.Value = _settings.OverlayDurationMs;
        CornerCombo.SelectedIndex = (int)_settings.VersionBadgeCorner;
        CompactMixerCheck.IsChecked = _settings.CompactMixerMode;
        CloseOnFocusLossCheck.IsChecked = _settings.CloseMixerOnFocusLoss;
        StepSlider.Value = _settings.VolumeStep;
        UiScaleSlider.Value = _settings.UiScalePercent;
        VisualizerTopmostCheck.IsChecked = _settings.VisualizerAlwaysOnTop;
        VisualizerStyleSettingsCombo.SelectedIndex = (int)_settings.VisualizerStyle;
        VisualizerSensitivitySlider.Value = _settings.VisualizerSensitivity;
        AudioReactionSlider.Value = _settings.AudioReactionSensitivity;
        OscilloscopeSensitivitySlider.Value = _settings.OscilloscopeSensitivity;
        VisualizerFpsCombo.SelectedIndex = _settings.VisualizerFrameRate >= 50 ? 1 : 0;
        RefreshSavedApps();
        _savedValues = GeneralSettingsSignature(ReadValues());
    }

    private AppSettings ReadValues()
    {
        var draft = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settings))!;
        draft.AutoVolumeEnabled = AutoVolumeCheck.IsChecked == true;
        draft.Language = (AppLanguage)Math.Max(0, AppLanguageCombo.SelectedIndex);
        draft.RunAtStartup = StartupCheck.IsChecked == true;
        draft.StartMinimized = StartMinimizedCheck.IsChecked == true;
        draft.FirstLaunchVolume = (int)Math.Round(FirstVolumeSlider.Value);
        draft.ShowOverlay = ShowOverlayCheck.IsChecked == true;
        draft.AnimateVolumeChange = AnimateCheck.IsChecked == true;
        draft.ShowVersionBadge = ShowVersionCheck.IsChecked == true;
        draft.OverlayPlacement = (OverlayPlacement)Math.Max(0, OverlayPositionCombo.SelectedIndex);
        draft.OverlayDurationMs = (int)Math.Round(DurationSlider.Value);
        draft.VersionBadgeCorner = (OverlayCorner)Math.Max(0, CornerCombo.SelectedIndex);
        draft.CompactMixerMode = CompactMixerCheck.IsChecked == true;
        draft.CloseMixerOnFocusLoss = CloseOnFocusLossCheck.IsChecked == true;
        draft.VolumeStep = (int)Math.Round(StepSlider.Value);
        draft.UiScalePercent = (int)Math.Round(UiScaleSlider.Value);
        draft.VisualizerAlwaysOnTop = VisualizerTopmostCheck.IsChecked == true;
        draft.VisualizerStyle = (VisualizerStyle)Math.Max(0, VisualizerStyleSettingsCombo.SelectedIndex);
        draft.VisualizerSensitivity = (int)Math.Round(VisualizerSensitivitySlider.Value);
        draft.AudioReactionSensitivity = (int)Math.Round(AudioReactionSlider.Value);
        draft.OscilloscopeSensitivity = (int)Math.Round(OscilloscopeSensitivitySlider.Value);
        draft.VisualizerFrameRate = VisualizerFpsCombo.SelectedIndex == 1 ? 60 : 30;
        draft.Normalize();
        return draft;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadValues();
        _settings = settings;
        _savedValues = GeneralSettingsSignature(settings);
        Localization.SetLanguage(settings.Language);
        ApplyLocalization();
        _settingsService.Save(settings);
        StartupService.Apply(settings.RunAtStartup);
        SettingsSaved?.Invoke(this, settings);
        SaveStatusText.Text = AppLocalization.Text("設定を保存しました");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        timer.Tick += (_, _) => { timer.Stop(); Hide(); };
        timer.Start();
    }

    private void Preview_Click(object sender, RoutedEventArgs e) => PreviewRequested?.Invoke(this, ReadValues());
    private void ResetVolumes_Click(object sender, RoutedEventArgs e)
    {
        _settings.AppVolumeProfiles.Clear();
        _settingsService.Save(_settings);
        RefreshSavedApps();
        AdvancedStatusText.Text = AppLocalization.Text("保存したアプリ名と音量をリセットしました。");
    }

    private void RemoveSavedApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key }) return;
        if (!_settings.AppVolumeProfiles.Remove(key)) return;
        _settingsService.Save(_settings);
        RefreshSavedApps();
        AdvancedStatusText.Text = AppLocalization.Text("選択したアプリの保存値を削除しました。");
    }

    private void RefreshSavedApps()
    {
        if (_settings is null || SavedAppsList is null) return;
        var query = SavedAppsSearch.Text.Trim();
        var items = _settings.AppVolumeProfiles
            .Where(pair => pair.Value.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || pair.Value.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || pair.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Value.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(pair => new SavedAppItem(pair.Key, pair.Value.DisplayName, pair.Value.ProcessName, pair.Value.Volume))
            .ToArray();
        SavedAppsList.ItemsSource = items;
        NoSavedAppsText.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoSavedAppsText.Text = AppLocalization.Text(query.Length > 0 ? "一致するアプリはありません" : "まだ保存されたアプリはありません");
    }
    private void SavedAppsSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshSavedApps();

    private void SavedVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider { IsLoaded: true, Tag: string key } slider
            || (!slider.IsMouseCaptureWithin && !slider.IsKeyboardFocusWithin)
            || !_settings.AppVolumeProfiles.TryGetValue(key, out var profile)) return;
        var volume = (int)Math.Round(e.NewValue);
        profile.Volume = volume;
        _settingsService.Save(_settings);
        SavedVolumeChanged?.Invoke(this, (key, profile.DisplayName, volume));
    }
    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_settingsService.SettingsPath}\"") { UseShellExecute = true });
    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void FirstVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (FirstVolumeText is not null) FirstVolumeText.Text = $"{e.NewValue:0}%"; }
    private void DurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (DurationText is not null) DurationText.Text = $"{e.NewValue / 1000:0.0} 秒"; }
    private void StepSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (StepText is not null) StepText.Text = $"{e.NewValue:0}%"; }
    private void UiScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (UiScaleText is not null) UiScaleText.Text = $"{e.NewValue:0}%"; }
    private void VisualizerSensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (VisualizerSensitivityText is not null) VisualizerSensitivityText.Text = $"{e.NewValue:0}%"; }
    private void OscilloscopeSensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (OscilloscopeSensitivityText is not null) OscilloscopeSensitivityText.Text = $"{e.NewValue:0}%"; }

    private void GeneralNav_Click(object sender, RoutedEventArgs e) => SelectPanel(GeneralPanel, GeneralNav);
    private void OverlayNav_Click(object sender, RoutedEventArgs e) => SelectPanel(OverlayPanel, OverlayNav);
    private void VisualizerNav_Click(object sender, RoutedEventArgs e) => SelectPanel(VisualizerSettingsPanel, VisualizerNav);
    private void AdvancedNav_Click(object sender, RoutedEventArgs e) => SelectPanel(AdvancedPanel, AdvancedNav);
    private void AboutNav_Click(object sender, RoutedEventArgs e) => SelectPanel(AboutPanel, AboutNav);

    private void SelectPanel(UIElement panel, Button nav)
    {
        foreach (var item in new UIElement[] { GeneralPanel, OverlayPanel, VisualizerSettingsPanel, AdvancedPanel, AboutPanel }) item.Visibility = ReferenceEquals(item, panel) ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { GeneralNav, OverlayNav, VisualizerNav, AdvancedNav, AboutNav })
        {
            button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ReferenceEquals(button, nav) ? "#30284A" : "#202431"));
            button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ReferenceEquals(button, nav) ? "#6E59B2" : "#303646"));
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 1) DragMove(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) => RequestClose();
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { e.Cancel = true; RequestClose(); }

    private static string GeneralSettingsSignature(AppSettings settings)
    {
        var copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        copy.AppVolumeProfiles.Clear();
        return JsonSerializer.Serialize(copy);
    }
    private void RequestClose()
    {
        if (GeneralSettingsSignature(ReadValues()) != _savedValues)
        {
            var dialog = new SaveChangesWindow { Owner = this };
            if (dialog.ShowDialog() == true) { Save_Click(this, new RoutedEventArgs()); return; }
        }
        LoadValues();
        Hide();
    }

    public void ApplyLocalization() => AppLocalization.Apply(this);

    private sealed record SavedAppItem(string Key, string DisplayName, string ProcessName, int Volume);
}
