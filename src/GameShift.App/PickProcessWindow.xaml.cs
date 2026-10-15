using System.Windows;
using GameShift.App.Theme;

namespace GameShift.App;

public partial class PickProcessWindow : Window
{
    public PickProcessWindow()
    {
        InitializeComponent();
        BrandMark.Attach(this);
        SourceInitialized += (_, _) => ThemeService.Instance?.ApplyTitleBar(this);
    }

    public static IReadOnlyList<string> Pick(Window owner, IEnumerable<string> names, string title)
    {
        var w = new PickProcessWindow { Owner = owner, Title = title };
        w.List.ItemsSource = names.ToList();
        return w.ShowDialog() == true ? w.List.SelectedItems.Cast<string>().ToList() : [];
    }

    void OnAdd(object sender, RoutedEventArgs e) => DialogResult = true;
}
