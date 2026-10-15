using System.Security;
using Microsoft.Win32;
using GameShift.Core;

namespace GameShift.Platform;

/// <summary>
/// Поиск установленных игр по файлам и реестру лаунчеров: Steam, Epic, Riot, Battle.net, EA app, Ubisoft Connect.
/// Процессы не перечисляются и не открываются. Сбой одного источника не мешает остальным.
/// </summary>
public static class GameScanner
{
    const int MaxDepth = 6;
    static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public static IReadOnlyList<FoundGame> Scan(CancellationToken ct = default)
    {
        var found = new List<FoundGame>();
        Source(found, ct, Steam);
        Source(found, ct, Epic);
        Source(found, ct, Riot);
        Source(found, ct, Uninstall);
        Source(found, ct, UbisoftInstalls);
        return found;
    }

    static void Source(List<FoundGame> into, CancellationToken ct, Func<CancellationToken, IEnumerable<FoundGame>> source)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            into.AddRange(source(ct));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException
                                      or InvalidOperationException or ArgumentException or NotSupportedException
                                      or System.Text.Json.JsonException)
        {
        }
    }


    static IEnumerable<FoundGame> Steam(CancellationToken ct)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (key?.GetValue("SteamPath") is not string steam || !Directory.Exists(steam)) yield break;
        steam = Path.GetFullPath(steam);

        var libraries = new List<string> { steam };
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (TryRead(vdf) is { } text)
            libraries.AddRange(SteamParser.ParseLibraryFolders(text).Select(Path.GetFullPath));

        foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var file in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                ct.ThrowIfCancellationRequested();
                if (TryRead(file) is not { } acf || SteamParser.ParseManifest(acf) is not { } m || SteamParser.IsTool(m)) continue;
                var folder = Path.Combine(apps, "common", m.InstallDir);
                if (!Directory.Exists(folder)) continue;
                yield return new FoundGame(m.Name, "Steam", folder, ExeIn(folder), m.AppId);
            }
        }
    }


    static IEnumerable<FoundGame> Epic(CancellationToken ct)
    {
        var dir = Path.Combine(ProgramData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(dir)) yield break;
        foreach (var file in Directory.EnumerateFiles(dir, "*.item"))
        {
            ct.ThrowIfCancellationRequested();
            if (TryRead(file) is not { } json || EpicParser.ParseItem(json) is not { } item) continue;
            if (!Directory.Exists(item.InstallLocation)) continue;
            var exe = ExeIn(item.InstallLocation)
                ?? (item.LaunchExecutable is { } l ? Path.GetFileName(l) : null);
            yield return new FoundGame(item.DisplayName, "Epic Games", item.InstallLocation, exe, null);
        }
    }


    static IEnumerable<FoundGame> Riot(CancellationToken ct)
    {
        var meta = Path.Combine(ProgramData, "Riot Games", "Metadata");
        if (!Directory.Exists(meta)) yield break;
        foreach (var file in Directory.EnumerateFiles(meta, "*.product_settings.yaml", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (TryRead(file) is not { } yaml || RiotParser.ParseProduct(yaml) is not { } p) continue;
            if (!Directory.Exists(p.InstallPath)) continue;
            var exe = p.KnownExe is { } k && File.Exists(Path.Combine(p.InstallPath, k))
                ? Path.GetFileName(k)
                : ExeIn(p.InstallPath);
            yield return new FoundGame(p.Name, "Riot Games", p.InstallPath, exe, null);
        }
    }

    // Battle.net, EA app, Ubisoft Connect: записи удаления программ.

    static IEnumerable<FoundGame> Uninstall(CancellationToken ct)
    {
        foreach (var path in new[]
                 {
                     @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                     @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                 })
        {
            using var root = Registry.LocalMachine.OpenSubKey(path);
            if (root is null) continue;
            foreach (var name in root.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                FoundGame? game = null;
                try
                {
                    using var sub = root.OpenSubKey(name);
                    var display = sub?.GetValue("DisplayName") as string;
                    var launcher = UninstallClassifier.Classify(sub?.GetValue("Publisher") as string, display);
                    var location = (sub?.GetValue("InstallLocation") as string)?.Trim().Trim('"');
                    if (launcher is not null && display is not null && !string.IsNullOrEmpty(location) && Directory.Exists(location))
                        game = new FoundGame(display, launcher, location, ExeIn(location), null);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException) { }
                if (game is not null) yield return game;
            }
        }
    }

    static IEnumerable<FoundGame> UbisoftInstalls(CancellationToken ct)
    {
        using var root = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs");
        if (root is null) yield break;
        foreach (var id in root.GetSubKeyNames())
        {
            ct.ThrowIfCancellationRequested();
            using var sub = root.OpenSubKey(id);
            if (sub?.GetValue("InstallDir") is not string dir) continue;
            dir = dir.Trim().Replace('/', '\\').TrimEnd('\\');
            if (!Directory.Exists(dir)) continue;
            yield return new FoundGame(Path.GetFileName(dir), "Ubisoft Connect", dir, ExeIn(dir), null);
        }
    }


    static string? TryRead(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string? ExeIn(string folder)
    {
        var picked = ExePicker.Pick(ListExes(folder));
        return picked is null ? null : Path.GetFileName(picked);
    }

    /// <summary>Все exe папки до MaxDepth уровней: относительный путь и размер. Ссылки-переходы пропускаются.</summary>
    public static IReadOnlyList<(string RelativePath, long Size)> ListExes(string root)
    {
        var result = new List<(string, long)>();
        var level = new List<string> { root };
        for (var depth = 0; depth <= MaxDepth && level.Count > 0; depth++)
        {
            var next = new List<string>();
            foreach (var dir in level)
            {
                try
                {
                    var info = new DirectoryInfo(dir);
                    foreach (var f in info.EnumerateFiles("*.exe"))
                        result.Add((Path.GetRelativePath(root, f.FullName), f.Length));
                    if (depth == MaxDepth) continue;
                    foreach (var sub in info.EnumerateDirectories())
                        if (!sub.Attributes.HasFlag(FileAttributes.ReparsePoint)) next.Add(sub.FullName);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException) { }
            }
            level = next;
        }
        return result;
    }
}
