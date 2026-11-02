using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class UndoToastPage : UserControl
{
    private int _count;
    public UndoToastPage() { InitializeComponent(); }
    private void Archive(object sender, RoutedEventArgs e) => Toast.Remove($"row-{++_count}", "archive");
    private void Undo(object sender, RoutedEventArgs e) => Toast.Undo();
}
