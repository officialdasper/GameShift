namespace GameShift.Core;

public enum StepStatus { Done, Failed, Skipped }

public sealed record StepResult(string Step, StepStatus Status, string Detail);

public sealed record SwitchReport(Mode From, Mode To, IReadOnlyList<StepResult> Steps)
{
    public bool HasFailures => Steps.Any(s => s.Status == StepStatus.Failed);
}

public static class StepNames
{
    public static string Power => Strings.Get("Step_Power");
    public static string Focus => Strings.Get("Step_Focus");
    public static string Apps => Strings.Get("Step_Apps");
    public static string Memory => Strings.Get("Step_Memory");
    public static string Mode => Strings.Get("Step_Mode");
}

public static class ModeNames
{
    public static string Title(Mode mode) => Strings.Get(mode == Mode.Work ? "Mode_Work" : "Mode_Gaming");
}
