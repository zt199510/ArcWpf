using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages; public partial class TagInputPage : UserControl { public TagInputPage() { InitializeComponent(); Tags.Tags.Add("Design"); Tags.Tags.Add("ArcWpf"); } private void TagAdded(object sender, RoutedEventArgs e) => Status.Text = "标签已添加"; private void TagRemoved(object sender, RoutedEventArgs e) => Status.Text = "标签已移除"; }
