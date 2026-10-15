namespace GameShift.Core.Tests;

public class GameScanTests
{
    static string Fx(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));


    [Fact]
    public void Steam_LibraryFolders_ReadsPathsAndUnescapes() =>
        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"],
            SteamParser.ParseLibraryFolders(Fx("libraryfolders.vdf")));

    [Fact]
    public void Steam_LibraryFolders_OldFormatPlainValues() =>
        Assert.Equal([@"E:\Games\Steam"], SteamParser.ParseLibraryFolders(Fx("libraryfolders_old.vdf")));

    [Theory]
    [InlineData("")]
    [InlineData("garbage {{{")]
    [InlineData("\"libraryfolders\" { \"0\" { \"path\" ")]
    public void Steam_LibraryFolders_BrokenInput_DoesNotThrow(string text) =>
        Assert.NotNull(SteamParser.ParseLibraryFolders(text));

    [Fact]
    public void Steam_Manifest_ReadsAppIdNameInstallDir()
    {
        var m = SteamParser.ParseManifest(Fx("appmanifest_730.acf"));
        Assert.Equal(new SteamManifest(730, "Counter-Strike 2", "Counter-Strike Global Offensive"), m);
    }

    [Fact]
    public void Steam_Manifest_BrokenOrIncomplete_ReturnsNull()
    {
        Assert.Null(SteamParser.ParseManifest(""));
        Assert.Null(SteamParser.ParseManifest("\"AppState\" { \"name\" \"x\" }"));
    }

    [Theory]
    [InlineData("appmanifest_228980.acf", true)]
    [InlineData("appmanifest_proton.acf", true)]
    [InlineData("appmanifest_730.acf", false)]
    public void Steam_IsTool_FiltersRedistributablesAndProton(string fixture, bool tool) =>
        Assert.Equal(tool, SteamParser.IsTool(SteamParser.ParseManifest(Fx(fixture))!));

    [Theory]
    [InlineData("SteamVR")]
    [InlineData("Steam Linux Runtime 3.0 (sniper)")]
    [InlineData("Proton Experimental")]
    public void Steam_IsTool_ByName(string name) =>
        Assert.True(SteamParser.IsTool(new SteamManifest(1, name, "x")));


    [Fact]
    public void Epic_Item_ReadsGame()
    {
        var i = EpicParser.ParseItem(Fx("epic_game.item"));
        Assert.Equal(new EpicItem("Rocket League", @"D:\Epic Games\rocketleague", @"Binaries\Win64\Game.exe"), i);
    }

    [Theory]
    [InlineData("epic_dlc.item")]
    [InlineData("epic_engine.item")]
    public void Epic_Item_SkipsDlcAndEngines(string fixture) => Assert.Null(EpicParser.ParseItem(Fx(fixture)));

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("{}")]
    public void Epic_Item_Broken_ReturnsNull(string text) => Assert.Null(EpicParser.ParseItem(text));


    [Fact]
    public void Riot_Valorant_ReadsNameAndPath()
    {
        var p = RiotParser.ParseProduct(Fx("valorant.product_settings.yaml"));
        Assert.Equal(new RiotProduct("valorant", "VALORANT", @"C:\Riot Games\VALORANT\live",
            @"ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe"), p);
    }

    [Fact]
    public void Riot_League_UnquotedAndSingleQuoted()
    {
        var p = RiotParser.ParseProduct(Fx("league.product_settings.yaml"))!;
        Assert.Equal("League of Legends", p.Name);
        Assert.Equal(@"D:\Riot Games\League of Legends", p.InstallPath);
    }

    [Fact]
    public void Riot_NoPath_ReturnsNull() => Assert.Null(RiotParser.ParseProduct("product_id: valorant"));


    [Theory]
    [InlineData("Electronic Arts", "Apex Legends", "EA app")]
    [InlineData("Blizzard Entertainment", "Overwatch", "Battle.net")]
    [InlineData("Ubisoft", "Far Cry 6", "Ubisoft Connect")]
    [InlineData("Valve", "Half-Life", null)]
    [InlineData(null, "Apex", null)]
    [InlineData("Electronic Arts", "EA app", null)]
    [InlineData("Electronic Arts", "EA Desktop", null)]
    [InlineData("Electronic Arts", "Origin", null)]
    [InlineData("Blizzard Entertainment", "Battle.net", null)]
    [InlineData("Ubisoft", "Ubisoft Connect", null)]
    [InlineData("Ubisoft", "Uplay", null)]
    [InlineData("Electronic Arts", "Microsoft Visual C++ 2015 Redistributable", null)]
    [InlineData("Electronic Arts", "EA Javelin Anticheat", null)]
    public void Uninstall_Classify(string? publisher, string name, string? expected) =>
        Assert.Equal(expected, UninstallClassifier.Classify(publisher, name));


    static IReadOnlyList<(string, long)> Files(params (string, long)[] f) => f;

    [Fact]
    public void ExePicker_PrefersShipping_OverBiggerExe() =>
        Assert.Equal(@"Game\Binaries\Win64\Game-Win64-Shipping.exe", ExePicker.Pick(Files(
            ("Game.exe", 500_000_000),
            (@"Game\Binaries\Win64\Game-Win64-Shipping.exe", 90_000_000),
            ("CrashReportClient.exe", 5_000_000))));

    [Fact]
    public void ExePicker_ExcludesNoiseNames() =>
        Assert.Equal("Witcher3.exe", ExePicker.Pick(Files(
            ("UnityCrashHandler64.exe", 9_000_000),
            ("vc_redist.x64.exe", 25_000_000),
            ("unins000.exe", 3_000_000),
            ("Setup.exe", 8_000_000),
            ("EasyAntiCheat_Setup.exe", 9_000_000),
            ("DXSETUP.exe", 500_000),
            (@"game\bin\win64\vconsole2.exe", 60_000_000),
            (@"game\bin\win64\SteamDedicated.exe", 70_000_000),
            ("UE4PrereqSetup_x64.exe", 30_000_000),
            ("Witcher3.exe", 40_000_000))));

    [Fact]
    public void ExePicker_LauncherExcluded_WhenOtherExists() =>
        Assert.Equal("game.exe", ExePicker.Pick(Files(("GameLauncher.exe", 90_000_000), ("game.exe", 1_000_000))));

    [Fact]
    public void ExePicker_LauncherUsed_WhenOnlyOne() =>
        Assert.Equal("GameLauncher.exe", ExePicker.Pick(Files(("GameLauncher.exe", 1_000_000), ("unins000.exe", 5))));

    [Fact]
    public void ExePicker_PrefersBinariesWin64() =>
        Assert.Equal(@"Game\Binaries\Win64\Game.exe", ExePicker.Pick(Files(
            ("Big.exe", 100_000_000), (@"Game\Binaries\Win64\Game.exe", 10_000_000))));

    [Fact]
    public void ExePicker_FallsBackToLargest() =>
        Assert.Equal(@"bin\main.exe", ExePicker.Pick(Files(("tool.exe", 100), (@"bin\main.exe", 5000))));

    [Fact]
    public void ExePicker_IgnoresNonExe_AndNothingPlausible_ReturnsNull()
    {
        Assert.Null(ExePicker.Pick(Files()));
        Assert.Null(ExePicker.Pick(Files(("readme.txt", 5), ("data.dll", 9))));
        Assert.Null(ExePicker.Pick(Files(("unins000.exe", 5), ("CrashReporter.exe", 9))));
    }

    [Fact]
    public void ExePicker_IsCaseInsensitive_AndAcceptsForwardSlashes() =>
        Assert.Equal("G/Binaries/Win64/G-WIN64-SHIPPING.EXE", ExePicker.Pick(Files(
            ("big.exe", 999), ("G/Binaries/Win64/G-WIN64-SHIPPING.EXE", 10))));


    [Fact]
    public void Merge_DedupesByExe_PrefersEntryWithSteamAppId_AndMarksExisting()
    {
        var found = new[]
        {
            new FoundGame("CS2", "Epic", @"D:\a", "cs2.exe", null),
            new FoundGame("Counter-Strike 2", "Steam", @"C:\b", "CS2.EXE", 730),
            new FoundGame("Other", "Steam", @"C:\c", "other.exe", 5),
            new FoundGame("NoExe", "Steam", @"C:\d", null, 6),
        };
        var existing = new[] { new GameEntry("other.exe", null) };

        var merged = GameMerge.Merge(found, existing);

        Assert.Equal(3, merged.Count);
        Assert.Contains(merged, m => m.Game.SteamAppId == 730 && m.Game.Launcher == "Steam" && !m.AlreadyAdded);
        Assert.DoesNotContain(merged, m => m.Game.Launcher == "Epic");
        Assert.Contains(merged, m => m.Game.ExeName == "other.exe" && m.AlreadyAdded);
        Assert.Contains(merged, m => m.Game.ExeName is null && !m.AlreadyAdded);
    }

    [Fact]
    public void Merge_SortsByName()
    {
        var merged = GameMerge.Merge(
        [
            new FoundGame("Zeta", "Steam", "z", "z.exe", null),
            new FoundGame("alpha", "Steam", "a", "a.exe", null),
        ], []);
        Assert.Equal(["alpha", "Zeta"], merged.Select(m => m.Game.Name));
    }
}
