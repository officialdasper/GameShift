using GameShift.App.Localization;
using GameShift.Core;

namespace GameShift.App.ViewModels;

public sealed partial class MainViewModel
{
    public IReadOnlyList<LanguageOption> LanguageOptions { get; private set; } = [];

    void InitLanguage()
    {
        LanguageOptions =
        [
            new LanguageOption(Languages.System, null),
            .. Languages.Supported.Select(l => new LanguageOption(l.Code, l.NativeName)),
        ];
        Loc.Instance.Changed += RefreshLanguage;
    }

    public string LanguageSetting
    {
        get => _host.Settings.Language;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _host.Settings.Language) return;
            _host.SetLanguage(value);
            Raise();
        }
    }

    /// <summary>Тексты, собранные в коде, пересобираются на новом языке. Привязки из XAML обновляет сам Loc.</summary>
    void RefreshLanguage()
    {
        UpdateModeTexts();
        foreach (var t in ModeTiles) t.RefreshText();
        foreach (var g in Games) g.RefreshText();
        foreach (var o in LanguageOptions) o.RefreshText();
        SystemAccentOption.RefreshText();
        foreach (var o in AccentOptions) o.RefreshText();
        Raise(nameof(NvidiaSettingNames));
    }
}
