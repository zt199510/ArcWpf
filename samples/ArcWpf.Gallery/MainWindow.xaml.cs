using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery;
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
    private void ToggleTheme(object sender, RoutedEventArgs e)
    {
        var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
        theme.Mode = theme.Mode == ArcThemeMode.Light ? ArcThemeMode.Dark : ArcThemeMode.Light;
    }
    private void ChangeMotion(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox picker && picker.SelectedIndex >= 0) { ArcMotion.SetLevel(this, (ArcMotionLevel)picker.SelectedIndex); }
    }
}
