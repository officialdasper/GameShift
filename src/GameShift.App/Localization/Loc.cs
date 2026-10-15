using System.ComponentModel;
using System.Globalization;
using GameShift.Core;

namespace GameShift.App.Localization;

/// <summary>Язык интерфейса. Индексатор нужен привязкам {loc:T Key}: событие «Item[]» обновляет все тексты без перезапуска.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    /// <summary>Язык Windows на момент запуска, до того как приложение подставит свой.</summary>
    public static CultureInfo SystemCulture { get; } = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? Changed;

    public string Code { get; private set; } = Languages.Fallback;

    public string this[string key] => Strings.Get(key);

    public static string Get(string key) => Strings.Get(key);

    public static string Format(string key, params object?[] args) => Strings.Format(key, args);

    public string Apply(string setting)
    {
        var code = Languages.Resolve(setting, SystemCulture);
        var culture = Languages.ToCulture(code);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Code = code;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        Changed?.Invoke();
        return code;
    }
}
