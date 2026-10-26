using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
using ArcCheckBox = ArcWpf.Controls.CheckBox;
using ArcButton = ArcWpf.Controls.Button;

namespace ArcWpf.Gallery.Pages;

public partial class DrawerPage : UserControl
{
    public DrawerPage() => InitializeComponent();
    private Drawer Get(string key) => (Drawer)Resources[key];
    private void OpenFilters(object sender, RoutedEventArgs e) => Get("FiltersDrawer").Show(this);
    private void OpenLeft(object sender, RoutedEventArgs e) => Get("LeftDrawer").Show(this);
    private void OpenTop(object sender, RoutedEventArgs e) => Get("TopDrawer").Show(this);
    private void OpenGuard(object sender, RoutedEventArgs e) => Get("GuardDrawer").Show(this);
    private void GuardClosing(object? sender, DrawerClosingEventArgs e) => e.Cancel = ((StackPanel)Get("GuardDrawer").Content).Children[0] is ArcCheckBox check && check.IsChecked == true;
    private void OpenLong(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel();
        for (int i = 1; i <= 24; i++) content.Children.Add(new TextBlock { Text = $"Section {i}: Review the workspace settings and confirm the details before continuing.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        content.Children.Add(new ArcButton { Content = "Done", Command = Drawer.CloseCommand, HorizontalAlignment = HorizontalAlignment.Right });
        new Drawer { Title = "Workspace details", Description = "Scroll to review all sections.", Content = content }.Show(this);
    }
}
