using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameShift.Core;

/// <summary>
/// Режимов два: «Работа» и «Игровой». Значение Wardogs осталось только для чтения старых файлов
/// (снимок и настройки прошлых версий) и при загрузке превращается в Gaming.
/// </summary>
public enum Mode { Work, Wardogs, Gaming }

public static class Modes
{
    public static Mode Normalize(Mode mode) => mode == Mode.Wardogs ? Mode.Gaming : mode;
}

public enum AppRule { CloseAndRestore, CloseOnly, KeepRunning }

public enum ThemeMode { System, Dark, Light }

public sealed record AppEntry(string ExeName, string DisplayName, AppRule Rule);

public enum BackdropMode { None, Mica, Acrylic }

/// <summary>SteamAppId нужен только для обложки из локального кэша Steam.</summary>
public sealed record GameEntry(string ExeName, string? Folder, int? SteamAppId = null);

public sealed class Settings : IJsonOnDeserialized
{
    public const string MaxPerformanceScheme = "fbf94b8d-fa2c-49fa-a879-dad3d86c3f39";

    List<GameEntry> _games =
    [
        new("WardogsClient-Win64-Shipping.exe", @"C:\Program Files (x86)\Steam\steamapps\common\Wardogs", 1867240),
        new("cs2.exe", @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive", 730),
        new("VALORANT-Win64-Shipping.exe", null),
        new("DeltaForceClient-Win64-Shipping.exe", @"C:\Program Files (x86)\Steam\steamapps\common\Delta Force", 2507950),
    ];
    bool _gamesLoaded;

    public List<GameEntry> Games
    {
        get => _games;
        set
        {
            _games = value;
            _gamesLoaded = true;
        }
    }

    // Два списка из прошлых версий (WARDOGS и остальные игры). Только читаются и сливаются в Games.
    [JsonInclude, JsonPropertyName("WardogsGames"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    List<GameEntry>? LegacyWardogsGames { get; set; }

    [JsonInclude, JsonPropertyName("GamingGames"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    List<GameEntry>? LegacyGamingGames { get; set; }

    /// <summary>Сетевые помощники: без них не работают Discord и Telegram. Их же защищает StopList.</summary>
    static readonly AppEntry[] NetworkHelpers =
    [
        new("winws.exe", "zapret", AppRule.KeepRunning),
        new("TgWsProxy_windows.exe", "TG WS Proxy", AppRule.KeepRunning),
    ];

    static readonly Dictionary<string, int> KnownSteamAppIds = new()
    {
        ["wardogsclient-win64-shipping"] = 1867240,
        ["cs2"] = 730,
        ["deltaforceclient-win64-shipping"] = 2507950,
    };

    public List<AppEntry> Apps { get; set; } = [new("Discord.exe", "Discord", AppRule.KeepRunning), .. NetworkHelpers];

    public bool PowerEnabled { get; set; } = true;
    public string GamePowerScheme { get; set; } = MaxPerformanceScheme;
    public bool FocusEnabled { get; set; } = true;
    public bool MemoryPurgeEnabled { get; set; }
    public bool AutoSwitchEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; }

    public ThemeMode Theme { get; set; } = ThemeMode.Dark;

    public string Accent { get; set; } = "#76B900";

    public BackdropMode Backdrop { get; set; } = BackdropMode.Mica;

    public string Language { get; set; } = Languages.System;

    void IJsonOnDeserialized.OnDeserialized()
    {
        if (LegacyWardogsGames is not null || LegacyGamingGames is not null)
        {
            var merged = (_gamesLoaded ? _games ?? [] : [])
                .Concat(LegacyWardogsGames ?? [])
                .Concat(LegacyGamingGames ?? []);
            _games = merged.Where(g => !string.IsNullOrWhiteSpace(g?.ExeName))
                .DistinctBy(g => ExeName.Normalize(g.ExeName))
                .ToList();
            LegacyWardogsGames = null;
            LegacyGamingGames = null;
        }
        _games ??= [];
        Apps ??= [];
        GamePowerScheme ??= MaxPerformanceScheme;
        Accent ??= "#76B900";
        Language ??= Languages.System;

        // Настройки из старых версий: подставить известные Steam ID и добавить сетевые исключения.
        _games = _games.Select(WithKnownAppId).ToList();
        foreach (var h in NetworkHelpers)
        {
            var n = ExeName.Normalize(h.ExeName);
            if (!Apps.Any(a => ExeName.Normalize(a.ExeName) == n)) Apps.Add(h);
        }
    }

    static GameEntry WithKnownAppId(GameEntry g) =>
        g.SteamAppId is null && KnownSteamAppIds.TryGetValue(ExeName.Normalize(g.ExeName), out var id)
            ? g with { SteamAppId = id }
            : g;

    public Settings Clone() =>
        JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this, JsonFileStore<Settings>.Options),
            JsonFileStore<Settings>.Options)!;
}
