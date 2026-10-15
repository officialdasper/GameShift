namespace GameShift.Core.Tests;

public class ExeNameTests
{
    [Theory]
    [InlineData("Discord.exe", "discord")]
    [InlineData("discord", "discord")]
    [InlineData("WardogsClient-Win64-Shipping", "wardogsclient-win64-shipping")]
    [InlineData("Riot Client", "riot client")]
    [InlineData("Riot Client.exe", "riot client")]
    [InlineData(@"C:\Games\cs2.EXE", "cs2")]
    [InlineData("  Telegram.exe ", "telegram")]
    [InlineData("driver.service.exe", "driver.service")]
    public void Normalize_StripsPathExtensionAndCase(string input, string expected) =>
        Assert.Equal(expected, ExeName.Normalize(input));
}
