using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages; public partial class ColorPickerPage : UserControl { public ColorPickerPage() { InitializeComponent(); } private void Changed(object sender, RoutedEventArgs e) => Status.Text = $"颜色：{Picker.Value}"; }
