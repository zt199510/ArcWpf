using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class FilterToolbarPage : UserControl
{
    public FilterToolbarPage() { InitializeComponent(); Toolbar.Filters.Add(new FilterToolbarFilter { Label = "Status", Value = "Open" }); Toolbar.Filters.Add(new FilterToolbarFilter { Label = "Owner", Value = "Me" }); Toolbar.HasActiveFilters = true; }
    private void QueryChanged(object sender, RoutedEventArgs e) => Status.Text = $"搜索：{Toolbar.Query}";
    private void FiltersCleared(object sender, RoutedEventArgs e) => Status.Text = "筛选已清除";
}
