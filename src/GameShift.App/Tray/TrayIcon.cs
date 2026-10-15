using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GameShift.App.Localization;
using GameShift.Core;

namespace GameShift.App.Tray;

public sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _icon = new() { Visible = true, Text = "GameShift" };

    readonly ToolStripItem _work;
    readonly ToolStripItem _gaming;
    readonly ToolStripItem _open;
    readonly ToolStripItem _exit;

    public event Action<Mode>? ModeRequested;
    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        var menu = new ContextMenuStrip();
        _work = menu.Items.Add("", null, (_, _) => ModeRequested?.Invoke(Mode.Work));
        _gaming = menu.Items.Add("", null, (_, _) => ModeRequested?.Invoke(Mode.Gaming));
        menu.Items.Add(new ToolStripSeparator());
        _open = menu.Items.Add("", null, (_, _) => OpenRequested?.Invoke());
        _exit = menu.Items.Add("", null, (_, _) => ExitRequested?.Invoke());
        _icon.ContextMenuStrip = menu;
        ApplyLanguage();
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    public void ApplyLanguage()
    {
        _work.Text = ModeNames.Title(Mode.Work);
        _gaming.Text = ModeNames.Title(Mode.Gaming);
        _open.Text = Loc.Get("Tray_Open");
        _exit.Text = Loc.Get("Tray_Exit");
    }

    public void Update(Mode mode, string? game, System.Windows.Media.Color accent)
    {
        var color = mode == Mode.Work ? Color.Gray : Color.FromArgb(accent.R, accent.G, accent.B);
        var old = _icon.Icon;
        _icon.Icon = MakeIcon(color);
        old?.Dispose();
        var text = "GameShift: " + ModeNames.Title(mode) + (game is null ? "" : $" ({game})");
        _icon.Text = text.Length > 63 ? text[..63] : text;
    }

    public void Notify(string title, string text) => _icon.ShowBalloonTip(3000, title, text, ToolTipIcon.None);

    static Icon MakeIcon(Color color)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            DrawGlyph(g, 32, color);
        }
        var handle = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    /// <summary>Знак GameShift: два шеврона, второй сдвинут вправо. Та же геометрия, что в Assets/GameShift.ico (сетка 256).</summary>
    static void DrawGlyph(Graphics g, float size, Color color)
    {
        var k = size / 256f;
        using var pen = new Pen(color, 44 * k) { LineJoin = LineJoin.Miter, StartCap = LineCap.Flat, EndCap = LineCap.Flat };
        foreach (var x in new[] { 40f, 120f })
        {
            g.DrawLines(pen, new[]
            {
                new PointF(x * k, 46 * k),
                new PointF((x + 82) * k, 128 * k),
                new PointF(x * k, 210 * k),
            });
        }
    }

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }
}
