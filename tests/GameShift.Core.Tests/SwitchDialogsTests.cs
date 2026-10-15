namespace GameShift.Core.Tests;

/// <summary>Окно выбора при входе в игру, ожидание несохранённых программ, окно возврата.</summary>
public class SwitchDialogsTests
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

    ModeController Make() => new(() => _settings, _power, _toasts, _memory, _procs, _snaps, TimeSpan.Zero);

    static StepResult StepOf(SwitchReport r, string name) => r.Steps.Single(s => s.Step == name);

    sealed class ScriptedCloser(Func<IReadOnlyList<AppCandidate>, IReadOnlyDictionary<string, CloseOutcome>> close) : IAppCloser
    {
        public IReadOnlyList<AppCandidate>? Got;
        public IReadOnlyDictionary<string, CloseOutcome> Close(IReadOnlyList<AppCandidate> apps, StopList stop)
        {
            Got = apps;
            return close(apps);
        }
    }

    static ScriptedCloser AllClosed() => new(apps => apps.ToDictionary(a => a.ExeName, _ => CloseOutcome.Closed));

    AppCloseSession Session(params AppCandidate[] apps) =>
        new(_procs, new StopList(_settings.Games), apps, _ => { });


    [Fact]
    public void Prepare_ChangesNothing()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        var c = Make();

        var plan = c.PrepareEnter(Mode.Gaming);

        Assert.Single(plan.Candidates);
        Assert.Equal(Mode.Work, c.Current);
        Assert.Null(_snaps.Value);
        Assert.Equal(0, _power.Sets);
        Assert.Equal(0, _toasts.Sets);
        Assert.Equal(0, _procs.CloseRequests);
    }

    [Fact]
    public void Prepare_ListsWindowedUnknownApps_UncheckedAndRuleApps_Checked()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");        // правило, окна нет (в трее)
        _procs.Add(20, "notepad++.exe", @"C:\Npp\notepad++.exe");     // незнакомая, с окном
        _procs.Add(30, "updater.exe", @"C:\Upd\updater.exe");         // незнакомая, фоновая
        _procs.Windowed.Add(20);

        var plan = Make().PrepareEnter(Mode.Gaming);

        Assert.Equal(["chrome", "notepad++"], plan.Candidates.Select(a => a.ExeName).Order());
        Assert.True(plan.Candidates.Single(a => a.ExeName == "chrome").DefaultChecked);
        Assert.False(plan.Candidates.Single(a => a.ExeName == "notepad++").DefaultChecked);
        Assert.Equal(["chrome"], plan.DefaultSelection);
        Assert.Equal("notepad++.exe", plan.Candidates.Single(a => a.ExeName == "notepad++").FileName);
        Assert.Equal(@"C:\Npp\notepad++.exe", plan.Candidates.Single(a => a.ExeName == "notepad++").Path);
    }

    [Fact]
    public void Prepare_KeepRunningApps_AreExcluded_AndNamedForNote()
    {
        _procs.Add(10, "Discord.exe", @"C:\Discord\Discord.exe");
        _procs.Add(11, "winws.exe", @"C:\zapret\winws.exe");
        _procs.Windowed.Add(10);
        _settings.Apps.Add(new AppEntry("winws.exe", "zapret", AppRule.KeepRunning));

        var plan = Make().PrepareEnter(Mode.Gaming);

        Assert.Empty(plan.Candidates);
        Assert.Equal(["Discord", "zapret"], plan.KeptRunning.Order());
        Assert.DoesNotContain(10, _procs.InfoRequested);
    }

    [Fact]
    public void Prepare_StopListedProcesses_AreNeitherWindowCheckedNorQueried()
    {
        _procs.Add(50, "vgc.exe", @"C:\Program Files\Riot Vanguard\vgc.exe");
        _procs.Add(51, "WardogsClient-Win64-Shipping.exe", @"C:\Games\w.exe");
        _procs.Add(52, "steam.exe", @"C:\Steam\steam.exe");
        _procs.Add(53, "EpicGamesLauncher.exe", @"C:\Epic\EpicGamesLauncher.exe");
        _procs.Add(20, "notepad++.exe", @"C:\Npp\notepad++.exe");
        _procs.Windowed.UnionWith([50, 51, 52, 53, 20]);

        var plan = Make().PrepareEnter(Mode.Gaming);

        Assert.Equal(["notepad++"], plan.Candidates.Select(a => a.ExeName));
        Assert.Equal([20], _procs.WindowCandidates!.Order());
        Assert.Equal([20], _procs.InfoRequested);
    }

    [Fact]
    public void Prepare_GroupsMultiProcessAppByExe()
    {
        _procs.Add(11, "chrome.exe", @"C:\Chrome\chrome.exe", "--type=renderer", parent: 10);
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe", null, parent: 4000);

        var app = Make().PrepareEnter(Mode.Gaming).Candidates.Single();

        Assert.Equal([10, 11], app.Pids.Order());
        Assert.Null(app.Target.Arguments);
    }

    [Fact]
    public void Prepare_UnknownAppInWindowsFolder_IsSilentlyLeftOut()
    {
        _procs.Add(20, "SystemSettings.exe", @"C:\Windows\ImmersiveControlPanel\SystemSettings.exe");
        _procs.Windowed.Add(20);
        var c = new ModeController(() => _settings, _power, _toasts, _memory, _procs, _snaps, TimeSpan.Zero);

        var plan = c.PrepareEnter(Mode.Gaming);
        var r = c.ApplyEnter(plan, plan.DefaultSelection, AllClosed());

        Assert.Empty(plan.Candidates);
        Assert.Equal(Strings.Get("Ctl_NothingToClose"), StepOf(r, StepNames.Apps).Detail);
    }

    [Fact]
    public void Prepare_WhenAlreadyGaming_IsAlreadyOn()
    {
        var c = Make();
        c.SwitchTo(Mode.Gaming);
        Assert.True(c.PrepareEnter(Mode.Gaming).AlreadyOn);
    }


    [Fact]
    public void Apply_ClosesOnlySelectedApps()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        _procs.Add(20, "Telegram.exe", @"C:\Tg\Telegram.exe");
        var c = Make();
        var plan = c.PrepareEnter(Mode.Gaming);

        c.ApplyEnter(plan, ["chrome"], new GracefulCloser(_procs, TimeSpan.Zero));

        Assert.DoesNotContain(_procs.Running, p => p.Pid == 10);
        Assert.Contains(_procs.Running, p => p.Pid == 20); // правило есть, но пользователь снял галочку
        Assert.Equal(Mode.Gaming, c.Current);
    }

    [Fact]
    public void Apply_SelectedUnknownApp_IsClosedAndOfferedBackAfterGame()
    {
        _procs.Add(20, "notepad++.exe", @"C:\Npp\notepad++.exe", "-multiInst");
        _procs.Windowed.Add(20);
        var c = Make();
        var plan = c.PrepareEnter(Mode.Gaming);

        c.ApplyEnter(plan, ["notepad++"], new GracefulCloser(_procs, TimeSpan.Zero));

        var closed = Assert.Single(_snaps.Value!.ClosedApps);
        Assert.Equal(new ClosedApp("notepad++", @"C:\Npp\notepad++.exe", "-multiInst", true), closed);
    }

    [Fact]
    public void Apply_WritesSnapshotBeforeClosingAnything()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        var c = Make();
        var plan = c.PrepareEnter(Mode.Gaming);
        var closer = new ScriptedCloser(apps =>
        {
            Assert.NotNull(_snaps.Value);
            Assert.Equal("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", _snaps.Value.PowerScheme);
            Assert.Equal(0, _power.Sets); // программы закрываются до остальных шагов
            return apps.ToDictionary(a => a.ExeName, _ => CloseOutcome.Closed);
        });

        var r = c.ApplyEnter(plan, ["chrome"], closer);

        Assert.False(r.HasFailures);
        Assert.Equal(Settings.MaxPerformanceScheme, _power.Active);
    }

    [Fact]
    public void Apply_SnapshotKeepsOnlyActuallyClosedApps()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        _procs.Add(20, "Telegram.exe", @"C:\Tg\Telegram.exe");
        var c = Make();
        var plan = c.PrepareEnter(Mode.Gaming);
        var closer = new ScriptedCloser(_ => new Dictionary<string, CloseOutcome>
        {
            ["chrome"] = CloseOutcome.Skipped,
            ["telegram"] = CloseOutcome.Closed,
        });

        var r = c.ApplyEnter(plan, ["chrome", "telegram"], closer);

        Assert.Equal(["telegram"], _snaps.Value!.ClosedApps.Select(a => a.ExeName));
        Assert.Equal(StepStatus.Done, StepOf(r, StepNames.Apps).Status);
        Assert.Contains("chrome", StepOf(r, StepNames.Apps).Detail);
    }

    [Fact]
    public void Apply_StillRunningApp_FailsAppsStep_OtherStepsRun()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        var c = Make();
        var plan = c.PrepareEnter(Mode.Gaming);

        var r = c.ApplyEnter(plan, ["chrome"], new ScriptedCloser(_ => new Dictionary<string, CloseOutcome> { ["chrome"] = CloseOutcome.StillRunning }));

        Assert.Equal(StepStatus.Failed, StepOf(r, StepNames.Apps).Status);
        Assert.Equal(StepStatus.Done, StepOf(r, StepNames.Power).Status);
        Assert.Equal(StepStatus.Done, StepOf(r, StepNames.Focus).Status);
        Assert.Empty(_snaps.Value!.ClosedApps);
        Assert.Equal(Mode.Gaming, c.Current);
    }

    [Fact]
    public void Apply_NothingSelected_ClosesNothing_ButEntersMode()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        var c = Make();
        var plan = c.PrepareEnter(Mode.Gaming);
        var closer = AllClosed();

        var r = c.ApplyEnter(plan, [], closer);

        Assert.Null(closer.Got);
        Assert.Contains(_procs.Running, p => p.Pid == 10);
        Assert.Equal(Mode.Gaming, c.Current);
        Assert.False(r.HasFailures);
    }

    [Fact]
    public void Apply_GameAddedWhileChoosing_IsNotClosed()
    {
        _procs.Add(20, "notepad++.exe", @"C:\Npp\notepad++.exe");
        _procs.Windowed.Add(20);
        var c = Make();
        var plan = c.PrepareEnter(Mode.Gaming);
        _settings.Games.Add(new GameEntry("notepad++.exe", null));

        c.ApplyEnter(plan, ["notepad++"], new GracefulCloser(_procs, TimeSpan.Zero));

        Assert.Contains(_procs.Running, p => p.Pid == 20);
    }


    AppCandidate Candidate(string file, params int[] pids)
    {
        var n = ExeName.Normalize(file);
        return new AppCandidate(n, file, n, pids, null, new ClosedApp(n, @"C:\X\" + file, null, true));
    }

    [Fact]
    public void Session_GracefulClose_ClosesApp()
    {
        _procs.Add(10, "chrome.exe");
        var s = Session(Candidate("chrome.exe", 10));

        s.RequestClose();

        Assert.True(s.WaitForExit(TimeSpan.Zero));
        Assert.Equal(CloseOutcome.Closed, s.Outcomes()["chrome"]);
        Assert.Empty(_procs.Killed);
    }

    [Fact]
    public void Session_AppWithUnsavedWork_WaitsAndIsNeverKilledAutomatically()
    {
        _procs.Add(10, "notepad.exe");
        _procs.IgnoresClose.Add(10);
        var s = Session(Candidate("notepad.exe", 10));

        s.RequestClose();
        var done = s.WaitForExit(TimeSpan.FromSeconds(1));

        Assert.False(done);
        Assert.Equal(AppCloseState.Pending, s.StateOf("notepad"));
        Assert.Equal("notepad", Assert.Single(s.Pending).ExeName);
        Assert.Empty(_procs.Killed);
        Assert.Contains(_procs.Running, p => p.Pid == 10);
        Assert.Equal(CloseOutcome.StillRunning, s.Outcomes()["notepad"]);
    }

    [Fact]
    public void Session_ClosedByUserWhileWaiting_CountsAsClosed()
    {
        _procs.Add(10, "notepad.exe");
        _procs.IgnoresClose.Add(10);
        var s = Session(Candidate("notepad.exe", 10));
        s.RequestClose();
        s.WaitForExit(TimeSpan.Zero);

        _procs.Running.RemoveAll(p => p.Pid == 10);
        s.Refresh();

        Assert.True(s.IsDone);
        Assert.Equal(CloseOutcome.Closed, s.Outcomes()["notepad"]);
    }

    [Fact]
    public void Session_WaitMore_AsksAgain()
    {
        _procs.Add(10, "notepad.exe");
        _procs.IgnoresClose.Add(10);
        var s = Session(Candidate("notepad.exe", 10));
        s.RequestClose();
        s.WaitForExit(TimeSpan.Zero);
        _procs.IgnoresClose.Clear();

        s.RequestClose();

        Assert.True(s.WaitForExit(TimeSpan.Zero));
        Assert.Equal(2, _procs.CloseRequests);
        Assert.Empty(_procs.Killed);
    }

    [Fact]
    public void Session_KillOnExplicitCommand_ClosesWithoutSaving()
    {
        _procs.Add(10, "notepad.exe");
        _procs.Add(11, "chrome.exe");
        _procs.IgnoresClose.UnionWith([10, 11]);
        var s = Session(Candidate("notepad.exe", 10), Candidate("chrome.exe", 11));
        s.RequestClose();
        s.WaitForExit(TimeSpan.Zero);

        s.Kill("notepad");

        Assert.Equal([10], _procs.Killed);
        Assert.Equal(AppCloseState.Closed, s.StateOf("notepad"));
        Assert.Equal(AppCloseState.Pending, s.StateOf("chrome"));
        Assert.False(s.IsDone);
    }

    [Fact]
    public void Session_Kill_RechecksNameBeforeKilling()
    {
        _procs.Add(10, "notepad.exe");
        _procs.IgnoresClose.Add(10);
        var s = Session(Candidate("notepad.exe", 10));
        s.RequestClose();
        s.WaitForExit(TimeSpan.Zero);
        _procs.Rename(10, "SGuard64.exe"); // PID достался античиту

        s.Kill("notepad");

        Assert.Empty(_procs.Killed);
        Assert.Contains(_procs.Running, p => p.Pid == 10 && p.ExeName == "SGuard64.exe");
    }

    [Fact]
    public void Session_Leave_IsSkippedAndNotKilled()
    {
        _procs.Add(10, "notepad.exe");
        _procs.IgnoresClose.Add(10);
        var s = Session(Candidate("notepad.exe", 10));
        s.RequestClose();
        s.WaitForExit(TimeSpan.Zero);

        s.LeaveAll();

        Assert.True(s.IsDone);
        Assert.Equal(CloseOutcome.Skipped, s.Outcomes()["notepad"]);
        Assert.Empty(_procs.Killed);
    }

    [Fact]
    public void Session_Unkillable_EndsAsFailed()
    {
        _procs.Add(10, "notepad.exe");
        _procs.Unkillable.Add(10);
        var s = Session(Candidate("notepad.exe", 10));
        s.RequestClose();
        s.WaitForExit(TimeSpan.Zero);

        s.KillAll();

        Assert.True(s.IsDone);
        Assert.Equal(AppCloseState.Failed, s.StateOf("notepad"));
        Assert.Equal(CloseOutcome.StillRunning, s.Outcomes()["notepad"]);
    }

    [Fact]
    public void Session_AppAlreadyGone_IsNotRunning_AndNotAskedToClose()
    {
        var s = Session(Candidate("notepad.exe", 10));

        s.RequestClose();

        Assert.True(s.IsDone);
        Assert.Equal(CloseOutcome.NotRunning, s.Outcomes()["notepad"]);
        Assert.Equal(0, _procs.CloseRequests);
    }

    [Fact]
    public void Apply_WithLeftOpenApp_DoesNotRecordIt()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        _procs.IgnoresClose.Add(10);
        var c = Make();
        var plan = c.PrepareEnter(Mode.Gaming);
        var closer = new ScriptedCloser(apps =>
        {
            var s = new AppCloseSession(_procs, new StopList(_settings.Games), apps, _ => { });
            s.RequestClose();
            s.WaitForExit(TimeSpan.Zero);
            s.LeaveAll();
            return s.Outcomes();
        });

        var r = c.ApplyEnter(plan, ["chrome"], closer);

        Assert.Empty(_snaps.Value!.ClosedApps);
        Assert.Contains(_procs.Running, p => p.Pid == 10);
        Assert.Equal(StepStatus.Done, StepOf(r, StepNames.Apps).Status);
    }


    ModeController EnterWith(params string[] selected)
    {
        var c = Make();
        var plan = c.PrepareEnter(Mode.Gaming);
        c.ApplyEnter(plan, selected, new GracefulCloser(_procs, TimeSpan.Zero));
        return c;
    }

    [Fact]
    public void Return_RestoresSettingsAtOnce_AndLeavesAppsToTheUser()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        _procs.Add(20, "Telegram.exe", @"C:\Tg\Telegram.exe");
        var c = EnterWith("chrome", "telegram");

        var back = c.ReturnToWork();

        Assert.Equal(Mode.Work, c.Current);
        Assert.True(_toasts.Enabled);
        Assert.Equal("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", _power.Active);
        Assert.Null(_snaps.Value);
        Assert.Empty(_procs.Started);
        Assert.DoesNotContain(back.Report.Steps, s => s.Step == StepNames.Apps);
        Assert.Equal(StepStatus.Done, StepOf(back.Report, StepNames.Power).Status);
        Assert.Equal(StepStatus.Done, StepOf(back.Report, StepNames.Focus).Status);
        Assert.Equal([("chrome", true), ("telegram", false)],
            back.ToRestore.Select(a => (a.ExeName, a.Restore)).OrderBy(x => x.ExeName));
    }

    [Fact]
    public void Return_SkipsAppsAlreadyRunningAgain()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        _procs.Add(20, "Telegram.exe", @"C:\Tg\Telegram.exe");
        var c = EnterWith("chrome", "telegram");
        _procs.Add(77, "chrome.exe", @"C:\Chrome\chrome.exe");

        var back = c.ReturnToWork();

        Assert.Equal(["telegram"], back.ToRestore.Select(a => a.ExeName));
    }

    [Fact]
    public void Return_NothingClosed_NothingToOffer()
    {
        var c = EnterWith();
        Assert.Empty(c.ReturnToWork().ToRestore);
    }

    [Fact]
    public void RestartApps_StartsOnlyChosen()
    {
        _procs.Add(10, "chrome.exe", @"C:\Chrome\chrome.exe");
        _procs.Add(20, "Telegram.exe", @"C:\Tg\Telegram.exe");
        var c = EnterWith("chrome", "telegram");
        var back = c.ReturnToWork();

        var step = c.RestartApps(back.ToRestore.Where(a => a.ExeName == "telegram").ToList());

        Assert.Equal(StepStatus.Done, step.Status);
        Assert.Equal([(@"C:\Tg\Telegram.exe", (string?)null)], _procs.Started);
    }


    [Fact]
    public void RememberEnter_UpdatesRulesAsAgreed()
    {
        var apps = new List<AppEntry>
        {
            new("chrome.exe", "Chrome", AppRule.CloseAndRestore),
            new("Telegram.exe", "Telegram", AppRule.CloseOnly),
            new("slack.exe", "Slack", AppRule.CloseOnly),
            new("Discord.exe", "Discord", AppRule.KeepRunning),
        };

        var result = AppChoices.RememberEnter(apps,
        [
            new("chrome.exe", "Google Chrome", false),     // снята: было правило закрытия → не трогать
            new("Telegram.exe", "Telegram Desktop", true), // отмечена, «насовсем» остаётся
            new("notepad++.exe", "Notepad++", true),        // незнакомая отмеченная → закрыть и вернуть
            new("code.exe", "VS Code", false),              // незнакомая снятая → не добавляется
        ]);

        Assert.Equal(
        [
            new AppEntry("chrome.exe", "Chrome", AppRule.KeepRunning),
            new AppEntry("Telegram.exe", "Telegram", AppRule.CloseOnly),
            new AppEntry("slack.exe", "Slack", AppRule.CloseOnly),
            new AppEntry("Discord.exe", "Discord", AppRule.KeepRunning),
            new AppEntry("notepad++.exe", "Notepad++", AppRule.CloseAndRestore),
        ], result);
    }

    [Fact]
    public void RememberEnter_CheckedCloseAndRestore_Stays()
    {
        var result = AppChoices.RememberEnter([new("chrome.exe", "Chrome", AppRule.CloseAndRestore)], [new("chrome.exe", "Chrome", true)]);
        Assert.Equal(AppRule.CloseAndRestore, Assert.Single(result).Rule);
    }

    [Fact]
    public void RememberRestore_CheckedReopens_UncheckedClosesForGood()
    {
        var apps = new List<AppEntry>
        {
            new("chrome.exe", "Chrome", AppRule.CloseAndRestore),
            new("Telegram.exe", "Telegram", AppRule.CloseOnly),
        };

        var result = AppChoices.RememberRestore(apps,
        [
            new("chrome.exe", "Chrome", false),
            new("Telegram.exe", "Telegram", true),
            new("notepad++.exe", "Notepad++", false),
        ]);

        Assert.Equal(
        [
            new AppEntry("chrome.exe", "Chrome", AppRule.CloseOnly),
            new AppEntry("Telegram.exe", "Telegram", AppRule.CloseAndRestore),
            new AppEntry("notepad++.exe", "Notepad++", AppRule.CloseOnly),
        ], result);
    }

    [Fact]
    public void Remember_NeverAddsStopListedNames()
    {
        var result = AppChoices.RememberEnter([], [new("winws.exe", "zapret", true)]);
        Assert.Empty(result);
    }
}
