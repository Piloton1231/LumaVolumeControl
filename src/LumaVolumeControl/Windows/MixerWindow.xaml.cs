using LumaVolumeControl.Infrastructure;
using LumaVolumeControl.Models;

namespace LumaVolumeControl.Windows;

public partial class MixerWindow : Window
{
    private const double NormalMeterHeight = 175;
    private const double CompactMeterHeight = 150;
    private const double NormalWindowWidth = 880;
    private const double NormalWindowHeight = 580;
    private const double CompactWindowWidth = 720;
    private const double CompactWindowHeight = 520;
    private readonly AudioSessionService _audio;
    private readonly AudioVisualizerService _visualizer;
    private readonly VisualizerFrameClock _visualizerTimer;
    private readonly Dictionary<string, SessionColumnControls> _rows = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings _settings;
    private AudioSnapshot? _snapshot;
    private bool _updating;
    private bool _masterMuted;
    private double _masterDisplayedPeak;
    private string _displayedSignature = string.Empty;
    private bool _showingVisualizer;
    private bool _updatingVisualizerStyle;
    private bool _showingWindow;

    public MixerWindow(AudioSessionService audio, AudioVisualizerService visualizer, AppSettings settings)
    {
        InitializeComponent();
        _audio = audio;
        _visualizer = visualizer;
        _settings = settings;
        _visualizerTimer = new VisualizerFrameClock();
        _visualizerTimer.Tick += (_, _) => EmbeddedVisualizer.SetFrame(_visualizer.GetFrame());
        IsVisibleChanged += (_, _) => UpdateVisualizerActivity();
        UpdateSettings(settings);
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? DetachVisualizerRequested;
    public event EventHandler<VisualizerStyle>? VisualizerStyleChanged;
    public event EventHandler<AutoVolumeApplied>? VolumeAdjusted;
    public event EventHandler<bool>? CompactModeChanged;

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        AutoStatusText.Text = AppLocalization.Text(settings.AutoVolumeEnabled ? "● 自動調整 ON" : "● 自動調整 OFF");
        AutoStatusText.Foreground = Brush(settings.AutoVolumeEnabled ? "#8FD9B4" : "#8B93A3");
        _updatingVisualizerStyle = true;
        VisualizerStyleCombo.SelectedIndex = (int)settings.VisualizerStyle;
        EmbeddedVisualizer.Mode = settings.VisualizerStyle;
        EmbeddedVisualizer.OscilloscopeSensitivity = settings.OscilloscopeSensitivity;
        EmbeddedVisualizer.AudioReactionSensitivity = settings.AudioReactionSensitivity;
        UpdateVisualizerTimerInterval(settings.VisualizerStyle);
        _visualizer.UpdateSettings(settings);
        _updatingVisualizerStyle = false;
        ApplyMixerDensity(settings.CompactMixerMode);
        ApplyUiScale(settings.UiScalePercent);
        ApplyLocalization();
    }

    private void SetVisualizerView(bool show)
    {
        _showingVisualizer = show;
        MixerToolbar.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        MixerPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        VisualizerPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        FooterHint.Text = AppLocalization.Text(show ? "種類を選択 · 別ウィンドウ表示に対応 · Escで閉じる" : "ホイールで調整 · Escで閉じる · トレイ右クリックで終了");
        UpdateVisualizerActivity();
    }

    private void UpdateVisualizerActivity()
    {
        var active = _showingVisualizer && IsVisible;
        _visualizer.SetConsumerActive("embedded", active);
        if (active) _visualizerTimer.Start(); else _visualizerTimer.Stop();
    }

    private void ApplyUiScale(int percent)
    {
        var scale = Math.Clamp(percent, 70, 100) / 100d;
        var compact = _settings.CompactMixerMode;
        var baseWidth = compact ? CompactWindowWidth : NormalWindowWidth;
        var baseHeight = compact ? CompactWindowHeight : NormalWindowHeight;
        var baseMinWidth = compact ? 580 : 660;
        var baseMinHeight = compact ? 460 : 500;
        var baseMaxHeight = compact ? 650 : 720;
        MixerShell.LayoutTransform = new ScaleTransform(scale, scale);
        MinWidth = baseMinWidth * scale;
        MinHeight = baseMinHeight * scale;
        MaxHeight = baseMaxHeight * scale;
        Width = baseWidth * scale;
        Height = baseHeight * scale;
    }

    private double CurrentMeterHeight => _settings.CompactMixerMode ? CompactMeterHeight : NormalMeterHeight;
    private double CurrentMeterRailHeight => CurrentMeterHeight - 28;

    private void ApplyMixerDensity(bool compact)
    {
        ToolbarDeviceColumn.Width = new GridLength(compact ? 116 : 150);
        MixerDeviceColumn.Width = new GridLength(compact ? 116 : 150);
        ToolbarGapColumn.Width = new GridLength(compact ? 8 : 12);
        MixerGapColumn.Width = new GridLength(compact ? 8 : 12);
        MasterCard.Padding = new Thickness(compact ? 8 : 12);
        MasterIconRow.Height = new GridLength(compact ? 42 : 48);
        MasterNameRow.Height = new GridLength(compact ? 34 : 42);
        MasterPercentRow.Height = new GridLength(compact ? 26 : 30);
        MasterMuteRow.Height = new GridLength(compact ? 42 : 46);
        MasterMeter.Height = CurrentMeterHeight;
        MasterMeterRail.Height = CurrentMeterRailHeight;
        MasterSlider.Height = CurrentMeterHeight;
        SearchBox.Width = compact ? 220 : 280;
        SessionList.Margin = new Thickness(0, 0, 0, compact ? 5 : 8);
        CompactModeButton.Background = Brush(compact ? "#56428A" : "#202431");
        CompactModeButton.BorderBrush = Brush(compact ? "#9B83F5" : "#303646");
        CompactModeButton.ToolTip = AppLocalization.Text(compact ? "標準表示に戻す（Ctrl+M）" : "コンパクトモード（Ctrl+M）");

        _displayedSignature = string.Empty;
        if (_snapshot is not null) RebuildSessions(FilteredSessions());
    }

    public void UpdateSnapshot(AudioSnapshot snapshot, bool force = false)
    {
        _snapshot = snapshot;
        if (!IsVisible && !force) return;
        if (_showingVisualizer && !force) return;

        var sliderIsBeingDragged = Mouse.Captured is not null;
        _updating = true;
        DeviceText.Text = snapshot.DeviceName;
        if (!sliderIsBeingDragged) MasterSlider.Value = snapshot.MasterVolume;
        MasterValueText.Text = $"{snapshot.MasterVolume}%";
        _masterMuted = snapshot.MasterMuted;
        MasterMuteButton.Content = MuteIcon(snapshot.MasterMuted, snapshot.MasterVolume);
        MasterMuteButton.Tag = snapshot.MasterMuted;
        _masterDisplayedPeak = UpdateMeter(MasterLevelFill, snapshot.MasterPeak, _masterDisplayedPeak, CurrentMeterRailHeight);
        _updating = false;

        var visibleSessions = FilteredSessions();
        var signature = string.Join('|', visibleSessions.Select(session => $"{session.Key}:{session.IsRemembered}"));
        if (force || !string.Equals(signature, _displayedSignature, StringComparison.Ordinal))
        {
            _displayedSignature = signature;
            RebuildSessions(visibleSessions);
        }
        else
        {
            UpdateSessionColumns(visibleSessions, !sliderIsBeingDragged);
        }
    }

    public void ShowNearTray()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        if (_showingWindow) return;
        _showingWindow = true;
        try
        {
            Show();
            Activate();
        }
        finally { _showingWindow = false; }
        var cursor = WinForms.Cursor.Position;
        var screen = WinForms.Screen.FromPoint(cursor);
        var dpi = VisualTreeHelper.GetDpi(this);
        var workLeft = screen.WorkingArea.Left / dpi.DpiScaleX;
        var workTop = screen.WorkingArea.Top / dpi.DpiScaleY;
        var workRight = screen.WorkingArea.Right / dpi.DpiScaleX;
        var workBottom = screen.WorkingArea.Bottom / dpi.DpiScaleY;
        Left = Math.Max(workLeft + 8, workRight - ActualWidth - 14);
        Top = Math.Max(workTop + 8, workBottom - ActualHeight - 14);
        UpdateSnapshot(_audio.ReadSnapshot(), true);
    }

    private AudioAppSession[] FilteredSessions()
    {
        if (_snapshot is null) return [];
        var query = SearchBox.Text.Trim();
        return _snapshot.Sessions
            .Where(session => string.IsNullOrWhiteSpace(query)
                || session.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || session.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private void RebuildSessions(AudioAppSession[] sessions)
    {
        var offset = SessionScroll.HorizontalOffset;
        SessionList.Children.Clear();
        _rows.Clear();
        EmptyState.Visibility = sessions.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var session in sessions) SessionList.Children.Add(CreateSessionCard(session));
        Dispatcher.BeginInvoke(() => SessionScroll.ScrollToHorizontalOffset(offset), DispatcherPriority.Loaded);
    }

    private void UpdateSessionColumns(IEnumerable<AudioAppSession> sessions, bool updateSliders)
    {
        _updating = true;
        foreach (var session in sessions)
        {
            if (!_rows.TryGetValue(session.Key, out var row)) continue;
            if (updateSliders) row.Slider.Value = session.Volume;
            row.Percent.Text = $"{session.Volume}%";
            row.Mute.Content = MuteIcon(session.IsMuted, session.Volume);
            row.Mute.Tag = session.IsMuted;
            row.DisplayedPeak = UpdateMeter(row.LevelFill, session.Peak, row.DisplayedPeak, row.MeterRailHeight);
        }
        _updating = false;
    }

    private UIElement CreateSessionCard(AudioAppSession session)
    {
        var card = new Border
        {
            Width = _settings.CompactMixerMode ? 108 : 142,
            Background = Brush("#151821"),
            BorderBrush = Brush("#303646"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(_settings.CompactMixerMode ? 8 : 12),
            Margin = new Thickness(0, 0, _settings.CompactMixerMode ? 5 : 10, 0)
        };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_settings.CompactMixerMode ? 40 : 46) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_settings.CompactMixerMode ? 32 : 38) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_settings.CompactMixerMode ? 25 : 28) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_settings.CompactMixerMode ? 40 : 44) });

        var badge = new Border
        {
            Width = _settings.CompactMixerMode ? 36 : 42,
            Height = _settings.CompactMixerMode ? 36 : 42,
            CornerRadius = new CornerRadius(_settings.CompactMixerMode ? 10 : 12),
            Background = Brush("#282D3A"),
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = true
        };
        var appIcon = AppIconService.TryLoad(session.ExecutablePath);
        badge.Child = appIcon is not null
            ? new Image { Source = appIcon, Width = _settings.CompactMixerMode ? 29 : 34, Height = _settings.CompactMixerMode ? 29 : 34, Stretch = Stretch.Uniform }
            : new TextBlock { Text = session.Name.Length > 0 ? session.Name[..1].ToUpperInvariant() : "A", FontWeight = FontWeights.Bold, FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(badge);

        var name = new TextBlock
        {
            Text = session.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = session.Name
        };
        Grid.SetRow(name, 1);
        grid.Children.Add(name);

        var remembered = new TextBlock
        {
            Text = session.IsRemembered ? $"● {AppLocalization.Text("音量を記憶済み")}" : session.Key,
            Foreground = Brush(session.IsRemembered ? "#8FD9B4" : "#7F899A"),
            FontSize = 9,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(remembered, 2);
        grid.Children.Add(remembered);

        var percent = new TextBlock
        {
            Text = $"{session.Volume}%",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(percent, 3);
        grid.Children.Add(percent);

        var meterHeight = CurrentMeterHeight;
        var meterRailHeight = CurrentMeterRailHeight;
        var meter = new Grid { Width = 42, Height = meterHeight, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var levelFill = new Border
        {
            Width = 14,
            Height = 0,
            CornerRadius = new CornerRadius(7),
            VerticalAlignment = VerticalAlignment.Bottom,
            Opacity = 0.9,
            Background = MeterBrush()
        };
        var railContents = new Grid();
        railContents.Children.Add(levelFill);
        meter.Children.Add(new Border
        {
            Width = 14,
            Height = meterRailHeight,
            CornerRadius = new CornerRadius(7),
            Background = Brush("#282D3A"),
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
            Child = railContents
        });

        var slider = new Slider
        {
            Value = session.Volume,
            Tag = session.Key,
            TickFrequency = _settings.VolumeStep,
            Style = (Style)FindResource("VerticalVolumeSlider"),
            Height = meterHeight
        };
        slider.ValueChanged += (_, args) =>
        {
            if (_updating || !slider.IsMouseCaptureWithin) return;
            var value = (int)Math.Round(args.NewValue);
            percent.Text = $"{value}%";
            _audio.SetSessionVolume(session.Key, session.Name, value);
            VolumeAdjusted?.Invoke(this, new AutoVolumeApplied(session.Name, (int)Math.Round(args.OldValue), value, session.ProcessId));
        };
        slider.PreviewMouseWheel += Slider_PreviewMouseWheel;
        Panel.SetZIndex(slider, 2);
        meter.Children.Add(slider);
        Grid.SetRow(meter, 4);
        grid.Children.Add(meter);

        var mute = new Button
        {
            Content = MuteIcon(session.IsMuted, session.Volume),
            Tag = session.IsMuted,
            Style = (Style)FindResource("IconButton"),
            ToolTip = AppLocalization.Text("ミュート切替"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        mute.Click += (_, _) =>
        {
            var nextMuted = !(mute.Tag is true);
            _audio.SetSessionMute(session.Key, nextMuted);
            mute.Tag = nextMuted;
            mute.Content = MuteIcon(nextMuted, (int)Math.Round(slider.Value));
        };
        Grid.SetRow(mute, 5);
        grid.Children.Add(mute);

        card.Child = grid;
        var controls = new SessionColumnControls(slider, percent, mute, levelFill, meterRailHeight)
        {
            DisplayedPeak = UpdateMeter(levelFill, session.Peak, 0, meterRailHeight)
        };
        _rows[session.Key] = controls;
        return card;
    }

    private static double UpdateMeter(Border fill, float rawPeak, double displayedPeak, double meterRailHeight)
    {
        var target = Math.Pow(Math.Clamp(rawPeak, 0f, 1f), 0.46);
        var response = target >= displayedPeak ? 0.62 : 0.2;
        var smoothed = displayedPeak + (target - displayedPeak) * response;
        if (smoothed < 0.002) smoothed = 0;
        var nextHeight = meterRailHeight * smoothed;
        var currentHeight = fill.ActualHeight;
        fill.Height = nextHeight;
        fill.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation
        {
            From = currentHeight,
            To = nextHeight,
            Duration = TimeSpan.FromMilliseconds(95),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        }, HandoffBehavior.SnapshotAndReplace);
        fill.Opacity = smoothed > 0.015 ? 0.95 : 0.22;
        return smoothed;
    }

    private static string MuteIcon(bool muted, int volume) => muted ? "🔇" : volume < 45 ? "🔉" : "🔊";
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static LinearGradientBrush MeterBrush() => new(
        new GradientStopCollection
        {
            new((Color)ColorConverter.ConvertFromString("#40CEFF"), 0),
            new((Color)ColorConverter.ConvertFromString("#8FFFD2"), 1)
        },
        new Point(0.5, 1),
        new Point(0.5, 0));

    private void MasterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || !MasterSlider.IsMouseCaptureWithin) return;
        var value = (int)Math.Round(e.NewValue);
        MasterValueText.Text = $"{value}%";
        _audio.SetMasterVolume(value);
        VolumeAdjusted?.Invoke(this, new AutoVolumeApplied("デバイス", (int)Math.Round(e.OldValue), value));
    }

    private void Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not Slider slider) return;
        slider.Value = Math.Clamp(slider.Value + (e.Delta > 0 ? _settings.VolumeStep : -_settings.VolumeStep), slider.Minimum, slider.Maximum);
        var value = (int)Math.Round(slider.Value);
        if (slider.Tag is string key)
        {
            var session = _snapshot?.Sessions.FirstOrDefault(item => item.Key == key);
            _audio.SetSessionVolume(key, session?.Name ?? key, value);
            if (_rows.TryGetValue(key, out var row)) row.Percent.Text = $"{value}%";
            if (session is not null) VolumeAdjusted?.Invoke(this, new AutoVolumeApplied(session.Name, session.Volume, value, session.ProcessId));
        }
        else
        {
            _audio.SetMasterVolume(value);
            MasterValueText.Text = $"{value}%";
        }
        e.Handled = true;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        _displayedSignature = string.Empty;
        if (_snapshot is not null) RebuildSessions(FilteredSessions());
    }

    private void SessionScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        SessionScroll.ScrollToHorizontalOffset(SessionScroll.HorizontalOffset - e.Delta * 0.7);
        e.Handled = true;
    }

    private void MasterMute_Click(object sender, RoutedEventArgs e)
    {
        var nextMuted = !_masterMuted;
        _audio.SetMasterMute(nextMuted);
        _masterMuted = nextMuted;
        MasterMuteButton.Tag = nextMuted;
        MasterMuteButton.Content = MuteIcon(nextMuted, (int)Math.Round(MasterSlider.Value));
    }

    private void CompactMode_Click(object sender, RoutedEventArgs e) => ToggleCompactMode();
    private void ToggleCompactMode()
    {
        _settings.CompactMixerMode = !_settings.CompactMixerMode;
        ApplyMixerDensity(_settings.CompactMixerMode);
        ApplyUiScale(_settings.UiScalePercent);
        CompactModeChanged?.Invoke(this, _settings.CompactMixerMode);
    }

    private void ToggleVisualizer_Click(object sender, RoutedEventArgs e) => SetVisualizerView(!_showingVisualizer);
    private void BackToMixer_Click(object sender, RoutedEventArgs e) => SetVisualizerView(false);
    private void OpenVisualizerWindow_Click(object sender, RoutedEventArgs e) => DetachVisualizerRequested?.Invoke(this, EventArgs.Empty);
    private void VisualizerStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VisualizerStyleCombo.SelectedIndex < 0) return;
        var style = (VisualizerStyle)VisualizerStyleCombo.SelectedIndex;
        EmbeddedVisualizer.Mode = style;
        UpdateVisualizerTimerInterval(style);
        if (!_updatingVisualizerStyle) VisualizerStyleChanged?.Invoke(this, style);
    }
    private void UpdateVisualizerTimerInterval(VisualizerStyle style)
    {
        var frameRate = _settings.VisualizerFrameRate;
        _visualizerTimer.Interval = TimeSpan.FromMilliseconds(1000d / frameRate);
    }
    private void Refresh_Click(object sender, RoutedEventArgs e) => UpdateSnapshot(_audio.ReadSnapshot(), true);
    private void Settings_Click(object sender, RoutedEventArgs e) { Hide(); SettingsRequested?.Invoke(this, EventArgs.Empty); }
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 1) DragMove(); }
    private void Window_Deactivated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_showingWindow && !IsActive && _settings.CloseMixerOnFocusLoss) Hide();
        }, DispatcherPriority.Background);
    }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Hide();
        else if (e.Key == Key.M && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ToggleCompactMode();
            e.Handled = true;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { e.Cancel = true; Hide(); }

    public void ApplyLocalization() => AppLocalization.Apply(this);

    private sealed class SessionColumnControls(Slider slider, TextBlock percent, Button mute, Border levelFill, double meterRailHeight)
    {
        public Slider Slider { get; } = slider;
        public TextBlock Percent { get; } = percent;
        public Button Mute { get; } = mute;
        public Border LevelFill { get; } = levelFill;
        public double MeterRailHeight { get; } = meterRailHeight;
        public double DisplayedPeak { get; set; }
    }
}
