using System.Diagnostics;
using GameShift.Core;

namespace GameShift.Platform;

public sealed class PowerCfg : IPower
{
    public string GetActiveScheme() =>
        PowerCfgParser.ParseActiveScheme(Run("/getactivescheme"))
        ?? throw new InvalidOperationException(Strings.Get("Power_ReadFailed"));

    public void SetActiveScheme(string guid)
    {
        if (!PowerCfgParser.ListContains(Run("/list"), guid))
            throw new InvalidOperationException(Strings.Get("Power_SchemeMissing"));
        Run($"/setactive {guid}");
    }

    static string Run(string args)
    {
        var psi = new ProcessStartInfo("powercfg.exe", args)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException(Strings.Get("Power_NotStarted"));
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(10_000);
        if (p.ExitCode != 0) throw new InvalidOperationException(Strings.Format("Power_ExitCode", args, p.ExitCode));
        return output;
    }
}
