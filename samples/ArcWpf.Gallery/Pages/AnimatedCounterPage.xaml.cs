using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class AnimatedCounterPage : UserControl
{
    public AnimatedCounterPage() => InitializeComponent();
    private void OnClick(object sender, RoutedEventArgs e) => Counter.Value += 240;
}
