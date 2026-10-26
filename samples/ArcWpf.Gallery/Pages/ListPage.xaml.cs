using System.Collections.ObjectModel;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ListPage : UserControl
{
    public ListPage()
    {
        InitializeComponent();
        DemoList.ItemsSource = new ObservableCollection<ListItem>
        {
            new() { Id = "workspace", Title = "Workspace activity", Description = "Recent changes across your workspace" },
            new() { Id = "summary", Title = "A longer summary that wraps", Description = "Review the latest notes and updates from your team" },
            new() { Id = "archived", Title = "Archived projects", Description = "This item is disabled", IsEnabled = false }
        };
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectionText.Text = DemoList.SelectedItem is ListItem item ? $"已选择：{item.Title}" : "请选择一个项目";
    }
}
