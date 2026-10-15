using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GameShift.App.Theme;

/// <summary>Ряд сегментов (RadioButton). Подложку выбранного рисует сама панель и плавно двигает к новому.</summary>
public sealed class SegmentPanel : Panel
{
    public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register(
        nameof(Highlight), typeof(Brush), typeof(SegmentPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightBorderProperty = DependencyProperty.Register(
        nameof(HighlightBorder), typeof(Brush), typeof(SegmentPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty LeftProperty = DependencyProperty.Register(
        "Left", typeof(double), typeof(SegmentPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty WidthOfProperty = DependencyProperty.Register(
        "WidthOf", typeof(double), typeof(SegmentPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty ShownProperty = DependencyProperty.Register(
        "Shown", typeof(double), typeof(SegmentPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? Highlight
    {
        get => (Brush?)GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public Brush? HighlightBorder
    {
        get => (Brush?)GetValue(HighlightBorderProperty);
        set => SetValue(HighlightBorderProperty, value);
    }

    bool _placed;
    UIElement? _target;

    public SegmentPanel()
    {
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => InvalidateArrange()));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, _) => InvalidateArrange()));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = 0, h = 0;
        foreach (UIElement c in InternalChildren)
        {
            c.Measure(new Size(double.PositiveInfinity, available.Height));
            w += c.DesiredSize.Width;
            h = Math.Max(h, c.DesiredSize.Height);
        }
        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double x = 0;
        UIElement? target = null;
        Rect rect = default;
        foreach (UIElement c in InternalChildren)
        {
            var r = new Rect(x, 0, c.DesiredSize.Width, final.Height);
            c.Arrange(r);
            if (c is ToggleButton { IsChecked: true } && c.Visibility == Visibility.Visible)
            {
                target = c;
                rect = r;
            }
            x += c.DesiredSize.Width;
        }
        MoveHighlight(target, rect);
        return final;
    }

    void MoveHighlight(UIElement? target, Rect rect)
    {
        var shown = target is null ? 0.0 : 1.0;
        var animate = _placed && Motion.Enabled && IsLoaded;
        if (target is not null && (!animate || _target is null))
        {
            // Первое появление: подложка встаёт на место сразу и только проявляется.
            Snap(LeftProperty, rect.X);
            Snap(WidthOfProperty, rect.Width);
        }
        else if (target is not null && (target != _target || rect.X != (double)GetValue(LeftProperty)))
        {
            Animate(LeftProperty, rect.X, 200);
            Animate(WidthOfProperty, rect.Width, 200);
        }
        if (animate) Animate(ShownProperty, shown, 150);
        else Snap(ShownProperty, shown);
        _target = target;
        _placed = true;
    }

    void Snap(DependencyProperty p, double v)
    {
        BeginAnimation(p, null);
        SetValue(p, v);
    }

    void Animate(DependencyProperty p, double v, double ms)
    {
        if ((double)GetValue(p) == v) return;
        BeginAnimation(p, Motion.To(v, ms, Motion.EaseOutQuint), HandoffBehavior.SnapshotAndReplace);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var shown = (double)GetValue(ShownProperty);
        var width = (double)GetValue(WidthOfProperty);
        if (shown <= 0 || width <= 0 || Highlight is null) return;
        dc.PushOpacity(shown);
        var pen = HighlightBorder is null ? null : new Pen(HighlightBorder, 1);
        var r = new Rect((double)GetValue(LeftProperty) + 0.5, 0.5, width - 1, Math.Max(0, ActualHeight - 1));
        dc.DrawRoundedRectangle(Highlight, pen, r, 4, 4);
        dc.Pop();
    }
}
