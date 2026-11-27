using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace LumaVolumeControl.Infrastructure;

public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly Drawing.Icon _icon;
    private readonly WinForms.ContextMenuStrip _menu;
    private WinForms.ToolStripMenuItem _openItem = null!;
    private WinForms.ToolStripMenuItem _visualizerItem = null!;
    private WinForms.ToolStripMenuItem _settingsItem = null!;
    private WinForms.ToolStripMenuItem _exitItem = null!;

    public TrayIconService()
    {
        _icon = CreateIcon();
        _menu = CreateMenu();
        _notifyIcon = new WinForms.NotifyIcon { Icon = _icon, Text = "Luma Volume Control", Visible = true, ContextMenuStrip = _menu };
        _notifyIcon.MouseClick += OnMouseClick;
        ApplyLocalization();
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? VisualizerRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    private void OnMouseClick(object? sender, WinForms.MouseEventArgs e)
    {
        // Leave the native NotifyIcon callback before creating a transparent WPF window.
        if (e.Button == WinForms.MouseButtons.Left)
            Application.Current.Dispatcher.BeginInvoke(() => OpenRequested?.Invoke(this, EventArgs.Empty));
    }

    private WinForms.ContextMenuStrip CreateMenu()
    {
        var menu = new WinForms.ContextMenuStrip
        {
            BackColor = Drawing.Color.FromArgb(21, 24, 33),
            ForeColor = Drawing.Color.FromArgb(245, 247, 251),
            ShowImageMargin = false,
            Renderer = new WinForms.ToolStripProfessionalRenderer(new DarkColorTable()),
            Padding = new WinForms.Padding(5),
            Font = new Drawing.Font("Segoe UI", 9.5f)
        };
        _openItem = new WinForms.ToolStripMenuItem { Padding = new WinForms.Padding(10, 7, 24, 7) };
        _visualizerItem = new WinForms.ToolStripMenuItem { Padding = new WinForms.Padding(10, 7, 24, 7) };
        _settingsItem = new WinForms.ToolStripMenuItem { Padding = new WinForms.Padding(10, 7, 24, 7) };
        _exitItem = new WinForms.ToolStripMenuItem { Padding = new WinForms.Padding(10, 7, 24, 7), ForeColor = Drawing.Color.FromArgb(255, 141, 160) };
        _openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _visualizerItem.Click += (_, _) => VisualizerRequested?.Invoke(this, EventArgs.Empty);
        _settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(_openItem);
        menu.Items.Add(_visualizerItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_exitItem);
        return menu;
    }

    public void ApplyLocalization()
    {
        _openItem.Text = AppLocalization.Text("ボリュームミキサーを開く");
        _visualizerItem.Text = AppLocalization.Text("オーディオビジュアライザーを開く");
        _settingsItem.Text = AppLocalization.Text("設定");
        _exitItem.Text = AppLocalization.Text("Luma Volume Control を終了");
    }

    private static Drawing.Icon CreateIcon()
    {
        using var bitmap = new Drawing.Bitmap(64, 64);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Drawing.Color.Transparent);
        using var gradient = new Drawing2D.LinearGradientBrush(new Drawing.Rectangle(3, 3, 58, 58), Drawing.Color.FromArgb(139, 108, 255), Drawing.Color.FromArgb(64, 206, 255), 42);
        graphics.FillEllipse(gradient, 3, 3, 58, 58);
        using var dark = new Drawing.SolidBrush(Drawing.Color.FromArgb(21, 24, 33));
        graphics.FillEllipse(dark, 11, 11, 42, 42);
        using var white = new Drawing.SolidBrush(Drawing.Color.White);
        graphics.FillPolygon(white, [new Drawing.Point(18, 27), new Drawing.Point(26, 27), new Drawing.Point(36, 19), new Drawing.Point(36, 45), new Drawing.Point(26, 37), new Drawing.Point(18, 37)]);
        using var pen = new Drawing.Pen(Drawing.Color.White, 3.5f) { StartCap = Drawing2D.LineCap.Round, EndCap = Drawing2D.LineCap.Round };
        graphics.DrawArc(pen, 34, 24, 14, 16, -55, 110);
        var handle = bitmap.GetHicon();
        try { using var temporary = Drawing.Icon.FromHandle(handle); return (Drawing.Icon)temporary.Clone(); }
        finally { NativeMethods.DestroyIcon(handle); }
    }

    public void Dispose()
    {
        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private sealed class DarkColorTable : WinForms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Background = Drawing.Color.FromArgb(21, 24, 33);
        private static readonly Drawing.Color Hover = Drawing.Color.FromArgb(48, 40, 74);
        private static readonly Drawing.Color Stroke = Drawing.Color.FromArgb(59, 66, 84);
        public override Drawing.Color ToolStripDropDownBackground => Background;
        public override Drawing.Color ImageMarginGradientBegin => Background;
        public override Drawing.Color ImageMarginGradientMiddle => Background;
        public override Drawing.Color ImageMarginGradientEnd => Background;
        public override Drawing.Color MenuItemSelected => Hover;
        public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(110, 89, 178);
        public override Drawing.Color SeparatorDark => Stroke;
        public override Drawing.Color SeparatorLight => Background;
        public override Drawing.Color ToolStripBorder => Stroke;
    }
}
