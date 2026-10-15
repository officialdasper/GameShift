namespace GameShift.Core;

public sealed record RunningProcess(int Pid, string ExeName);

public sealed record ProcessLaunchInfo(string? Path, string? Arguments, int ParentPid);

public interface IPower
{
    string GetActiveScheme();
    void SetActiveScheme(string guid);
}

public interface IToasts
{
    bool GetEnabled();
    void SetEnabled(bool enabled);
}

public interface IMemoryPurger
{
    bool IsConfigured { get; }
    void Purge();
}

public interface IProcessOps
{
    /// <summary>Только имена и PID, без открытия процессов.</summary>
    IReadOnlyList<RunningProcess> List();
    /// <summary>Вызывать только для процессов, прошедших стоп-лист по имени.</summary>
    ProcessLaunchInfo? TryGetLaunchInfo(int pid);
    /// <summary>
    /// PID из candidates, у которых есть видимое окно верхнего уровня с заголовком. Окна чужих PID
    /// (игры, античиты, стоп-лист) только сопоставляются с PID и дальше не опрашиваются.
    /// </summary>
    IReadOnlySet<int> WindowedPids(IReadOnlySet<int> candidates);
    /// <summary>
    /// Вежливо просит закрыться (как крестик окна). Перед этим сверяет имя каждого PID со свежим списком
    /// и стоп-листом: PID мог достаться другому процессу.
    /// </summary>
    void RequestClose(IReadOnlyCollection<PlannedClose> targets, StopList stop);
    /// <summary>Завершает без сохранения, с той же сверкой имён. Только по явной команде пользователя.</summary>
    void Kill(IReadOnlyCollection<PlannedClose> targets, StopList stop);
    void Start(string path, string? arguments);
}

public interface IStore<T> where T : class
{
    T? Load();
    void Save(T value);
    void Delete();
}
