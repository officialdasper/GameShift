namespace GameShift.Core;

/// <summary>Игра, найденная в файлах или реестре лаунчера. ExeName пуст, если главный exe определить не удалось.</summary>
public sealed record FoundGame(string Name, string Launcher, string Folder, string? ExeName, int? SteamAppId);

public sealed record ScanResult(FoundGame Game, bool AlreadyAdded);

public static class GameMerge
{
    /// <summary>
    /// Склеивает игры из разных лаунчеров по имени exe: остаётся запись со Steam ID, иначе первая.
    /// Игры без exe не склеиваются. Результат отсортирован по названию.
    /// </summary>
    public static IReadOnlyList<ScanResult> Merge(IEnumerable<FoundGame> found, IEnumerable<GameEntry> existing)
    {
        var have = existing.Select(g => ExeName.Normalize(g.ExeName)).ToHashSet();
        var byExe = new Dictionary<string, FoundGame>();
        var result = new List<FoundGame>();
        foreach (var g in found)
        {
            if (string.IsNullOrWhiteSpace(g.ExeName))
            {
                result.Add(g);
                continue;
            }
            var key = ExeName.Normalize(g.ExeName);
            if (!byExe.TryGetValue(key, out var prev))
            {
                byExe[key] = g;
                result.Add(g);
            }
            else if (prev.SteamAppId is null && g.SteamAppId is not null)
            {
                result[result.IndexOf(prev)] = g;
                byExe[key] = g;
            }
        }
        return result
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ScanResult(g, !string.IsNullOrWhiteSpace(g.ExeName) && have.Contains(ExeName.Normalize(g.ExeName))))
            .ToList();
    }
}
