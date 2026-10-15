using System.IO;
using System.Windows;
using System.Windows.Threading;
using GameShift.App.Localization;
using GameShift.App.Theme;
using GameShift.App.Tray;
using GameShift.App.ViewModels;
using GameShift.Core;
using GameShift.Platform;

namespace GameShift.App;

public sealed class AppHost : IDisposable
{
    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameShift");
    public static string ExePath => Environment.ProcessPath!;

    public readonly ThemeService Theme = new();
    public readonly TrayIcon Tray = new();
    public readonly MainWindow Window = new();
    public readonly MainViewModel Vm;
    public readonly IStore<Settings> SettingsStore = new JsonFileStore<Settings>(Path.Combine(DataDir, "settings.json"));
    public readonly ProcessOps Processes = new();
    public readonly StandbyPurger Purger = new(ExePath);
    public readonly NvidiaProfiles Nvidia = new(new JsonFileStore<NvidiaBackup>(Path.Combine(DataDir, "nvidia-backup.json")));
    public readonly ModeController Controller;
    public Settings Settings { get; }

    readonly GameWatcher _watcher = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly SemaphoreSlim _gate = new(1, 1);
    static readonly TimeSpan AutoRetryPause = TimeSpan.FromSeconds(30);
    DateTime _autoRetryAfter;
    static readonly TimeSpan CloseGrace = TimeSpan.FromSeconds(5);
    SwitchAppsWindow? _enterWindow;
    SwitchAppsWindow? _restoreWindow;
    Task? _restoreTask;
    SwitchReport? _lastReport;
    int _switchNumber;
    readonly object _settingsLock = new();

    /// <summary>Под этой блокировкой UI меняет Settings, а контроллер берёт клон.</summary>
    public object SettingsLock => _settingsLock;

    public AppHost(bool startHidden)
    {
        Settings = SettingsStore.Load() ?? new Settings();
        Loc.Instance.Apply(Settings.Language);
        Tray.ApplyLanguage();
        Controller = new ModeController(CurrentSettings, new PowerCfg(), new ToastSwitch(),
            Purger, Processes, new JsonFileStore<Snapshot>(Path.Combine(DataDir, "snapshot.json")));
        Vm = new MainViewModel(this);
        Window.DataContext = Vm;

        Theme.BackdropWindow = Window;
        Theme.Apply(Settings.Theme, Settings.Accent, Settings.Backdrop);
        BrandMark.Follow(Theme);
        Theme.Changed += () => Tray.Update(Controller.Current, _watcher.ActiveGame, Theme.Accent);
        Loc.Instance.Changed += () =>
        {
            Tray.ApplyLanguage();
            Tray.Update(Controller.Current, _watcher.ActiveGame, Theme.Accent);
        };
        Window.SourceInitialized += (_, _) => Theme.ApplyTitleBar(Window);
        Window.IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) Theme.ApplyTitleBar(Window); // окно из трея: фон и заголовок заново
        };
        Tray.OpenRequested += ShowWindow;
        Tray.ExitRequested += Exit;
        Tray.ModeRequested += m =>
        {
            if (_gate.CurrentCount == 0) Tray.Notify("GameShift", Loc.Get("Tray_SwitchBusy"));
            else _ = SwitchAsync(m, manual: true);
        };

        Vm.ShowMode(Controller.Current, null);
        Tray.Update(Controller.Current, null, Theme.Accent);
        if (!startHidden) ShowWindow();

        _timer.Tick += async (_, _) =>
        {
            try { await OnTimerAsync(); }
            catch (Exception e) { ErrorLog.Write(e); }
        };
        _timer.Start();
        _ = OfferRecoveryAsync();
    }

    public Settings CurrentSettings()
    {
        lock (_settingsLock) return Settings.Clone();
    }

    public void SaveSettings()
    {
        lock (_settingsLock) SettingsStore.Save(Settings);
    }

    public void SetAppearance(ThemeMode theme, string accent, BackdropMode backdrop)
    {
        lock (_settingsLock)
        {
            Settings.Theme = theme;
            Settings.Accent = accent;
            Settings.Backdrop = backdrop;
        }
        SaveSettings();
        // Вызов идёт из setter'а привязки: исключение там WPF проглотит молча, поэтому пишем его в журнал.
        try { Theme.Apply(theme, accent, backdrop); }
        catch (Exception e) { ErrorLog.Write(e); }
    }

    public void SetLanguage(string setting)
    {
        lock (_settingsLock) Settings.Language = setting;
        SaveSettings();
        Loc.Instance.Apply(setting);
    }

    public async Task SwitchAsync(Mode target, bool manual)
    {
        // Щелчок по уже включённому режиму ничего не меняет и не затирает отчёт о прошлом переключении.
        if (target == Controller.Current) return;
        if (!await _gate.WaitAsync(0)) return;
        var game = _watcher.ActiveGame;
        try
        {
            // Новое переключение отменяет открытое окно «Что открыть обратно»: ничего не открываем.
            _restoreWindow?.Finish();
            _switchNumber++;
            Vm.SetSwitching(target);
            if (manual)
            {
                var names = await Task.Run(() => Processes.List().Select(p => p.ExeName).ToList());
                _watcher.NotifyManualSwitch(names);
            }

            SwitchReport? report;
            try
            {
                report = target == Mode.Work ? await ReturnAsync(manual) : await EnterAsync(target, manual, game);
            }
            catch (Exception e)
            {
                ErrorLog.Write(e);
                report = new SwitchReport(Controller.Current, target,
                    [new StepResult(StepNames.Mode, StepStatus.Failed, Loc.Format("Err_SwitchFailed", e.Message))]);
            }
            // Отмена в окне выбора: ничего не менялось. Игра остаётся «увиденной», поэтому окно
            // не всплывёт снова, пока её не перезапустят.
            if (report is null) return;
            if (!manual && Controller.Current != target)
            {
                _watcher.Reset(); // следующий тик попробует ещё раз, но не раньше чем через паузу
                _autoRetryAfter = DateTime.Now + AutoRetryPause;
            }

            _lastReport = report;
            Vm.ShowReport(report);
            Vm.ShowMode(Controller.Current, _watcher.ActiveGame);
            Tray.Update(Controller.Current, _watcher.ActiveGame, Theme.Accent);
            if (!manual && (target == Mode.Work || report.HasFailures))
                Tray.Notify(Loc.Format("Tray_ModeTitle", ModeNames.Title(target)),
                    Loc.Get(report.HasFailures ? "Tray_StepsFailed" : "Tray_GameClosed"));
        }
        finally
        {
            Vm.SetSwitching(null);
            _gate.Release();
        }
    }

    async Task<SwitchReport?> EnterAsync(Mode target, bool manual, string? game)
    {
        var plan = await Task.Run(() => Controller.PrepareEnter(target));
        if (plan.AlreadyOn) return await Task.Run(() => Controller.SwitchTo(target));
        if (!manual)
        {
            // До переключения: после шага «Не беспокоить» Windows уведомление уже не покажет.
            Tray.Notify(Loc.Format("Tray_ModeTitle", ModeNames.Title(target)),
                Loc.Format(plan.Candidates.Count > 0 ? "Tray_ChooseApps" : "Tray_GameStarted", game));
        }
        if (plan.Candidates.Count == 0)
            return await Task.Run(() => Controller.ApplyEnter(plan, [], new GracefulCloser(Processes, CloseGrace)));

        var window = await SwitchAppsWindow.ForEnterAsync(plan, manual ? null : game, Processes);
        _enterWindow = window;
        try
        {
            window.ShowFor(Window, askAttention: !manual);
            if (!await window.Choice) return null;
            if (window.Model.Remember) Vm.RememberChoices(apps => AppChoices.RememberEnter(apps, window.Model.Choices));
            var selected = window.Model.Checked.Select(r => r.ExeName).ToList();
            var closer = window.CreateCloser(Processes, CloseGrace);
            return await Task.Run(() => Controller.ApplyEnter(plan, selected, closer));
        }
        finally
        {
            window.Finish();
            _enterWindow = null;
        }
    }

    /// <summary>
    /// Возврат: уведомления и питание сразу, программы потом по выбору в окне «Что открыть обратно».
    /// Окно не держит переключатель: пока оно открыто, автопереключение работает как обычно.
    /// </summary>
    async Task<SwitchReport> ReturnAsync(bool manual)
    {
        var back = await Task.Run(Controller.ReturnToWork);
        var report = back.Report;
        if (Controller.Current != Mode.Work) return report; // снимок не прочитан: ничего не вернули
        if (back.ToRestore.Count == 0)
            return report with { Steps = [Controller.RestartApps([]), .. report.Steps] };

        report = report with { Steps = [new StepResult(StepNames.Apps, StepStatus.Skipped, Loc.Get("Ctl_WaitingChoice")), .. report.Steps] };
        _restoreTask = OfferRestoreAsync(back.ToRestore, report, askAttention: !manual);
        return report;
    }

    async Task OfferRestoreAsync(IReadOnlyList<ClosedApp> apps, SwitchReport report, bool askAttention)
    {
        try
        {
            var number = _switchNumber;
            await Task.Yield(); // окно появится, когда переключение уже завершится
            List<AppEntry> rules;
            lock (_settingsLock) rules = Settings.Apps.ToList();
            var window = await SwitchAppsWindow.ForRestoreAsync(apps, rules, Processes);
            if (number != _switchNumber) return; // пока готовили окно, началось новое переключение
            _restoreWindow = window;
            window.ShowFor(Window, askAttention);
            var confirmed = await window.Choice;
            if (_restoreWindow == window) _restoreWindow = null;
            if (confirmed && window.Model.Remember)
                Vm.RememberChoices(list => AppChoices.RememberRestore(list, window.Model.Choices));

            var chosen = confirmed ? window.Model.Checked.Select(r => r.Closed!).ToList() : [];
            var step = chosen.Count == 0
                ? new StepResult(StepNames.Apps, StepStatus.Skipped, Loc.Get("Ctl_NoneReopened"))
                : await Task.Run(() => Controller.RestartApps(chosen));
            if (ReferenceEquals(_lastReport, report))
            {
                _lastReport = report with { Steps = report.Steps.Select(x => x.Step == StepNames.Apps ? step : x).ToList() };
                Vm.ReplaceStep(0, step);
            }
        }
        catch (Exception e)
        {
            ErrorLog.Write(e);
        }
    }

    async Task OnTimerAsync()
    {
        if (_gate.CurrentCount == 0 || DateTime.Now < _autoRetryAfter) return;
        var s = CurrentSettings();
        if (!s.AutoSwitchEnabled) return;
        var names = await Task.Run(() => Processes.List().Select(p => p.ExeName).ToList());
        if (_gate.CurrentCount == 0) return;
        if (_watcher.Tick(names, s, Controller.Current, DateTime.Now) is Mode target)
            await SwitchAsync(target, manual: false);
    }

    async Task OfferRecoveryAsync()
    {
        if (Controller.Current == Mode.Work) return;
        var s = CurrentSettings();
        var games = s.Games.Select(g => ExeName.Normalize(g.ExeName)).ToHashSet();
        var running = await Task.Run(() => Processes.List().Select(p => ExeName.Normalize(p.ExeName)).ToList());
        if (running.Any(games.Contains)) return;
        var answer = MessageBox.Show(Loc.Get("Dlg_Recover"),
            "GameShift", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) await SwitchAsync(Mode.Work, manual: true);
    }

    public void ShowWindow()
    {
        Window.Show();
        if (Window.WindowState == WindowState.Minimized) Window.WindowState = WindowState.Normal;
        Window.Activate();
        (_enterWindow ?? _restoreWindow)?.BringToFront(askAttention: false);
    }

    async void Exit()
    {
        if ((_enterWindow ?? _restoreWindow) is { } open)
        {
            open.BringToFront(askAttention: false);
            return;
        }
        _timer.Stop(); // автопереключение не должно вклиниться, пока мы спрашиваем
        try
        {
            // Если идёт переключение, дожидаемся его, чтобы не оборвать на середине.
            await _gate.WaitAsync();
            _gate.Release();
            if (Controller.Current != Mode.Work)
            {
                var answer = MessageBox.Show(Loc.Format("Dlg_ExitRestore", ModeNames.Title(Mode.Work)), "GameShift",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (answer == MessageBoxResult.Cancel)
                {
                    _timer.Start();
                    return;
                }
                if (answer == MessageBoxResult.Yes)
                {
                    await SwitchAsync(Mode.Work, manual: true);
                    if (Controller.Current != Mode.Work)
                    {
                        _timer.Start();
                        ShowWindow(); // не получилось: отчёт в окне, не выходим
                        return;
                    }
                    if (_restoreTask is { } restore) await restore;
                }
            }
        }
        catch (Exception e)
        {
            ErrorLog.Write(e);
        }
        Window.AllowClose = true;
        Window.Close();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _timer.Stop();
        Tray.Dispose();
        Theme.Dispose();
    }
}
