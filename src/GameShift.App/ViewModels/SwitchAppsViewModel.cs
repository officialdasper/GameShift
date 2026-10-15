using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using GameShift.App.Localization;
using GameShift.Core;

namespace GameShift.App.ViewModels;

public enum SwitchAppsPhase { Select, Closing, Waiting }

public sealed class SwitchAppRow : INotifyPropertyChanged
{
    public required string ExeName { get; init; }
    public required string FileName { get; init; }
    public required string Name { get; init; }
    public string? Path { get; init; }
    public ImageSource? Icon { get; init; }
    public AppCandidate? Candidate { get; init; }
    public ClosedApp? Closed { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? CheckedChanged;

    bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            Raise();
            CheckedChanged?.Invoke();
        }
    }

    AppCloseState _state;
    bool _waitingPhase;

    public AppCloseState State => _state;

    public void Update(AppCloseState state, bool waitingPhase)
    {
        if (_state == state && _waitingPhase == waitingPhase) return;
        _state = state;
        _waitingPhase = waitingPhase;
        foreach (var n in new[] { nameof(State), nameof(StateText), nameof(IsClosed), nameof(IsFailed), nameof(IsLeft),
                     nameof(IsOpen), nameof(ShowActions) })
            Raise(n);
    }

    public bool IsClosed => _state is AppCloseState.Closed or AppCloseState.NotRunning;
    public bool IsFailed => _state == AppCloseState.Failed;
    public bool IsLeft => _state == AppCloseState.Left;
    public bool IsOpen => _state is AppCloseState.Pending or AppCloseState.Killing;
    public bool ShowActions => _state == AppCloseState.Pending && _waitingPhase;

    public string StateText => Loc.Get(_state switch
    {
        AppCloseState.Pending => _waitingPhase ? "Close_StateWaiting" : "Close_StateClosing",
        AppCloseState.Killing => "Close_StateKilling",
        AppCloseState.Left => "Close_StateLeft",
        AppCloseState.Failed => "Close_StateFailed",
        _ => "Close_StateClosed",
    });

    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class SwitchAppsViewModel : INotifyPropertyChanged
{
    public bool IsRestore { get; }
    public ObservableCollection<SwitchAppRow> Rows { get; }
    public ObservableCollection<SwitchAppRow> Progress { get; } = [];
    public string? KeptText { get; }
    public bool ShowKept => IsSelect && KeptText is not null;
    public string RememberHint => Loc.Get(IsRestore ? "Restore_RememberHint" : "Close_RememberHint");
    public string CancelText => Loc.Get(IsRestore ? "Restore_Skip" : "Common_Cancel");

    readonly string _selectTitle;
    readonly string _selectSubtitle;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SwitchAppsViewModel(bool isRestore, IEnumerable<SwitchAppRow> rows, string title, string subtitle, string? keptText)
    {
        IsRestore = isRestore;
        _selectTitle = title;
        _selectSubtitle = subtitle;
        KeptText = keptText;
        Rows = new(rows.OrderByDescending(r => r.IsChecked).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase));
        foreach (var r in Rows) r.CheckedChanged += OnRowChecked;
    }

    SwitchAppsPhase _phase;
    public SwitchAppsPhase Phase
    {
        get => _phase;
        set
        {
            if (_phase == value) return;
            _phase = value;
            foreach (var n in new[] { nameof(Phase), nameof(IsSelect), nameof(IsClosing), nameof(IsWaiting), nameof(IsProgress), nameof(ShowKept),
                         nameof(Title), nameof(Subtitle) })
                Raise(n);
        }
    }

    public bool IsSelect => _phase == SwitchAppsPhase.Select;
    public bool IsClosing => _phase == SwitchAppsPhase.Closing;
    public bool IsWaiting => _phase == SwitchAppsPhase.Waiting;
    public bool IsProgress => _phase != SwitchAppsPhase.Select;

    public string Title => _phase switch
    {
        SwitchAppsPhase.Closing => Loc.Get("Close_Closing"),
        SwitchAppsPhase.Waiting => Loc.Get("Close_WaitTitle"),
        _ => _selectTitle,
    };

    public string Subtitle => _phase switch
    {
        SwitchAppsPhase.Select => _selectSubtitle,
        _ => Loc.Get("Close_WaitHint"),
    };

    int CheckedCount => Rows.Count(r => r.IsChecked);

    public bool HasRows => Rows.Count > 0;
    public bool IsEmpty => Rows.Count == 0;
    public string EmptyText => Loc.Get(IsRestore ? "Restore_Empty" : "Close_Empty");

    public void RemoveRows(IReadOnlyCollection<string> exeNames)
    {
        var gone = Rows.Where(r => exeNames.Contains(r.ExeName)).ToList();
        if (gone.Count == 0) return;
        foreach (var r in gone)
        {
            r.CheckedChanged -= OnRowChecked;
            Rows.Remove(r);
        }
        foreach (var n in new[] { nameof(HasRows), nameof(IsEmpty) }) Raise(n);
        OnRowChecked();
    }

    /// <summary>Для «Отметить все»: true, false или null (часть). Щелчок по частичному отмечает все.</summary>
    public bool? AllChecked
    {
        get => CheckedCount == 0 ? false : CheckedCount == Rows.Count ? true : null;
        set
        {
            var check = value != false || AllChecked is null;
            foreach (var r in Rows) r.IsChecked = check;
        }
    }

    public string CountText => Loc.Format("Close_Count", CheckedCount, Rows.Count);

    public string ConfirmText => IsRestore ? Loc.Get("Restore_Confirm")
        : Loc.Get(CheckedCount == 0 ? "Close_ConfirmNone" : "Close_Confirm");

    /// <summary>«Открыть выбранные» без отметок ничего бы не сделала: для этого есть «Не открывать ничего».</summary>
    public bool CanConfirm => !IsRestore || CheckedCount > 0;

    bool _remember;
    public bool Remember { get => _remember; set { _remember = value; Raise(); } }

    public IReadOnlyList<SwitchAppRow> Checked => Rows.Where(r => r.IsChecked).ToList();

    public IReadOnlyList<AppChoice> Choices => Rows.Select(r => new AppChoice(r.FileName, r.Name, r.IsChecked)).ToList();

    void OnRowChecked()
    {
        foreach (var n in new[] { nameof(AllChecked), nameof(CountText), nameof(ConfirmText), nameof(CanConfirm) }) Raise(n);
    }

    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
