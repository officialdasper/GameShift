namespace GameShift.Core.Tests;

public class StopListTests
{
    static StopList Make() => new(new Settings().Games, windowsFolder: @"C:\Windows");

    [Theory]
    [InlineData("WardogsClient-Win64-Shipping.exe")]
    [InlineData("wardogsclient-win64-shipping")]
    [InlineData("cs2.exe")]
    [InlineData("VALORANT-Win64-Shipping")]
    [InlineData("Elytra-Setup.exe")]
    [InlineData("WardogsLauncher-Shipping.exe")]
    [InlineData("vgc.exe")]
    [InlineData("vgtray")]
    [InlineData("RiotClientServices.exe")]
    [InlineData("Riot Client")]
    [InlineData("FACEIT.exe")]
    [InlineData("faceitservice.exe")]
    [InlineData("EasyAntiCheat_EOS.exe")]
    [InlineData("BEService_x64.exe")]
    [InlineData("SGuard64.exe")]
    [InlineData("ACE-Tray.exe")]
    [InlineData("steamwebhelper")]
    [InlineData("EpicGamesLauncher.exe")]
    [InlineData("EpicOnlineServicesHost.exe")]
    [InlineData("Battle.net.exe")]
    [InlineData("EADesktop.exe")]
    [InlineData("upc.exe")]
    [InlineData("GalaxyClient.exe")]
    [InlineData("iyx_ast_embed.exe")]
    [InlineData("driver.service.exe")]
    [InlineData("NVDisplay.Container.exe")]
    [InlineData("explorer.exe")]
    [InlineData("svchost")]
    [InlineData("GameShift.exe")]
    [InlineData("winws.exe")]
    [InlineData("WINWS")]
    [InlineData("zapret.exe")]
    [InlineData("Zapret-Service")]
    [InlineData("TgWsProxy_windows.exe")]
    [InlineData("tgwsproxy_windows")]
    [InlineData("goodbyedpi.exe")]
    [InlineData("GoodbyeDPI")]
    public void IsBlockedName_BlocksProtectedProcesses(string name) =>
        Assert.True(Make().IsBlockedName(name));

    [Theory]
    [InlineData("chrome.exe")]
    [InlineData("Telegram")]
    [InlineData("Docker Desktop.exe")]
    [InlineData("acer.exe")]
    [InlineData("steamy.exe")]
    public void IsBlockedName_AllowsRegularApps(string name) =>
        Assert.False(Make().IsBlockedName(name));

    [Theory]
    [InlineData(@"C:\Windows\System32\notepad.exe")]
    [InlineData(@"c:\windows\explorer.exe")]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\Wardogs\Engine\Binaries\Win64\CrashReportClient.exe")]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\Delta Force\Game\x.exe")]
    public void IsBlockedPath_BlocksWindowsAndGameFolders(string path) =>
        Assert.True(Make().IsBlockedPath(path));

    [Theory]
    [InlineData(@"C:\Program Files\Google\Chrome\Application\chrome.exe")]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\WardogsExtra\x.exe")]
    public void IsBlockedPath_AllowsOtherFolders(string path) =>
        Assert.False(Make().IsBlockedPath(path));

    [Fact]
    public void IsBuiltInBlocked_DoesNotDependOnGameLists()
    {
        Assert.True(StopList.IsBuiltInBlocked("vgc.exe"));
        Assert.False(StopList.IsBuiltInBlocked("cs2.exe"));
        Assert.True(StopList.IsBuiltInBlocked("TgWsProxy_windows.exe"));
    }
}
