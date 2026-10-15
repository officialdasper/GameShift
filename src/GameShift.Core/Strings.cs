using System.Globalization;
using System.Resources;

namespace GameShift.Core;

/// <summary>Строки интерфейса и отчётов. Язык берётся из CultureInfo.CurrentUICulture; нейтральный ресурс английский.</summary>
public static class Strings
{
    static readonly ResourceManager Manager = new("GameShift.Core.Strings", typeof(Strings).Assembly);

    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentUICulture, Get(key), args);
}
