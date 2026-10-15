using System.Text.Json;

namespace GameShift.Core.Tests;

public class SettingsDesign2Tests
{
    static Settings RoundTrip(Settings s) => JsonSerializer.Deserialize<Settings>(
        JsonSerializer.Serialize(s, JsonFileStore<Settings>.Options), JsonFileStore<Settings>.Options)!;

    static Settings Load(string json) => JsonSerializer.Deserialize<Settings>(json, JsonFileStore<Settings>.Options)!;

    [Fact]
    public void DefaultGames_OneListWithSteamAppIds()
    {
        var s = new Settings();
        Assert.Equal(
            ["WardogsClient-Win64-Shipping.exe", "cs2.exe", "VALORANT-Win64-Shipping.exe", "DeltaForceClient-Win64-Shipping.exe"],
            s.Games.Select(g => g.ExeName));
        Assert.Equal([1867240, 730, null, 2507950], s.Games.Select(g => g.SteamAppId));
        Assert.Equal(@"C:\Program Files (x86)\Steam\steamapps\common\Wardogs", s.Games[0].Folder);
    }

    [Fact]
    public void SteamAppId_SurvivesRoundTrip_AndOldEntriesStillLoad()
    {
        Assert.Equal(1867240, RoundTrip(new Settings()).Games[0].SteamAppId);
        Assert.Equal(1867240, new Settings().Clone().Games[0].SteamAppId);

        var loaded = Load("""{"Games":[{"ExeName":"a.exe","Folder":null}]}""");
        Assert.Equal("a.exe", loaded.Games.Single().ExeName);
        Assert.Null(loaded.Games[0].SteamAppId);
    }

    [Fact]
    public void OldJson_TwoLists_MergeIntoGames_WithoutDuplicates()
    {
        var s = Load("""
            {"WardogsGames":[{"ExeName":"WardogsClient-Win64-Shipping.exe","Folder":"D:\\Wardogs"},
                             {"ExeName":"x.exe","Folder":null}],
             "GamingGames":[{"ExeName":"CS2.exe","Folder":null},{"ExeName":"X.EXE","Folder":null}]}
            """);

        Assert.Equal(["WardogsClient-Win64-Shipping.exe", "x.exe", "CS2.exe"], s.Games.Select(g => g.ExeName));
        Assert.Equal(@"D:\Wardogs", s.Games[0].Folder);
        Assert.Equal(1867240, s.Games[0].SteamAppId);
        Assert.Equal(730, s.Games[2].SteamAppId);

        var json = JsonSerializer.Serialize(s, JsonFileStore<Settings>.Options);
        Assert.DoesNotContain("WardogsGames", json);
        Assert.DoesNotContain("GamingGames", json);
    }

    [Fact]
    public void OldJson_EmptyLegacyLists_GiveEmptyGames()
    {
        var s = Load("""{"WardogsGames":[],"GamingGames":[]}""");
        Assert.Empty(s.Games);
    }

    [Fact]
    public void OldSnapshot_GameShift_LoadsAsGaming()
    {
        var snap = JsonSerializer.Deserialize<Snapshot>("""{"Mode":"Wardogs"}""", JsonFileStore<Snapshot>.Options)!;
        Assert.Equal(Mode.Gaming, snap.Mode);
    }

    [Fact]
    public void Backdrop_DefaultsToMica_AndRoundTrips()
    {
        Assert.Equal(BackdropMode.Mica, new Settings().Backdrop);
        foreach (var b in Enum.GetValues<BackdropMode>())
        {
            Assert.Equal(b, RoundTrip(new Settings { Backdrop = b }).Backdrop);
            Assert.Equal(b, new Settings { Backdrop = b }.Clone().Backdrop);
        }
    }

    [Theory]
    [InlineData("winws.exe", "zapret")]
    [InlineData("TgWsProxy_windows.exe", "TG WS Proxy")]
    [InlineData("Discord.exe", "Discord")]
    public void DefaultApps_KeepNetworkHelpersRunning(string exe, string name)
    {
        var e = new Settings().Apps.Single(a => a.ExeName == exe);
        Assert.Equal(name, e.DisplayName);
        Assert.Equal(AppRule.KeepRunning, e.Rule);
    }

    [Fact]
    public void OldJson_AppsGetNetworkHelpers_ButKeepUserEntries()
    {
        var s = Load("""
            {"Apps":[{"ExeName":"Discord.exe","DisplayName":"Discord","Rule":"KeepRunning"},
                     {"ExeName":"WINWS.EXE","DisplayName":"мой zapret","Rule":"KeepRunning"}]}
            """);
        Assert.Equal("мой zapret", s.Apps.Single(a => ExeName.Normalize(a.ExeName) == "winws").DisplayName);
        Assert.Single(s.Apps, a => a.ExeName == "TgWsProxy_windows.exe");
        Assert.Equal(3, s.Apps.Count);
    }

    [Fact]
    public void LegacyGameShift_HasGamingTitle() => Assert.Equal("Игровой", ModeNames.Title(Mode.Wardogs));
}
