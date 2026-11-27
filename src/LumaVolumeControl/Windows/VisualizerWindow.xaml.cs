using LumaVolumeControl.Infrastructure;
using LumaVolumeControl.Models;

namespace LumaVolumeControl.Windows;

public partial class VisualizerWindow : Window
{
    private readonly AudioVisualizerService _visualizer;
    private readonly VisualizerFrameClock _timer;
    private bool _fullscreen;
    private Rect _restoreBounds;
    private bool _toolbarVisible;
    private AppSettings _settings;
    private bool _updating;

    public VisualizerWindow(AudioVisualizerService visualizer, AppSettings settings)
    {
        InitializeComponent();
        _visualizer = visualizer;
        _settings = settings;
        _timer = new VisualizerFrameClock();
        _timer.Tick += (_, _) => VisualizerCanvas.SetFrame(_visualizer.GetFrame());
        IsVisibleChanged += (_, _) => UpdateActivity();
        StateChanged += (_, _) => UpdateActivity();
        UpdateSettings(settings);
    }

    public event EventHandler<(VisualizerStyle Style, bool Topmost)>? PreferencesChanged;

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        _updating = true;
        ModeCombo.SelectedIndex = (int)settings.VisualizerStyle;
        VisualizerCanvas.Mode = settings.VisualizerStyle;
        VisualizerCanvas.OscilloscopeSensitivity = settings.OscilloscopeSensitivity;
        VisualizerCanvas.AudioReactionSensitivity = settings.AudioReactionSensitivity;
        Topmost = settings.VisualizerAlwaysOnTop;
        UpdatePin();
        UpdateTimerInterval(settings.VisualizerStyle);
        _updating = false;
        ApplyLocalization();
    }

    public void ShowVisualizer()
    {
        if (!IsVisible) Show();
        WindowState = WindowState.Normal;
        Activate();
        UpdateActivity();
    }

    private void UpdateActivity()
    {
        var active = IsVisible && WindowState != WindowState.Minimized;
        _visualizer.SetConsumerActive("detached", active);
        if (active) _timer.Start(); else _timer.Stop();
    }

    private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeCombo.SelectedIndex < 0) return;
        var style = (VisualizerStyle)ModeCombo.SelectedIndex;
        VisualizerCanvas.Mode = style;
        UpdateTimerInterval(style);
        if (!_updating) PreferencesChanged?.Invoke(this, (style, Topmost));
    }

    private void UpdateTimerInterval(VisualizerStyle style)
    {
        var frameRate = _settings.VisualizerFrameRate;
        _timer.Interval = TimeSpan.FromMilliseconds(1000d / frameRate);
    }

    private void Topmost_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePin();
        PreferencesChanged?.Invoke(this, (VisualizerCanvas.Mode, Topmost));
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    private void UpdatePin()
    {
        TopmostButton.Content = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 7,2 L 17,2 L 16,9 L 20,13 L 20,15 L 13,15 L 13,22 L 11,25 L 11,15 L 4,15 L 4,13 L 8,9 Z"),
            Width = 17, Height = 22, Stretch = Stretch.Uniform,
            Stroke = Brushes.White, StrokeThickness = 1.5,
            Fill = Topmost ? Brushes.White : Brushes.Transparent
        };
        TopmostButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Topmost ? "#514078" : "#202431"));
        TopmostButton.ToolTip = AppLocalization.Text(Topmost ? "最前面表示 ON" : "最前面表示 OFF");
    }
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_fullscreen && e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Toolbar.IsMouseOver) return;
        ToggleFullscreen();
        e.Handled = true;
    }
    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _restoreBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
            var screen = WinForms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            var dpi = VisualTreeHelper.GetDpi(this);
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;
            Left = screen.Bounds.Left / dpi.DpiScaleX;
            Top = screen.Bounds.Top / dpi.DpiScaleY;
            Width = screen.Bounds.Width / dpi.DpiScaleX;
            Height = screen.Bounds.Height / dpi.DpiScaleY;
            _fullscreen = true;
            RevealToolbar(false);
        }
        else
        {
            _fullscreen = false;
            ResizeMode = ResizeMode.CanResize;
            Left = _restoreBounds.Left; Top = _restoreBounds.Top;
            Width = _restoreBounds.Width; Height = _restoreBounds.Height;
        }
    }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _fullscreen) { ToggleFullscreen(); e.Handled = true; }
    }
    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        RevealToolbar(_fullscreen ? point.Y < 72 || ModeCombo.IsDropDownOpen : true);
    }
    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!ModeCombo.IsDropDownOpen) RevealToolbar(false);
    }
    private void RevealToolbar(bool show)
    {
        if (_toolbarVisible == show) return;
        _toolbarVisible = show;
        Toolbar.IsHitTestVisible = show;
        Toolbar.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(160)));
        ToolbarShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(show ? 0 : -64, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { e.Cancel = true; Hide(); }

    public void ApplyLocalization() => AppLocalization.Apply(this);
}
