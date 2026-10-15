namespace GameShift.Core;

public sealed record SteamManifest(int AppId, string Name, string InstallDir);

public static class SteamParser
{
    static readonly HashSet<int> ToolAppIds = [228980, 250820, 1070560, 1391110, 1628350, 1493710, 1113280, 1826330];

    static readonly string[] ToolNamePrefixes =
    [
        "Proton", "SteamVR", "Steamworks", "Steam Linux Runtime", "Steam Controller", "Source SDK Base",
    ];

    public static IReadOnlyList<string> ParseLibraryFolders(string vdf)
    {
        var paths = new List<string>();
        if (Vdf.Parse(vdf).Values.FirstOrDefault() is not Dictionary<string, object> folders) return paths;
        foreach (var (key, value) in folders)
        {
            if (key.Length == 0 || !key.All(char.IsDigit)) continue;
            var path = value switch
            {
                string s => s,
                Dictionary<string, object> d when d.TryGetValue("path", out var p) => p as string,
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(path)) paths.Add(path);
        }
        return paths;
    }

    public static SteamManifest? ParseManifest(string acf)
    {
        if (Vdf.Parse(acf).Values.FirstOrDefault() is not Dictionary<string, object> state) return null;
        if (state.GetValueOrDefault("appid") is not string id || !int.TryParse(id, out var appId)) return null;
        var name = state.GetValueOrDefault("name") as string;
        var dir = state.GetValueOrDefault("installdir") as string;
        return string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dir) ? null : new SteamManifest(appId, name, dir);
    }

    public static bool IsTool(SteamManifest m) =>
        ToolAppIds.Contains(m.AppId)
        || ToolNamePrefixes.Any(p => m.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
