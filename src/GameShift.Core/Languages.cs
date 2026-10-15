using System.Globalization;

namespace GameShift.Core;

public sealed record LanguageInfo(string Code, string NativeName);

public static class Languages
{
    public const string System = "system";
    public const string Fallback = "en";

    public static readonly IReadOnlyList<LanguageInfo> Supported =
    [
        new("ru", "Русский"),
        new("en", "English"),
        new("uk", "Українська"),
        new("de", "Deutsch"),
        new("es", "Español"),
        new("pt-BR", "Português (Brasil)"),
        new("fr", "Français"),
        new("pl", "Polski"),
        new("tr", "Türkçe"),
        new("zh-Hans", "简体中文"),
    ];

    public static string Resolve(string? setting, CultureInfo system)
    {
        if (Find(setting) is { } chosen) return chosen;
        var name = system.Name;
        if (Find(name) is { } exact) return exact;
        var two = system.TwoLetterISOLanguageName;
        return two switch
        {
            "pt" => "pt-BR",
            "zh" => "zh-Hans",
            _ => Supported.FirstOrDefault(l => l.Code.Equals(two, StringComparison.OrdinalIgnoreCase))?.Code ?? Fallback,
        };
    }

    public static CultureInfo ToCulture(string code) => CultureInfo.GetCultureInfo(code);

    static string? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Supported.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Code;
}
