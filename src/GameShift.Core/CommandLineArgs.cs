namespace GameShift.Core;

public static class CommandLineArgs
{
    /// <summary>Отрезает путь exe от полной командной строки WMI. Пустой остаток даёт null.</summary>
    public static string? StripExecutable(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return null;
        var s = commandLine.TrimStart();
        string rest;
        if (s.StartsWith('"'))
        {
            var end = s.IndexOf('"', 1);
            rest = end < 0 ? "" : s[(end + 1)..];
        }
        else
        {
            var space = s.IndexOf(' ');
            rest = space < 0 ? "" : s[space..];
        }
        rest = rest.Trim();
        return rest.Length == 0 ? null : rest;
    }
}
