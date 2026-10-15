namespace GameShift.Core;

public static class SettingsValidator
{
    public static string? CanAddGame(Settings s, string exeName)
    {
        var n = ExeName.Normalize(exeName);
        return s.Games.Any(g => ExeName.Normalize(g.ExeName) == n)
            ? Strings.Get("Val_GameExists")
            : null;
    }

    public static string? CanAddApp(Settings s, StopList stop, string exeName)
    {
        var n = ExeName.Normalize(exeName);
        if (stop.IsBlockedName(n)) return Strings.Get("Val_AppProtected");
        if (s.Apps.Any(a => ExeName.Normalize(a.ExeName) == n)) return Strings.Get("Val_AppExists");
        return null;
    }
}
