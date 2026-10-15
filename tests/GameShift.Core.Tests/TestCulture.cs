using System.Globalization;
using System.Runtime.CompilerServices;

namespace GameShift.Core.Tests;

static class TestCulture
{
    // Тесты сверяют русские тексты отчётов.
    [ModuleInitializer]
    internal static void Init()
    {
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ru");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru");
    }
}
