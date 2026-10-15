using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameShift.App.Theme;

/// <summary>Знак GameShift (два шеврона на тёмной плашке) в цвете акцента для значка окон. Логотип в рельсе рисует XAML; геометрия как в GameShift.ico и TrayIcon.DrawGlyph.</summary>
public static class BrandMark
{
    public const double Grid = 256;
    public const double Stroke = 44;
    public const double TileRadius = 52;
    public static readonly Color Tile = Color.FromRgb(0x15, 0x15, 0x15);

    public static Geometry Chevrons { get; } = Frozen(Geometry.Parse("M40,46 L122,128 L40,210 M120,46 L202,128 L120,210"));

    static readonly int[] Sizes = [16, 20, 24, 32, 40, 48, 64, 256];
    static Color? _iconColor;

    public static ImageSource? Icon { get; private set; }

    public static void Follow(ThemeService theme)
    {
        Update(theme.Accent);
        theme.Changed += () => Update(theme.Accent);
    }

    public static void Attach(Window window)
    {
        if (Icon is not null) window.Icon = Icon;
    }

    static void Update(Color accent)
    {
        if (_iconColor == accent) return;
        try
        {
            Icon = MakeIcon(accent);
            _iconColor = accent;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or InvalidOperationException)
        {
            ErrorLog.Write(e);
            return;
        }
        foreach (Window w in Application.Current.Windows) w.Icon = Icon;
    }

    static ImageSource MakeIcon(Color accent)
    {
        // ICO из PNG-кадров: WPF берёт из декодера подходящий размер для заголовка и панели задач.
        var pngs = Sizes.Select(s => Png(Render(accent, s))).ToList();
        var ico = new MemoryStream();
        using (var w = new BinaryWriter(ico, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)Sizes.Length);
            var offset = 6 + 16 * Sizes.Length;
            for (var i = 0; i < Sizes.Length; i++)
            {
                var s = Sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((short)1);
                w.Write((short)32);
                w.Write(pngs[i].Length);
                w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
        }
        ico.Position = 0;
        var frame = BitmapFrame.Create(ico, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return frame;
    }

    public static BitmapSource Render(Color accent, int size)
    {
        var k = size / Grid;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(k, k));
            dc.DrawRoundedRectangle(new SolidColorBrush(Tile), null, new Rect(0, 0, Grid, Grid), TileRadius, TileRadius);
            var pen = new Pen(new SolidColorBrush(accent), Stroke)
            {
                LineJoin = PenLineJoin.Miter,
                StartLineCap = PenLineCap.Flat,
                EndLineCap = PenLineCap.Flat,
            };
            dc.DrawGeometry(null, pen, Chevrons);
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    static byte[] Png(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    static Geometry Frozen(Geometry g)
    {
        g.Freeze();
        return g;
    }
}
