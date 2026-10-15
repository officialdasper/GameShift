using GameShift.App.Localization;
using GameShift.Core;

namespace GameShift.App.ViewModels;

public sealed class StepRow
{
    public required string Step { get; init; }
    public required string Status { get; init; }
    public required string Detail { get; init; }
    public bool IsFailed { get; init; }
    public bool IsSkipped { get; init; }

    public int Index { get; init; }

    public static StepRow From(StepResult r, int index = 0) => new()
    {
        Index = index,
        Step = r.Step,
        Detail = r.Detail,
        IsFailed = r.Status == StepStatus.Failed,
        IsSkipped = r.Status == StepStatus.Skipped,
        Status = Loc.Get(r.Status switch
        {
            StepStatus.Done => "Status_Done",
            StepStatus.Failed => "Status_Failed",
            _ => "Status_Skipped",
        }),
    };
}

public sealed class AppRow : System.ComponentModel.INotifyPropertyChanged
{
    static int _counter;
    AppRule _rule;

    public AppRow(AppEntry e)
    {
        ExeName = e.ExeName;
        DisplayName = e.DisplayName;
        _rule = e.Rule;
        Group = "app" + Interlocked.Increment(ref _counter);
    }

    public string ExeName { get; }
    public string DisplayName { get; }
    public string Group { get; }
    public AppRule Rule => _rule;
    public event Action<AppRow>? Changed;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public bool IsCloseAndRestore { get => _rule == AppRule.CloseAndRestore; set { if (value) SetRule(AppRule.CloseAndRestore); } }
    public bool IsCloseOnly { get => _rule == AppRule.CloseOnly; set { if (value) SetRule(AppRule.CloseOnly); } }
    public bool IsKeepRunning { get => _rule == AppRule.KeepRunning; set { if (value) SetRule(AppRule.KeepRunning); } }

    public AppEntry ToEntry() => new(ExeName, DisplayName, _rule);

    void SetRule(AppRule rule)
    {
        if (_rule == rule) return;
        _rule = rule;
        foreach (var n in new[] { nameof(IsCloseAndRestore), nameof(IsCloseOnly), nameof(IsKeepRunning) })
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
        Changed?.Invoke(this);
    }
}

public sealed class GameRow : System.ComponentModel.INotifyPropertyChanged
{
    System.Windows.Media.ImageSource? _icon;
    bool _iconRequested;

    System.Windows.Media.ImageSource? _art;
    bool _artRequested;

    public required string ExeName { get; init; }
    public string? Folder { get; init; }
    public int? SteamAppId { get; init; }
    public string FolderText => Folder ?? Loc.Get("Games_FolderNotSet");
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public void RefreshText() =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(FolderText)));

    public System.Windows.Media.ImageSource? Icon
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

    public System.Windows.Media.ImageSource? Art
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

    async Task LoadArtAsync()
    {
        var art = await SteamArt.GetAsync(SteamAppId, ArtKind.Header);
        if (art is null) return;
        _art = art;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Art)));
    }

    async Task LoadIconAsync()
    {
        var icon = await GameIcons.GetAsync(Folder, ExeName);
        if (icon is null) return;
        _icon = icon;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Icon)));
    }
}

public sealed class ModeTile(Mode mode, string glyph, string hintKey) : System.ComponentModel.INotifyPropertyChanged
{
    bool _isActive;
    bool _isSwitching;
    System.Windows.Media.ImageSource? _art;

    public Mode Mode { get; } = mode;
    public string Title => ModeNames.Title(Mode);
    public string Glyph { get; } = glyph;
    public string Hint => Loc.Get(hintKey);
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public void RefreshText()
    {
        Raise(nameof(Title));
        Raise(nameof(Hint));
        RefreshSummary();
    }

    public bool IsWallpaper => Mode == Mode.Work;

    public Func<Mode, IReadOnlyList<string>>? SummarySource { get; set; }

    public IReadOnlyList<string> Summary => SummarySource?.Invoke(Mode) ?? [];

    public string SummaryText => string.Join(Environment.NewLine, Summary);

    public void RefreshSummary()
    {
        Raise(nameof(Summary));
        Raise(nameof(SummaryText));
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            Raise(nameof(IsActive));
        }
    }

    public bool IsSwitching
    {
        get => _isSwitching;
        set
        {
            if (_isSwitching == value) return;
            _isSwitching = value;
            Raise(nameof(IsSwitching));
        }
    }

    public System.Windows.Media.ImageSource? Art => _art;

    public System.Windows.Media.ImageSource? ArtGray { get; private set; }

    public bool HasArt => _art is not null;

    public void SetArt(System.Windows.Media.ImageSource? art, System.Windows.Media.ImageSource? gray)
    {
        if (ReferenceEquals(_art, art)) return;
        _art = art;
        ArtGray = gray;
        Raise(nameof(Art));
        Raise(nameof(ArtGray));
        Raise(nameof(HasArt));
    }

    void Raise(string name) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public sealed class AccentOption(string nameKey, string value, System.Windows.Media.Color color, Action<string> select)
    : System.ComponentModel.INotifyPropertyChanged
{
    public string Name => Loc.Get(nameKey);
    public string Value { get; } = value;
    public System.Windows.Media.Brush Brush { get; } = Frozen(color);
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    bool _isSelected;

    public bool IsChecked
    {
        get => _isSelected;
        set { if (value && !_isSelected) select(Value); }
    }

    public void RefreshText() =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Name)));

    public void Refresh(string current)
    {
        _isSelected = string.Equals(current, Value, StringComparison.OrdinalIgnoreCase);
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsChecked)));
    }

    static System.Windows.Media.SolidColorBrush Frozen(System.Windows.Media.Color c)
    {
        var b = new System.Windows.Media.SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}

public sealed class LanguageOption(string code, string? nativeName) : System.ComponentModel.INotifyPropertyChanged
{
    public string Code { get; } = code;
    public string Name => nativeName ?? Loc.Get("Common_System");
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public void RefreshText() =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Name)));
}
