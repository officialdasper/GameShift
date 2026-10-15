using System.Text.Json.Serialization;
using NvAPIWrapper;
using NvAPIWrapper.DRS;
using NvAPIWrapper.Native.DRS;
using NvAPIWrapper.Native.Exceptions;
using GameShift.Core;

namespace GameShift.Platform;

public sealed class NvidiaBackup : IJsonOnDeserialized
{
    public List<NvidiaGameBackup> Games { get; set; } = [];

    void IJsonOnDeserialized.OnDeserialized() => Games = (Games ?? []).Where(g => g is not null).ToList();
}

public sealed class NvidiaGameBackup : IJsonOnDeserialized
{
    public string Exe { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public bool CreatedByUs { get; set; }
    /// <summary>null — значение не было задано пользователем в этом профиле.</summary>
    public Dictionary<uint, uint?> Original { get; set; } = [];

    void IJsonOnDeserialized.OnDeserialized()
    {
        Exe ??= "";
        ProfileName ??= "";
        Original ??= [];
    }
}

/// <summary>Настройки драйвера NVIDIA для профилей игр (не глобально). ID из NvApiDriverSettings.h.</summary>
public sealed class NvidiaProfiles
{
    public static readonly IReadOnlyList<(uint Id, uint Value, string NameKey)> GameSettings =
    [
        (0x1057EB71, 0x00000001, "Nvidia_PowerMgmt"),
        (0x007BA09E, 0x00000001, "Nvidia_LowLatency"),
        (0x00CE2691, 0x00000014, "Nvidia_TextureFiltering"),
        (0x00A879CF, 0x08416747, "Nvidia_Vsync"),
        (0x00AC8497, 0xFFFFFFFF, "Nvidia_ShaderCache"),
    ];

    public static IReadOnlyList<string> SettingNames() => GameSettings.Select(s => Strings.Get(s.NameKey)).ToList();

    readonly IStore<NvidiaBackup> _backup;

    public NvidiaProfiles(IStore<NvidiaBackup> backup) => _backup = backup;

    public bool HasApplied => (_backup.Load()?.Games.Count ?? 0) > 0;

    public static bool IsAvailable()
    {
        try
        {
            NVIDIA.Initialize();
            return true;
        }
        catch (Exception)
        {
            return false; // нет nvapi64.dll, не NVIDIA, драйвер без DRS
        }
    }

    public IReadOnlyList<StepResult> Apply(IEnumerable<string> gameExes)
    {
        NVIDIA.Initialize();
        var backup = _backup.Load() ?? new NvidiaBackup();
        var results = new List<StepResult>();
        using var session = DriverSettingsSession.CreateAndLoad();
        foreach (var exe in gameExes)
        {
            try
            {
                var profile = Find(session, exe);
                var created = false;
                if (profile is null)
                {
                    profile = DriverSettingsProfile.CreateProfile(session, "GameShift " + exe);
                    ProfileApplication.CreateApplication(profile, exe.ToLowerInvariant());
                    created = true;
                }
                if (!backup.Games.Any(g => g.Exe.Equals(exe, StringComparison.OrdinalIgnoreCase)))
                {
                    var entry = new NvidiaGameBackup { Exe = exe, ProfileName = profile.Name, CreatedByUs = created };
                    foreach (var (id, _, _) in GameSettings) entry.Original[id] = ReadUserValue(profile, id);
                    backup.Games.Add(entry);
                }
                var unsupported = new List<string>();
                foreach (var (id, value, nameKey) in GameSettings)
                {
                    try { profile.SetSetting(id, value); }
                    catch (NVIDIAApiException) { unsupported.Add(Strings.Get(nameKey)); }
                }
                results.Add(unsupported.Count == 0
                    ? new StepResult(exe, StepStatus.Done, Strings.Get("Nvidia_Applied"))
                    : new StepResult(exe, StepStatus.Failed, Strings.Format("Nvidia_Unsupported", string.Join(", ", unsupported))));
            }
            catch (NVIDIAApiException e)
            {
                results.Add(new StepResult(exe, StepStatus.Failed, e.Message));
            }
        }
        session.Save();
        _backup.Save(backup);
        return results;
    }

    public IReadOnlyList<StepResult> Restore()
    {
        var backup = _backup.Load();
        if (backup is null || backup.Games.Count == 0)
            return [new StepResult("NVIDIA", StepStatus.Skipped, Strings.Get("Nvidia_NothingToRestore"))];

        NVIDIA.Initialize();
        var results = new List<StepResult>();
        var left = new List<NvidiaGameBackup>(); // не восстановились: оставляем в бэкапе для повтора
        using var session = DriverSettingsSession.CreateAndLoad();
        foreach (var g in backup.Games)
        {
            try
            {
                var profile = Find(session, g.Exe);
                if (profile is null)
                {
                    results.Add(new StepResult(g.Exe, StepStatus.Skipped, Strings.Get("Nvidia_ProfileNotFound")));
                    continue;
                }
                if (g.CreatedByUs)
                {
                    profile.Delete();
                    results.Add(new StepResult(g.Exe, StepStatus.Done, Strings.Get("Nvidia_ProfileDeleted")));
                    continue;
                }
                var failed = new Dictionary<uint, uint?>();
                foreach (var (id, original) in g.Original)
                {
                    try
                    {
                        if (original is uint v) profile.SetSetting(id, v);
                        else profile.RestoreSettingToDefault(id);
                    }
                    catch (NVIDIAApiException)
                    {
                        try { profile.DeleteSetting(id); }
                        catch (NVIDIAApiException) { failed[id] = original; }
                    }
                }
                if (failed.Count == 0)
                {
                    results.Add(new StepResult(g.Exe, StepStatus.Done, Strings.Get("Nvidia_Restored")));
                }
                else
                {
                    left.Add(new NvidiaGameBackup { Exe = g.Exe, ProfileName = g.ProfileName, Original = failed });
                    results.Add(new StepResult(g.Exe, StepStatus.Failed, Strings.Format("Nvidia_RestoreFailed", failed.Count)));
                }
            }
            catch (NVIDIAApiException e)
            {
                left.Add(g);
                results.Add(new StepResult(g.Exe, StepStatus.Failed, e.Message));
            }
        }
        session.Save();
        if (left.Count == 0) _backup.Delete();
        else _backup.Save(new NvidiaBackup { Games = left });
        return results;
    }

    static DriverSettingsProfile? Find(DriverSettingsSession session, string exe)
    {
        try { return session.FindApplicationProfile(exe.ToLowerInvariant()); }
        catch (NVIDIAApiException) { return null; }
    }

    static uint? ReadUserValue(DriverSettingsProfile profile, uint id)
    {
        try
        {
            var s = profile.GetSetting(id);
            return s.SettingLocation == DRSSettingLocation.CurrentProfile && !s.IsCurrentValuePredefined && s.CurrentValue is uint v
                ? v
                : null;
        }
        catch (NVIDIAApiException)
        {
            return null;
        }
    }
}
