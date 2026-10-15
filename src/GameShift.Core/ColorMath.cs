using System.Globalization;

namespace GameShift.Core;

public readonly record struct Rgb(byte R, byte G, byte B);

public readonly record struct Hsv(double H, double S, double V);

public static class ColorMath
{
    public static Rgb ToRgb(Hsv hsv)
    {
        var h = hsv.H % 360;
        if (h < 0) h += 360;
        var s = Math.Clamp(hsv.S, 0, 1);
        var v = Math.Clamp(hsv.V, 0, 1);

        var c = v * s;
        var sector = h / 60;
        var x = c * (1 - Math.Abs(sector % 2 - 1));
        var (r, g, b) = (int)sector switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        var m = v - c;
        return new Rgb(Channel(r + m), Channel(g + m), Channel(b + m));
    }

    /// <summary>У серых цветов оттенка нет: возвращается 0.</summary>
    public static Hsv ToHsv(Rgb rgb)
    {
        double r = rgb.R, g = rgb.G, b = rgb.B;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double h = 0;
        if (delta > 0)
        {
            if (max == r) h = 60 * ((g - b) / delta);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else h = 60 * ((r - g) / delta + 4);
            if (h < 0) h += 360;
        }
        var s = max == 0 ? 0 : delta / max;
        return new Hsv(h, s, max / 255);
    }

    public static string ToHex(Rgb c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static bool TryParseHex(string? text, out Rgb color)
    {
        color = default;
        var s = text?.Trim();
        if (string.IsNullOrEmpty(s)) return false;
        if (s[0] == '#') s = s[1..];
        if (s.Length != 6 || !s.All(Uri.IsHexDigit)) return false;
        var value = int.Parse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        color = new Rgb((byte)(value >> 16), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
        return true;
    }

    static byte Channel(double unit) => (byte)Math.Round(Math.Clamp(unit, 0, 1) * 255, MidpointRounding.AwayFromZero);
}

public sealed record AccentPreset(string NameKey, string Hex);

public static class Accents
{
    public const string System = "system";
    public const string Default = "#76B900";

    public static IReadOnlyList<AccentPreset> Presets { get; } =
    [
        new("Accent_Green", Default),
        new("Accent_Red", "#E5484D"),
        new("Accent_Orange", "#F28C28"),
        new("Accent_Blue", "#3B82F6"),
        new("Accent_Purple", "#8B5CF6"),
    ];

    /// <summary>Свой цвет: корректный #RRGGBB, которого нет среди готовых.</summary>
    public static bool IsCustom(string? accent) =>
        ColorMath.TryParseHex(accent, out var c) &&
        !Presets.Any(p => ColorMath.TryParseHex(p.Hex, out var preset) && preset == c);
}
