using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GameShift.App.Theme;

/// <summary>Если в Windows выключены «Эффекты анимации», все длительности нулевые.</summary>
public static class Motion
{
    public static readonly bool Enabled = SystemParameters.ClientAreaAnimation;

    public static readonly IEasingFunction EaseOut = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });
    public static readonly IEasingFunction EaseOutQuint = Freeze(new QuinticEase { EasingMode = EasingMode.EaseOut });

    public static Visibility WhenEnabled => Enabled ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility WhenDisabled => Enabled ? Visibility.Collapsed : Visibility.Visible;

    public static Duration Ms(double ms) => new(TimeSpan.FromMilliseconds(Enabled ? ms : 0));

    public static DoubleAnimation To(double to, double ms, IEasingFunction? ease = null) =>
        new(to, Ms(ms)) { EasingFunction = ease ?? EaseOut };

    static IEasingFunction Freeze(EasingFunctionBase e)
    {
        e.Freeze();
        return e;
    }

    // Порядковый номер строки отчёта: строки проявляются по очереди, по 30 мс на строку.
    public static readonly DependencyProperty StaggerProperty = DependencyProperty.RegisterAttached(
        "Stagger", typeof(int), typeof(Motion), new PropertyMetadata(-1, OnStaggerChanged));

    public static int GetStagger(DependencyObject d) => (int)d.GetValue(StaggerProperty);
    public static void SetStagger(DependencyObject d, int value) => d.SetValue(StaggerProperty, value);

    // Каждая строка проявляется один раз: при возврате на страницу повторной анимации нет.
    static readonly ConditionalWeakTable<object, object> Shown = new();

    static void OnStaggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el || !Enabled) return;
        el.Loaded -= OnStaggerLoaded;
        el.Loaded += OnStaggerLoaded;
    }

    static void OnStaggerLoaded(object sender, RoutedEventArgs e)
    {
        var el = (FrameworkElement)sender;
        el.Loaded -= OnStaggerLoaded;
        if (el.DataContext is not { } item || Shown.TryGetValue(item, out _)) return;
        Shown.Add(item, item);

        var delay = TimeSpan.FromMilliseconds(Math.Min(GetStagger(el), 20) * 30);
        var move = new TranslateTransform(0, 6);
        el.RenderTransform = move;
        el.Opacity = 0;
        el.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, Ms(180)) { BeginTime = delay, EasingFunction = EaseOut });
        move.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(6, 0, Ms(220)) { BeginTime = delay, EasingFunction = EaseOutQuint });
    }
}

/// <summary>Длительность в XAML: {theme:Ms 160}. Обнуляется, когда анимации в Windows выключены.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class MsExtension(double value) : MarkupExtension
{
    public MsExtension() : this(0)
    {
    }

    [ConstructorArgument("value")]
    public double Value { get; set; } = value;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var span = TimeSpan.FromMilliseconds(Motion.Enabled ? Value : 0);
        var target = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        var type = target?.TargetProperty switch
        {
            DependencyProperty dp => dp.PropertyType,
            System.Reflection.PropertyInfo pi => pi.PropertyType,
            _ => null,
        };
        return type == typeof(Duration) ? new Duration(span) : span;
    }
}
