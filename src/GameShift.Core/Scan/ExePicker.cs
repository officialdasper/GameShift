namespace GameShift.Core;

public static class ExePicker
{
    static readonly string[] Noise =
    [
        "crash", "report", "setup", "install", "unins", "redist", "helper", "easyanticheat", "battleye",
        "ue4prereq", "vc_redist", "dxsetup", "dotnet", "vcredist", "directx",
        "vconsole", "dedicated", "editor", "benchmark", "modkit",
    ];

    static readonly string[] NoiseFolders = ["redist", "_commonredist", "directx", "prerequisites", "easyanticheat", "battleye"];

    /// <summary>
    /// Возвращает относительный путь выбранного exe или null. Порядок: *Shipping.exe, затем exe под
    /// Binaries\Win64, затем самый большой. Служебные exe отброшены, «launcher» только если есть другие.
    /// </summary>
    public static string? Pick(IReadOnlyList<(string RelativePath, long Size)> exes)
    {
        var candidates = exes
            .Select(e => (e.RelativePath, e.Size, Name: NameOf(e.RelativePath)))
            .Where(e => e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Where(e => !Noise.Any(n => e.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .Where(e => !InNoiseFolder(e.RelativePath))
            .ToList();
        if (candidates.Any(e => !IsLauncher(e.Name)))
            candidates = candidates.Where(e => !IsLauncher(e.Name)).ToList();
        if (candidates.Count == 0) return null;

        return candidates
            .OrderByDescending(e => e.Name.EndsWith("Shipping.exe", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(e => InBinariesWin64(e.RelativePath))
            .ThenByDescending(e => e.Size)
            .ThenBy(e => e.RelativePath.Length)
            .ThenBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .First().RelativePath;
    }

    static bool IsLauncher(string name) => name.Contains("launcher", StringComparison.OrdinalIgnoreCase);

    static string NameOf(string path) => path[(path.LastIndexOfAny(['\\', '/']) + 1)..];

    static bool InNoiseFolder(string path)
    {
        var parts = path.Split('\\', '/');
        return parts[..^1].Any(p => NoiseFolders.Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    static bool InBinariesWin64(string path) =>
        path.Replace('/', '\\').Contains(@"Binaries\Win64\", StringComparison.OrdinalIgnoreCase);
}
