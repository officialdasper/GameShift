namespace GameShift.Core.Tests;

/// <summary>Какие строки убрать из открытых окон выбора, когда пользователь сам закрыл или открыл программу.</summary>
public class LiveAppListTests
{
    static AppCandidate Cand(string exe, params int[] pids) =>
        new(exe, exe, exe, pids, null, new ClosedApp(exe, @"C:\" + exe, null, true));

    static RunningProcess P(int pid, string exe) => new(pid, exe);

    [Fact]
    public void Gone_NoLiveProcesses_AllDropped()
    {
        var gone = LiveAppList.GoneFromClose([Cand("chrome.exe", 10), Cand("telegram.exe", 20)], []);

        Assert.Equal(["chrome.exe", "telegram.exe"], gone);
    }

    [Fact]
    public void Gone_AllPidsAlive_NothingDropped()
    {
        var gone = LiveAppList.GoneFromClose([Cand("chrome.exe", 10, 11)], [P(10, "chrome.exe"), P(11, "chrome.exe")]);

        Assert.Empty(gone);
    }

    [Fact]
    public void Gone_OneOfManyPidsAlive_Kept()
    {
        var gone = LiveAppList.GoneFromClose([Cand("chrome.exe", 10, 11)], [P(11, "chrome.exe")]);

        Assert.Empty(gone);
    }

    [Fact]
    public void Gone_PidReusedByOtherProcess_Dropped()
    {
        var gone = LiveAppList.GoneFromClose([Cand("chrome.exe", 10)], [P(10, "other.exe")]);

        Assert.Equal(["chrome.exe"], gone);
    }

    [Fact]
    public void Gone_NameCaseDiffers_StillAlive()
    {
        var gone = LiveAppList.GoneFromClose([Cand("chrome.exe", 10)], [P(10, "Chrome.EXE")]);

        Assert.Empty(gone);
    }

    [Fact]
    public void Gone_NewProcessWithSameNameOnOtherPid_DoesNotKeepRow()
    {
        // Новый запуск той же программы закрывать не планировали: строка относится к старым PID.
        var gone = LiveAppList.GoneFromClose([Cand("chrome.exe", 10)], [P(99, "chrome.exe")]);

        Assert.Equal(["chrome.exe"], gone);
    }

    [Fact]
    public void RunningAgain_MatchesByNameAnyPid()
    {
        var again = LiveAppList.RunningAgain(["chrome.exe", "telegram.exe"], [P(5, "Chrome.exe"), P(6, "other.exe")]);

        Assert.Equal(["chrome.exe"], again);
    }

    [Fact]
    public void RunningAgain_NothingRunning_Empty()
    {
        Assert.Empty(LiveAppList.RunningAgain(["chrome.exe"], []));
    }
}
