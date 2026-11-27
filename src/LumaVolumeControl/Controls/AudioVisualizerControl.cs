using System.Globalization;
using LumaVolumeControl.Infrastructure;
using LumaVolumeControl.Models;

namespace LumaVolumeControl.Controls;

public sealed class AudioVisualizerControl : FrameworkElement
{
    private static readonly Color Purple = (Color)ColorConverter.ConvertFromString("#8B6CFF");
    private static readonly Color Cyan = (Color)ColorConverter.ConvertFromString("#40CEFF");
    private AudioVisualizerFrame _frame = AudioVisualizerFrame.Empty;
    private float _displayLeft;
    private float _displayRight;
    private double _scopeGain = 1;
    private readonly float[] _displayBands = new float[56];
    private long _lastFrameTimestamp;

    public VisualizerStyle Mode { get; set; } = VisualizerStyle.NeonSpectrum;
    public int OscilloscopeSensitivity { get; set; } = 100;
    public int AudioReactionSensitivity { get; set; } = 100;
    private readonly ScopeRasterRenderer _scopeRaster = new();

    public void SetFrame(AudioVisualizerFrame frame)
    {
        _frame = frame;
        var now = Stopwatch.GetTimestamp();
        var elapsed = _lastFrameTimestamp == 0 ? 1d / 60 : Math.Clamp((now - _lastFrameTimestamp) / (double)Stopwatch.Frequency, 0, 0.1);
        _lastFrameTimestamp = now;
        for (var i = 0; i < Math.Min(_displayBands.Length, frame.Bands.Length); i++)
        {
            var target = frame.Bands[i];
            var response = 1 - Math.Exp(-elapsed / (target > _displayBands[i] ? 0.025 : 0.09));
            _displayBands[i] += (float)((target - _displayBands[i]) * response);
        }
        var scopePeak = ScopePeak(frame);
        var targetGain = scopePeak < 0.003 ? 1 : Math.Clamp(0.84 / scopePeak, 1, 18);
        _scopeGain += (targetGain - _scopeGain) * (targetGain < _scopeGain ? 0.42 : 0.16);
        var reaction = Math.Clamp(AudioReactionSensitivity, 10, 300) / 100f;
        _displayLeft = Follow(_displayLeft, Math.Clamp(frame.LeftPeak * reaction, 0, 1));
        _displayRight = Follow(_displayRight, Math.Clamp(frame.RightPeak * reaction, 0, 1));
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var area = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(7, 10, 15)), null, area, 16, 16);
        if (ActualWidth < 80 || ActualHeight < 60) return;
        switch (Mode)
        {
            case VisualizerStyle.SmoothWave: DrawWave(dc, area); break;
            case VisualizerStyle.AnalogVu: DrawAnalog(dc, area); break;
            case VisualizerStyle.VfdMeter: DrawVfd(dc, area); break;
            case VisualizerStyle.LedMeter: DrawLed(dc, area); break;
            case VisualizerStyle.RoundOscilloscope: DrawRoundOscilloscope(dc, area); break;
            default: DrawSpectrum(dc, area); break;
        }
    }

    private void DrawSpectrum(DrawingContext dc, Rect area)
    {
        var plot = new Rect(30, 34, Math.Max(1, area.Width - 60), Math.Max(1, area.Height - 66));
        var count = Math.Min(_frame.Bands.Length, 56);
        var gap = Math.Max(2, plot.Width / count * 0.24);
        var barWidth = Math.Max(3, (plot.Width - gap * (count - 1)) / count);
        var gradient = new LinearGradientBrush(Purple, Cyan, new Point(0, 1), new Point(0, 0));
        for (var index = 0; index < count; index++)
        {
            var value = Math.Clamp(_displayBands[index] * AudioReactionSensitivity / 100f, 0, 1);
            var height = Math.Max(3, value * plot.Height);
            var rect = new Rect(plot.Left + index * (barWidth + gap), plot.Bottom - height, barWidth, height);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(34, Cyan.R, Cyan.G, Cyan.B)), null, new Rect(rect.X - 3, rect.Y - 5, rect.Width + 6, rect.Height + 8), 5, 5);
            dc.DrawRoundedRectangle(gradient, null, rect, barWidth / 2, barWidth / 2);
        }
        DrawText(dc, "NEON SPECTRUM", new Point(30, 12), 11, Color.FromRgb(117, 128, 148));
        var labels = new[] { "40 Hz", "200 Hz", "1 kHz", "5 kHz", "18 kHz" };
        var frequencies = new[] { 40d, 200, 1000, 5000, 18000 };
        for (var i = 0; i < labels.Length; i++)
        {
            var x = plot.Left + Math.Log(frequencies[i] / 40) / Math.Log(18000d / 40) * plot.Width;
            x = Math.Clamp(x, plot.Left + 18, plot.Right - 20);
            DrawCenteredText(dc, labels[i], new Point(x, area.Bottom - 15), 10, Color.FromRgb(117, 128, 148));
        }
    }

    private void DrawWave(DrawingContext dc, Rect area)
    {
        var mid = area.Height / 2;
        var width = Math.Max(1, area.Width - 48);
        var left = 24d;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var index = 0; index < _frame.WaveLeft.Length; index++)
            {
                var x = left + width * index / Math.Max(1, _frame.WaveLeft.Length - 1);
                var sample = Math.Clamp((_frame.WaveLeft[index] + _frame.WaveRight[index]) * 0.5 * AudioReactionSensitivity / 100d, -1, 1);
                var y = mid - sample * area.Height * 0.34;
                if (index == 0) context.BeginFigure(new Point(x, y), false, false); else context.LineTo(new Point(x, y), true, false);
            }
        }
        geometry.Freeze();
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(55, 98, 117, 143)), 1), new Point(24, mid), new Point(area.Width - 24, mid));
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(45, Cyan.R, Cyan.G, Cyan.B)), 10), geometry);
        dc.DrawGeometry(null, new Pen(new LinearGradientBrush(Purple, Cyan, new Point(0, 0), new Point(1, 0)), 2.5), geometry);
        DrawText(dc, "SMOOTH WAVE", new Point(30, 16), 11, Color.FromRgb(117, 128, 148));
    }

    private void DrawAnalog(DrawingContext dc, Rect area)
    {
        dc.DrawRectangle(Brushes.Black, null, area);
        var scale = Math.Min(area.Width / 1320, area.Height / 430);
        dc.PushTransform(new TranslateTransform((area.Width - 1320 * scale) / 2, (area.Height - 430 * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        DrawReferenceVu(dc, 8, _displayLeft);
        DrawReferenceVu(dc, 672, _displayRight);
        dc.Pop();
        dc.Pop();
    }

    private void DrawReferenceVu(DrawingContext dc, double x, float value)
    {
        dc.PushTransform(new TranslateTransform(x, 10));
        var gold = Color.FromRgb(197, 143, 24);
        var red = Color.FromRgb(185, 12, 10);
        var normalized = Math.Clamp(Math.Pow(value, 0.48), 0, 1);
        var glow = Math.Clamp((normalized - 0.7375) / 0.2625, 0, 1);
        dc.DrawRoundedRectangle(Brushes.Black, new Pen(new SolidColorBrush(Color.FromRgb(117, 109, 73)), 4), new Rect(0, 0, 640, 400), 18, 18);
        var pivot = new Point(320, 585);
        const double radius = 402;
        for (var i = 0; i < 100; i++)
        {
            var angle = 230 + i * 0.8;
            if (i >= 74 && glow > 0)
            {
                var glowStart = Polar(pivot, radius, angle);
                var endGlow = Polar(pivot, radius, angle + 1);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb((byte)(glow * 40), 255, 15, 8)), 35), glowStart, endGlow);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb((byte)(glow * 110), 255, 32, 14)), 21), glowStart, endGlow);
            }
            dc.DrawLine(new Pen(new SolidColorBrush(i < 74 ? gold : red), 13), Polar(pivot, radius, angle), Polar(pivot, radius, angle + 1));
        }
        var labels = new[] { "20", "15", "10", "7", "5", "3", "1", "0", "3", "5" };
        var angles = new[] { 232d, 240, 248, 255, 262, 270, 281, 289, 301, 309 };
        for (var i = 0; i < labels.Length; i++)
        {
            var color = i < 7 ? gold : red;
            var dot = Polar(pivot, radius + 20, angles[i]);
            dc.DrawEllipse(new SolidColorBrush(color), null, dot, 4, 4);
            DrawCenteredText(dc, labels[i], Polar(pivot, radius + 46, angles[i]), 29, color);
        }
        for (var i = 0; i <= 44; i++)
        {
            var angle = 230 + i * 80d / 44;
            dc.DrawLine(new Pen(new SolidColorBrush(gold), 3), Polar(pivot, 299, angle), Polar(pivot, i % 5 == 0 ? 276 : 280, angle));
        }
        DrawCenteredText(dc, "dB", new Point(320, 102), 24, gold);
        DrawCenteredText(dc, "10", Polar(pivot, 328, 230), 22, gold);
        DrawCenteredText(dc, "50", Polar(pivot, 328, 258), 22, gold);
        DrawCenteredText(dc, "100", Polar(pivot, 328, 292), 22, gold);
        DrawCenteredText(dc, "%", new Point(320, 309), 23, gold);
        DrawCenteredText(dc, "VU", new Point(320, 360), 35, gold);
        var end = Polar(pivot, radius + 7, 230 + 80 * normalized);
        var start = new Point(320 + (end.X - 320) * (185d / (585 - end.Y)), 400);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(210, 212, 208)), 3), start, end);
        dc.Pop();
    }


    private void DrawVfd(DrawingContext dc, Rect area)
    {
        // All bars, ticks and labels share one reference coordinate system.
        var scale = Math.Min(area.Width / 809, area.Height / 430);
        dc.PushTransform(new TranslateTransform((area.Width - 809 * scale) / 2, (area.Height - 430 * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, 809, 430));
        DrawVfdChannel(dc, new Rect(0, 0, 809, 430), 145d / 430, _displayLeft, "L");
        DrawVfdChannel(dc, new Rect(0, 0, 809, 430), 311d / 430, _displayRight, "R");
        var white = Color.FromRgb(231, 233, 230);
        var red = Color.FromRgb(191, 65, 39);
        DrawCenteredText(dc, "dB", new Point(65, 229), 32, red);
        for (var db = -42; db <= 10; db++)
        {
            var x = 123 + (db + 40) / 50d * 589;
            var major = db % 10 == 0;
            var pen = new Pen(new SolidColorBrush(db > 0 ? red : white), major ? 4 : 3);
            dc.DrawLine(pen, new Point(x, 206), new Point(x, major ? 185 : 194));
            dc.DrawLine(pen, new Point(x, 250), new Point(x, major ? 271 : 263));
            if (major) DrawCenteredText(dc, db > 0 ? $"+{db}" : db.ToString(), new Point(x, 229), 32, white);
        }
        dc.Pop();
        dc.Pop();
    }

    private void DrawVfdChannel(DrawingContext dc, Rect area, double yFactor, float peak, string channel)
    {
        var left = 98d;
        var right = 712d;
        var y = area.Height * yFactor;
        var segments = 53;
        var db = peak > 0.00001 ? 20 * Math.Log10(peak) : -100;
        var active = (int)Math.Clamp(Math.Round((db + 42) / 52 * segments), 0, segments);
        DrawCenteredText(dc, channel, new Point(56, y), 34, Color.FromRgb(191, 65, 39));
        for (var index = 0; index < segments; index++)
        {
            var x = left + (right - left) * index / segments;
            var width = 4d;
            var on = index < active;
            var t = index / (segments - 1d);
            var color = Color.FromRgb((byte)(12 + 214 * t), (byte)(192 - 77 * t), (byte)(109 - 70 * t));
            dc.DrawRectangle(new SolidColorBrush(on ? color : Color.FromArgb(12, color.R, color.G, color.B)), null, new Rect(x, y - 25, width, 50));
        }
    }

    private void DrawLed(DrawingContext dc, Rect area)
    {
        var panel = Inset(area, 26, 30);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(3, 13, 18)), new Pen(new SolidColorBrush(Color.FromRgb(20, 42, 53)), 1), panel, 10, 10);
        DrawLedChannel(dc, panel, panel.Top + panel.Height * 0.33, _displayLeft, "L");
        DrawLedChannel(dc, panel, panel.Top + panel.Height * 0.7, _displayRight, "R");
        var labels = new[] { "-40", "-30", "-20", "-10", "0", "+10" };
        for (var index = 0; index < labels.Length; index++)
        {
            var x = panel.Left + 68 + (panel.Width - 104) * index / (labels.Length - 1d);
            DrawCenteredText(dc, labels[index], new Point(x, panel.Top + panel.Height * 0.5), 11, Color.FromRgb(151, 165, 175));
        }
    }

    private void DrawLedChannel(DrawingContext dc, Rect panel, double y, float peak, string channel)
    {
        const int segments = 32;
        var left = panel.Left + 68;
        var right = panel.Right - 28;
        var gap = 4d;
        var width = (right - left - gap * (segments - 1)) / segments;
        var active = (int)Math.Round(Math.Pow(Math.Clamp(peak, 0, 1), 0.48) * segments);
        DrawText(dc, channel, new Point(panel.Left + 25, y - 15), 19, Color.FromRgb(205, 214, 220));
        for (var index = 0; index < segments; index++)
        {
            var color = index >= 28 ? Color.FromRgb(255, 74, 73) : index >= 23 ? Color.FromRgb(255, 192, 65) : Color.FromRgb(36, 229, 101);
            var on = index < active;
            var rect = new Rect(left + index * (width + gap), y - 10, width, 20);
            if (on) dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(34, color.R, color.G, color.B)), null, new Rect(rect.X - 3, rect.Y - 4, rect.Width + 6, rect.Height + 8), 3, 3);
            dc.DrawRectangle(new SolidColorBrush(on ? color : Color.FromArgb(45, color.R, color.G, color.B)), null, rect);
        }
    }

    private void DrawRoundOscilloscope(DrawingContext dc, Rect area)
        => _scopeRaster.Draw(dc, area, _frame, _scopeGain * Math.Clamp(OscilloscopeSensitivity, 25, 200) / 100d * AudioReactionSensitivity / 100d);

    private static double ScopePeak(AudioVisualizerFrame frame)
    {
        var peak = 0d;
        var count = Math.Min(frame.WaveLeft.Length, frame.WaveRight.Length);
        for (var index = 0; index < count; index++)
            peak = Math.Max(peak, Math.Max(Math.Abs(frame.WaveLeft[index]), Math.Abs(frame.WaveRight[index])));
        return peak;
    }


    private static Rect Inset(Rect area, double x, double y) => new(area.Left + x, area.Top + y, Math.Max(0, area.Width - x * 2), Math.Max(0, area.Height - y * 2));
    private static Point Polar(Point center, double radius, double angleDegrees)
    {
        var angle = angleDegrees * Math.PI / 180;
        return new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
    }
    private static float Follow(float current, float next) => next >= current ? current + (next - current) * 0.72f : current * 0.88f;

    private void DrawText(DrawingContext dc, string text, Point point, double size, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, new SolidColorBrush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, point);
    }

    private void DrawCenteredText(DrawingContext dc, string text, Point center, double size, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, new SolidColorBrush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }
}
