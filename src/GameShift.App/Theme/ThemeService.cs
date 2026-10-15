using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using GameShift.Core;

namespace GameShift.App.Theme;

/// <summary>Палитра по выбранной теме и акценту. «Как в Windows» читает реестр и обновляется на лету.</summary>
public sealed class ThemeService : IDisposable
{
    public const string DefaultAccent = Accents.Default;
    public const string SystemAccent = Accents.System;

    public static ThemeService? Instance { get; private set; }

    public event Action? Changed;
    public bool IsDark { get; private set; }
    public Color Accent { get; private set; }

    ThemeMode _mode = ThemeMode.Dark;
    string _accent = DefaultAccent;
    Color _bg;
    Color _text;
    Color _surface;

    public ThemeService()
    {
        Instance = this;
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
    }

    sealed record Palette(int Bg, int Rail, int Surface, int Raised, int Hover, int SegmentOn,
        int Line, int Text, int Subtle, int Muted, int Danger);

    static readonly Palette Dark = new(0x0F1012, 0x0B0C0D, 0x17181B, 0x202125, 0x2A2B30, 0x34363C,
        0x2A2C30, 0xECEDEE, 0xA3A6AD, 0x6E7178, 0xFF6B6E);

    static readonly Palette Light = new(0xF2F3F0, 0xE8EAE5, 0xFFFFFF, 0xECEEE9, 0xE1E4DD, 0xFFFFFF,
        0xD8DBD3, 0x15171A, 0x565A62, 0x979BA2, 0xC4282D);

    BackdropMode _backdrop = BackdropMode.None;
    readonly Dictionary<string, SolidColorBrush> _brushes = [];

    public Window? BackdropWindow { get; set; }

    /// <summary>Mica и Acrylic есть начиная с Windows 11 22H2 (сборка 22621).</summary>
    public static bool BackdropSupported { get; } =
        Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Build >= 22621;

    public bool BackdropActive => _backdrop != BackdropMode.None && BackdropSupported && !_backdropFailed;
    bool _backdropFailed;

    public void Apply(ThemeMode mode, string? accent, BackdropMode backdrop)
    {
        _mode = mode;
        _accent = string.IsNullOrWhiteSpace(accent) ? DefaultAccent : accent;
        _backdrop = backdrop;
        Apply();
    }

    void Apply()
    {
        IsDark = _mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => ReadIsDark(),
        };
        Accent = ResolveAccent(_accent);
        var p = IsDark ? Dark : Light;
        var surface = ToColor(p.Surface);
        _surface = surface;
        _bg = ToColor(p.Bg);
        _text = ToColor(p.Text);
        var glass = BackdropActive;

        // С системным фоном страница и рельс — полупрозрачные слои поверх Mica/Acrylic,
        // карточки почти плотные, чтобы текст на них читался в обеих темах.
        Set("BgBrush", _bg);
        Set("PageBrush", glass ? WithAlpha(_bg, IsDark ? 0x8C : 0x99) : Colors.Transparent);
        // Затухание у краёв прокрутки на странице: цвет того, что под страницей (окно или слой поверх Mica).
        Set("PageShadeBrush", glass ? WithAlpha(_bg, IsDark ? 0x8C : 0x99) : _bg);
        Set("RailBrush", glass ? WithAlpha(ToColor(p.Rail), IsDark ? 0xB3 : 0xA6) : ToColor(p.Rail));
        Set("SurfaceBrush", glass ? WithAlpha(surface, IsDark ? 0xE6 : 0xEB) : surface);
        Set("RaisedBrush", ToColor(p.Raised));
        Set("HoverBrush", ToColor(p.Hover));
        Set("SegmentOnBrush", ToColor(p.SegmentOn));
        Set("LineBrush", ToColor(p.Line));
        Set("TextBrush", _text);
        Set("SubtleBrush", ToColor(p.Subtle));
        Set("MutedBrush", ToColor(p.Muted));
        Set("DangerBrush", ToColor(p.Danger));
        SetAccentBrushes(Accent);
        foreach (Window w in Application.Current.Windows) ApplyTitleBar(w);
        Changed?.Invoke();
    }

    void SetAccentBrushes(Color accent)
    {
        Set("AccentBrush", accent);
        Set("AccentTextBrush", Readable(accent, _surface, 4.5));
        Set("AccentSoftBrush", Color.FromArgb(IsDark ? (byte)0x2E : (byte)0x26, accent.R, accent.G, accent.B));
        Set("OnAccentBrush", Contrast(accent, Colors.Black) >= Contrast(accent, Colors.White) ? Colors.Black : Colors.White);
    }

    /// <summary>Примерка акцента из палитры: перекрашиваются только акцентные кисти, настройки не меняются. null возвращает сохранённый.</summary>
    public void PreviewAccent(Color? color) => SetAccentBrushes(color ?? Accent);

    void Set(string key, Color color)
    {
        // WPF замораживает кисти из ресурсов приложения, анимировать их нельзя.
        // Поэтому каждый раз кладём новую кисть, которая плавно перетекает из старого цвета.
        // Анимацию запускаем до добавления в ресурсы: анимируемую кисть словарь заморозить не может.
        var from = _brushes.TryGetValue(key, out var old) ? old.Color : color;
        var brush = new SolidColorBrush(from);
        if (from != color)
        {
            try
            {
                var animation = new ColorAnimation(color, Motion.Ms(200)) { EasingFunction = Motion.EaseOut };
                brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }
            catch (InvalidOperationException)
            {
                brush = new SolidColorBrush(color);
            }
        }
        _brushes[key] = brush;
        Application.Current.Resources[key] = brush;
    }

    static Color WithAlpha(Color c, int alpha) => Color.FromArgb((byte)alpha, c.R, c.G, c.B);

    public void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var dark = IsDark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE

        if (window == BackdropWindow && ApplyBackdrop(window, handle)) return;

        // Windows 11: заголовок в цвет окна. На Windows 10 вызовы просто не сработают.
        var caption = ColorRef(_bg);
        DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR
        var text = ColorRef(_text);
        DwmSetWindowAttribute(handle, 36, ref text, sizeof(int)); // DWMWA_TEXT_COLOR
    }

    bool ApplyBackdrop(Window window, IntPtr handle)
    {
        if (!BackdropSupported) return false;
        var source = HwndSource.FromHwnd(handle);
        var type = BackdropActive ? (_backdrop == BackdropMode.Acrylic ? 3 : 2) : 1; // DWMSBT_TRANSIENTWINDOW / MAINWINDOW / NONE

        if (BackdropActive && DwmSetWindowAttribute(handle, 38, ref type, sizeof(int)) == 0) // DWMWA_SYSTEMBACKDROP_TYPE
        {
            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(handle, ref margins);
            if (source?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
            window.Background = Brushes.Transparent;
            // Заголовок без своего цвета, чтобы фон доходил и до него.
            var none = unchecked((int)0xFFFFFFFF); // DWMWA_COLOR_DEFAULT
            DwmSetWindowAttribute(handle, 35, ref none, sizeof(int));
            DwmSetWindowAttribute(handle, 36, ref none, sizeof(int));
            return true;
        }

        if (BackdropActive)
        {
            // Система отказала: остаёмся на непрозрачном фоне и перекрашиваем слои.
            _backdropFailed = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(Apply));
        }
        var off = 1;
        DwmSetWindowAttribute(handle, 38, ref off, sizeof(int));
        var zero = new Margins();
        DwmExtendFrameIntoClientArea(handle, ref zero);
        if (source?.CompositionTarget is { } t) t.BackgroundColor = _bg;
        window.ClearValue(Window.BackgroundProperty);
        return false;
    }

    void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_mode != ThemeMode.System && _accent != SystemAccent) return;
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
            Application.Current.Dispatcher.BeginInvoke(new Action(Apply));
    }

    static Color ResolveAccent(string accent)
    {
        if (accent == SystemAccent) return ReadSystemAccent();
        try
        {
            if (ColorConverter.ConvertFromString(accent) is Color c) return Color.FromRgb(c.R, c.G, c.B);
        }
        catch (FormatException)
        {
        }
        return (Color)ColorConverter.ConvertFromString(DefaultAccent);
    }

    static bool ReadIsDark()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return (k?.GetValue("AppsUseLightTheme") as int? ?? 1) == 0;
    }

    public static Color ReadSystemAccent()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        if (k?.GetValue("AccentColor") is int abgr)
            return Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
        return Color.FromRgb(0x00, 0x78, 0xD4);
    }

    static Color Readable(Color c, Color bg, double min)
    {
        var target = Luminance(bg) > 0.5 ? Colors.Black : Colors.White;
        var result = c;
        for (var t = 0.05; t <= 1.0 && Contrast(result, bg) < min; t += 0.05)
            result = Mix(c, target, t);
        return result;
    }

    static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    static double Contrast(Color a, Color b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    static double Luminance(Color c) => 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);

    static double Linear(byte v)
    {
        var s = v / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    static Color ToColor(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));

    [StructLayout(LayoutKind.Sequential)]
    struct Margins
    {
        public int Left, Right, Top, Bottom;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        if (Instance == this) Instance = null;
    }
}
