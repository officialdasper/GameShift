using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using GameShift.App.Localization;
using GameShift.Core;

namespace GameShift.App.ViewModels;

public sealed partial class MainViewModel
{
    public ObservableCollection<AppRow> Apps { get; } = [];
    public ObservableCollection<GameRow> Games { get; } = [];

    public RelayCommand AddRunningAppsCommand { get; private set; } = null!;
    public RelayCommand RemoveAppCommand { get; private set; } = null!;
    public RelayCommand FindGamesCommand { get; private set; } = null!;
    public RelayCommand AddGameFileCommand { get; private set; } = null!;
    public RelayCommand AddRunningGameCommand { get; private set; } = null!;
    public RelayCommand RemoveGameCommand { get; private set; } = null!;

    void InitLists()
    {
        var s = _host.Settings;
        foreach (var a in s.Apps) AddAppRow(a);
        foreach (var g in s.Games) Games.Add(ToRow(g));

        AddRunningAppsCommand = new RelayCommand(_ => AddRunningApps());
        RemoveAppCommand = new RelayCommand(p => RemoveApp((AppRow)p!));
        FindGamesCommand = new RelayCommand(_ => FindGames());
        AddGameFileCommand = new RelayCommand(_ => AddGameFromFile());
        AddRunningGameCommand = new RelayCommand(_ => AddRunningGame());
        RemoveGameCommand = new RelayCommand(p => RemoveGame((GameRow)p!));
    }

    void AddAppRow(AppEntry e)
    {
        var row = new AppRow(e);
        row.Changed += _ => SaveApps();
        Apps.Add(row);
    }

    public void RememberChoices(Func<IReadOnlyList<AppEntry>, List<AppEntry>> update)
    {
        List<AppEntry> apps;
        lock (_host.SettingsLock)
        {
            apps = update(_host.Settings.Apps);
            _host.Settings.Apps = apps;
        }
        _host.SaveSettings();
        Apps.Clear();
        foreach (var a in apps) AddAppRow(a);
    }

    void SaveApps() => Mutate(x => x.Apps = Apps.Select(a => a.ToEntry()).ToList());

    void Mutate(Action<Settings> change)
    {
        lock (_host.SettingsLock) change(_host.Settings);
        _host.SaveSettings();
    }

    void AddRunningApps()
    {
        var s = _host.Settings;
        var stop = new StopList(s.Games);
        var names = _host.Processes.List()
            .Select(p => p.ExeName)
            .Where(n => SettingsValidator.CanAddApp(s, stop, n) is null)
            .DistinctBy(ExeName.Normalize)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        foreach (var exe in PickProcessWindow.Pick(_host.Window, names, Loc.Get("Apps_AddTitle")))
            AddAppRow(new AppEntry(exe, Path.GetFileNameWithoutExtension(exe), AppRule.CloseAndRestore));
        SaveApps();
    }

    void RemoveApp(AppRow row)
    {
        Apps.Remove(row);
        SaveApps();
    }

    void AddGameFromFile()
    {
        var dlg = new OpenFileDialog { Filter = Loc.Get("Games_FilterExe"), Title = Loc.Get("Games_PickExeTitle") };
        if (dlg.ShowDialog(_host.Window) != true) return;
        var folder = Path.GetDirectoryName(dlg.FileName);
        AddGame(new GameEntry(Path.GetFileName(dlg.FileName), folder, SteamArt.FindAppId(folder)));
    }

    void AddRunningGame()
    {
        // Только имена процессов: к процессу игры не обращаемся даже за путём.
        var names = _host.Processes.List()
            .Select(p => p.ExeName)
            .Where(n => !StopList.IsBuiltInBlocked(n))
            .DistinctBy(ExeName.Normalize)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        foreach (var exe in PickProcessWindow.Pick(_host.Window, names, Loc.Get("Games_AddTitle")))
            AddGame(new GameEntry(exe, null));
    }

    void FindGames()
    {
        List<GameEntry> existing;
        lock (_host.SettingsLock) existing = _host.Settings.Games.ToList();
        var picked = FindGamesWindow.Pick(_host.Window, existing);
        if (picked.Count == 0) return;

        // Дубликаты пропускаются молча: окно уже отметило такие игры как добавленные.
        var fresh = picked.Where(g => SettingsValidator.CanAddGame(_host.Settings, g.ExeName) is null)
            .DistinctBy(g => ExeName.Normalize(g.ExeName))
            .ToList();
        if (fresh.Count == 0) return;
        Mutate(x => x.Games.AddRange(fresh));
        foreach (var g in fresh) Games.Add(ToRow(g));
        _ = RefreshArtAsync();
    }

    void AddGame(GameEntry game)
    {
        if (SettingsValidator.CanAddGame(_host.Settings, game.ExeName) is string error)
        {
            MessageBox.Show(error, "GameShift", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Mutate(x => x.Games.Add(game));
        Games.Add(ToRow(game));
        _ = RefreshArtAsync();
    }

    void RemoveGame(GameRow row)
    {
        Mutate(x =>
        {
            x.Games.RemoveAll(g => g.ExeName == row.ExeName);
        });
        Games.Remove(row);
        _ = RefreshArtAsync();
    }

    static GameRow ToRow(GameEntry g) => new() { ExeName = g.ExeName, Folder = g.Folder, SteamAppId = g.SteamAppId };
}
