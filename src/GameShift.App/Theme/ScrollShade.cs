using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace GameShift.App.Theme;

/// <summary>Мягкое затухание у краёв прокрутки. Включается цветом фона: theme:ScrollShade.Brush="{DynamicResource SurfaceBrush}" (наследуется, можно ставить на ListBox). Полосы рисует шаблон ScrollViewer, здесь только кисти и видимость.</summary>
public static class ScrollShade
{
    public static readonly DependencyProperty BrushProperty = DependencyProperty.RegisterAttached(
        "Brush", typeof(SolidColorBrush), typeof(ScrollShade),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, OnBrushChanged));

    public static SolidColorBrush? GetBrush(DependencyObject d) => (SolidColorBrush?)d.GetValue(BrushProperty);
    public static void SetBrush(DependencyObject d, SolidColorBrush? value) => d.SetValue(BrushProperty, value);

    // Плотность фона по высоте полосы: у края почти сплошной, дальше быстро сходит на нет.
    static readonly (double Offset, double Alpha)[] Ramp = [(0, 1), (0.45, 0.5), (1, 0)];

    sealed class State
    {
        public readonly List<GradientStop> Stops = [];
        public LinearGradientBrush Top = null!, Bottom = null!;
        public bool? Up, Down;
    }

    static readonly ConditionalWeakTable<ScrollViewer, State> States = new();

    static void OnBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv) return;
        if (!States.TryGetValue(sv, out var st))
        {
            st = Create();
            States.Add(sv, st);
            sv.ScrollChanged += (_, _) => Update(sv, animate: true);
            sv.Loaded += (_, _) => Update(sv, animate: false);
        }
        // Кисти темы при смене темы подменяются и плавно меняют цвет: привязка к Color идёт следом за анимацией.
        foreach (var stop in st.Stops)
        {
            if (e.NewValue is SolidColorBrush brush)
                BindingOperations.SetBinding(stop, GradientStop.ColorProperty,
                    new Binding(nameof(SolidColorBrush.Color)) { Source = brush, Converter = Alpha.Instance, ConverterParameter = stop.Offset });
            else
                BindingOperations.ClearBinding(stop, GradientStop.ColorProperty);
        }
        Update(sv, animate: false);
    }

    static State Create()
    {
        var st = new State();
        st.Top = Gradient(st, new Point(0, 0), new Point(0, 1));
        st.Bottom = Gradient(st, new Point(0, 1), new Point(0, 0));
        return st;
    }

    static LinearGradientBrush Gradient(State st, Point start, Point end)
    {
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        foreach (var (offset, _) in Ramp)
        {
            var stop = new GradientStop(Colors.Transparent, offset);
            brush.GradientStops.Add(stop);
            st.Stops.Add(stop);
        }
        return brush;
    }

    static void Update(ScrollViewer sv, bool animate)
    {
        if (!States.TryGetValue(sv, out var st)) return;
        sv.ApplyTemplate();
        if (sv.Template?.FindName("PART_ShadeTop", sv) is not Border top ||
            sv.Template.FindName("PART_ShadeBottom", sv) is not Border bottom) return;
        top.Background = st.Top;
        bottom.Background = st.Bottom;

        var on = GetBrush(sv) is not null;
        var up = on && sv.VerticalOffset > 0.5;
        var down = on && sv.ScrollableHeight - sv.VerticalOffset > 0.5;
        if (st.Up != up) Fade(top, up, animate);
        if (st.Down != down) Fade(bottom, down, animate);
        (st.Up, st.Down) = (up, down);
    }

    static void Fade(UIElement e, bool show, bool animate) =>
        e.BeginAnimation(UIElement.OpacityProperty, Motion.To(show ? 1 : 0, animate ? 120 : 0));

    sealed class Alpha : IValueConverter
    {
        public static readonly Alpha Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var c = (Color)value;
            var offset = (double)parameter;
            var k = Ramp.First(r => r.Offset == offset).Alpha;
            return Color.FromArgb((byte)Math.Round(c.A * k), c.R, c.G, c.B);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
