using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class CarouselPage : UserControl
{
    public CarouselPage() { InitializeComponent(); Main.SelectedIndexChanged += (_, _) => Status.Text = $"当前第 {Main.SelectedIndex + 1} 张，共 {Main.Items.Count} 张"; Status.Text = "当前第 1 张，共 3 张"; }
    private void Previous(object sender, RoutedEventArgs e) => Main.Previous();
    private void Next(object sender, RoutedEventArgs e) => Main.Next();
}
