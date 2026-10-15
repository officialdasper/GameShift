using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GameShift.App.Theme;
using GameShift.App.ViewModels;
using GameShift.Core;

namespace GameShift.App;

/// <summary>Результаты поиска установленных игр. Ищет только по файлам и реестру лаунчеров, процессов не трогает.</summary>
public partial class FindGamesWindow : Window
{
    readonly FindGamesViewModel _vm = new();
    readonly CancellationTokenSource _cts = new();

    FindGamesWindow(IReadOnlyList<GameEntry> existing)
    {
        InitializeComponent();
        BrandMark.Attach(this);
        DataContext = _vm;
        SourceInitialized += (_, _) => ThemeService.Instance?.ApplyTitleBar(this);
        Loaded += async (_, _) =>
        {
            StartRunner();
            await _vm.RunAsync(existing, _cts.Token);
            RunnerShift.BeginAnimation(TranslateTransform.XProperty, null);
        };
        Closed += (_, _) => _cts.Cancel();
    }

    public static IReadOnlyList<GameEntry> Pick(Window owner, IReadOnlyList<GameEntry> existing)
    {
        var w = new FindGamesWindow(existing) { Owner = owner, Title = Localization.Loc.Get("Scan_Title") };
        return w.ShowDialog() == true ? w._vm.Selected() : [];
    }

    void StartRunner()
    {
        if (!Motion.Enabled) return;
        RunnerShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-0.7, 0.7, new Duration(TimeSpan.FromMilliseconds(1100)))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        });
    }

    void OnAdd(object sender, RoutedEventArgs e) => DialogResult = true;

    void OnPickExe(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is ScanRow row) row.PickExe(this);
    }
}
