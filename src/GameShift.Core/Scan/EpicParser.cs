using System.Text.Json;

namespace GameShift.Core;

public sealed record EpicItem(string DisplayName, string InstallLocation, string? LaunchExecutable);

public static class EpicParser
{
    /// <summary>Игра из манифеста; null для DLC, движков, недоустановленных и битых файлов.</summary>
    public static EpicItem? ParseItem(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;
            var name = Str(r, "DisplayName");
            var dir = Str(r, "InstallLocation");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dir)) return null;
            if (r.TryGetProperty("bIsIncompleteInstall", out var inc) && inc.ValueKind == JsonValueKind.True) return null;

            var app = Str(r, "AppName");
            var main = Str(r, "MainGameAppName");
            if (!string.IsNullOrEmpty(main) && !string.IsNullOrEmpty(app) && !main.Equals(app, StringComparison.OrdinalIgnoreCase))
                return null;

            if (r.TryGetProperty("AppCategories", out var cats) && cats.ValueKind == JsonValueKind.Array
                && !cats.EnumerateArray().Any(c => c.ValueKind == JsonValueKind.String
                    && string.Equals(c.GetString(), "games", StringComparison.OrdinalIgnoreCase)))
                return null;

            var exe = Str(r, "LaunchExecutable");
            return new EpicItem(name, dir, string.IsNullOrWhiteSpace(exe) ? null : exe);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
