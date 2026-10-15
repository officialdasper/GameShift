namespace GameShift.Core.Tests;

public class ModeControllerTests
{
    readonly Settings _settings = new()
    {
        Apps =
        [
            new("chrome.exe", "Chrome", AppRule.CloseAndRestore),
            new("Telegram.exe", "Telegram", AppRule.CloseOnly),
            new("Discord.exe", "Discord", AppRule.KeepRunning),
        ],
    };
    readonly FakePower _power = new();
    readonly FakeToasts _toasts = new();
    readonly FakeMemory _memory = new();
    readonly FakeProcesses _procs = new();
    readonly MemoryStore<Snapshot> _snaps = new();

    ModeController Make() =>
        new(() => _settings, _power, _toasts, _memory, _procs, _snaps, TimeSpan.Zero);

    static StepResult StepOf(SwitchReport r, string name) => r.Steps.Single(s => s.Step == name);

    [Fact]
    public void Starts_InWork_WithoutSnapshot() => Assert.Equal(Mode.Work, Make().Current);

    [Fact]
    public void Starts_InSnapshotMode_WhenSnapshotLeftOver()
    {
        _snaps.Value = new Snapshot { Mode = Mode.Gaming };
        Assert.Equal(Mode.Gaming, Make().Current);
    }

    [Fact]
    public void EnterGame_SavesSnapshotBeforeAnyChange()
    {
        var c = Make();
        _power.OnSet = () => Assert.NotNull(_snaps.Value);
        c.SwitchTo(Mode.Gaming);
        Assert.Equal(Mode.Gaming, _snaps.Value!.Mode);
        Assert.Equal("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", _snaps.Value.PowerScheme);
        Assert.True(_snaps.Value.ToastsEnabled);
    }

    [Fact]
    public void EnterGame_AppliesAllSteps()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        _procs.Add(20, "Telegram.exe", @"C:\Tg\Telegram.exe");
        _procs.Add(30, "Discord.exe", @"C:\Discord\Discord.exe");
        var c = Make();

        var r = c.SwitchTo(Mode.Gaming);

        Assert.Equal(Mode.Gaming, c.Current);
        Assert.Equal(Settings.MaxPerformanceScheme, _power.Active);
        Assert.False(_toasts.Enabled);
        Assert.DoesNotContain(_procs.Running, p => p.Pid is 10 or 20);
        Assert.Contains(_procs.Running, p => p.Pid == 30);
        Assert.False(r.HasFailures);
        Assert.Equal(StepStatus.Skipped, StepOf(r, StepNames.Memory).Status);
        // Закрытые записываются все; вернуть после игры предлагается только chrome.
        Assert.Equal(["chrome", "telegram"], _snaps.Value!.ClosedApps.Select(a => a.ExeName).Order());
        Assert.False(_snaps.Value.ClosedApps.Single(a => a.ExeName == "telegram").Restore);
    }

    [Fact]
    public void EnterGame_PowerFails_OtherStepsStillRun()
    {
        _power.Fail = true;
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        var c = Make();

        var r = c.SwitchTo(Mode.Gaming);

        Assert.Equal(StepStatus.Failed, StepOf(r, StepNames.Power).Status);
        Assert.Contains("не найдена", StepOf(r, StepNames.Power).Detail);
        Assert.Equal(StepStatus.Done, StepOf(r, StepNames.Focus).Status);
        Assert.Equal(StepStatus.Done, StepOf(r, StepNames.Apps).Status);
        Assert.Equal(Mode.Gaming, c.Current);
    }

    [Fact]
    public void EnterGame_DisabledFeatures_AreSkippedAndNotRecorded()
    {
        _settings.PowerEnabled = false;
        _settings.FocusEnabled = false;
        var r = Make().SwitchTo(Mode.Gaming);

        Assert.Equal(StepStatus.Skipped, StepOf(r, StepNames.Power).Status);
        Assert.Equal(StepStatus.Skipped, StepOf(r, StepNames.Focus).Status);
        Assert.Null(_snaps.Value!.PowerScheme);
        Assert.Null(_snaps.Value.ToastsEnabled);
        Assert.True(_toasts.Enabled);
    }

    [Fact]
    public void EnterGame_MemoryEnabledButNotConfigured_IsSkipped()
    {
        _settings.MemoryPurgeEnabled = true;
        var r = Make().SwitchTo(Mode.Gaming);
        Assert.Equal(StepStatus.Skipped, StepOf(r, StepNames.Memory).Status);
        Assert.Equal(0, _memory.Purges);
    }

    [Fact]
    public void EnterGame_MemoryConfigured_Purges()
    {
        _settings.MemoryPurgeEnabled = true;
        _memory.IsConfigured = true;
        var r = Make().SwitchTo(Mode.Gaming);
        Assert.Equal(StepStatus.Done, StepOf(r, StepNames.Memory).Status);
        Assert.Equal(1, _memory.Purges);
    }

    [Fact]
    public void EnterGame_AppInsideGameFolder_IsSkippedNotClosed()
    {
        _settings.Apps.Add(new AppEntry("CrashReportClient.exe", "Crash", AppRule.CloseOnly));
        _procs.Add(40, "CrashReportClient.exe",
            @"C:\Program Files (x86)\Steam\steamapps\common\Wardogs\Engine\Binaries\Win64\CrashReportClient.exe");

        var r = Make().SwitchTo(Mode.Gaming);

        Assert.Contains(_procs.Running, p => p.Pid == 40);
        Assert.Contains("crashreportclient", StepOf(r, StepNames.Apps).Detail);
    }

    [Fact]
    public void EnterGame_NeverQueriesStopListedProcesses()
    {
        _settings.Apps.Add(new AppEntry("vgc.exe", "Vanguard", AppRule.CloseOnly));
        _procs.Add(50, "vgc.exe", @"C:\Program Files\Riot Vanguard\vgc.exe");
        _procs.Add(51, "WardogsClient-Win64-Shipping.exe", @"C:\Games\w.exe");

        Make().SwitchTo(Mode.Gaming);

        Assert.DoesNotContain(50, _procs.InfoRequested);
        Assert.DoesNotContain(51, _procs.InfoRequested);
    }

    [Fact]
    public void EnterGame_UnkillableApp_MarksAppsStepFailed()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        _procs.Unkillable.Add(10);
        var r = Make().SwitchTo(Mode.Gaming);
        Assert.Equal(StepStatus.Failed, StepOf(r, StepNames.Apps).Status);
    }

    [Fact]
    public void ReturnToWork_RestoresEverythingFromSnapshot()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe", "--profile-directory=Default");
        var c = Make();
        c.SwitchTo(Mode.Gaming);

        var r = c.SwitchTo(Mode.Work);

        Assert.Equal(Mode.Work, c.Current);
        Assert.Equal("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", _power.Active);
        Assert.True(_toasts.Enabled);
        Assert.Equal(new[] { (@"C:\Chrome\chrome.exe", (string?)"--profile-directory=Default") }, _procs.Started);
        Assert.Null(_snaps.Value);
        Assert.False(r.HasFailures);
    }

    [Fact]
    public void ReturnToWork_CloseOnlyAppIsNotRestarted()
    {
        _procs.Add(20, "Telegram.exe", @"C:\Tg\Telegram.exe");
        var c = Make();
        c.SwitchTo(Mode.Gaming);
        c.SwitchTo(Mode.Work);
        Assert.Empty(_procs.Started);
    }

    [Fact]
    public void ReturnToWork_DoesNotStartAppAlreadyRunning()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        var c = Make();
        c.SwitchTo(Mode.Gaming);
        _procs.Add(77, "chrome.exe", @"C:\Chrome\chrome.exe"); // пользователь открыл сам во время игры

        c.SwitchTo(Mode.Work);

        Assert.Empty(_procs.Started);
    }

    [Fact]
    public void MultiProcessApp_RestartedOnceFromRootProcess()
    {
        _procs.Add(11, "chrome.exe", @"C:\Chrome\chrome.exe", "--type=renderer", parent: 10);
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe", null, parent: 4000);
        _procs.Add(12, "chrome.exe", @"C:\Chrome\chrome.exe", "--type=gpu-process", parent: 10);
        var c = Make();
        c.SwitchTo(Mode.Gaming);

        c.SwitchTo(Mode.Work);

        Assert.Equal(new[] { (@"C:\Chrome\chrome.exe", (string?)null) }, _procs.Started);
    }

    [Fact]
    public void LegacyWardogsTarget_IsGaming_AndNoOpWhileGaming()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        var c = Make();
        c.SwitchTo(Mode.Wardogs);
        Assert.Equal(Mode.Gaming, c.Current);
        Assert.Equal(Mode.Gaming, _snaps.Value!.Mode);
        var powerAfterFirst = _power.Active;

        var r = c.SwitchTo(Mode.Wardogs);

        Assert.Equal(StepStatus.Skipped, r.Steps.Single().Status);
        Assert.Equal(powerAfterFirst, _power.Active);
        Assert.Empty(_procs.Started);
        Assert.Single(_snaps.Value.ClosedApps);
    }

    [Fact]
    public void LegacyWardogsSnapshot_LoadsAsGaming()
    {
        _snaps.Value = new Snapshot { Mode = Mode.Wardogs };
        Assert.Equal(Mode.Gaming, Make().Current);
    }

    [Fact]
    public void SwitchToCurrentMode_IsNoOp()
    {
        var r = Make().SwitchTo(Mode.Work);
        Assert.Equal(StepStatus.Skipped, r.Steps.Single().Status);
        Assert.Equal(new FakePower().Active, _power.Active);
    }

    [Fact]
    public void ReturnToWork_WithFailure_KeepsSnapshotForRetry()
    {
        var c = Make();
        c.SwitchTo(Mode.Gaming);
        _power.Fail = true;

        var r = c.SwitchTo(Mode.Work);

        Assert.True(r.HasFailures);
        Assert.NotNull(_snaps.Value);
        Assert.Equal(Mode.Work, c.Current);
    }

    [Fact]
    public void EnterGame_PidReusedByStopListedProcess_IsNotClosed()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        _procs.BeforeClose = () => _procs.Rename(10, "SGuard64.exe");

        Make().SwitchTo(Mode.Gaming);

        Assert.Contains(_procs.Running, p => p.Pid == 10 && p.ExeName == "SGuard64.exe");
    }

    [Fact]
    public void EnterGame_UnknownPath_IsSkippedNotClosed()
    {
        _procs.Add(10, "chrome.exe", path: null);
        _procs.Add(20, "Telegram.exe", @"C:\Tg\Telegram.exe");
        _procs.Infos.Remove(20);
        _procs.Add(30, "Telegram.exe", @"C:\Tg\Telegram.exe");

        var r = Make().SwitchTo(Mode.Gaming);

        Assert.Contains(_procs.Running, p => p.Pid == 10);
        Assert.Contains(_procs.Running, p => p.Pid == 20);
        Assert.Contains(_procs.Running, p => p.Pid == 30);
        Assert.Contains("не удалось проверить путь", StepOf(r, StepNames.Apps).Detail);
        Assert.Empty(_snaps.Value!.ClosedApps);
    }

    [Fact]
    public void EnterGame_CannotReadPowerScheme_DoesNotChangeIt()
    {
        _power.GetFails = true;
        var c = Make();

        var r = c.SwitchTo(Mode.Gaming);

        Assert.Equal(0, _power.Sets);
        Assert.Equal(StepStatus.Skipped, StepOf(r, StepNames.Power).Status);
        Assert.Contains("не удалось прочитать", StepOf(r, StepNames.Power).Detail);
        c.SwitchTo(Mode.Work);
        Assert.Equal(0, _power.Sets);
    }

    [Fact]
    public void EnterGame_CannotReadToasts_DoesNotChangeThem()
    {
        _toasts.GetFails = true;

        var r = Make().SwitchTo(Mode.Gaming);

        Assert.Equal(0, _toasts.Sets);
        Assert.Equal(StepStatus.Skipped, StepOf(r, StepNames.Focus).Status);
        Assert.Contains("не удалось прочитать", StepOf(r, StepNames.Focus).Detail);
    }

    [Fact]
    public void EnterGame_SnapshotSaveFails_ChangesNothing()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        _snaps.SaveFails = true;
        var c = Make();

        var r = c.SwitchTo(Mode.Gaming);

        Assert.True(r.HasFailures);
        Assert.Contains("снимок", r.Steps.Single(s => s.Status == StepStatus.Failed).Detail);
        Assert.Equal(Mode.Work, c.Current);
        Assert.Equal(0, _power.Sets);
        Assert.Equal(0, _toasts.Sets);
        Assert.Contains(_procs.Running, p => p.Pid == 10);
    }

    [Fact]
    public void ReturnToWork_SnapshotDeleteFails_ReportsFailureButIsInWork()
    {
        var c = Make();
        c.SwitchTo(Mode.Gaming);
        _snaps.DeleteFails = true;

        var r = c.SwitchTo(Mode.Work);

        Assert.True(r.HasFailures);
        Assert.Equal(Mode.Work, c.Current);
        Assert.True(_toasts.Enabled);
    }
}
