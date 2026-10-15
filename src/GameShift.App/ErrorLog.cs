using System.IO;
using System.Windows;
using GameShift.App.Localization;

namespace GameShift.App;

/// <summary>Пишет необработанные ошибки в error.log в папке данных и коротко сообщает о них.</summary>
public static class ErrorLog
{
    public static string FilePath => Path.Combine(AppHost.DataDir, "error.log");

    public static void Write(Exception e)
    {
        try
        {
            Directory.CreateDirectory(AppHost.DataDir);
            File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // лог недоступен: падать из-за этого не будем
        }
    }

    public static void Report(Exception e)
    {
        Write(e);
        try
        {
            MessageBox.Show(Loc.Format("Err_Generic", e.Message, FilePath), "GameShift",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch
        {
        }
    }
}
