using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using GameShift.Core;

namespace GameShift.Platform;

public sealed class ProcessOps : IProcessOps
{
    public IReadOnlyList<RunningProcess> List()
    {
        // Id и ProcessName берутся из системного снимка, процесс при этом не открывается.
        var result = new List<RunningProcess>();
        foreach (var p in Process.GetProcesses())
        {
            using (p) result.Add(new RunningProcess(p.Id, p.ProcessName + ".exe"));
        }
        return result;
    }

    public ProcessLaunchInfo? TryGetLaunchInfo(int pid)
    {
        try
        {
            // Берём один экземпляр по ключу Handle. Запрос WHERE ProcessId заставил бы провайдер обойти все процессы, включая игры.
            using var o = new ManagementObject($"Win32_Process.Handle='{pid}'");
            o.Get();
            return new ProcessLaunchInfo(
                o["ExecutablePath"] as string,
                CommandLineArgs.StripExecutable(o["CommandLine"] as string),
                Convert.ToInt32(o["ParentProcessId"]));
        }
        catch (Exception)
        {
            return null; // нет процесса, WMI недоступен, нет прав
        }
    }

    public IReadOnlySet<int> WindowedPids(IReadOnlySet<int> candidates) => NativeMethods.PidsWithVisibleWindows(candidates);

    public void RequestClose(IReadOnlyCollection<PlannedClose> targets, StopList stop) =>
        WithVerified(targets, stop, p => p.CloseMainWindow());

    public void Kill(IReadOnlyCollection<PlannedClose> targets, StopList stop) => WithVerified(targets, stop, p => p.Kill());

    /// <summary>
    /// Свежий снимок без открытия процессов: PID мог освободиться и достаться другому процессу, например античиту.
    /// Действие выполняется только для PID, которые носят то же имя и не попали в стоп-лист.
    /// </summary>
    static void WithVerified(IReadOnlyCollection<PlannedClose> targets, StopList stop, Action<Process> action)
    {
        var snapshot = Process.GetProcesses();
        try
        {
            var fresh = snapshot.Select(p => new RunningProcess(p.Id, p.ProcessName + ".exe")).ToList();
            var verified = AppsPlanner.Reverify(fresh, targets, stop).Select(t => t.Pid).ToHashSet();
            foreach (var p in snapshot.Where(p => verified.Contains(p.Id)))
            {
                try { action(p); }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
        }
        finally
        {
            foreach (var p in snapshot) p.Dispose();
        }
    }

    public void Start(string path, string? arguments)
    {
        var psi = new ProcessStartInfo(path, arguments ?? "")
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(path) ?? "",
        };
        Process.Start(psi)?.Dispose();
    }
}
