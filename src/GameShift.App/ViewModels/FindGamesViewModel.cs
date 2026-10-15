using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using Microsoft.Win32;
using GameShift.App.Localization;
using GameShift.Core;
using GameShift.Platform;

namespace GameShift.App.ViewModels;

public sealed class ScanRow : INotifyPropertyChanged
{
    string? _exeName;
    string? _folder;
    int? _steamAppId;
    bool _checked;
    ImageSource? _icon;
    bool _iconRequested;
    ImageSource? _art;
    bool _artRequested;

    public ScanRow(ScanResult r)
    {
        Name = r.Game.Name;
        Launcher = r.Game.Launcher;
        _exeName = r.Game.ExeName;
        _folder = r.Game.Folder;
        _steamAppId = r.Game.SteamAppId;
        AlreadyAdded = r.AlreadyAdded;
        _checked = r.AlreadyAdded || _exeName is not null;
    }

    public string Name { get; }
    public string Launcher { get; }
    public bool AlreadyAdded { get; }
    public string? ExeName => _exeName;
    public string? Folder => _folder;
    public int? SteamAppId => _steamAppId;

    public string Details => $"{Launcher}  ·  {_exeName ?? Loc.Get("Scan_NoExe")}";
    public string FolderText => _folder ?? "";

    public bool CanToggle => !AlreadyAdded && _exeName is not null;

    public bool IsChecked
    {
        get => _checked;
        set
        {
            if (!CanToggle || _checked == value) return;
            _checked = value;
            Raise(nameof(IsChecked));
            Changed?.Invoke();
        }
    }

    public bool IsSelected => !AlreadyAdded && _checked && _exeName is not null;

    public event Action? Changed;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageSource? Icon
    {
        get
        {
            if (!_iconRequested)
            {
                _iconRequested = true;
                _ = LoadIconAsync();
            }
            return _icon;
        }
    }

    public ImageSource? Art
    {
        get
        {
            if (!_artRequested)
            {
                _artRequested = true;
                _ = LoadArtAsync();
            }
            return _art;
        }
    }

    public void PickExe(System.Windows.Window owner)
    {
        var dlg = new OpenFileDialog { Filter = Loc.Get("Games_FilterExe"), Title = Loc.Get("Games_PickExeTitle") };
        if (_folder is not null && Directory.Exists(_folder)) dlg.InitialDirectory = _folder;
        if (dlg.ShowDialog(owner) != true) return;

        var dir = Path.GetDirectoryName(dlg.FileName);
        var inside = _folder is not null && dir is not null
            && Path.GetFullPath(dir).StartsWith(Path.GetFullPath(_folder), StringComparison.OrdinalIgnoreCase);
        if (!inside)
        {
            _folder = dir;
            _steamAppId = SteamArt.FindAppId(dir);
            _artRequested = false;
            Raise(nameof(Art));
        }
        _exeName = Path.GetFileName(dlg.FileName);
        _iconRequested = false;
        _icon = null;
        if (!AlreadyAdded) _checked = true;
        Raise(nameof(ExeName));
        Raise(nameof(Details));
        Raise(nameof(FolderText));
        Raise(nameof(CanToggle));
        Raise(nameof(IsChecked));
        Raise(nameof(Icon));
        Changed?.Invoke();
    }

    async Task LoadArtAsync()
    {
        var art = await SteamArt.GetAsync(_steamAppId, ArtKind.Header);
        if (art is null) return;
        _art = art;
        Raise(nameof(Art));
    }

    async Task LoadIconAsync()
    {
        var icon = _exeName is null ? null : await GameIcons.GetAsync(_folder, _exeName);
        if (icon is null) return;
        _icon = icon;
        Raise(nameof(Icon));
    }

    void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class FindGamesViewModel : INotifyPropertyChanged
{
    bool _scanning = true;

    public ObservableCollection<ScanRow> Rows { get; } = [];

    public bool IsScanning
    {
        get => _scanning;
        private set
        {
            if (_scanning == value) return;
            _scanning = value;
            Raise(nameof(IsScanning));
            Raise(nameof(IsEmpty));
        }
    }

    public bool IsEmpty => !_scanning && Rows.Count == 0;

    public int SelectedCount => Rows.Count(r => r.IsSelected);
    public bool CanAdd => SelectedCount > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RunAsync(IReadOnlyList<GameEntry> existing, CancellationToken ct)
    {
        IReadOnlyList<ScanResult> results;
        try
        {
            results = await Task.Run(() => GameMerge.Merge(GameScanner.Scan(ct), existing), ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        foreach (var r in results)
        {
            var row = new ScanRow(r);
            row.Changed += RaiseSelection;
            Rows.Add(row);
        }
        IsScanning = false;
        RaiseSelection();
    }

    public List<GameEntry> Selected() =>
        Rows.Where(r => r.IsSelected).Select(r => new GameEntry(r.ExeName!, r.Folder, r.SteamAppId)).ToList();

    void RaiseSelection()
    {
        Raise(nameof(SelectedCount));
        Raise(nameof(CanAdd));
    }

    void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
