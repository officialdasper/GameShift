namespace GameShift.Core;

public enum AppCloseState { Pending, Killing, Closed, Left, Failed, NotRunning }

/// <summary>Закрытие выбранных программ с ожиданием: сначала вежливая просьба, не закрывшееся ждёт решения пользователя. Без явной команды ничего принудительно не завершается. Методы можно звать из разных потоков.</summary>
public sealed class AppCloseSession
{
    static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(200);
    static readonly TimeSpan KillWait = TimeSpan.FromSeconds(3);

    readonly IProcessOps _processes;
    readonly StopList _stop;
    readonly Action<TimeSpan> _sleep;
    readonly Dictionary<string, AppCloseState> _states = [];
    readonly object _lock = new();

    public IReadOnlyList<AppCandidate> Apps { get; }

    public event Action? Changed;

    public AppCloseSession(IProcessOps processes, StopList stop, IReadOnlyList<AppCandidate> apps, Action<TimeSpan>? sleep = null)
    {
        _processes = processes;
        _stop = stop;
        _sleep = sleep ?? Thread.Sleep;
        Apps = apps.DistinctBy(a => a.ExeName).ToList();
        var live = Live();
        foreach (var a in Apps)
            _states[a.ExeName] = IsAlive(a, live) ? AppCloseState.Pending : AppCloseState.NotRunning;
    }

    public AppCloseState StateOf(string exeName)
    {
        lock (_lock) return _states[exeName];
    }

    public IReadOnlyList<AppCandidate> Pending
    {
        get { lock (_lock) return Apps.Where(a => _states[a.ExeName] == AppCloseState.Pending).ToList(); }
    }

    public bool IsDone
    {
        get { lock (_lock) return _states.Values.All(s => s is not (AppCloseState.Pending or AppCloseState.Killing)); }
    }

    public void RequestClose()
    {
        var targets = Targets(Pending);
        if (targets.Count > 0) _processes.RequestClose(targets, _stop);
    }

    public void Refresh()
    {
        var live = Live();
        var changed = false;
        lock (_lock)
        {
            foreach (var a in Apps)
            {
                if (_states[a.ExeName] is not (AppCloseState.Pending or AppCloseState.Killing)) continue;
                if (IsAlive(a, live)) continue;
                _states[a.ExeName] = AppCloseState.Closed;
                changed = true;
            }
        }
        if (changed) Changed?.Invoke();
    }

    public bool WaitForExit(TimeSpan timeout)
    {
        var rounds = (int)Math.Ceiling(timeout / Poll);
        for (var i = 0; ; i++)
        {
            Refresh();
            if (IsDone || i >= rounds) return IsDone;
            _sleep(Poll);
        }
    }

    /// <summary>Закрыть без сохранения. Только по явной команде пользователя; имя PID сверяется заново.</summary>
    public void Kill(string exeName)
    {
        AppCandidate app;
        lock (_lock)
        {
            if (_states[exeName] != AppCloseState.Pending) return;
            _states[exeName] = AppCloseState.Killing;
            app = Apps.First(a => a.ExeName == exeName);
        }
        Changed?.Invoke();
        try { _processes.Kill(Targets([app]), _stop); }
        catch (Exception) { } // нет прав или процесс уже завершился: итог покажет свежий список

        var rounds = (int)Math.Ceiling(KillWait / Poll);
        for (var i = 0; ; i++)
        {
            Refresh();
            if (StateOf(exeName) != AppCloseState.Killing || i >= rounds) break;
            _sleep(Poll);
        }
        lock (_lock)
        {
            if (_states[exeName] != AppCloseState.Killing) return;
            _states[exeName] = AppCloseState.Failed;
        }
        Changed?.Invoke();
    }

    public void KillAll()
    {
        foreach (var a in Pending) Kill(a.ExeName);
    }

    public void Leave(string exeName)
    {
        lock (_lock)
        {
            if (_states[exeName] != AppCloseState.Pending) return;
            _states[exeName] = AppCloseState.Left;
        }
        Changed?.Invoke();
    }

    public void LeaveAll()
    {
        foreach (var a in Pending) Leave(a.ExeName);
    }

    public IReadOnlyDictionary<string, CloseOutcome> Outcomes()
    {
        lock (_lock)
        {
            return _states.ToDictionary(kv => kv.Key, kv => kv.Value switch
            {
                AppCloseState.Closed => CloseOutcome.Closed,
                AppCloseState.Left => CloseOutcome.Skipped,
                AppCloseState.NotRunning => CloseOutcome.NotRunning,
                _ => CloseOutcome.StillRunning,
            });
        }
    }

    /// <summary>Живым считается PID с тем же именем: освободившийся PID мог достаться другому процессу.</summary>
    HashSet<(int Pid, string Name)> Live() =>
        _processes.List().Select(p => (p.Pid, ExeName.Normalize(p.ExeName))).ToHashSet();

    static bool IsAlive(AppCandidate a, HashSet<(int Pid, string Name)> live) => a.Pids.Any(pid => live.Contains((pid, a.ExeName)));

    static List<PlannedClose> Targets(IEnumerable<AppCandidate> apps) =>
        apps.SelectMany(a => a.Pids.Select(pid => new PlannedClose(pid, a.ExeName, false))).ToList();
}
