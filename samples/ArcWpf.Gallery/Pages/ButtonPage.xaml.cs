using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ButtonPage : UserControl
{
    public ButtonPage() { InitializeComponent(); DataContext = new ButtonViewModel(); }
}
