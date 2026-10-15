using System.Text.Json;

namespace GameShift.Core.Tests;

public class SettingsThemeTests
{
    [Fact]
    public void Defaults_AreDarkAndNvidiaGreen()
    {
        var s = new Settings();
        Assert.Equal(ThemeMode.Dark, s.Theme);
        Assert.Equal("#76B900", s.Accent);
    }

    [Theory]
    [InlineData(ThemeMode.System, "system")]
    [InlineData(ThemeMode.Light, "#3B82F6")]
    public void ThemeAndAccent_SurviveJsonRoundTrip(ThemeMode theme, string accent)
    {
        var s = new Settings { Theme = theme, Accent = accent };
        var back = JsonSerializer.Deserialize<Settings>(
            JsonSerializer.Serialize(s, JsonFileStore<Settings>.Options), JsonFileStore<Settings>.Options)!;
        Assert.Equal(theme, back.Theme);
        Assert.Equal(accent, back.Accent);
        Assert.Equal(theme, s.Clone().Theme);
    }
}
