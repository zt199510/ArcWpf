using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class TextRevealPage : UserControl
{
    public TextRevealPage() => InitializeComponent();

    private void OnReplay(object sender, RoutedEventArgs e)
    {
        Title.Replay();
        Lead.Replay();
    }
}
