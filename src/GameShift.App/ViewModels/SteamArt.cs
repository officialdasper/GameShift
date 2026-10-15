using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace GameShift.App.ViewModels;

public enum ArtKind { Hero, Header }

/// <summary>Обложки игр из локального кэша Steam (appcache\librarycache), без сети. Файл декодируется сразу и не остаётся открытым; промахи кэшируются.</summary>
public static partial class SteamArt
{
    const string DefaultSteam = @"C:\Program Files (x86)\Steam";
    static readonly ConcurrentDictionary<(int, ArtKind), Task<ImageSource?>> Cache = new();
    static readonly Lazy<string> Root = new(FindSteam);

    public static Task<ImageSource?> GetAsync(int? appId, ArtKind kind)
    {
        if (appId is not int id || id <= 0) return Task.FromResult<ImageSource?>(null);
        return Cache.GetOrAdd((id, kind), k => Task.Run(() => Load(k.Item1, k.Item2)));
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource, Task<ImageSource?>> GrayCache = new();

    public static Task<ImageSource?> GrayAsync(ImageSource art) =>
        GrayCache.GetValue(art, a => Task.Run(() => ToGray(a)));

    static ImageSource? ToGray(ImageSource art)
    {
        if (art is not BitmapSource source) return null;
        try
        {
            var gray = new WriteableBitmap(new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0));
            gray.Freeze();
            return gray;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    public static int? FindAppId(string? gameFolder)
    {
        if (string.IsNullOrWhiteSpace(gameFolder)) return null;
        try
        {
            var full = Path.GetFullPath(gameFolder);
            var marker = full.IndexOf(@"\steamapps\common\", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return null;
            var steamapps = full[..(marker + @"\steamapps".Length)];
            var installDir = full[(marker + @"\steamapps\common\".Length)..].Split('\\')[0];
            foreach (var manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
            {
                var text = File.ReadAllText(manifest);
                var dir = InstallDirRegex().Match(text);
                if (!dir.Success || !dir.Groups[1].Value.Equals(installDir, StringComparison.OrdinalIgnoreCase)) continue;
                var id = AppIdRegex().Match(text);
                if (id.Success && int.TryParse(id.Groups[1].Value, out var appId)) return appId;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                                      or NotSupportedException or System.Security.SecurityException)
        {
        }
        return null;
    }

    static ImageSource? Load(int appId, ArtKind kind)
    {
        try
        {
            var folder = Path.Combine(Root.Value, "appcache", "librarycache", appId.ToString());
            var path = FindFile(folder, appId, kind);
            if (path is null) return null;
            // Широкий hero уменьшаем до размера карточки, мелкие картинки берём как есть.
            var isHero = Path.GetFileName(path).EndsWith("library_hero.jpg", StringComparison.OrdinalIgnoreCase);
            return Decode(path, kind == ArtKind.Header ? 360 : isHero ? 1280 : 0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException or InvalidOperationException
                                      or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    static string? FindFile(string folder, int appId, ArtKind kind)
    {
        string[] order = kind == ArtKind.Hero
            ? ["library_hero.jpg", "header.jpg", "library_header.jpg", "library_600x900.jpg", "library_capsule.jpg"]
            : ["header.jpg", "library_header.jpg", "library_hero.jpg", "library_600x900.jpg", "library_capsule.jpg"];

        if (Directory.Exists(folder))
        {
            var files = Directory.EnumerateFiles(folder, "*.jpg", SearchOption.AllDirectories).ToList();
            foreach (var name in order)
            {
                var hit = files
                    .Where(f => Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.Length) // файл в корне папки раньше вложенного
                    .FirstOrDefault();
                if (hit is not null) return hit;
            }
        }

        // Старая раскладка кэша: файлы вида 730_library_hero.jpg прямо в librarycache.
        var flat = Path.GetDirectoryName(folder)!;
        return order.Select(n => Path.Combine(flat, $"{appId}_{n}")).FirstOrDefault(File.Exists);
    }

    static ImageSource Decode(string path, int width)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad; // читает всё сразу, поток можно закрыть
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        if (width > 0) image.DecodePixelWidth = width;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    static string FindSteam()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string p && Directory.Exists(p)) return Path.GetFullPath(p);
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
        }
        return DefaultSteam;
    }

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirRegex();

    [GeneratedRegex("\"appid\"\\s+\"(\\d+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AppIdRegex();
}
