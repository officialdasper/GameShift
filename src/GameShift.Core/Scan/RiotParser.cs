namespace GameShift.Core;

public sealed record RiotProduct(string ProductId, string Name, string InstallPath, string? KnownExe);

public static class RiotParser
{
    static readonly Dictionary<string, (string Name, string? Exe)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["valorant"] = ("VALORANT", @"ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe"),
        ["league_of_legends"] = ("League of Legends", @"Game\League of Legends.exe"),
        ["bacon"] = ("Legends of Runeterra", null),
        ["lor"] = ("Legends of Runeterra", null),
    };

    public static RiotProduct? ParseProduct(string yaml)
    {
        string? id = null, path = null;
        foreach (var raw in yaml.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("product_id:", StringComparison.Ordinal)) id = Value(line);
            else if (line.StartsWith("product_install_full_path:", StringComparison.Ordinal)) path = Value(line);
        }
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(path)) return null;
        path = path.Replace('/', '\\').TrimEnd('\\');
        return Known.TryGetValue(id, out var k)
            ? new RiotProduct(id, k.Name, path, k.Exe)
            : new RiotProduct(id, id, path, null);
    }

    static string Value(string line)
    {
        var v = line[(line.IndexOf(':') + 1)..].Trim();
        if (v.Length >= 2 && (v[0] == '"' || v[0] == '\'') && v[^1] == v[0]) v = v[1..^1];
        return v;
    }
}
