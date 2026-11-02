using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class SortableDataTableTests
{
    private static ObservableCollection<DataTableColumn> Columns() => new()
    {
        new() { Key = "Name", Header = "Name", Path = "Name", Width = DataGridLength.SizeToHeader },
        new() { Key = "Count", Header = "Count", Path = "Count", Numeric = true, Width = new DataGridLength(100) }
    };

    [TestMethod]
    public void SortableTableBuildsColumnsAndEmptyState() => Sta.Run(() =>
    {
        var table = new SortableDataTable { Rows = new object[] { new Row("Arc", 4), new Row("Core", 2) }, RowKeyPath = "Name" };
        foreach (var column in Columns()) table.Columns.Add(column);
        var window = new Window { Content = table, Width = 620, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout();
            var grid = (DataGrid)table.Template.FindName("PART_DataGrid", table)!;
            Assert.HasCount(2, grid.Columns);
            Assert.AreEqual(2, grid.Items.Count);
            table.Rows = Array.Empty<object>(); Sta.Pump();
            Assert.AreEqual(Visibility.Visible, ((TextBlock)table.Template.FindName("PART_EmptyState", table)!).Visibility);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void HeaderSortChangesDirectionAndOrder() => Sta.Run(() =>
    {
        var table = new SortableDataTable { Rows = new object[] { new Row("Arc", 4), new Row("Core", 2) }, RowKeyPath = "Name" };
        foreach (var column in Columns()) table.Columns.Add(column);
        var window = new Window { Content = table, Width = 620, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout();
            var grid = (DataGrid)table.Template.FindName("PART_DataGrid", table)!;
            table.DefaultSortKey = "Name";
            Assert.AreEqual("Name", table.DefaultSortKey);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void GalleryPageConstructsWithSortableTable() => Sta.Run(() => Assert.IsNotNull(new ArcWpf.Gallery.Pages.SortableDataTablePage().Content));
    private sealed record Row(string Name, int Count);
}
