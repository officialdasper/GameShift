namespace GameShift.Core;

/// <summary>Процессы и папки, которые программа не закрывает никогда.</summary>
public sealed class StopList
{
    // Нормализованные имена; "*" в конце означает любой хвост.
    static readonly string[] BuiltIn =
    [
        "elytra*", "wardogslauncher*", "vgc", "vgtray", "riotclient*", "riot client", "faceit*",
        "easyanticheat*", "beservice*", "sguard*", "ace-*",
        "steam", "steamwebhelper", "steamservice",
        // Другие лаунчеры: через них запускаются игры, а окно выбора программ не должно их даже опрашивать.
        "epicgameslauncher", "epicwebhelper", "epiconlineservices*", "eosoverlayrenderer*",
        "battle.net", "eadesktop", "eabackgroundservice", "origin", "originwebhelperservice",
        "upc", "uplaywebcore", "ubisoftconnect", "ubisoftgamelauncher*", "galaxyclient*", "goggalaxy*",
        "rockstarservice", "socialclubhelper",
        "iyx", "iyx_ast_embed", "driver.service",
        "nvcontainer", "nvdisplay.container",
        "system", "registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost",
        "dwm", "explorer", "audiodg", "fontdrvhost", "sihost", "ctfmon",
        "gameshift",
        // Обход блокировок для Discord/Telegram: закрытие ломает связь.
        "winws", "zapret*", "tgwsproxy*", "goodbyedpi*",
    ];

    readonly string[] _gameNames;
    readonly string[] _blockedFolders;

    public StopList(IEnumerable<GameEntry> games, string? windowsFolder = null)
    {
        var list = games.ToList();
        _gameNames = list.Select(g => ExeName.Normalize(g.ExeName)).ToArray();
        var windows = windowsFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        _blockedFolders = list
            .Where(g => !string.IsNullOrWhiteSpace(g.Folder))
            .Select(g => WithSlash(g.Folder!))
            .Append(WithSlash(windows))
            .ToArray();
    }

    public static bool IsBuiltInBlocked(string exeName) => Matches(BuiltIn, ExeName.Normalize(exeName));

    public bool IsBlockedName(string exeName)
    {
        var n = ExeName.Normalize(exeName);
        return Matches(BuiltIn, n) || _gameNames.Contains(n);
    }

    public bool IsBlockedPath(string fullPath) =>
        _blockedFolders.Any(f => fullPath.StartsWith(f, StringComparison.OrdinalIgnoreCase));

    static bool Matches(string[] patterns, string n)
    {
        foreach (var p in patterns)
        {
            if (p.EndsWith('*') ? n.StartsWith(p[..^1], StringComparison.Ordinal) : n == p)
                return true;
        }
        return false;
    }

    static string WithSlash(string p) => p.EndsWith('\\') ? p : p + "\\";
}
