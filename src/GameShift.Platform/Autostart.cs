using Microsoft.Win32;

namespace GameShift.Platform;

public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "GameShift";

    public static void Set(bool enabled, string exePath)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) k.SetValue(Name, $"\"{exePath}\" --tray");
        else k.DeleteValue(Name, throwOnMissingValue: false);
    }
}
