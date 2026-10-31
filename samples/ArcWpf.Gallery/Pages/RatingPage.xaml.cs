using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages; public partial class RatingPage : UserControl { public RatingPage() { InitializeComponent(); } private void Changed(object sender, RoutedEventArgs e) => Status.Text = $"{Rating.Value:0} / {Rating.Maximum}"; }
