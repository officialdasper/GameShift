namespace GameShift.Core.Tests;

public class JsonFileStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "wm-tests-" + Guid.NewGuid().ToString("N"));
    string FilePath => Path.Combine(_dir, "settings.json");

    [Fact]
    public void Load_MissingFile_ReturnsNull() =>
        Assert.Null(new JsonFileStore<Settings>(FilePath).Load());

    [Fact]
    public void SaveThenLoad_RoundTripsSettings()
    {
        var store = new JsonFileStore<Settings>(FilePath);
        var s = new Settings { AutoSwitchEnabled = false, MemoryPurgeEnabled = true };
        s.Apps.Add(new AppEntry("chrome.exe", "Chrome", AppRule.CloseAndRestore));
        store.Save(s);

        var loaded = store.Load()!;
        Assert.False(loaded.AutoSwitchEnabled);
        Assert.True(loaded.MemoryPurgeEnabled);
        Assert.Contains(loaded.Apps, a => a.ExeName == "chrome.exe" && a.Rule == AppRule.CloseAndRestore);
        Assert.Equal(4, loaded.Games.Count);
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsNullAndKeepsBackup()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ это не json");
        var store = new JsonFileStore<Settings>(FilePath);

        Assert.Null(store.Load());
        Assert.False(File.Exists(FilePath));
        Assert.True(File.Exists(FilePath + ".broken"));
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        var store = new JsonFileStore<Snapshot>(Path.Combine(_dir, "snapshot.json"));
        store.Save(new Snapshot { Mode = Mode.Gaming });
        store.Delete();
        Assert.Null(store.Load());
    }

    [Fact]
    public void Clone_IsDeepCopy()
    {
        var s = new Settings();
        var c = s.Clone();
        c.Apps.Add(new AppEntry("x.exe", "X", AppRule.CloseOnly));
        Assert.DoesNotContain(s.Apps, a => a.ExeName == "x.exe");
    }

    [Fact]
    public void Load_NullLists_BecomeEmpty_ExceptNetworkHelpers()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "Apps": null, "Games": null }""");

        var s = new JsonFileStore<Settings>(FilePath).Load()!;

        Assert.Equal(["winws.exe", "TgWsProxy_windows.exe"], s.Apps.Select(a => a.ExeName));
        Assert.Empty(s.Games);
    }

    [Fact]
    public void Load_SnapshotWithNullClosedApps_BecomesEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "Mode": "Gaming", "ClosedApps": null }""");

        var s = new JsonFileStore<Snapshot>(FilePath).Load()!;

        Assert.NotNull(s.ClosedApps);
        Assert.Empty(s.ClosedApps);
    }

    [Fact]
    public void Save_DoesNotWriteLegacyGameLists()
    {
        new JsonFileStore<Settings>(FilePath).Save(new Settings());
        var json = File.ReadAllText(FilePath);
        Assert.DoesNotContain("WardogsGames", json);
        Assert.DoesNotContain("GamingGames", json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
