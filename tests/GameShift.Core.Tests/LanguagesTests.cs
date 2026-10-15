using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;

namespace GameShift.Core.Tests;

public class LanguagesTests
{
    static string Resolve(string setting, string system) => Languages.Resolve(setting, CultureInfo.GetCultureInfo(system));

    [Theory]
    [InlineData("ru-RU", "ru")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("uk-UA", "uk")]
    [InlineData("de-AT", "de")]
    [InlineData("es-MX", "es")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("fr-CA", "fr")]
    [InlineData("pl-PL", "pl")]
    [InlineData("tr-TR", "tr")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-Hans", "zh-Hans")]
    [InlineData("ja-JP", "en")]
    [InlineData("ar-SA", "en")]
    public void System_MapsToClosestSupported(string windows, string expected) =>
        Assert.Equal(expected, Resolve("system", windows));

    [Fact]
    public void InvariantCulture_FallsBackToEnglish() =>
        Assert.Equal("en", Languages.Resolve("system", CultureInfo.InvariantCulture));

    [Fact]
    public void ExplicitChoice_WinsOverSystem() => Assert.Equal("de", Resolve("de", "ru-RU"));

    [Fact]
    public void ExplicitChoice_IsCaseInsensitive() => Assert.Equal("pt-BR", Resolve("PT-br", "ru-RU"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xx")]
    public void UnknownOrEmptySetting_BehavesLikeSystem(string? setting) =>
        Assert.Equal("ru", Languages.Resolve(setting, CultureInfo.GetCultureInfo("ru-RU")));

    [Fact]
    public void Supported_HasTenLanguagesWithOwnNames()
    {
        Assert.Equal(10, Languages.Supported.Count);
        Assert.Contains(Languages.Supported, l => l.Code == "ru" && l.NativeName == "Русский");
        Assert.Contains(Languages.Supported, l => l.Code == "zh-Hans" && l.NativeName == "简体中文");
        Assert.Contains(Languages.Supported, l => l.Code == "pt-BR" && l.NativeName == "Português (Brasil)");
    }

    [Fact]
    public void Settings_LanguageDefaultsToSystem_AndRoundTrips()
    {
        var s = new Settings();
        Assert.Equal("system", s.Language);
        s.Language = "tr";
        Assert.Equal("tr", s.Clone().Language);
    }

    [Fact]
    public void Settings_OldJsonWithoutLanguage_IsSystem() =>
        Assert.Equal("system", System.Text.Json.JsonSerializer.Deserialize<Settings>("{}", JsonFileStore<Settings>.Options)!.Language);

    [Fact]
    public void Settings_NullLanguage_BecomesSystem() =>
        Assert.Equal("system", System.Text.Json.JsonSerializer.Deserialize<Settings>("""{"Language":null}""", JsonFileStore<Settings>.Options)!.Language);
}

public class StringsTests
{
    static readonly ResourceManager Rm = new("GameShift.Core.Strings", typeof(Strings).Assembly);
    static readonly Regex Placeholder = new(@"\{\d+\}");

    static Dictionary<string, string> Load(CultureInfo culture)
    {
        var set = Rm.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        return set!.Cast<System.Collections.DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
    }

    public static IEnumerable<object[]> Codes() => Languages.Supported.Where(l => l.Code != "en").Select(l => new object[] { l.Code });

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryLanguage_HasSameKeysAsNeutral(string code)
    {
        var neutral = Load(CultureInfo.InvariantCulture);
        var other = Load(CultureInfo.GetCultureInfo(code));
        Assert.Empty(neutral.Keys.Except(other.Keys));
        Assert.Empty(other.Keys.Except(neutral.Keys));
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryLanguage_KeepsPlaceholders_AndHasNoEmptyValues(string code)
    {
        var neutral = Load(CultureInfo.InvariantCulture);
        var other = Load(CultureInfo.GetCultureInfo(code));
        foreach (var (key, value) in neutral)
        {
            Assert.False(string.IsNullOrWhiteSpace(other[key]), $"{code}: {key} is empty");
            var expected = Placeholder.Matches(value).Select(m => m.Value).OrderBy(x => x);
            var actual = Placeholder.Matches(other[key]).Select(m => m.Value).OrderBy(x => x);
            Assert.True(expected.SequenceEqual(actual), $"{code}: {key} placeholders differ");
        }
    }

    [Fact]
    public void Get_UsesCurrentUICulture()
    {
        var old = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru");
            Assert.Equal("Работа", ModeNames.Title(Mode.Work));
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
            Assert.NotEqual("Работа", ModeNames.Title(Mode.Work));
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            Assert.Equal("Work", ModeNames.Title(Mode.Work));
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }

    [Fact]
    public void Get_UnknownKey_ReturnsKey() => Assert.Equal("No_Such_Key", Strings.Get("No_Such_Key"));

    [Fact]
    public void Format_FillsArguments()
    {
        var old = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru");
            Assert.Equal("Закрыто: a.exe", Strings.Format("Ctl_Closed", "a.exe"));
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }
}
