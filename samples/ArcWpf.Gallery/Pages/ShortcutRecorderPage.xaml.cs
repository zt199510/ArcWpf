using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages; public partial class ShortcutRecorderPage : UserControl { public ShortcutRecorderPage() { InitializeComponent(); } private void Changed(object sender, RoutedEventArgs e) => Status.Text = $"已记录：{Recorder.Shortcut}"; }
