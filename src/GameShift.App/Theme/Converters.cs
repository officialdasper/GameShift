using System.Globalization;
using System.Windows.Data;

namespace GameShift.App.Theme;

/// <summary>true, если значение задано. Для триггеров вида «картинка загрузилась».</summary>
public sealed class NotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
