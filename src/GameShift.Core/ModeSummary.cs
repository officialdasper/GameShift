namespace GameShift.Core;

/// <summary>Ключи строк подсказки на карточке. Выключенное в настройках не упоминается, чтобы подсказка не обещала лишнего.</summary>
public static class ModeSummary
{
    public static IReadOnlyList<string> Keys(Mode mode, Settings settings, bool notificationsSupported, bool nvidiaApplied)
    {
        var notifications = settings.FocusEnabled && notificationsSupported;
        var keys = new List<string>();
        if (Modes.Normalize(mode) == Mode.Gaming)
        {
            if (settings.PowerEnabled) keys.Add("Summary_GamingPower");
            keys.Add("Summary_GamingApps");
            if (notifications) keys.Add("Summary_GamingNotifications");
            if (settings.MemoryPurgeEnabled) keys.Add("Summary_GamingMemory");
            if (nvidiaApplied) keys.Add("Summary_GamingNvidia");
        }
        else
        {
            if (settings.PowerEnabled) keys.Add("Summary_WorkPower");
            if (notifications) keys.Add("Summary_WorkNotifications");
            keys.Add("Summary_WorkApps");
        }
        return keys;
    }
}
