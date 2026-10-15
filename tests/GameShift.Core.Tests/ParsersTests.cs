namespace GameShift.Core.Tests;

public class ParsersTests
{
    [Fact]
    public void ParseActiveScheme_ReadsGuidFromLocalizedOutput()
    {
        const string output = "GUID схемы питания: 8C5E7FDA-E8BF-4A96-9A85-A6E23A8C635C  (Высокая производительность)";
        Assert.Equal("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", PowerCfgParser.ParseActiveScheme(output));
    }

    [Fact]
    public void ParseActiveScheme_NoGuid_ReturnsNull() =>
        Assert.Null(PowerCfgParser.ParseActiveScheme("ошибка"));

    [Fact]
    public void ListContains_IgnoresCase() =>
        Assert.True(PowerCfgParser.ListContains("GUID: FBF94B8D-FA2C-49FA-A879-DAD3D86C3F39 (Макс)", Settings.MaxPerformanceScheme));

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" --tray --x=1", "--tray --x=1")]
    [InlineData("\"C:\\Program Files\\App\\app.exe\"", null)]
    [InlineData("C:\\Tools\\tool.exe -v", "-v")]
    [InlineData("C:\\Tools\\tool.exe", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void StripExecutable_ReturnsOnlyArguments(string? commandLine, string? expected) =>
        Assert.Equal(expected, CommandLineArgs.StripExecutable(commandLine));
}
