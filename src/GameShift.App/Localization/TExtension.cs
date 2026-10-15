using System.Windows.Data;
using System.Windows.Markup;

namespace GameShift.App.Localization;

/// <summary>Текст из ресурсов: {loc:T Key}. Привязан к Loc, поэтому меняется вместе с языком.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension(string key) : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
