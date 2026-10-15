namespace GameShift.Core.Tests;

public class AppsPlannerTests
{
    static readonly StopList Stop = new(new Settings().Games, @"C:\Windows");

    [Fact]
    public void Plan_ClosesOnlyListedAppsAndRespectsRules()
    {
        var running = new List<RunningProcess>
        {
            new(1, "chrome.exe"), new(2, "chrome.exe"), new(3, "Discord.exe"),
            new(4, "Telegram.exe"), new(5, "notepad.exe"),
        };
        var apps = new List<AppEntry>
        {
            new("chrome.exe", "Chrome", AppRule.CloseAndRestore),
            new("Discord.exe", "Discord", AppRule.KeepRunning),
            new("telegram", "Telegram", AppRule.CloseOnly),
        };

        var plan = AppsPlanner.Plan(running, apps, Stop);

        Assert.Equal(new[] { 1, 2, 4 }, plan.Select(p => p.Pid).Order().ToArray());
        Assert.All(plan.Where(p => p.ExeName == "chrome"), p => Assert.True(p.Restore));
        Assert.False(plan.Single(p => p.ExeName == "telegram").Restore);
    }

    [Fact]
    public void Plan_NeverIncludesStopListedNames_EvenIfUserListedThem()
    {
        var running = new List<RunningProcess> { new(1, "vgc.exe"), new(2, "cs2.exe") };
        var apps = new List<AppEntry>
        {
            new("vgc.exe", "Vanguard", AppRule.CloseOnly),
            new("cs2.exe", "CS2", AppRule.CloseOnly),
        };
        Assert.Empty(AppsPlanner.Plan(running, apps, Stop));
    }

    [Fact]
    public void PickRestoreTarget_PrefersRootProcessOfGroup()
    {
        var group = new List<(int, ProcessLaunchInfo)>
        {
            (11, new ProcessLaunchInfo(@"C:\Chrome\chrome.exe", "--type=renderer", 10)),
            (10, new ProcessLaunchInfo(@"C:\Chrome\chrome.exe", "--profile-directory=Default", 4000)),
            (12, new ProcessLaunchInfo(@"C:\Chrome\chrome.exe", "--type=gpu-process", 10)),
        };
        var t = AppsPlanner.PickRestoreTarget("chrome", group)!;
        Assert.Equal(@"C:\Chrome\chrome.exe", t.Path);
        Assert.Equal("--profile-directory=Default", t.Arguments);
        Assert.True(t.Restore);
    }

    [Fact]
    public void PickRestoreTarget_NoPaths_ReturnsNull()
    {
        var group = new List<(int, ProcessLaunchInfo)> { (1, new ProcessLaunchInfo(null, null, 0)) };
        Assert.Null(AppsPlanner.PickRestoreTarget("x", group));
    }

    [Fact]
    public void Reverify_KeepsOnlyPidsWithSameNameAndNotStopListed()
    {
        var planned = new List<PlannedClose> { new(1, "chrome", true), new(2, "chrome", true), new(3, "telegram", false), new(4, "chrome", true) };
        var fresh = new List<RunningProcess> { new(1, "chrome.exe"), new(2, "SGuard64.exe"), new(3, "notepad.exe") };

        var ok = AppsPlanner.Reverify(fresh, planned, Stop);

        Assert.Equal(new[] { 1 }, ok.Select(p => p.Pid).ToArray());
    }

    [Fact]
    public void Reverify_DropsPlannedNameThatIsStopListed()
    {
        var planned = new List<PlannedClose> { new(5, "vgc", false) };
        var fresh = new List<RunningProcess> { new(5, "vgc.exe") };
        Assert.Empty(AppsPlanner.Reverify(fresh, planned, Stop));
    }
}
