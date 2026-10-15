using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GameShift.App.Theme;
using GameShift.Core;

namespace GameShift.App;

/// <summary>Палитра своего акцента. Пока она открыта, акцент примеряется на всё приложение; «Отмена» возвращает прежний.</summary>
public partial class AccentPickerWindow : Window
{
    const double KnobRadius = 9;

    // Оттенок хранится отдельно от RGB: у серого и чёрного его нет, и без этого он бы сбрасывался при перетаскивании.
    double _h, _s, _v;
    readonly Color _initial;
    readonly SolidColorBrush _hueBrush = new();
    readonly SolidColorBrush _knobBrush = new();
    readonly SolidColorBrush _hueKnobBrush = new();
    readonly GradientStop _newStop = new() { Offset = 0.5 };
    readonly GradientStop _newEnd = new() { Offset = 1 };
    readonly TranslateTransform _areaMove = new();
    readonly TranslateTransform _hueMove = new();
    readonly ScaleTransform _areaScale = new(1, 1);
    readonly ScaleTransform _hueScale = new(1, 1);
    bool _previewQueued;
    bool _closed;

    AccentPickerWindow(Color initial)
    {
        InitializeComponent();
        BrandMark.Attach(this);
        _initial = initial;
        var hsv = ColorMath.ToHsv(new Rgb(initial.R, initial.G, initial.B));
        (_h, _s, _v) = (hsv.H, hsv.S, hsv.V);

        // Свои кисти и трансформации: их можно менять и анимировать, в отличие от замороженных из ресурсов.
        HueLayer.Fill = _hueBrush;
        AreaKnobFill.Fill = _knobBrush;
        HueKnobFill.Fill = _hueKnobBrush;
        AreaKnob.RenderTransform = new TransformGroup { Children = { _areaScale, _areaMove } };
        HueKnob.RenderTransform = new TransformGroup { Children = { _hueScale, _hueMove } };
        Preview.Background = new LinearGradientBrush(new GradientStopCollection
        {
            new(initial, 0), new(initial, 0.5), _newStop, _newEnd,
        }, new Point(0, 0), new Point(1, 0));

        Hook(Area, OnAreaPointer, OnAreaKey, AreaRing, _areaScale);
        Hook(Hue, OnHuePointer, OnHueKey, HueRing, _hueScale);

        SourceInitialized += (_, _) => ThemeService.Instance?.ApplyTitleBar(this);
        Loaded += (_, _) =>
        {
            Render(animate: false);
            Area.Focus();
            AnimateIn();
        };
        Closed += (_, _) =>
        {
            _closed = true;
            if (DialogResult != true) ThemeService.Instance?.PreviewAccent(null);
        };
    }

    /// <summary>Открывает палитру с начальным цветом. Возвращает #RRGGBB или null, если нажали «Отмена».</summary>
    public static string? Pick(Window owner, Color initial)
    {
        var w = new AccentPickerWindow(initial) { Owner = owner };
        return w.ShowDialog() == true ? ColorMath.ToHex(w.Current) : null;
    }

    Rgb Current => ColorMath.ToRgb(new Hsv(_h, _s, _v));

    void Hook(FrameworkElement target, Action<Point, bool> pointer, Func<Key, bool, bool> key,
        FrameworkElement ring, ScaleTransform knobScale)
    {
        target.MouseLeftButtonDown += (_, e) =>
        {
            target.Focus();
            target.CaptureMouse();
            Grow(knobScale, true);
            pointer(e.GetPosition(target), true);
            e.Handled = true;
        };
        target.MouseMove += (_, e) =>
        {
            if (target.IsMouseCaptured) pointer(e.GetPosition(target), false);
        };
        target.MouseLeftButtonUp += (_, _) => target.ReleaseMouseCapture();
        target.LostMouseCapture += (_, _) => Grow(knobScale, false);
        target.PreviewKeyDown += (_, e) =>
        {
            if (key(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))) e.Handled = true;
        };
        var cue = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(FocusCue.VisibleProperty, typeof(FrameworkElement));
        cue.AddValueChanged(target, (_, _) =>
            ring.Visibility = FocusCue.GetVisible(target) ? Visibility.Visible : Visibility.Collapsed);
    }

    void OnAreaPointer(Point p, bool pressed)
    {
        _s = Math.Clamp(p.X / Area.ActualWidth, 0, 1);
        _v = 1 - Math.Clamp(p.Y / Area.ActualHeight, 0, 1);
        Render(animate: pressed);
    }

    void OnHuePointer(Point p, bool pressed)
    {
        _h = Math.Clamp(p.X / Hue.ActualWidth, 0, 1) * 360;
        Render(animate: pressed);
    }

    bool OnAreaKey(Key key, bool big)
    {
        var step = big ? 0.1 : 0.01;
        switch (key)
        {
            case Key.Left: _s -= step; break;
            case Key.Right: _s += step; break;
            case Key.Up: _v += step; break;
            case Key.Down: _v -= step; break;
            default: return false;
        }
        _s = Math.Clamp(_s, 0, 1);
        _v = Math.Clamp(_v, 0, 1);
        Render(animate: false);
        return true;
    }

    bool OnHueKey(Key key, bool big)
    {
        var step = big ? 10 : 1;
        switch (key)
        {
            case Key.Left or Key.Down: _h -= step; break;
            case Key.Right or Key.Up: _h += step; break;
            case Key.Home: _h = 0; break;
            case Key.End: _h = 360; break;
            default: return false;
        }
        _h = Math.Clamp(_h, 0, 360);
        Render(animate: false);
        return true;
    }

    void Render(bool animate)
    {
        var pure = ColorMath.ToRgb(new Hsv(_h, 1, 1));
        var c = Current;
        var color = Color.FromRgb(c.R, c.G, c.B);
        _hueBrush.Color = Color.FromRgb(pure.R, pure.G, pure.B);
        _hueKnobBrush.Color = _hueBrush.Color;
        _knobBrush.Color = color;
        _newStop.Color = color;
        _newEnd.Color = color;

        Move(_areaMove, _s * Area.ActualWidth - KnobRadius, (1 - _v) * Area.ActualHeight - KnobRadius, animate);
        Move(_hueMove, _h / 360 * Hue.ActualWidth - KnobRadius, Hue.ActualHeight / 2 - KnobRadius, animate);
        QueuePreview(color);
    }

    // Щелчок мимо бегунка: он коротко доезжает до точки. При перетаскивании и с клавиатуры идёт следом без задержки.
    static void Move(TranslateTransform t, double x, double y, bool animate)
    {
        if (animate)
        {
            t.BeginAnimation(TranslateTransform.XProperty, Motion.To(x, 120, Motion.EaseOutQuint));
            t.BeginAnimation(TranslateTransform.YProperty, Motion.To(y, 120, Motion.EaseOutQuint));
            return;
        }
        t.BeginAnimation(TranslateTransform.XProperty, null);
        t.BeginAnimation(TranslateTransform.YProperty, null);
        t.X = x;
        t.Y = y;
    }

    static void Grow(ScaleTransform s, bool on)
    {
        var to = on ? 1.15 : 1;
        var ms = on ? 120 : 180;
        s.BeginAnimation(ScaleTransform.ScaleXProperty, Motion.To(to, ms));
        s.BeginAnimation(ScaleTransform.ScaleYProperty, Motion.To(to, ms));
    }

    // Примерка на всё приложение перекрашивает много элементов, поэтому не чаще, чем диспетчер успевает рисовать.
    void QueuePreview(Color color)
    {
        _pending = color;
        if (_previewQueued) return;
        _previewQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _previewQueued = false;
            if (!_closed) ThemeService.Instance?.PreviewAccent(_pending);
        });
    }

    Color _pending;

    void AnimateIn()
    {
        var move = new TranslateTransform(0, 8);
        Root.RenderTransform = move;
        Root.Opacity = 0;
        Root.BeginAnimation(OpacityProperty, Motion.To(1, 180));
        move.BeginAnimation(TranslateTransform.YProperty, Motion.To(0, 240, Motion.EaseOutQuint));
    }

    void OnApply(object sender, RoutedEventArgs e) => DialogResult = true;
}
