namespace GameShift.Core;

/// <summary>
/// Решает, когда переключать режим по запущенным играм. Видит только имена процессов.
/// Ручной выбор режима подавляет уже запущенные игры до их перезапуска.
/// </summary>
public sealed class GameWatcher
{
    readonly TimeSpan _exitGrace;
    readonly HashSet<string> _suppressed = [];
    readonly Dictionary<string, DateTime> _firstSeen = [];
    DateTime _activeLastSeen;

    public string? ActiveGame { get; private set; }

    public GameWatcher(TimeSpan? exitGrace = null) => _exitGrace = exitGrace ?? TimeSpan.FromSeconds(10);

    public Mode? Tick(IReadOnlyCollection<string> runningExeNames, Settings s, Mode current, DateTime now)
    {
        var running = runningExeNames.Select(ExeName.Normalize).ToHashSet();
        var games = GameModes(s);
        var runningGames = games.Keys.Where(running.Contains).ToList();

        foreach (var g in _firstSeen.Keys.Where(g => !running.Contains(g)).ToList()) _firstSeen.Remove(g);
        foreach (var g in runningGames) _firstSeen.TryAdd(g, now);
        _suppressed.RemoveWhere(g => !running.Contains(g));

        if (ActiveGame is not null && running.Contains(ActiveGame))
        {
            _activeLastSeen = now;
            return null;
        }
        if (ActiveGame is not null && now - _activeLastSeen < _exitGrace) return null;

        var next = runningGames
            .Where(g => !_suppressed.Contains(g))
            .OrderBy(g => _firstSeen[g])
            .FirstOrDefault();
        if (next is not null)
        {
            ActiveGame = next;
            _activeLastSeen = now;
            return games[next] == current ? null : games[next];
        }

        if (ActiveGame is not null)
        {
            ActiveGame = null;
            return current == Mode.Work ? null : Mode.Work;
        }
        return null;
    }

    public void Reset() => ActiveGame = null;

    public void NotifyManualSwitch(IReadOnlyCollection<string> runningExeNames)
    {
        ActiveGame = null;
        foreach (var n in runningExeNames) _suppressed.Add(ExeName.Normalize(n));
    }

    static Dictionary<string, Mode> GameModes(Settings s)
    {
        var map = new Dictionary<string, Mode>();
        foreach (var g in s.Games) map.TryAdd(ExeName.Normalize(g.ExeName), Mode.Gaming);
        return map;
    }
}
