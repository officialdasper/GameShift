namespace GameShift.Core.Tests;

sealed class FakePower : IPower
{
    public string Active = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public bool Fail;
    public bool GetFails;
    public int Sets;
    public Action? OnSet;
    public string GetActiveScheme() => GetFails ? throw new TimeoutException("powercfg завис") : Active;
    public void SetActiveScheme(string guid)
    {
        OnSet?.Invoke();
        Sets++;
        if (Fail) throw new InvalidOperationException("Схема питания не найдена");
        Active = guid;
    }
}

sealed class FakeToasts : IToasts
{
    public bool Enabled = true;
    public bool GetFails;
    public int Sets;
    public bool GetEnabled() => GetFails ? throw new UnauthorizedAccessException("нет доступа") : Enabled;
    public void SetEnabled(bool enabled) { Sets++; Enabled = enabled; }
}

sealed class FakeMemory : IMemoryPurger
{
    public bool IsConfigured { get; set; }
    public int Purges;
    public void Purge() => Purges++;
}

sealed class FakeProcesses : IProcessOps
{
    public readonly List<RunningProcess> Running = [];
    public readonly Dictionary<int, ProcessLaunchInfo> Infos = [];
    public readonly HashSet<int> Unkillable = [];
    public readonly List<int> InfoRequested = [];
    public readonly List<(string Path, string? Args)> Started = [];

    public Action? BeforeClose;

    public void Add(int pid, string exe, string? path = null, string? args = null, int parent = 1)
    {
        Running.Add(new RunningProcess(pid, exe));
        Infos[pid] = new ProcessLaunchInfo(path, args, parent);
    }

    public void Rename(int pid, string exe)
    {
        Running.RemoveAll(p => p.Pid == pid);
        Running.Add(new RunningProcess(pid, exe));
    }

    public IReadOnlyList<RunningProcess> List() => Running.ToList();

    public ProcessLaunchInfo? TryGetLaunchInfo(int pid)
    {
        InfoRequested.Add(pid);
        return Infos.GetValueOrDefault(pid);
    }

    public readonly HashSet<int> Windowed = [];
    public readonly HashSet<int> IgnoresClose = []; // «несохранённые изменения»: на просьбу закрыться не реагирует
    public readonly List<int> Killed = [];
    public IReadOnlySet<int>? WindowCandidates;
    public int CloseRequests;

    public IReadOnlySet<int> WindowedPids(IReadOnlySet<int> candidates)
    {
        WindowCandidates = candidates;
        return Windowed.Where(candidates.Contains).ToHashSet();
    }

    public void RequestClose(IReadOnlyCollection<PlannedClose> targets, StopList stop)
    {
        BeforeClose?.Invoke();
        CloseRequests++;
        var pids = AppsPlanner.Reverify(List(), targets, stop).Select(t => t.Pid).ToHashSet();
        Running.RemoveAll(p => pids.Contains(p.Pid) && !Unkillable.Contains(p.Pid) && !IgnoresClose.Contains(p.Pid));
    }

    public void Kill(IReadOnlyCollection<PlannedClose> targets, StopList stop)
    {
        var pids = AppsPlanner.Reverify(List(), targets, stop).Select(t => t.Pid).ToHashSet();
        Killed.AddRange(pids);
        Running.RemoveAll(p => pids.Contains(p.Pid) && !Unkillable.Contains(p.Pid));
    }

    public void Start(string path, string? arguments)
    {
        Started.Add((path, arguments));
        Running.Add(new RunningProcess(9000 + Started.Count, Path.GetFileName(path)));
    }
}

sealed class MemoryStore<T> : IStore<T> where T : class
{
    public T? Value;
    public bool SaveFails;
    public bool DeleteFails;
    public T? Load() => Value;
    public void Save(T value)
    {
        if (SaveFails) throw new IOException("Файл занят другим процессом");
        Value = value;
    }
    public void Delete()
    {
        if (DeleteFails) throw new IOException("Файл занят другим процессом");
        Value = null;
    }
}
