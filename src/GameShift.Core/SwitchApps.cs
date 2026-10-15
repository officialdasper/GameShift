namespace GameShift.Core;

/// <summary>
/// Программа в окне «Что закрыть перед игрой»: все процессы одного exe.
/// Попадает сюда только после проверки стоп-листа по имени и по пути.
/// </summary>
public sealed record AppCandidate(
    string ExeName,
    string FileName,
    string DisplayName,
    IReadOnlyList<int> Pids,
    AppRule? SavedRule,
    ClosedApp Target)
{
    public string Path => Target.Path;

    public bool DefaultChecked => SavedRule is AppRule.CloseAndRestore or AppRule.CloseOnly;
}

/// <summary>Всё, что нужно прочитать до входа в игру. Пока план не применён, система не меняется.</summary>
public sealed class EnterPlan
{
    public required Mode Target { get; init; }
    public bool AlreadyOn { get; init; }
    internal Settings Settings { get; init; } = new();
    internal bool PowerRead { get; init; }
    internal string? PowerScheme { get; init; }
    internal bool FocusRead { get; init; }
    internal bool? ToastsEnabled { get; init; }

    public IReadOnlyList<AppCandidate> Candidates { get; init; } = [];
    public IReadOnlyList<string> KeptRunning { get; init; } = [];
    internal IReadOnlyList<string> Protected { get; init; } = [];
    internal IReadOnlyList<string> Unverified { get; init; } = [];

    public IReadOnlyList<string> DefaultSelection =>
        Candidates.Where(c => c.DefaultChecked).Select(c => c.ExeName).ToList();
}

public enum CloseOutcome { Closed, StillRunning, Skipped, NotRunning }

public interface IAppCloser
{
    IReadOnlyDictionary<string, CloseOutcome> Close(IReadOnlyList<AppCandidate> apps, StopList stop);
}

/// <summary>Без участия пользователя: просит закрыться и ждёт. Принудительно ничего не завершает.</summary>
public sealed class GracefulCloser(IProcessOps processes, TimeSpan grace) : IAppCloser
{
    public IReadOnlyDictionary<string, CloseOutcome> Close(IReadOnlyList<AppCandidate> apps, StopList stop)
    {
        var session = new AppCloseSession(processes, stop, apps);
        session.RequestClose();
        session.WaitForExit(grace);
        return session.Outcomes();
    }
}

/// <summary>Возврат в «Работу» без перезапуска программ: их выбирает пользователь отдельно.</summary>
public sealed record ReturnResult(SwitchReport Report, IReadOnlyList<ClosedApp> ToRestore);

public sealed record AppChoice(string FileName, string DisplayName, bool Checked);

public static class AppChoices
{
    /// <summary>
    /// Вход в игру: отмеченные получают «Закрыть и вернуть» (у кого было «Закрыть насовсем», оно остаётся);
    /// снятые, у которых было правило закрытия, становятся «Не трогать»; незнакомые снятые не добавляются.
    /// </summary>
    public static List<AppEntry> RememberEnter(IReadOnlyList<AppEntry> apps, IEnumerable<AppChoice> choices) =>
        Apply(apps, choices, (old, isChecked) => isChecked
            ? old == AppRule.CloseOnly ? AppRule.CloseOnly : AppRule.CloseAndRestore
            : old is AppRule.CloseAndRestore or AppRule.CloseOnly ? AppRule.KeepRunning : null);

    public static List<AppEntry> RememberRestore(IReadOnlyList<AppEntry> apps, IEnumerable<AppChoice> choices) =>
        Apply(apps, choices, (_, isChecked) => isChecked ? AppRule.CloseAndRestore : AppRule.CloseOnly);

    static List<AppEntry> Apply(IReadOnlyList<AppEntry> apps, IEnumerable<AppChoice> choices, Func<AppRule?, bool, AppRule?> rule)
    {
        var result = apps.ToList();
        foreach (var c in choices)
        {
            var n = ExeName.Normalize(c.FileName);
            if (StopList.IsBuiltInBlocked(n)) continue;
            var i = result.FindIndex(a => ExeName.Normalize(a.ExeName) == n);
            var next = rule(i >= 0 ? result[i].Rule : null, c.Checked);
            if (next is not AppRule r) continue;
            if (i >= 0) result[i] = result[i] with { Rule = r };
            else result.Add(new AppEntry(c.FileName, c.DisplayName, r));
        }
        return result;
    }
}
