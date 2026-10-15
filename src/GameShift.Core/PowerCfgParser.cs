using System.Text.RegularExpressions;

namespace GameShift.Core;

public static partial class PowerCfgParser
{
    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidPattern();

    public static string? ParseActiveScheme(string output)
    {
        var m = GuidPattern().Match(output);
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    public static bool ListContains(string listOutput, string guid) =>
        listOutput.Contains(guid, StringComparison.OrdinalIgnoreCase);
}
