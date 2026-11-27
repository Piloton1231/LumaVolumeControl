using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace LumaVolumeControl.Infrastructure;

public static class AppIconService
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static ImageSource? TryLoad(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;
        lock (Gate)
        {
            if (Cache.TryGetValue(executablePath, out var cached)) return cached;
            ImageSource? result = null;
            try
            {
                using var icon = Drawing.Icon.ExtractAssociatedIcon(executablePath);
                if (icon is not null)
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(48, 48));
                    source.Freeze();
                    result = source;
                }
            }
            catch { }
            Cache[executablePath] = result;
            return result;
        }
    }
}
