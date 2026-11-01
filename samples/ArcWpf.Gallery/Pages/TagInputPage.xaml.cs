using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class TagInputPage : UserControl
{
    public TagInputPage() { InitializeComponent(); Tags.Tags = ["release", "web"]; }
}
