using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameShift.App.ViewModels;

/// <summary>Иконки exe игр из их папок. Ищет не глубже 5 уровней, результат кэширует (и промахи тоже).</summary>
public static class GameIcons
{
    const int MaxDepth = 5;
    static readonly ConcurrentDictionary<string, Task<ImageSource?>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Task<ImageSource?> GetAsync(string? folder, string exeName)
    {
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(exeName)) return Task.FromResult<ImageSource?>(null);
        return Cache.GetOrAdd(folder + "|" + exeName, _ => Task.Run(() => Load(folder, exeName)));
    }

    static ImageSource? Load(string folder, string exeName)
    {
        try
        {
            if (Find(folder, exeName) is not string path) return null;
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;
            var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    static string? Find(string root, string exeName)
    {
        if (!Directory.Exists(root)) return null;
        var level = new List<string> { root };
        for (var depth = 0; depth <= MaxDepth && level.Count > 0; depth++)
        {
            var next = new List<string>();
            foreach (var dir in level)
            {
                var candidate = Path.Combine(dir, exeName);
                if (File.Exists(candidate)) return candidate;
                if (depth == MaxDepth) continue;
                try
                {
                    foreach (var sub in new DirectoryInfo(dir).EnumerateDirectories())
                        if (!sub.Attributes.HasFlag(FileAttributes.ReparsePoint)) next.Add(sub.FullName);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                }
            }
            level = next;
        }
        return null;
    }
}
