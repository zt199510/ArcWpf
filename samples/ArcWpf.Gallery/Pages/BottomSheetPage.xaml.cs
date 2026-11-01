using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class BottomSheetPage : UserControl
{
    public BottomSheetPage() => InitializeComponent();
    private void Open(object sender, RoutedEventArgs e) => TripSheet.Show(Host);
}
