namespace GameShift.Core.Tests;

public class SettingsValidatorTests
{
    [Fact]
    public void CanAddGame_RejectsDuplicates()
    {
        var s = new Settings();
        Assert.NotNull(SettingsValidator.CanAddGame(s, "CS2.exe"));
        Assert.NotNull(SettingsValidator.CanAddGame(s, "WardogsClient-Win64-Shipping"));
        Assert.Null(SettingsValidator.CanAddGame(s, "r5apex.exe"));
    }

    [Fact]
    public void CanAddApp_RejectsProtectedAndDuplicates()
    {
        var s = new Settings();
        var stop = new StopList(s.Games, @"C:\Windows");
        Assert.NotNull(SettingsValidator.CanAddApp(s, stop, "vgc.exe"));
        Assert.NotNull(SettingsValidator.CanAddApp(s, stop, "cs2.exe"));
        Assert.NotNull(SettingsValidator.CanAddApp(s, stop, "discord")); // уже в списке по умолчанию
        Assert.Null(SettingsValidator.CanAddApp(s, stop, "chrome.exe"));
    }
}
