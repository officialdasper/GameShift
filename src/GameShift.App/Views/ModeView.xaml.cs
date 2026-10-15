using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GameShift.App.Theme;
using GameShift.App.ViewModels;

namespace GameShift.App.Views;

public partial class ModeView : UserControl
{
    public ModeView() => InitializeComponent();

    // Подсказка «что делает режим» у значка «i» на карточке: наведение или фокус с клавиатуры открывают, уход и Esc закрывают.

    void OnInfoEnter(object sender, MouseEventArgs e) => Show(sender, true);

    void OnInfoLeave(object sender, MouseEventArgs e)
    {
        if (!FocusCue.GetVisible((DependencyObject)sender)) Show(sender, false);
    }

    void OnInfoFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (FocusCue.GetVisible((DependencyObject)sender)) Show(sender, true);
    }

    void OnInfoBlur(object sender, KeyboardFocusChangedEventArgs e) => Show(sender, false);

    void OnInfoKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || LayerOf(sender) is not { Opacity: > 0.5 }) return;
        Show(sender, false);
        e.Handled = true;
    }

    // Щелчок (или касание) тоже показывает подсказку; режим при этом не переключается.
    void OnInfoClick(object sender, RoutedEventArgs e)
    {
        Show(sender, true);
        e.Handled = true;
    }

    static FrameworkElement? PartOf(object sender, string name) =>
        sender is FrameworkElement { TemplatedParent: Control card } ? card.Template.FindName(name, card) as FrameworkElement : null;

    static FrameworkElement? LayerOf(object sender) => PartOf(sender, "InfoLayer");

    // Второй скруглённый слой поверх карточки всегда давал светлый стык на углах,
    // поэтому список проступает на собственном фоне карточки: обложка и надпись гаснут.
    static void Show(object sender, bool open)
    {
        if (LayerOf(sender) is not { } layer) return;
        if (open && (sender as FrameworkElement)?.DataContext is ModeTile tile) tile.RefreshSummary();
        var ms = open ? 140 : 110;
        layer.BeginAnimation(OpacityProperty, Motion.To(open ? 1 : 0, ms));
        PartOf(sender, "ArtLayers")?.BeginAnimation(OpacityProperty, Motion.To(open ? 0 : 1, ms));
        if (PartOf(sender, "BodyHost") is { } body) body.BeginAnimation(OpacityProperty, Motion.To(open ? 0 : 1, ms));
        if (PartOf(sender, "OnLabelHost") is { } label) label.BeginAnimation(OpacityProperty, Motion.To(open ? 0 : 1, ms));
    }
}
