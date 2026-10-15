using System.Collections.ObjectModel;
using System.Windows;
using GameShift.App.Localization;
using GameShift.Core;
using GameShift.Platform;

namespace GameShift.App.ViewModels;

public sealed partial class MainViewModel
{
    public ObservableCollection<StepRow> NvidiaReport { get; } = [];
    public IReadOnlyList<string> NvidiaSettingNames => NvidiaProfiles.SettingNames();
    public bool NvidiaAvailable { get; private set; }
    public RelayCommand ApplyNvidiaCommand { get; private set; } = null!;
    public RelayCommand RestoreNvidiaCommand { get; private set; } = null!;

    public bool FocusSupported => ToastSwitch.IsSupported;
    public Visibility FocusUnsupportedVisibility => FocusSupported ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NvidiaUnavailableVisibility => NvidiaAvailable ? Visibility.Collapsed : Visibility.Visible;

    void InitSettings()
    {
        if (!FocusSupported && _host.Settings.FocusEnabled) Update(s => s.FocusEnabled = false, nameof(FocusEnabled));
        NvidiaAvailable = NvidiaProfiles.IsAvailable();
        ApplyNvidiaCommand = new RelayCommand(_ => RunNvidia(() =>
            _host.Nvidia.Apply(_host.CurrentSettings().Games.Select(g => g.ExeName))), _ => NvidiaAvailable && !_nvidiaBusy);
        RestoreNvidiaCommand = new RelayCommand(_ => RunNvidia(_host.Nvidia.Restore), _ => NvidiaAvailable && !_nvidiaBusy);
    }

    public bool PowerEnabled
    {
        get => _host.Settings.PowerEnabled;
        set => Update(s => s.PowerEnabled = value);
    }

    public bool FocusEnabled
    {
        get => _host.Settings.FocusEnabled;
        set => Update(s => s.FocusEnabled = value);
    }

    public bool AutoSwitchEnabled
    {
        get => _host.Settings.AutoSwitchEnabled;
        set => Update(s => s.AutoSwitchEnabled = value);
    }

    public bool StartWithWindows
    {
        get => _host.Settings.StartWithWindows;
        set
        {
            Autostart.Set(value, AppHost.ExePath);
            Update(s => s.StartWithWindows = value);
        }
    }

    public bool MemoryPurgeEnabled
    {
        get => _host.Settings.MemoryPurgeEnabled;
        set
        {
            if (value && !_host.Purger.IsConfigured && !_host.Purger.Configure())
            {
                MessageBox.Show(Loc.Get("Dlg_MemoryNeedsAdmin"), "GameShift",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Raise();
                return;
            }
            if (!value && _host.Purger.IsConfigured) _host.Purger.Remove();
            Update(s => s.MemoryPurgeEnabled = value);
        }
    }

    void Update(Action<Settings> change, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        lock (_host.SettingsLock) change(_host.Settings);
        _host.SaveSettings();
        Raise(name);
    }

    bool _nvidiaBusy;

    async void RunNvidia(Func<IReadOnlyList<StepResult>> action)
    {
        if (_nvidiaBusy) return;
        SetNvidiaBusy(true);
        NvidiaReport.Clear();
        try
        {
            var results = await Task.Run(action);
            foreach (var r in results) NvidiaReport.Add(StepRow.From(r));
        }
        catch (Exception e)
        {
            NvidiaReport.Add(StepRow.From(new StepResult("NVIDIA", StepStatus.Failed, e.Message)));
        }
        finally
        {
            SetNvidiaBusy(false);
        }
    }

    void SetNvidiaBusy(bool busy)
    {
        _nvidiaBusy = busy;
        ApplyNvidiaCommand.RaiseCanExecuteChanged();
        RestoreNvidiaCommand.RaiseCanExecuteChanged();
    }
}
