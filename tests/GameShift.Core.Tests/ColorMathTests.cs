namespace GameShift.Core.Tests;

public class ColorMathTests
{
    [Theory]
    [InlineData(0, 1, 1, 0xFF, 0x00, 0x00)]
    [InlineData(60, 1, 1, 0xFF, 0xFF, 0x00)]
    [InlineData(120, 1, 1, 0x00, 0xFF, 0x00)]
    [InlineData(180, 1, 1, 0x00, 0xFF, 0xFF)]
    [InlineData(240, 1, 1, 0x00, 0x00, 0xFF)]
    [InlineData(300, 1, 1, 0xFF, 0x00, 0xFF)]
    [InlineData(360, 1, 1, 0xFF, 0x00, 0x00)]
    [InlineData(200, 0, 1, 0xFF, 0xFF, 0xFF)]
    [InlineData(200, 1, 0, 0x00, 0x00, 0x00)]
    [InlineData(0, 0, 0.5, 0x80, 0x80, 0x80)]
    public void ToRgb_KnownColors(double h, double s, double v, int r, int g, int b)
    {
        Assert.Equal(new Rgb((byte)r, (byte)g, (byte)b), ColorMath.ToRgb(new Hsv(h, s, v)));
    }

    [Fact]
    public void ToRgb_WrapsHueAndClampsSaturationAndValue()
    {
        Assert.Equal(ColorMath.ToRgb(new Hsv(330, 1, 1)), ColorMath.ToRgb(new Hsv(-30, 1.5, 2)));
        Assert.Equal(ColorMath.ToRgb(new Hsv(30, 0, 0)), ColorMath.ToRgb(new Hsv(390, -1, -1)));
    }

    [Theory]
    [InlineData(0xFF, 0x00, 0x00, 0, 1, 1)]
    [InlineData(0x00, 0xFF, 0x00, 120, 1, 1)]
    [InlineData(0xFF, 0x00, 0xFF, 300, 1, 1)]
    [InlineData(0xFF, 0xFF, 0xFF, 0, 0, 1)]
    [InlineData(0x00, 0x00, 0x00, 0, 0, 0)]
    public void ToHsv_KnownColors(int r, int g, int b, double h, double s, double v)
    {
        var hsv = ColorMath.ToHsv(new Rgb((byte)r, (byte)g, (byte)b));
        Assert.Equal(h, hsv.H, 3);
        Assert.Equal(s, hsv.S, 3);
        Assert.Equal(v, hsv.V, 3);
    }

    [Fact]
    public void ToHsv_NvidiaGreen()
    {
        var hsv = ColorMath.ToHsv(new Rgb(0x76, 0xB9, 0x00));
        Assert.Equal(81.7, hsv.H, 1);
        Assert.Equal(1, hsv.S, 3);
        Assert.Equal(0xB9 / 255.0, hsv.V, 3);
    }

    [Fact]
    public void RoundTrip_KeepsEveryColorOnAGrid()
    {
        for (var r = 0; r < 256; r += 15)
        for (var g = 0; g < 256; g += 17)
        for (var b = 0; b < 256; b += 51)
        {
            var c = new Rgb((byte)r, (byte)g, (byte)b);
            Assert.Equal(c, ColorMath.ToRgb(ColorMath.ToHsv(c)));
        }
    }

    [Fact]
    public void RoundTrip_KeepsPresets()
    {
        foreach (var p in Accents.Presets)
        {
            Assert.True(ColorMath.TryParseHex(p.Hex, out var c));
            Assert.Equal(p.Hex, ColorMath.ToHex(ColorMath.ToRgb(ColorMath.ToHsv(c))));
        }
    }

    [Fact]
    public void ToHex_IsUpperCaseWithHash()
    {
        Assert.Equal("#76B900", ColorMath.ToHex(new Rgb(0x76, 0xB9, 0x00)));
        Assert.Equal("#0A0B0C", ColorMath.ToHex(new Rgb(10, 11, 12)));
    }

    [Theory]
    [InlineData("#76B900", 0x76, 0xB9, 0x00)]
    [InlineData("#76b900", 0x76, 0xB9, 0x00)]
    [InlineData("76B900", 0x76, 0xB9, 0x00)]
    [InlineData("  #3B82F6 ", 0x3B, 0x82, 0xF6)]
    public void TryParseHex_Valid(string text, int r, int g, int b)
    {
        Assert.True(ColorMath.TryParseHex(text, out var c));
        Assert.Equal(new Rgb((byte)r, (byte)g, (byte)b), c);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("system")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    [InlineData("#+12345")]
    [InlineData("##12345")]
    public void TryParseHex_Invalid(string? text)
    {
        Assert.False(ColorMath.TryParseHex(text, out _));
    }
}

public class AccentsTests
{
    [Fact]
    public void Presets_StartWithDefaultGreen()
    {
        Assert.Equal(Accents.Default, Accents.Presets[0].Hex);
        Assert.Equal(Accents.Default, new Settings().Accent);
        Assert.Equal(5, Accents.Presets.Count);
        Assert.All(Accents.Presets, p => Assert.True(ColorMath.TryParseHex(p.Hex, out _)));
    }

    [Theory]
    [InlineData("#123456", true)]
    [InlineData("#12ab9f", true)]
    [InlineData("#76B900", false)]
    [InlineData("#76b900", false)]
    [InlineData("#E5484D", false)]
    [InlineData("system", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("garbage", false)]
    public void IsCustom(string? accent, bool expected)
    {
        Assert.Equal(expected, Accents.IsCustom(accent));
    }

    [Fact]
    public void PresetNames_AreLocalized()
    {
        foreach (var p in Accents.Presets) Assert.NotEqual(p.NameKey, Strings.Get(p.NameKey));
    }
}
