using System.Windows;
using System.Windows.Input;

namespace GameShift.App.Theme;

/// <summary>Рамка фокуса только для клавиатуры: Visible = true, если фокус пришёл с клавиатуры. Стандартный пунктир WPF отключён (FocusVisualStyle = null).</summary>
public static class FocusCue
{
    public static readonly DependencyProperty VisibleProperty = DependencyProperty.RegisterAttached(
        "Visible", typeof(bool), typeof(FocusCue), new PropertyMetadata(false));

    public static bool GetVisible(DependencyObject d) => (bool)d.GetValue(VisibleProperty);
    public static void SetVisible(DependencyObject d, bool value) => d.SetValue(VisibleProperty, value);

    static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnGotFocus), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.LostKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnLostFocus), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(UIElement), Mouse.PreviewMouseDownEvent,
            new MouseButtonEventHandler(OnMouseDown), handledEventsToo: true);
    }

    static void OnGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, e.NewFocus) || e.NewFocus is not DependencyObject d) return;
        SetVisible(d, InputManager.Current.MostRecentInputDevice is KeyboardDevice);
    }

    static void OnLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (ReferenceEquals(sender, e.OldFocus) && e.OldFocus is DependencyObject d) d.ClearValue(VisibleProperty);
    }

    // Щелчок по уже сфокусированному элементу тоже прячет рамку.
    static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.FocusedElement is DependencyObject d && GetVisible(d)) d.ClearValue(VisibleProperty);
    }
}
