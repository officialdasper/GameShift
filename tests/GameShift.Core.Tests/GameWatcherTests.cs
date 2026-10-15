namespace GameShift.Core.Tests;

public class GameWatcherTests
{
    readonly Settings _s = new();
    readonly GameWatcher _w = new(TimeSpan.FromSeconds(10));
    readonly DateTime _t0 = new(2026, 10, 15, 20, 0, 0);

    Mode? Tick(Mode current, int seconds, params string[] running) =>
        _w.Tick(running, _s, current, _t0.AddSeconds(seconds));

    [Fact]
    public void Tick_WardogsStarts_FromWork_SwitchesToGaming() =>
        Assert.Equal(Mode.Gaming, Tick(Mode.Work, 0, "chrome", "WardogsClient-Win64-Shipping"));

    [Fact]
    public void Tick_Cs2Starts_FromWork_SwitchesToGaming() =>
        Assert.Equal(Mode.Gaming, Tick(Mode.Work, 0, "cs2"));

    [Fact]
    public void Tick_NamesWithoutExe_AreRecognized()
    {
        Assert.Equal(Mode.Gaming, Tick(Mode.Work, 0, "VALORANT-Win64-Shipping"));
        Assert.Equal("valorant-win64-shipping", _w.ActiveGame);
    }

    [Fact]
    public void Tick_NoGames_NoSwitch() => Assert.Null(Tick(Mode.Work, 0, "chrome", "explorer"));

    [Fact]
    public void Tick_GameExits_SwitchesToWorkOnlyAfterGrace()
    {
        Tick(Mode.Work, 0, "cs2");
        Assert.Null(Tick(Mode.Gaming, 2, "cs2"));
        Assert.Null(Tick(Mode.Gaming, 4));
        Assert.Null(Tick(Mode.Gaming, 11));
        Assert.Equal(Mode.Work, Tick(Mode.Gaming, 13));
    }

    [Fact]
    public void Tick_ShortRestartWithinGrace_KeepsMode()
    {
        Tick(Mode.Work, 0, "cs2");
        Assert.Null(Tick(Mode.Gaming, 2));
        Assert.Null(Tick(Mode.Gaming, 6, "cs2"));
        Assert.Null(Tick(Mode.Gaming, 20, "cs2"));
    }

    [Fact]
    public void Tick_FirstGameWins_WhileItRuns()
    {
        Assert.Equal(Mode.Gaming, Tick(Mode.Work, 0, "WardogsClient-Win64-Shipping"));
        Assert.Null(Tick(Mode.Gaming, 2, "WardogsClient-Win64-Shipping", "cs2"));
        Assert.Equal("wardogsclient-win64-shipping", _w.ActiveGame);
    }

    [Fact]
    public void Tick_FirstExits_OtherRunning_StaysInGamingAndTracksIt()
    {
        Tick(Mode.Work, 0, "WardogsClient-Win64-Shipping");
        Tick(Mode.Gaming, 2, "WardogsClient-Win64-Shipping", "cs2");
        Assert.Null(Tick(Mode.Gaming, 4, "cs2"));
        Assert.Null(Tick(Mode.Gaming, 15, "cs2"));
        Assert.Equal("cs2", _w.ActiveGame);
        Assert.Null(Tick(Mode.Gaming, 17));
        Assert.Equal(Mode.Work, Tick(Mode.Gaming, 30));
    }

    [Fact]
    public void ManualWork_WhileGameRuns_NotReEnabledUntilGameRestarts()
    {
        Tick(Mode.Work, 0, "cs2");
        _w.NotifyManualSwitch(["cs2", "explorer"]);
        Assert.Null(Tick(Mode.Work, 2, "cs2"));
        Assert.Null(Tick(Mode.Work, 30, "cs2"));
        Assert.Null(Tick(Mode.Work, 32));
        Assert.Equal(Mode.Gaming, Tick(Mode.Work, 34, "cs2"));
    }

    [Fact]
    public void ManualGameMode_WithoutGame_StaysUntilUserChanges()
    {
        _w.NotifyManualSwitch(["explorer"]);
        Assert.Null(Tick(Mode.Gaming, 0, "explorer"));
        Assert.Null(Tick(Mode.Gaming, 60, "explorer"));
    }

    [Fact]
    public void ManualGameMode_WhileOtherGameRuns_IsNotOverridden()
    {
        _w.NotifyManualSwitch(["cs2"]);
        Assert.Null(Tick(Mode.Gaming, 0, "cs2"));
    }

    [Fact]
    public void ManualGameMode_ThenGameStarts_FollowsGame()
    {
        _w.NotifyManualSwitch(["explorer"]);
        Assert.Null(Tick(Mode.Gaming, 5, "explorer", "cs2"));
        Assert.Equal("cs2", _w.ActiveGame);
        Assert.Equal(Mode.Work, Tick(Mode.Gaming, 20, "explorer"));
    }

    [Fact]
    public void Reset_AfterFailedSwitch_NextTickRetries()
    {
        Assert.Equal(Mode.Gaming, Tick(Mode.Work, 0, "cs2"));
        Assert.Null(Tick(Mode.Work, 2, "cs2"));

        _w.Reset();

        Assert.Equal(Mode.Gaming, Tick(Mode.Work, 4, "cs2"));
    }
}
