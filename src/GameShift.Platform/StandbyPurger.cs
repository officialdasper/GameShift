using System.ComponentModel;
using System.Diagnostics;
using GameShift.Core;

namespace GameShift.Platform;

/// <summary>
/// Очистка standby-списка памяти. Права администратора выдаются один раз: при включении функции
/// создаётся задача планировщика с наивысшими правами, дальше она запускается без окна UAC.
/// </summary>
public sealed class StandbyPurger : IMemoryPurger
{
    public const string TaskName = @"GameShift\PurgeStandby";
    public const string HelperArg = "--purge-standby";

    readonly string _exePath;

    public StandbyPurger(string exePath) => _exePath = exePath;

    public bool IsConfigured => Schtasks($"/Query /TN \"{TaskName}\"", elevated: false) == 0;

    public void Purge()
    {
        if (Schtasks($"/Run /TN \"{TaskName}\"", elevated: false) != 0)
            throw new InvalidOperationException(Strings.Get("Purge_TaskFailed"));
    }

    public bool Configure()
    {
        var command = $"\\\"{_exePath}\\\" {HelperArg}";
        return Schtasks($"/Create /F /TN \"{TaskName}\" /SC ONCE /ST 00:00 /RL HIGHEST /TR \"{command}\"", elevated: true) == 0;
    }

    public bool Remove() => Schtasks($"/Delete /F /TN \"{TaskName}\"", elevated: true) == 0;

    public static int PurgeNow()
    {
        if (!NativeMethods.EnablePrivilege("SeProfileSingleProcessPrivilege")) return 2;
        var command = NativeMethods.MemoryPurgeStandbyList;
        var status = NativeMethods.NtSetSystemInformation(NativeMethods.SystemMemoryListInformation, ref command, sizeof(int));
        return status == 0 ? 0 : 3;
    }

    static int Schtasks(string args, bool elevated)
    {
        var psi = new ProcessStartInfo("schtasks.exe", args)
        {
            UseShellExecute = elevated,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        if (elevated) psi.Verb = "runas";
        try
        {
            using var p = Process.Start(psi)!;
            p.WaitForExit(15_000);
            return p.HasExited ? p.ExitCode : -1;
        }
        catch (Win32Exception)
        {
            return -1; // пользователь отказал в UAC
        }
    }
}
