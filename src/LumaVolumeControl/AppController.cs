using LumaVolumeControl.Infrastructure;
using LumaVolumeControl.Models;
using LumaVolumeControl.Windows;
using Localization = LumaVolumeControl.Infrastructure.Localization;

namespace LumaVolumeControl;

public sealed class AppController : IDisposable
{
    private const string MutexName = "Local\\LumaVolumeControl.SingleInstance";
    private const string SignalName = "Local\\LumaVolumeControl.ShowMixer";
    private readonly string[] _arguments;
    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;
    private EventWaitHandle? _signal;
    private RegisteredWaitHandle? _signalWait;
    private SettingsService? _settingsService;
    private AppSettings? _settings;
    private AudioSessionService? _audio;
    private AudioVisualizerService? _visualizerService;
    private TrayIconService? _tray;
    private MixerWindow? _mixer;
    private SettingsWindow? _settingsWindow;
    private OverlayWindow? _overlay;
    private VisualizerWindow? _visualizerWindow;
    private DispatcherTimer? _pollTimer;
    private bool _disposed;

    public AppController(string[] arguments)
    {
        _arguments = arguments;
        _mutex = new Mutex(true, MutexName, out _ownsMutex);
    }

    public void Start()
    {
        if (!_ownsMutex)
        {
            try { using var signal = EventWaitHandle.OpenExisting(SignalName); signal.Set(); } catch { }
            Application.Current.Shutdown();
            return;
        }

        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
        _signalWait = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) => Application.Current.Dispatcher.BeginInvoke(ShowMixer), null, Timeout.Infinite, false);

        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        Localization.SetLanguage(_settings.Language);
        var safePreview = _arguments.Any(arg => string.Equals(arg, "--safe-preview", StringComparison.OrdinalIgnoreCase));
        if (safePreview)
        {
            _settings.AutoVolumeEnabled = false;
            _settings.CloseMixerOnFocusLoss = false;
        }
        else StartupService.Apply(_settings.RunAtStartup);
        _audio = new AudioSessionService(_settings, _settingsService);
        _visualizerService = new AudioVisualizerService();
        _visualizerService.UpdateSettings(_settings);
        _audio.AutoVolumeApplied += Audio_AutoVolumeApplied;
        _tray = new TrayIconService();
        _tray.OpenRequested += (_, _) => ShowMixer();
        _tray.VisualizerRequested += (_, _) => ShowVisualizerWindow();
        _tray.SettingsRequested += (_, _) => ShowSettings();
        _tray.ExitRequested += (_, _) => Exit();

        _mixer = new MixerWindow(_audio, _visualizerService, _settings);
        if (safePreview)
        {
            _mixer.ShowInTaskbar = true;
        }
        _mixer.SettingsRequested += (_, _) => ShowSettings();
        _mixer.DetachVisualizerRequested += (_, _) => ShowVisualizerWindow();
        _mixer.VisualizerStyleChanged += (_, style) => SaveVisualizerPreferences(style, null);
        _mixer.CompactModeChanged += (_, compact) => SaveCompactMode(compact);
        _mixer.VolumeAdjusted += (_, change) => ShowOverlay(change);
        _settingsWindow = new SettingsWindow(_settings, _settingsService);
        _settingsWindow.SettingsSaved += SettingsWindow_SettingsSaved;
        _settingsWindow.SavedVolumeChanged += (_, change) => _audio?.ApplySavedSessionVolume(change.Key, change.Volume);
        _settingsWindow.PreviewRequested += (_, preview) => ShowOverlay(new AutoVolumeApplied(AppLocalization.Text("プレビュー"), 100, preview.FirstLaunchVolume), preview);
        _settingsWindow.ExitRequested += (_, _) => Exit();
        _overlay = new OverlayWindow();
        _visualizerWindow = new VisualizerWindow(_visualizerService, _settings);
        _visualizerWindow.PreferencesChanged += (_, preferences) => SaveVisualizerPreferences(preferences.Style, preferences.Topmost);

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _pollTimer.Tick += PollTimer_Tick;
        _pollTimer.Start();
        PollTimer_Tick(this, EventArgs.Empty);

        var background = _arguments.Any(arg => string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase));
        var openSettings = _arguments.Any(arg => string.Equals(arg, "--settings", StringComparison.OrdinalIgnoreCase));
        var previewOverlay = _arguments.Any(arg => string.Equals(arg, "--overlay-preview", StringComparison.OrdinalIgnoreCase));
        if (openSettings) ShowSettings();
        else if (!background && !_settings.StartMinimized) ShowMixer();
        if (previewOverlay) ShowOverlay(new AutoVolumeApplied(AppLocalization.Text("プレビュー"), 100, _settings.FirstLaunchVolume));
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        if (_audio is null || _mixer is null) return;
        var snapshot = _audio.ReadSnapshot();
        _mixer.UpdateSnapshot(snapshot);
    }

    private void Audio_AutoVolumeApplied(object? sender, AutoVolumeApplied change) => ShowOverlay(change);

    private void ShowOverlay(AutoVolumeApplied change, AppSettings? settings = null)
    {
        var effective = settings ?? _settings;
        if (effective is null || _overlay is null || !effective.ShowOverlay) return;
        Application.Current.Dispatcher.BeginInvoke(() => _overlay.ShowChange(change.AppName, change.FromVolume, change.ToVolume, effective, change.ProcessId));
    }

    private void SettingsWindow_SettingsSaved(object? sender, AppSettings settings)
    {
        _settings = settings;
        Localization.SetLanguage(settings.Language);
        _audio?.UpdateSettings(settings, reapply: true);
        _visualizerService?.UpdateSettings(settings);
        _mixer?.UpdateSettings(settings);
        _visualizerWindow?.UpdateSettings(settings);
        _settingsWindow?.ApplyLocalization();
        _tray?.ApplyLocalization();
    }

    private void SaveVisualizerPreferences(VisualizerStyle style, bool? topmost)
    {
        if (_settings is null || _settingsService is null) return;
        _settings.VisualizerStyle = style;
        if (topmost.HasValue) _settings.VisualizerAlwaysOnTop = topmost.Value;
        _settings.Normalize();
        _settingsService.Save(_settings);
        _mixer?.UpdateSettings(_settings);
        _visualizerWindow?.UpdateSettings(_settings);
    }

    private void SaveCompactMode(bool compact)
    {
        if (_settings is null || _settingsService is null) return;
        _settings.CompactMixerMode = compact;
        _settings.Normalize();
        _settingsService.Save(_settings);
    }

    private void ShowVisualizerWindow()
    {
        _visualizerWindow?.ShowVisualizer();
    }

    private void ShowMixer()
    {
        if (_mixer is null || _audio is null) return;
        _settingsWindow?.Hide();
        _mixer.ShowNearTray();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is null || _settings is null) return;
        _mixer?.Hide();
        _settingsWindow.Reload(_settings);
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void Exit()
    {
        _pollTimer?.Stop();
        _mixer?.Hide();
        _settingsWindow?.Hide();
        _overlay?.Hide();
        _visualizerWindow?.Hide();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer?.Stop();
        _signalWait?.Unregister(null);
        _signal?.Dispose();
        if (_audio is not null) _audio.AutoVolumeApplied -= Audio_AutoVolumeApplied;
        _audio?.Dispose();
        _visualizerService?.Dispose();
        _tray?.Dispose();
        if (_ownsMutex) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
