using Microsoft.Win32;
using GameShift.Core;

namespace GameShift.Platform;

/// <summary>Главный переключатель уведомлений Windows (Параметры → Система → Уведомления).</summary>
public sealed class ToastSwitch : IToasts
{
    /// <summary>false, если Windows игнорирует ToastEnabled.</summary>
    public static readonly bool IsSupported = true;

    const string Key = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";
    const string Value = "ToastEnabled";

    public bool GetEnabled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key);
        return (k?.GetValue(Value) as int? ?? 1) != 0;
    }

    public void SetEnabled(bool enabled)
    {
        using var k = Registry.CurrentUser.CreateSubKey(Key);
        k.SetValue(Value, enabled ? 1 : 0, RegistryValueKind.DWord);
    }
}
