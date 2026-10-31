using System.Collections.ObjectModel;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ReorderableListPage : UserControl
{
    public ReorderableListPage()
    {
        InitializeComponent();
        List.ItemsSource = new ObservableCollection<ReorderableListItem>
        {
            new() { Id = "one", Title = "Clarify onboarding", Detail = "Product brief" },
            new() { Id = "two", Title = "Review empty states", Detail = "Design system" },
            new() { Id = "three", Title = "Ship the update", Detail = "Release checklist" },
        };
    }

    private void Moved(object sender, ReorderableListItemMovedEventArgs e) => Status.Text = $"{((ReorderableListItem)e.Item).Title}: {e.OldIndex + 1} → {e.NewIndex + 1}";
}
