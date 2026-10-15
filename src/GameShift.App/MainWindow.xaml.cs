using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameShift.App.Theme;

namespace GameShift.App;

public partial class MainWindow : Window
{
    public bool AllowClose { get; set; }

    FrameworkElement? _bar;
    FrameworkElement? _items;
    FrameworkElement? _page;

    public MainWindow()
    {
        InitializeComponent();
        Tabs.Loaded += (_, _) =>
        {
            _bar = Tabs.Template.FindName("PART_Bar", Tabs) as FrameworkElement;
            _items = Tabs.Template.FindName("PART_Items", Tabs) as FrameworkElement;
            _page = Tabs.Template.FindName("PART_Page", Tabs) as FrameworkElement;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => MoveBar(animate: false));
        };
        Tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, Tabs) || !Tabs.IsLoaded) return;
            MoveBar(animate: true);
            AnimatePageIn();
        };
    }

    /// <summary>Трансформации из шаблонов WPF заморожены, анимировать их нельзя: перед анимацией элемент получает свою копию.</summary>
    static TranslateTransform OwnTranslate(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform t && !t.IsFrozen) return t;
        var own = element.RenderTransform is TranslateTransform frozen
            ? new TranslateTransform(frozen.X, frozen.Y)
            : new TranslateTransform();
        element.RenderTransform = own;
        return own;
    }

    void MoveBar(bool animate)
    {
        if (_bar is null || _items is null) return;
        if (Tabs.ItemContainerGenerator.ContainerFromIndex(Tabs.SelectedIndex) is not FrameworkElement item) return;
        var top = item.TranslatePoint(new Point(0, 0), _items).Y + (item.ActualHeight - _bar.Height) / 2;
        var move = OwnTranslate(_bar);
        if (animate)
        {
            move.BeginAnimation(TranslateTransform.YProperty, Motion.To(top, 220, Motion.EaseOutQuint),
                HandoffBehavior.SnapshotAndReplace);
        }
        else
        {
            move.BeginAnimation(TranslateTransform.YProperty, null);
            move.Y = top;
        }
        _bar.Opacity = 1;
    }

    void AnimatePageIn()
    {
        if (_page is null || !Motion.Enabled) return;
        var move = OwnTranslate(_page);
        _page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Motion.Ms(180)) { EasingFunction = Motion.EaseOut });
        move.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, Motion.Ms(240)) { EasingFunction = Motion.EaseOutQuint });
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true; // крестик сворачивает в трей
            Hide();
        }
        base.OnClosing(e);
    }
}
