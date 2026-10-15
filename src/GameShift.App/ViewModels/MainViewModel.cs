using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GameShift.App.Localization;
using GameShift.Core;

namespace GameShift.App.ViewModels;

public sealed partial class MainViewModel : INotifyPropertyChanged
{
    readonly AppHost _host;

    public MainViewModel(AppHost host)
    {
        _host = host;
        SwitchCommand = new RelayCommand(p => _ = _host.SwitchAsync((Mode)p!, manual: true), _ => !IsBusy);
        InitLists();
        InitSettings();
        InitAppearance();
        InitLanguage();
        foreach (var t in ModeTiles) t.SummarySource = ModeSummaryLines;
        _ = RefreshWallpaperAsync();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    IReadOnlyList<string> ModeSummaryLines(Mode mode) =>
        ModeSummary.Keys(mode, _host.Settings, FocusSupported, NvidiaAvailable && _host.Nvidia.HasApplied)
            .Select(Loc.Get).ToList();

    async Task RefreshWallpaperAsync()
    {
        try
        {
            var art = await Wallpaper.LoadAsync();
            ModeTiles.Single(t => t.Mode == Mode.Work).SetArt(art?.Color, art?.Gray);
        }
        catch (Exception e)
        {
            ErrorLog.Write(e);
        }
    }

    // Событие приходит из системного потока; новые обои подхватываются на UI-потоке.
    void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category == Microsoft.Win32.UserPreferenceCategory.Desktop)
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => _ = RefreshWallpaperAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RelayCommand SwitchCommand { get; }

    public IReadOnlyList<ModeTile> ModeTiles { get; } =
    [
        new(Mode.Work, "", "Mode_WorkHint"),
        new(Mode.Gaming, "", "Mode_GamingHint"),
    ];

    public ObservableCollection<StepRow> Report { get; } = [];

    string _modeTitle = ModeNames.Title(Mode.Work);
    public string ModeTitle { get => _modeTitle; private set => Set(ref _modeTitle, value); }

    string _modeSubtitle = "";
    public string ModeSubtitle { get => _modeSubtitle; private set => Set(ref _modeSubtitle, value); }

    bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    public void SetSwitching(Mode? target)
    {
        IsBusy = target is not null;
        foreach (var t in ModeTiles) t.IsSwitching = t.Mode == target;
        SwitchCommand.RaiseCanExecuteChanged();
    }

    Mode _shownMode;
    string? _shownGame;

    public void ShowMode(Mode mode, string? game)
    {
        foreach (var t in ModeTiles) t.IsActive = t.Mode == mode;
        _shownMode = mode;
        _shownGame = game;
        UpdateModeTexts();
        _ = RefreshArtAsync();
    }

    void UpdateModeTexts()
    {
        ModeTitle = ModeNames.Title(_shownMode);
        ModeSubtitle = _shownMode == Mode.Work ? Loc.Get("Mode_SubtitleWork")
            : _shownGame is null ? Loc.Get("Mode_SubtitleManual")
            : Loc.Format("Mode_SubtitleGame", _shownGame);
    }

    int _artVersion;

    /// <summary>
    /// Обложка карточки «Игровой»: запущенная игра, если режим включился по ней,
    /// иначе первая игра списка, у которой есть картинка.
    /// </summary>
    async Task RefreshArtAsync()
    {
        var version = ++_artVersion;
        var running = _shownMode == Mode.Gaming ? _shownGame : null;
        var art = await FirstArtAsync(Games.ToList(), running);
        var gray = art is null ? null : await SteamArt.GrayAsync(art);
        if (version != _artVersion) return; // пока грузили, пришёл новый запрос
        ModeTiles.Single(t => t.Mode == Mode.Gaming).SetArt(art, gray);
    }

    static async Task<System.Windows.Media.ImageSource?> FirstArtAsync(List<GameRow> games, string? preferred)
    {
        var ordered = preferred is null
            ? games
            : games.OrderBy(g => ExeName.Normalize(g.ExeName) == ExeName.Normalize(preferred) ? 0 : 1).ToList();
        foreach (var g in ordered)
        {
            if (await SteamArt.GetAsync(g.SteamAppId, ArtKind.Hero) is { } art) return art;
        }
        return null;
    }

    public void ShowReport(SwitchReport report)
    {
        Report.Clear();
        var i = 0;
        foreach (var s in report.Steps) Report.Add(StepRow.From(s, i++));
    }

    /// <summary>Меняет одну строку отчёта (программы после окна возврата), остальные не проявляются заново.</summary>
    public void ReplaceStep(int index, StepResult step)
    {
        if (index < Report.Count) Report[index] = StepRow.From(step, index);
    }

    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
