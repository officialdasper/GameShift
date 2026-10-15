using System.Text.Json.Serialization;

namespace GameShift.Core;

public sealed record ClosedApp(string ExeName, string Path, string? Arguments, bool Restore);

/// <summary>Состояние «до игры». Пишется на диск до первого изменения.</summary>
public sealed class Snapshot : IJsonOnDeserialized
{
    public Mode Mode { get; set; }
    public string? PowerScheme { get; set; }
    public bool? ToastsEnabled { get; set; }
    public List<ClosedApp> ClosedApps { get; set; } = [];

    void IJsonOnDeserialized.OnDeserialized()
    {
        ClosedApps ??= [];
        Mode = Modes.Normalize(Mode); // снимок версии с отдельным режимом WARDOGS
    }
}
