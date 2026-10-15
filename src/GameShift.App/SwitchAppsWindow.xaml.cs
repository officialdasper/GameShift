using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameShift.App.Localization;
using GameShift.App.Theme;
using GameShift.App.ViewModels;
using GameShift.Core;

namespace GameShift.App;

/// <summary>Окна «Что закрыть перед игрой» и «Что открыть обратно». Не поверх всех: при автопереключении окно лишь мигает кнопкой на панели задач.</summary>
public partial class SwitchAppsWindow : Window
{
    readonly SwitchAppsViewModel _vm;
    readonly IProcessOps _processes;
    readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(1) };
    bool _refreshing;
    readonly TaskCompletionSource<bool> _choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
    AppCloseSession? _session;
    TimeSpan _grace;
    bool _finished;

    SwitchAppsWindow(SwitchAppsViewModel vm, IProcessOps processes)
    {
        InitializeComponent();
        BrandMark.Attach(this);
        DataContext = _vm = vm;
        _processes = processes;
        _refresh.Tick += async (_, _) => await RefreshRowsAsync();
        SourceInitialized += (_, _) => ThemeService.Instance?.ApplyTitleBar(this);
        Closing += OnWindowClosing;
        Closed += (_, _) =>
        {
            _finished = true;
            _refresh.Stop();
            _choice.TrySetResult(false);
            RunnerShift.BeginAnimation(TranslateTransform.XProperty, null);
        };
    }

    public SwitchAppsViewModel Model => _vm;

    /// <summary>true: пользователь подтвердил выбор; false: отмена или окно закрыто.</summary>
    public Task<bool> Choice => _choice.Task;

    public static async Task<SwitchAppsWindow> ForEnterAsync(EnterPlan plan, string? game, IProcessOps processes)
    {
        var infos = await Task.WhenAll(plan.Candidates.Select(c => AppIcons.GetAsync(c.Path)));
        var rows = plan.Candidates.Select((c, i) => new SwitchAppRow
        {
            ExeName = c.ExeName,
            FileName = c.FileName,
            Name = infos[i].Description ?? c.DisplayName,
            Path = c.Path,
            Icon = infos[i].Icon,
            Candidate = c,
            IsChecked = c.DefaultChecked,
        }).ToList();
        var kept = plan.KeptRunning.Count == 0 ? null : Loc.Format("Close_Kept", string.Join(", ", plan.KeptRunning));
        var subtitle = game is null ? Loc.Get("Close_Subtitle") : Loc.Format("Close_SubtitleGame", game);
        return new SwitchAppsWindow(new SwitchAppsViewModel(false, rows, Loc.Get("Close_Title"), subtitle, kept), processes);
    }

    public static async Task<SwitchAppsWindow> ForRestoreAsync(IReadOnlyList<ClosedApp> apps, IReadOnlyList<AppEntry> rules, IProcessOps processes)
    {
        var saved = new Dictionary<string, string>();
        foreach (var r in rules) saved[ExeName.Normalize(r.ExeName)] = r.DisplayName;
        var infos = await Task.WhenAll(apps.Select(a => AppIcons.GetAsync(a.Path)));
        var rows = apps.Select((a, i) =>
        {
            var key = ExeName.Normalize(a.ExeName);
            return new SwitchAppRow
            {
                ExeName = key,
                FileName = Path.GetFileName(a.Path),
                Name = infos[i].Description ?? saved.GetValueOrDefault(key) ?? Path.GetFileNameWithoutExtension(a.Path),
                Path = a.Path,
                Icon = infos[i].Icon,
                Closed = a,
                IsChecked = a.Restore,
            };
        }).ToList();
        return new SwitchAppsWindow(new SwitchAppsViewModel(true, rows, Loc.Get("Restore_Title"),
            Loc.Format("Restore_Subtitle", ModeNames.Title(Mode.Work)), null), processes);
    }

    public void ShowFor(Window main, bool askAttention)
    {
        if (main.IsVisible && main.WindowState != WindowState.Minimized)
        {
            Owner = main;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        Show();
        _refresh.Start();
        BringToFront(askAttention);
    }

    /// <summary>Раз в секунду сверяет список со свежим снимком имён и PID (процессы не открываются), убирает закрытые или снова открытые программы. Во время закрытия список ведёт сессия.</summary>
    async Task RefreshRowsAsync()
    {
        if (_finished || _refreshing || _vm.Phase != SwitchAppsPhase.Select || _vm.Rows.Count == 0) return;
        _refreshing = true;
        try
        {
            var live = await Task.Run(_processes.List);
            if (_finished || _vm.Phase != SwitchAppsPhase.Select) return; // пока читали, нажали «Закрыть»
            var gone = _vm.IsRestore
                ? LiveAppList.RunningAgain(_vm.Rows.Select(r => r.ExeName), live)
                : LiveAppList.GoneFromClose(_vm.Rows.Select(r => r.Candidate!), live);
            _vm.RemoveRows(gone.ToHashSet());
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>Активирует окно; если Windows не отдала фокус (например, идёт игра), мигает кнопкой на панели задач.</summary>
    public void BringToFront(bool askAttention = true)
    {
        if (_finished) return;
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        var hwnd = new WindowInteropHelper(this).Handle;
        if (askAttention && hwnd != IntPtr.Zero && GetForegroundWindow() != hwnd)
        {
            var info = new FlashInfo
            {
                Size = (uint)Marshal.SizeOf<FlashInfo>(),
                Hwnd = hwnd,
                Flags = FlashAll | FlashTimerNoForeground,
            };
            FlashWindowEx(ref info);
        }
    }

    public void Finish()
    {
        if (_finished) return;
        _finished = true;
        Close();
    }

    public IAppCloser CreateCloser(IProcessOps processes, TimeSpan grace) => new DialogCloser(this, processes, grace);

    void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!_vm.CanConfirm) return;
        if (_vm.IsRestore || _vm.Checked.Count == 0)
        {
            _choice.TrySetResult(true);
            Finish();
            return;
        }
        foreach (var r in _vm.Checked)
        {
            r.Update(AppCloseState.Pending, false);
            _vm.Progress.Add(r);
        }
        SetPhase(SwitchAppsPhase.Closing);
        _choice.TrySetResult(true);
    }

    void OnCancel(object sender, RoutedEventArgs e)
    {
        _choice.TrySetResult(false);
        Finish();
    }

    void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_finished) return;
        switch (_vm.Phase)
        {
            case SwitchAppsPhase.Closing:
                e.Cancel = true; // просьбы закрыться уже отправлены, ждём не дольше нескольких секунд
                return;
            case SwitchAppsPhase.Waiting:
                _session?.LeaveAll(); // крестик при ожидании: оставить оставшиеся открытыми
                break;
        }
        _finished = true;
        _choice.TrySetResult(false);
    }

    void SetPhase(SwitchAppsPhase phase)
    {
        _vm.Phase = phase;
        CancelButton.IsCancel = phase == SwitchAppsPhase.Select;
        ConfirmButton.IsDefault = phase == SwitchAppsPhase.Select;
        if (phase == SwitchAppsPhase.Closing) StartRunner();
        else RunnerShift.BeginAnimation(TranslateTransform.XProperty, null);
        Sync();
    }

    void Attach(AppCloseSession session, TimeSpan grace)
    {
        _session = session;
        _grace = grace;
        session.Changed += () => Dispatcher.BeginInvoke(Sync);
        Sync();
    }

    void Sync()
    {
        if (_session is null) return;
        foreach (var r in _vm.Progress) r.Update(_session.StateOf(r.ExeName), _vm.IsWaiting);
    }

    void EnterWaiting()
    {
        if (_finished || _session is null || _session.IsDone) return;
        SetPhase(SwitchAppsPhase.Waiting);
        BringToFront(); // пока закрывали, пользователь мог уйти в игру
    }

    async void OnWaitMore(object sender, RoutedEventArgs e)
    {
        if (_session is not { } session) return;
        try
        {
            SetPhase(SwitchAppsPhase.Closing);
            // Если программа держит открытым свой вопрос о сохранении, повторная просьба ничего не меняет.
            await Task.Run(() =>
            {
                session.RequestClose();
                session.WaitForExit(_grace);
            });
            EnterWaiting();
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
        }
    }

    async void OnKill(object sender, RoutedEventArgs e)
    {
        if (_session is not { } session || ((Button)sender).Tag is not SwitchAppRow row) return;
        try { await Task.Run(() => session.Kill(row.ExeName)); }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }

    async void OnKillAll(object sender, RoutedEventArgs e)
    {
        if (_session is not { } session) return;
        try { await Task.Run(session.KillAll); }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }

    void OnLeave(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is SwitchAppRow row) _session?.Leave(row.ExeName);
    }

    void OnLeaveAll(object sender, RoutedEventArgs e) => _session?.LeaveAll();

    void StartRunner()
    {
        if (!Motion.Enabled) return;
        RunnerShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-0.7, 0.7, new Duration(TimeSpan.FromMilliseconds(1100)))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        });
    }

    /// <summary>Работает в потоке контроллера: вежливо просит закрыться, ждёт, оставшееся отдаёт на решение пользователю.</summary>
    sealed class DialogCloser(SwitchAppsWindow window, IProcessOps processes, TimeSpan grace) : IAppCloser
    {
        public IReadOnlyDictionary<string, CloseOutcome> Close(IReadOnlyList<AppCandidate> apps, StopList stop)
        {
            var session = new AppCloseSession(processes, stop, apps);
            window.Dispatcher.Invoke(() => window.Attach(session, grace));
            session.RequestClose();
            if (!session.WaitForExit(grace))
            {
                window.Dispatcher.Invoke(window.EnterWaiting);
                while (!session.IsDone)
                {
                    Thread.Sleep(500);
                    session.Refresh();
                }
            }
            return session.Outcomes();
        }
    }

    const uint FlashAll = 3;
    const uint FlashTimerNoForeground = 12;

    [StructLayout(LayoutKind.Sequential)]
    struct FlashInfo
    {
        public uint Size;
        public IntPtr Hwnd;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool FlashWindowEx(ref FlashInfo info);
}
