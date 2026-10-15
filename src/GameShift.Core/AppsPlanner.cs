namespace GameShift.Core;

public sealed record PlannedClose(int Pid, string ExeName, bool Restore);

public static class AppsPlanner
{
    public static IReadOnlyList<PlannedClose> Plan(
        IReadOnlyList<RunningProcess> running, IReadOnlyList<AppEntry> apps, StopList stop)
    {
        var rules = new Dictionary<string, AppRule>();
        foreach (var a in apps) rules[ExeName.Normalize(a.ExeName)] = a.Rule;

        var result = new List<PlannedClose>();
        foreach (var p in running)
        {
            var n = ExeName.Normalize(p.ExeName);
            if (stop.IsBlockedName(n)) continue;
            if (!rules.TryGetValue(n, out var rule) || rule == AppRule.KeepRunning) continue;
            result.Add(new PlannedClose(p.Pid, n, rule == AppRule.CloseAndRestore));
        }
        return result;
    }

    /// <summary>
    /// Оставляет только те запланированные PID, которые в свежем списке носят то же имя и не попали в стоп-лист.
    /// </summary>
    public static IReadOnlyList<PlannedClose> Reverify(
        IReadOnlyList<RunningProcess> fresh, IReadOnlyCollection<PlannedClose> planned, StopList stop)
    {
        var names = new Dictionary<int, string>();
        foreach (var p in fresh) names[p.Pid] = ExeName.Normalize(p.ExeName);
        return planned
            .Where(t => names.TryGetValue(t.Pid, out var n) && n == ExeName.Normalize(t.ExeName)
                        && !stop.IsBlockedName(n) && !stop.IsBlockedName(t.ExeName))
            .ToList();
    }

    public static ClosedApp? PickRestoreTarget(string exeName, IReadOnlyList<(int Pid, ProcessLaunchInfo Info)> group, bool restore = true)
    {
        var pids = group.Select(g => g.Pid).ToHashSet();
        (int Pid, ProcessLaunchInfo Info)? chosen = null;
        foreach (var g in group)
        {
            if (g.Info.Path is null) continue;
            if (!pids.Contains(g.Info.ParentPid)) { chosen = g; break; }
            chosen ??= g;
        }
        return chosen is { } c ? new ClosedApp(exeName, c.Info.Path!, c.Info.Arguments, restore) : null;
    }
}
