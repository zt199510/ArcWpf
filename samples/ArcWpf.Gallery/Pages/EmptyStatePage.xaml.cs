using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class EmptyStatePage : UserControl
{
    // lucide folder-check, shown once the view exists (Arc empty-state demo).
    private static readonly Geometry FolderCheck = Geometry.Parse("M20,20 a2,2 0 0 0 2,-2 V8 a2,2 0 0 0 -2,-2 h-7.9 a2,2 0 0 1 -1.69,-0.9 L9.6,3.9 A2,2 0 0 0 7.93,3 H4 a2,2 0 0 0 -2,2 v13 a2,2 0 0 0 2,2 Z M9,13 l2,2 l4,-4");
    private bool _created;

    public EmptyStatePage() { InitializeComponent(); Apply(); }

    private void ToggleClick(object sender, RoutedEventArgs e) { _created = !_created; Apply(); }

    private void Apply()
    {
        State.Title = _created ? "View created" : "No saved views";
        State.Description = _created ? "Launch review is saved with your current filters." : "Create a view to keep your favorite filters close at hand.";
        State.Icon = _created ? FolderCheck : EmptyState.FolderIcon;
        Toggle.Variant = _created ? ButtonVariant.Secondary : ButtonVariant.Primary;
        Toggle.Content = _created ? "Undo" : "Create view";
    }
}
