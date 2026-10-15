namespace GameShift.Core.Tests;

public class ModeSummaryTests
{
    static Settings All() => new() { PowerEnabled = true, FocusEnabled = true, MemoryPurgeEnabled = true };

    [Fact]
    public void Gaming_ListsEverythingThatIsOn()
    {
        Assert.Equal(
            ["Summary_GamingPower", "Summary_GamingApps", "Summary_GamingNotifications", "Summary_GamingMemory", "Summary_GamingNvidia"],
            ModeSummary.Keys(Mode.Gaming, All(), notificationsSupported: true, nvidiaApplied: true));
    }

    [Fact]
    public void Gaming_SkipsFeaturesThatAreOff()
    {
        var s = new Settings { PowerEnabled = false, FocusEnabled = false, MemoryPurgeEnabled = false };
        Assert.Equal(["Summary_GamingApps"], ModeSummary.Keys(Mode.Gaming, s, notificationsSupported: true, nvidiaApplied: false));
    }

    [Fact]
    public void Notifications_AreSkippedWhereWindowsCannotHideThem()
    {
        Assert.DoesNotContain("Summary_GamingNotifications", ModeSummary.Keys(Mode.Gaming, All(), false, false));
        Assert.DoesNotContain("Summary_WorkNotifications", ModeSummary.Keys(Mode.Work, All(), false, false));
    }

    [Fact]
    public void Work_RestoresWhatGamingChanged()
    {
        Assert.Equal(
            ["Summary_WorkPower", "Summary_WorkNotifications", "Summary_WorkApps"],
            ModeSummary.Keys(Mode.Work, All(), notificationsSupported: true, nvidiaApplied: true));
        Assert.Equal(["Summary_WorkApps"],
            ModeSummary.Keys(Mode.Work, new Settings { PowerEnabled = false, FocusEnabled = false }, true, true));
    }

    [Fact]
    public void LegacyWardogs_IsGaming()
    {
        Assert.Equal(ModeSummary.Keys(Mode.Gaming, All(), true, true), ModeSummary.Keys(Mode.Wardogs, All(), true, true));
    }

    [Fact]
    public void EveryKey_IsLocalized()
    {
        foreach (var mode in new[] { Mode.Work, Mode.Gaming })
        foreach (var key in ModeSummary.Keys(mode, All(), true, true))
            Assert.NotEqual(key, Strings.Get(key));
    }
}

public class BoxBlurTests
{
    static uint Px(byte r, byte g, byte b) => 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;

    [Fact]
    public void UniformImage_StaysTheSameUpToTheEdges()
    {
        var pixels = Enumerable.Repeat(Px(40, 120, 200), 12 * 8).ToArray();
        ImageMath.BoxBlur(pixels, 12, 8, radius: 3, passes: 3);
        Assert.All(pixels, p => Assert.Equal(Px(40, 120, 200), p));
    }

    [Fact]
    public void BrightPixel_SpreadsToNeighbours()
    {
        var pixels = Enumerable.Repeat(Px(0, 0, 0), 9 * 9).ToArray();
        pixels[4 * 9 + 4] = Px(255, 255, 255);
        ImageMath.BoxBlur(pixels, 9, 9, radius: 1, passes: 1);
        var center = pixels[4 * 9 + 4] & 0xFF;
        var neighbour = pixels[4 * 9 + 5] & 0xFF;
        var corner = pixels[0] & 0xFF;
        Assert.True(center is > 0 and < 255);
        Assert.True(neighbour > 0);
        Assert.Equal(0u, corner);
    }

    [Fact]
    public void KeepsOpaqueAlpha()
    {
        var pixels = new[] { Px(255, 0, 0), Px(0, 0, 255), Px(0, 255, 0), Px(10, 10, 10) };
        ImageMath.BoxBlur(pixels, 2, 2, radius: 2, passes: 2);
        Assert.All(pixels, p => Assert.Equal(0xFFu, p >> 24));
    }

    [Fact]
    public void RejectsWrongSize()
    {
        Assert.Throws<ArgumentException>(() => ImageMath.BoxBlur(new uint[5], 2, 2, 1, 1));
    }
}
