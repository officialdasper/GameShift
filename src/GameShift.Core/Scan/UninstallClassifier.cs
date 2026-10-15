namespace GameShift.Core;

public static class UninstallClassifier
{
    static readonly string[] LauncherNames =
    [
        "Battle.net", "Blizzard", "EA app", "EA Desktop", "Origin", "Ubisoft Connect", "Uplay",
    ];

    static readonly string[] NotGames =
    [
        "redistributable", "runtime", "visual c++", "directx", "anticheat", "anti-cheat", "easyanticheat",
        "battleye", ".net", "vulkan", "physx", "webview", "updater", "support", "service",
    ];

    public static string? Classify(string? publisher, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(publisher) || string.IsNullOrWhiteSpace(displayName)) return null;
        var launcher =
            publisher.Contains("Blizzard", StringComparison.OrdinalIgnoreCase) ? "Battle.net"
            : publisher.Contains("Electronic Arts", StringComparison.OrdinalIgnoreCase) ? "EA app"
            : publisher.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase) ? "Ubisoft Connect"
            : null;
        if (launcher is null) return null;
        var name = displayName.Trim();
        if (LauncherNames.Any(l => name.Equals(l, StringComparison.OrdinalIgnoreCase))) return null;
        if (NotGames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase))) return null;
        return launcher;
    }
}
