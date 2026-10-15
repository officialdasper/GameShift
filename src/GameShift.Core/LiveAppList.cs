namespace GameShift.Core;

/// <summary>Что убрать из открытых окон выбора, пока пользователь сам закрывает и открывает программы. Только сравнение со свежим списком имён и PID.</summary>
public static class LiveAppList
{
    /// <summary>
    /// Окно «Что закрыть»: программы, у которых не осталось живых процессов.
    /// Живым считается PID с прежним именем: освободившийся PID мог достаться другому процессу.
    /// </summary>
    public static IReadOnlyList<string> GoneFromClose(IEnumerable<AppCandidate> apps, IEnumerable<RunningProcess> live)
    {
        var alive = live.Select(p => (p.Pid, ExeName.Normalize(p.ExeName))).ToHashSet();
        return apps.Where(a => !a.Pids.Any(pid => alive.Contains((pid, ExeName.Normalize(a.ExeName)))))
            .Select(a => a.ExeName).ToList();
    }

    /// <summary>Окно «Что открыть обратно»: программы, которые уже запущены снова (по имени, PID не важен).</summary>
    public static IReadOnlyList<string> RunningAgain(IEnumerable<string> exeNames, IEnumerable<RunningProcess> live)
    {
        var names = live.Select(p => ExeName.Normalize(p.ExeName)).ToHashSet();
        return exeNames.Where(n => names.Contains(ExeName.Normalize(n))).ToList();
    }
}
