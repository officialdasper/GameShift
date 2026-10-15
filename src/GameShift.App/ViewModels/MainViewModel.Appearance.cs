using System.Windows.Media;
using GameShift.App.Theme;
using GameShift.Core;

namespace GameShift.App.ViewModels;

public sealed partial class MainViewModel
{
    public AccentOption SystemAccentOption { get; private set; } = null!;
    public IReadOnlyList<AccentOption> AccentOptions { get; private set; } = [];

    void InitAppearance()
    {
        SystemAccentOption = new AccentOption("Common_System", ThemeService.SystemAccent,
            ThemeService.ReadSystemAccent(), SetAccent);
        AccentOptions = Accents.Presets.Select(p => Swatch(p.NameKey, p.Hex)).ToList();
        PickCustomAccentCommand = new RelayCommand(_ => PickCustomAccent());
        RefreshAccents();
    }

    AccentOption Swatch(string nameKey, string hex) =>
        new(nameKey, hex, (Color)ColorConverter.ConvertFromString(hex), SetAccent);

    public RelayCommand PickCustomAccentCommand { get; private set; } = null!;

    public bool IsCustomAccent => Accents.IsCustom(_host.Settings.Accent);

    public Brush? CustomAccentBrush
    {
        get
        {
            if (!IsCustomAccent || !ColorMath.TryParseHex(_host.Settings.Accent, out var c)) return null;
            var brush = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            brush.Freeze();
            return brush;
        }
    }

    void PickCustomAccent()
    {
        var start = ThemeService.Instance?.Accent ?? (Color)ColorConverter.ConvertFromString(ThemeService.DefaultAccent);
        var hex = AccentPickerWindow.Pick(_host.Window, start);
        if (hex is null) return;
        SetAccent(hex);
    }

    public bool IsThemeSystem
    {
        get => _host.Settings.Theme == ThemeMode.System;
        set { if (value) SetTheme(ThemeMode.System); }
    }

    public bool IsThemeDark
    {
        get => _host.Settings.Theme == ThemeMode.Dark;
        set { if (value) SetTheme(ThemeMode.Dark); }
    }

    public bool IsThemeLight
    {
        get => _host.Settings.Theme == ThemeMode.Light;
        set { if (value) SetTheme(ThemeMode.Light); }
    }

    void SetTheme(ThemeMode theme)
    {
        if (_host.Settings.Theme == theme) return;
        _host.SetAppearance(theme, _host.Settings.Accent, _host.Settings.Backdrop);
        Raise(nameof(IsThemeSystem));
        Raise(nameof(IsThemeDark));
        Raise(nameof(IsThemeLight));
    }

    void SetAccent(string accent)
    {
        _host.SetAppearance(_host.Settings.Theme, accent, _host.Settings.Backdrop);
        RefreshAccents();
    }

    public bool BackdropSupported => ThemeService.BackdropSupported;
    public System.Windows.Visibility BackdropUnsupportedVisibility =>
        BackdropSupported ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public bool IsBackdropMica
    {
        get => _host.Settings.Backdrop == BackdropMode.Mica;
        set { if (value) SetBackdrop(BackdropMode.Mica); }
    }

    public bool IsBackdropAcrylic
    {
        get => _host.Settings.Backdrop == BackdropMode.Acrylic;
        set { if (value) SetBackdrop(BackdropMode.Acrylic); }
    }

    public bool IsBackdropNone
    {
        get => _host.Settings.Backdrop == BackdropMode.None;
        set { if (value) SetBackdrop(BackdropMode.None); }
    }

    void SetBackdrop(BackdropMode backdrop)
    {
        if (_host.Settings.Backdrop == backdrop) return;
        _host.SetAppearance(_host.Settings.Theme, _host.Settings.Accent, backdrop);
        Raise(nameof(IsBackdropMica));
        Raise(nameof(IsBackdropAcrylic));
        Raise(nameof(IsBackdropNone));
    }

    void RefreshAccents()
    {
        var current = _host.Settings.Accent;
        SystemAccentOption.Refresh(current);
        foreach (var o in AccentOptions) o.Refresh(current);
        Raise(nameof(IsCustomAccent));
        Raise(nameof(CustomAccentBrush));
    }
}
