using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameShift.Core;
using Microsoft.Win32;

namespace GameShift.App.ViewModels;

/// <summary>Обои рабочего стола для фона карточки «Работа»: уменьшенная слегка размытая копия и серая версия. Файл читается целиком и сразу закрывается, обои не блокируются.</summary>
public static class Wallpaper
{
    // Ширина около ширины карточки, размытие лёгкое: формы обоев узнаются, мелкие детали не спорят с текстом.
    const int Width = 480;
    const int BlurRadius = 7;

    public sealed record Art(ImageSource Color, ImageSource Gray);

    public static Task<Art?> LoadAsync() => Task.Run(Load);

    static Art? Load()
    {
        try
        {
            var path = FindFile();
            if (path is null) return null;
            byte[] bytes;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                bytes = new byte[file.Length];
                file.ReadExactly(bytes);
            }

            var decoded = new BitmapImage();
            decoded.BeginInit();
            decoded.CacheOption = BitmapCacheOption.OnLoad;
            decoded.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            decoded.DecodePixelWidth = Width;
            decoded.StreamSource = new MemoryStream(bytes);
            decoded.EndInit();
            decoded.Freeze();

            var bgr = new FormatConvertedBitmap(decoded, PixelFormats.Bgr32, null, 0);
            int w = bgr.PixelWidth, h = bgr.PixelHeight;
            var pixels = new uint[w * h];
            bgr.CopyPixels(pixels, w * 4, 0);
            ImageMath.BoxBlur(pixels, w, h, BlurRadius, passes: 3);

            var color = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr32, null, pixels, w * 4);
            color.Freeze();
            var gray = new WriteableBitmap(new FormatConvertedBitmap(color, PixelFormats.Gray8, null, 0));
            gray.Freeze();
            return new Art(color, gray);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                       or ArgumentException or InvalidOperationException or System.Security.SecurityException)
        {
            return null; // нет обоев или формат не читается: карточка остаётся спокойной поверхностью с глифом
        }
    }

    static string? FindFile()
    {
        using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var registered = desktop?.GetValue("WallPaper") as string;
        if (registered is not null && registered.Length == 0) return null;

        var transcoded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Themes\TranscodedWallpaper");
        if (File.Exists(transcoded)) return transcoded;
        return !string.IsNullOrWhiteSpace(registered) && File.Exists(registered) ? registered : null;
    }
}
