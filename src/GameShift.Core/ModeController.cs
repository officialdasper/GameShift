namespace GameShift.Core;

public sealed class ModeController
{
    readonly Func<Settings> _settings;
    readonly IPower _power;
    readonly IToasts _toasts;
    readonly IMemoryPurger _memory;
    readonly IProcessOps _processes;
    readonly IStore<Snapshot> _snapshots;
    readonly TimeSpan _closeGrace;
    readonly object _gate = new();

    public Mode Current { get; private set; }

    public ModeController(Func<Settings> settings, IPower power, IToasts toasts,
        IMemoryPurger memory, IProcessOps processes, IStore<Snapshot> snapshots, TimeSpan? closeGrace = null)
    {
        _settings = settings;
        _power = power;
        _toasts = toasts;
        _memory = memory;
        _processes = processes;
        _snapshots = snapshots;
        _closeGrace = closeGrace ?? TimeSpan.FromSeconds(5);
        Current = Modes.Normalize(snapshots.Load()?.Mode ?? Mode.Work);
    }

    /// <summary>Без участия пользователя: вход закрывает программы по правилам (только вежливо), возврат запускает всё, что закрыто с правилом «Закрыть и вернуть».</summary>
    public SwitchReport SwitchTo(Mode target)
    {
        target = Modes.Normalize(target);
        lock (_gate)
        {
            if (target == Current) return AlreadyOn(target);
            if (target != Mode.Work)
            {
                var plan = PrepareEnter(target);
                return ApplyEnter(plan, plan.DefaultSelection, new GracefulCloser(_processes, _closeGrace));
            }
            var back = ReturnToWork();
            if (back.Report.Steps.Any(x => x.Step == StepNames.Mode && x.Status == StepStatus.Failed) && Current != Mode.Work)
                return back.Report;
            return back.Report with { Steps = [RestartApps(back.ToRestore.Where(a => a.Restore).ToList()), .. back.Report.Steps] };
        }
    }

    SwitchReport AlreadyOn(Mode target) =>
        new(Current, target, [new StepResult(StepNames.Mode, StepStatus.Skipped, Strings.Get("Ctl_AlreadyOn"))]);

    /// <summary>
    /// Первая половина входа в игру: читает исходные значения и собирает список программ для окна выбора.
    /// Ничего не меняет. К процессам из стоп-листа не обращается даже за путём.
    /// </summary>
    public EnterPlan PrepareEnter(Mode target)
    {
        target = Modes.Normalize(target);
        var s = _settings();
        lock (_gate)
        {
            if (target == Current || target == Mode.Work)
                return new EnterPlan { Target = target, AlreadyOn = target == Current, Settings = s };
        }

        var stop = new StopList(s.Games);
        string? scheme = null;
        bool toasts = false;
        // Если исходное значение не прочитать, его нельзя будет вернуть, поэтому шаг потом не выполняется.
        var powerRead = s.PowerEnabled && TryGet(_power.GetActiveScheme, out scheme);
        var focusRead = s.FocusEnabled && TryGet(_toasts.GetEnabled, out toasts);

        var rules = new Dictionary<string, AppEntry>();
        foreach (var a in s.Apps) rules[ExeName.Normalize(a.ExeName)] = a;

        var running = _processes.List();
        var kept = running
            .Select(p => rules.GetValueOrDefault(ExeName.Normalize(p.ExeName)))
            .Where(e => e?.Rule == AppRule.KeepRunning)
            .Select(e => e!.DisplayName)
            .Distinct()
            .ToList();

        // Дальше только процессы, прошедшие стоп-лист по имени.
        var allowed = running.Where(p => !stop.IsBlockedName(p.ExeName)).ToList();
        IReadOnlySet<int> windowed = new HashSet<int>();
        try { windowed = _processes.WindowedPids(allowed.Select(p => p.Pid).ToHashSet()); }
        catch { } // без списка окон остаются программы с правилами

        var candidates = new List<AppCandidate>();
        var protectedApps = new List<string>();
        var unverified = new List<string>();
        foreach (var group in allowed.GroupBy(p => ExeName.Normalize(p.ExeName)))
        {
            var entry = rules.GetValueOrDefault(group.Key);
            if (entry?.Rule == AppRule.KeepRunning) continue;
            // Незнакомые фоновые процессы без окна не показываем: пользователь их не узнает.
            if (entry is null && !group.Any(p => windowed.Contains(p.Pid))) continue;

            var infos = group.Select(p => (p.Pid, Info: LaunchInfo(p.Pid))).ToList();
            if (infos.Any(i => i.Info?.Path is string path && stop.IsBlockedPath(path)))
            {
                if (entry is not null) protectedApps.Add(group.Key);
                continue;
            }
            if (infos.Any(i => i.Info?.Path is null))
            {
                // Путь неизвестен: закрывать нельзя, вдруг это папка игры или Windows.
                if (entry is not null) unverified.Add(group.Key);
                continue;
            }
            var restore = entry?.Rule != AppRule.CloseOnly; // незнакомую программу после игры предлагаем открыть
            if (AppsPlanner.PickRestoreTarget(group.Key, infos.Select(i => (i.Pid, i.Info!)).ToList(), restore) is not { } closed)
                continue;
            var display = entry?.DisplayName ?? Path.GetFileNameWithoutExtension(group.First().ExeName);
            candidates.Add(new AppCandidate(group.Key, Path.GetFileName(closed.Path), display,
                group.Select(p => p.Pid).ToList(), entry?.Rule, closed));
        }

        return new EnterPlan
        {
            Target = target,
            Settings = s,
            PowerRead = powerRead,
            PowerScheme = scheme,
            FocusRead = focusRead,
            ToastsEnabled = focusRead ? toasts : null,
            Candidates = candidates,
            KeptRunning = kept,
            Protected = protectedApps,
            Unverified = unverified,
        };
    }

    /// <summary>Вторая половина входа: снимок на диск, закрытие выбранных программ через closer (может ждать пользователя), питание, уведомления, память. В снимке остаются только реально закрытые.</summary>
    public SwitchReport ApplyEnter(EnterPlan plan, IReadOnlyCollection<string> selected, IAppCloser closer)
    {
        lock (_gate)
        {
            var from = Current;
            var target = plan.Target;
            if (plan.AlreadyOn || target == from || target == Mode.Work) return AlreadyOn(target);

            var s = plan.Settings;
            var stop = new StopList(_settings().Games); // свежий список игр: за время выбора его могли пополнить
            var picked = selected.Select(ExeName.Normalize).ToHashSet();
            var chosen = plan.Candidates.Where(c => picked.Contains(c.ExeName) && !stop.IsBlockedName(c.ExeName)).ToList();
            var snap = new Snapshot
            {
                Mode = target,
                PowerScheme = plan.PowerRead ? plan.PowerScheme : null,
                ToastsEnabled = plan.FocusRead ? plan.ToastsEnabled : null,
                ClosedApps = chosen.Select(c => c.Target).ToList(),
            };
            if (Fails(() => _snapshots.Save(snap)) is string error)
                return new SwitchReport(from, target,
                    [new StepResult(StepNames.Mode, StepStatus.Failed, Strings.Format("Ctl_SnapshotWriteFailed", error))]);

            var steps = new List<StepResult>
            {
                Step(StepNames.Apps, () => CloseApps(chosen, stop, closer, snap, plan)),
                !s.PowerEnabled ? Skip(StepNames.Power, Strings.Get("Ctl_Off"))
                : !plan.PowerRead ? Skip(StepNames.Power, Strings.Get("Ctl_PowerUnreadable"))
                : Step(StepNames.Power, () => { _power.SetActiveScheme(s.GamePowerScheme); return Strings.Get("Ctl_MaxPerformance"); }),
                !s.FocusEnabled ? Skip(StepNames.Focus, Strings.Get("Ctl_Off"))
                : !plan.FocusRead ? Skip(StepNames.Focus, Strings.Get("Ctl_FocusUnreadable"))
                : Step(StepNames.Focus, () => { _toasts.SetEnabled(false); return Strings.Get("Ctl_NotificationsHidden"); }),
                !s.MemoryPurgeEnabled ? Skip(StepNames.Memory, Strings.Get("Ctl_Off"))
                : !_memory.IsConfigured ? Skip(StepNames.Memory, Strings.Get("Ctl_MemoryNotConfigured"))
                : Step(StepNames.Memory, () => { _memory.Purge(); return Strings.Get("Ctl_MemoryPurged"); }),
            };
            Current = target;
            return new SwitchReport(from, target, steps);
        }
    }

    /// <summary>
    /// Возврат в «Работу»: уведомления и питание сразу по снимку. Программы не запускаются:
    /// в результате список закрытых нами и пока не открытых, выбор за пользователем (RestartApps).
    /// </summary>
    public ReturnResult ReturnToWork()
    {
        lock (_gate)
        {
            var from = Current;
            if (from == Mode.Work) return new ReturnResult(AlreadyOn(Mode.Work), []);
            Snapshot? loaded = null;
            if (Fails(() => loaded = _snapshots.Load()) is string loadError)
                return new ReturnResult(new SwitchReport(from, Mode.Work,
                    [new StepResult(StepNames.Mode, StepStatus.Failed, Strings.Format("Ctl_SnapshotReadFailed", loadError))]), []);
            var snap = loaded ?? new Snapshot();
            var steps = new List<StepResult>
            {
                snap.ToastsEnabled is bool toasts
                    ? Step(StepNames.Focus, () => { _toasts.SetEnabled(toasts); return Strings.Get(toasts ? "Ctl_NotificationsOn" : "Ctl_LeftAsWas"); })
                    : Skip(StepNames.Focus, Strings.Get("Ctl_NotChanged")),
                snap.PowerScheme is string scheme
                    ? Step(StepNames.Power, () => { _power.SetActiveScheme(scheme); return Strings.Get("Ctl_PowerRestored"); })
                    : Skip(StepNames.Power, Strings.Get("Ctl_NotChanged")),
            };
            // Список программ уже в памяти, поэтому снимок можно удалить до выбора пользователя.
            if (!steps.Any(x => x.Status == StepStatus.Failed) && Fails(_snapshots.Delete) is string deleteError)
                steps.Add(new StepResult(StepNames.Mode, StepStatus.Failed, Strings.Format("Ctl_SnapshotDeleteFailed", deleteError)));
            Current = Mode.Work;

            HashSet<string> running = [];
            try { running = _processes.List().Select(p => ExeName.Normalize(p.ExeName)).ToHashSet(); }
            catch { }
            var toRestore = snap.ClosedApps
                .DistinctBy(a => ExeName.Normalize(a.ExeName))
                .Where(a => !running.Contains(ExeName.Normalize(a.ExeName)))
                .ToList();
            return new ReturnResult(new SwitchReport(from, Mode.Work, steps), toRestore);
        }
    }

    public StepResult RestartApps(IReadOnlyCollection<ClosedApp> apps) => Step(StepNames.Apps, () => StartApps(apps));

    ProcessLaunchInfo? LaunchInfo(int pid)
    {
        try { return _processes.TryGetLaunchInfo(pid); }
        catch { return null; }
    }

    string CloseApps(List<AppCandidate> chosen, StopList stop, IAppCloser closer, Snapshot snap, EnterPlan plan)
    {
        var outcomes = chosen.Count == 0 ? new Dictionary<string, CloseOutcome>() : closer.Close(chosen, stop);
        CloseOutcome Of(AppCandidate c) => outcomes.GetValueOrDefault(c.ExeName, CloseOutcome.StillRunning);

        // В снимке только то, что реально закрылось. Если перезаписать не удалось, лишние строки безвредны:
        // при возврате уже запущенные программы пропускаются.
        snap.ClosedApps = chosen.Where(c => Of(c) == CloseOutcome.Closed).Select(c => c.Target).ToList();
        Fails(() => _snapshots.Save(snap));

        var stuck = chosen.Where(c => Of(c) == CloseOutcome.StillRunning).Select(c => c.ExeName).ToList();
        if (stuck.Count > 0)
            throw new InvalidOperationException(Strings.Format("Ctl_CloseFailed", string.Join(", ", stuck)));
        var closed = chosen.Where(c => Of(c) == CloseOutcome.Closed).Select(c => c.ExeName).ToList();
        var left = chosen.Where(c => Of(c) == CloseOutcome.Skipped).Select(c => c.ExeName).ToList();
        var msg = closed.Count == 0 ? Strings.Get("Ctl_NothingToClose") : Strings.Format("Ctl_Closed", string.Join(", ", closed));
        if (left.Count > 0) msg += Strings.Format("Ctl_LeftOpen", string.Join(", ", left));
        if (plan.Protected.Count > 0) msg += Strings.Format("Ctl_ProtectedApps", string.Join(", ", plan.Protected));
        if (plan.Unverified.Count > 0) msg += Strings.Format("Ctl_UnverifiedApps", string.Join(", ", plan.Unverified));
        return msg;
    }

    string StartApps(IReadOnlyCollection<ClosedApp> apps)
    {
        if (apps.Count == 0) return Strings.Get("Ctl_NothingToStart");
        var running = _processes.List().Select(p => ExeName.Normalize(p.ExeName)).ToHashSet();
        var started = new List<string>();
        var errors = new List<string>();
        foreach (var a in apps.DistinctBy(a => ExeName.Normalize(a.ExeName)))
        {
            if (running.Contains(ExeName.Normalize(a.ExeName))) continue;
            try
            {
                _processes.Start(a.Path, a.Arguments);
                started.Add(a.ExeName);
            }
            catch (Exception e)
            {
                errors.Add($"{a.ExeName}: {e.Message}");
            }
        }
        if (errors.Count > 0) throw new InvalidOperationException(Strings.Format("Ctl_StartFailed", string.Join("; ", errors)));
        return started.Count == 0 ? Strings.Get("Ctl_AllRunning") : Strings.Format("Ctl_Started", string.Join(", ", started));
    }

    static StepResult Step(string name, Func<string> action)
    {
        try { return new StepResult(name, StepStatus.Done, action()); }
        catch (Exception e) { return new StepResult(name, StepStatus.Failed, e.Message); }
    }

    static StepResult Skip(string name, string why) => new(name, StepStatus.Skipped, why);

    static bool TryGet<T>(Func<T> get, out T? value)
    {
        try { value = get(); return true; }
        catch { value = default; return false; }
    }

    static string? Fails(Action action)
    {
        try { action(); return null; }
        catch (Exception e) { return e.Message; }
    }
}
