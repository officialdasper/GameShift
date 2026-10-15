using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameShift.App.ViewModels;

/// <summary>Иконка и название программы по exe (FileDescription). Читается только файл на диске; путь приходит от ядра лишь для процессов, прошедших стоп-лист.</summary>
public static class AppIcons
{
    public sealed record Info(ImageSource? Icon, string? Description);

    static readonly ConcurrentDictionary<string, Task<Info>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Task<Info> GetAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(new Info(null, null));
        return Cache.GetOrAdd(path, p => Task.Run(() => Load(p)));
    }

    static Info Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new Info(null, null);
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
            ImageSource? image = null;
            using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
            {
                if (icon is not null)
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    image = source;
                }
            }
            return new Info(image, string.IsNullOrWhiteSpace(description) ? null : description);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                                  or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.ExternalException)
        {
            return new Info(null, null);
        }
    }
}
