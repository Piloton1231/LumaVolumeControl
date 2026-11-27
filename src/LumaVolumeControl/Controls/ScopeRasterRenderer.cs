using System.Windows.Media.Imaging;
using LumaVolumeControl.Infrastructure;

namespace LumaVolumeControl.Controls;

// A bounded pixel surface avoids WPF tessellating thousands of glowing segments.
internal sealed class ScopeRasterRenderer
{
    private WriteableBitmap? _bitmap;
    private int[] _pixels = [];
    private int _width;
    private int _height;

    public void Draw(DrawingContext dc, Rect area, AudioVisualizerFrame frame, double gain)
    {
        var ratio = Math.Min(1, 640 / Math.Max(area.Width, area.Height));
        var width = Math.Max(2, (int)(area.Width * ratio));
        var height = Math.Max(2, (int)(area.Height * ratio));
        if (_bitmap is null || width != _width || height != _height)
        {
            _width = width;
            _height = height;
            _pixels = new int[width * height];
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
        }
        Array.Clear(_pixels);
        var scale = Math.Min(width, height) * 0.44;
        var count = Math.Min(frame.WaveLeft.Length, frame.WaveRight.Length);
        var lastX = 0;
        var lastY = 0;
        for (var i = 0; i < count; i++)
        {
            var x = (int)Math.Clamp(width * 0.5 + frame.WaveLeft[i] * gain * scale, 1, width - 2);
            var y = (int)Math.Clamp(height * 0.5 - frame.WaveRight[i] * gain * scale, 1, height - 2);
            if (i > 0) Line(lastX, lastY, x, y);
            lastX = x;
            lastY = y;
        }
        _bitmap.WritePixels(new Int32Rect(0, 0, width, height), _pixels, width * 4, 0);
        dc.DrawImage(_bitmap, area);
    }

    private void Line(int x, int y, int endX, int endY)
    {
        var dx = Math.Abs(endX - x);
        var dy = -Math.Abs(endY - y);
        var sx = x < endX ? 1 : -1;
        var sy = y < endY ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            var index = y * _width + x;
            _pixels[index] = 0x91FFA8;
            Glow(index - 1);
            Glow(index + 1);
            Glow(index - _width);
            Glow(index + _width);
            if (x == endX && y == endY) break;
            var twice = error * 2;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
        }
    }

    private void Glow(int index)
    {
        if (_pixels[index] == 0) _pixels[index] = 0x164828;
    }
}
