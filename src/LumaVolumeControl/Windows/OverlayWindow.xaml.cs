using LumaVolumeControl.Models;
using LumaVolumeControl.Infrastructure;

namespace LumaVolumeControl.Windows;

public partial class OverlayWindow : Window
{
    private DispatcherTimer? _numberTimer;
    private DispatcherTimer? _hideTimer;
    private DateTime _animationStarted;
    private int _from;
    private int _to;
    private int _animationMs;

    public OverlayWindow() => InitializeComponent();

    public void ShowChange(string appName, int from, int to, AppSettings settings, int processId = 0)
    {
        _numberTimer?.Stop();
        _hideTimer?.Stop();
        _from = Math.Clamp(from, 0, 100);
        _to = Math.Clamp(to, 0, 100);
        _animationMs = settings.AnimateVolumeChange ? Math.Clamp(Math.Abs(_from - _to) * 12, 280, 850) : 1;
        _animationStarted = DateTime.UtcNow;

        AppNameText.Text = appName;
        AppLocalization.Apply(this);
        ChangeText.Text = string.Format(AppLocalization.Text("{0}% → {1}% にしました"), _from, _to);
        VersionText.Visibility = settings.ShowVersionBadge ? Visibility.Visible : Visibility.Collapsed;
        ApplyCorner(settings.VersionBadgeCorner);
        PositionOnTargetMonitor(settings.OverlayPlacement, processId);

        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ProgressScale.ScaleX = _from / 100d;
        var progress = new DoubleAnimation(_to / 100d, TimeSpan.FromMilliseconds(_animationMs)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, progress);

        VolumeText.Text = $"{_from}%";
        _numberTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _numberTimer.Tick += NumberTimer_Tick;
        _numberTimer.Start();

        if (!IsVisible) Show();
        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(settings.OverlayDurationMs) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer?.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(230));
            fade.Completed += (_, _) => Hide();
            BeginAnimation(OpacityProperty, fade);
        };
        _hideTimer.Start();
    }

    private void NumberTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _animationStarted).TotalMilliseconds;
        var t = Math.Clamp(elapsed / _animationMs, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3);
        VolumeText.Text = $"{(int)Math.Round(_from + (_to - _from) * eased)}%";
        if (t < 1) return;
        VolumeText.Text = $"{_to}%";
        _numberTimer?.Stop();
    }

    private void ApplyCorner(OverlayCorner corner)
    {
        VersionText.HorizontalAlignment = corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        VersionText.VerticalAlignment = corner is OverlayCorner.TopLeft or OverlayCorner.TopRight ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        VersionText.Margin = corner switch
        {
            OverlayCorner.TopLeft => new Thickness(0, -12, 0, 0),
            OverlayCorner.TopRight => new Thickness(0, -12, 0, 0),
            OverlayCorner.BottomLeft => new Thickness(0, 0, 0, -12),
            _ => new Thickness(0, 0, 0, -12)
        };
    }

    private void PositionOnTargetMonitor(OverlayPlacement placement, int processId)
    {
        WinForms.Screen screen;
        try
        {
            using var process = processId > 0 ? Process.GetProcessById(processId) : null;
            screen = process is not null && process.MainWindowHandle != IntPtr.Zero
                ? WinForms.Screen.FromHandle(process.MainWindowHandle)
                : WinForms.Screen.FromPoint(WinForms.Cursor.Position);
        }
        catch
        {
            screen = WinForms.Screen.FromPoint(WinForms.Cursor.Position);
        }
        var dpi = VisualTreeHelper.GetDpi(this);
        var area = screen.WorkingArea;
        var left = area.Left / dpi.DpiScaleX;
        var top = area.Top / dpi.DpiScaleY;
        var width = area.Width / dpi.DpiScaleX;
        var height = area.Height / dpi.DpiScaleY;
        const double margin = 42;
        var centerLeft = left + (width - Width) / 2;
        var centerTop = top + (height - Height) / 2;
        Left = placement switch
        {
            OverlayPlacement.TopLeft or OverlayPlacement.BottomLeft => left + margin,
            OverlayPlacement.TopRight or OverlayPlacement.BottomRight => left + width - Width - margin,
            _ => centerLeft
        };
        Top = placement switch
        {
            OverlayPlacement.TopCenter or OverlayPlacement.TopLeft or OverlayPlacement.TopRight => top + margin,
            OverlayPlacement.BottomCenter or OverlayPlacement.BottomLeft or OverlayPlacement.BottomRight => top + height - Height - margin,
            _ => centerTop
        };
    }
}
