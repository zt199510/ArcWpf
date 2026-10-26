using System.Windows;
namespace ArcWpf.Consumer;
public partial class MainWindow : Window
{
    private void OpenDialog(object sender, RoutedEventArgs e) => new Controls.Dialog { Title="Confirm changes", Description="A DialogHost wraps this window.", Content=new Controls.Button { Content="Done", Command=Controls.Dialog.CloseCommand, HorizontalAlignment=HorizontalAlignment.Right } }.Show((FrameworkElement)sender);
    private void OpenDrawer(object sender, RoutedEventArgs e) => new Controls.Drawer { Title="Filters", Description="A DrawerHost keeps the page in context.", Content=new Controls.Button { Content="Done", Command=Controls.Drawer.CloseCommand, HorizontalAlignment=HorizontalAlignment.Right } }.Show((FrameworkElement)sender);
    private int _count;
    public MainWindow() => InitializeComponent();
    private void ShowToast(object sender, RoutedEventArgs e) => Toasts.Show("Changes saved", "Synced to all devices.");
    private void OnClick(object sender, RoutedEventArgs e) => Result.Text = $"已执行 {++_count} 次";
    private void TogglePopover(object sender, RoutedEventArgs e) => SharePopover.SetCurrentValue(Controls.Popover.IsOpenProperty, !SharePopover.IsOpen);
}


