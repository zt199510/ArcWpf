using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages; public partial class ChipGroupPage : UserControl { public ChipGroupPage() { InitializeComponent(); } private void Changed(object sender, RoutedEventArgs e) => Status.Text = $"已选择：{Group.SelectedItems.Count}"; }
