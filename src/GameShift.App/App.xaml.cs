using System.Threading;
using System.Windows;
using System.Windows.Threading;
using GameShift.App.Theme;
using GameShift.Platform;

namespace GameShift.App;

public partial class App : Application
{
    const string MutexName = "GameShift.SingleInstance";
    const string ShowEventName = "GameShift.Show";

    Mutex? _single;
    EventWaitHandle? _showEvent;
    RegisteredWaitHandle? _showWait;
    AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains(StandbyPurger.HelperArg))
        {
            Shutdown(StandbyPurger.PurgeNow());
            return;
        }

        FocusCue.Register();
        DispatcherUnhandledException += OnDispatcherError;
        TaskScheduler.UnobservedTaskException += OnUnobservedTask;
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
        {
            if (a.ExceptionObject is Exception ex) ErrorLog.Report(ex);
        };

        _single = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
            {
                using (existing) existing.Set();
            }
            Shutdown();
            return;
        }

        _host = new AppHost(startHidden: e.Args.Contains("--tray"));
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(() => _host?.ShowWindow()), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    void OnDispatcherError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ErrorLog.Report(e.Exception);
    }

    void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Dispatcher.BeginInvoke(() => ErrorLog.Report(e.Exception.GetBaseException()));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _host?.Dispose();
        _single?.Dispose();
        base.OnExit(e);
    }
}
