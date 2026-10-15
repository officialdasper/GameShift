namespace GameShift.Core;

public static class ExeName
{
    /// <summary>"C:\x\Discord.EXE" и "Discord" дают "discord". Process.ProcessName приходит без ".exe".</summary>
    public static string Normalize(string name)
    {
        var n = name.Trim();
        var slash = n.LastIndexOfAny(['\\', '/']);
        if (slash >= 0) n = n[(slash + 1)..];
        if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n[..^4];
        return n.ToLowerInvariant();
    }
}
