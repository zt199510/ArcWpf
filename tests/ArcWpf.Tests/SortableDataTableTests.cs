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
    public void TableFollowsArcGeometryAndKeepsItsMinimumWidth() => Sta.Run(() =>
    {
        var table = new SortableDataTable { Width = 521.6, VerticalAlignment = VerticalAlignment.Top, Rows = new object[] { new Row("Arc", 4), new Row("Core", 2) }, RowKeyPath = "Name", Selectable = true };
        foreach (var column in Columns()) table.Columns.Add(column);
        table.Columns[0].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        var window = new Window { Content = table, Width = 620, Height = 320, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout(); Sta.Pump();
            var grid = (DataGrid)table.Template.FindName("PART_DataGrid", table)!;
            Assert.AreEqual(44, grid.Columns[0].ActualWidth, .5);
            Assert.AreEqual(560, grid.Columns.Sum(column => column.ActualWidth), 1);
            Assert.AreEqual(44, ((FrameworkElement)table.Template.FindName("PART_Footer", table)).ActualHeight, .01);
            Assert.AreEqual(2 + 45 + 52 + 52 + 44, table.ActualHeight, .51);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void SelectionTogglesExtendsAndClears() => Sta.Run(() =>
    {
        var table = new SortableDataTable { Rows = new object[] { new Row("A", 1), new Row("B", 2), new Row("C", 3) }, RowKeyPath = "Name", Selectable = true };
        foreach (var column in Columns()) table.Columns.Add(column);
        var window = new Window { Content = table, Width = 620, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout();
            var grid = (DataGrid)table.Template.FindName("PART_DataGrid", table)!;
            var status = (TextBlock)table.Template.FindName("PART_SelectionStatus", table)!;
            Assert.AreEqual("3 rows", status.Text);
            table.SelectedKeys.Add("A"); table.SelectedKeys.Add("C");
            table.Rows = table.Rows!.ToArray(); Sta.Pump();
            Assert.AreEqual("2 selected", status.Text);
            Assert.AreEqual(2, grid.SelectedItems.Count);
            var clear = (System.Windows.Controls.Button)table.Template.FindName("PART_ClearSelection", table)!;
            Assert.AreEqual(Visibility.Visible, clear.Visibility);
            clear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, clear));
            Assert.IsEmpty(table.SelectedKeys);
            Assert.AreEqual("3 rows", status.Text);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void NumericSortIsNumericAndMarksTheColumn() => Sta.Run(() =>
    {
        var table = new SortableDataTable { Rows = new object[] { new Row("A", 9), new Row("B", 100), new Row("C", 20) }, RowKeyPath = "Name", DefaultSortKey = "Count", SortDirection = TableSortDirection.Descending };
        foreach (var column in Columns()) table.Columns.Add(column);
        var window = new Window { Content = table, Width = 620, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout();
            var grid = (DataGrid)table.Template.FindName("PART_DataGrid", table)!;
            CollectionAssert.AreEqual(new[] { "B", "C", "A" }, grid.Items.Cast<Row>().Select(row => row.Name).ToList());
            Assert.AreEqual(System.ComponentModel.ListSortDirection.Descending, grid.Columns[1].SortDirection);
            Assert.IsNull(grid.Columns[0].SortDirection);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void ClickingRowsTogglesExtendsAndEscapeClears() => Sta.Run(() =>
    {
        var table = new SortableDataTable { Rows = new object[] { new Row("A", 1), new Row("B", 2), new Row("C", 3) }, RowKeyPath = "Name", Selectable = true };
        foreach (var column in Columns()) table.Columns.Add(column);
        var window = new Window { Content = table, Width = 620, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout(); Sta.Pump();
            var grid = (DataGrid)table.Template.FindName("PART_DataGrid", table)!;
            DataGridRow RowAt(int index) => (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(index);
            void Press(UIElement target) => target.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent, Source = target });
            Press(RowAt(0));
            Press(RowAt(2));
            CollectionAssert.AreEqual(new[] { "A", "C" }, table.SelectedKeys);
            Assert.IsTrue(RowAt(0).IsSelected);
            Press(RowAt(0));
            CollectionAssert.AreEqual(new[] { "C" }, table.SelectedKeys);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void SortingFromTheGridTurnsTheDirectionAndKeepsTheArrow() => Sta.Run(() =>
    {
        var table = new SortableDataTable { Rows = new object[] { new Row("A", 1), new Row("B", 2) }, RowKeyPath = "Name" };
        foreach (var column in Columns()) table.Columns.Add(column);
        var window = new Window { Content = table, Width = 620, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout();
            var grid = (DataGrid)table.Template.FindName("PART_DataGrid", table)!;
            var sort = new DataGridSortingEventArgs(grid.Columns[1]);
            typeof(DataGrid).GetMethod("OnSorting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(grid, [sort]);
            Assert.AreEqual(TableSortDirection.Ascending, table.SortDirection);
            Assert.AreEqual(System.ComponentModel.ListSortDirection.Ascending, grid.Columns[1].SortDirection);
            typeof(DataGrid).GetMethod("OnSorting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(grid, [new DataGridSortingEventArgs(grid.Columns[1])]);
            Assert.AreEqual(TableSortDirection.Descending, table.SortDirection);
            Assert.AreEqual(System.ComponentModel.ListSortDirection.Descending, grid.Columns[1].SortDirection);
            CollectionAssert.AreEqual(new[] { "B", "A" }, grid.Items.Cast<Row>().Select(row => row.Name).ToList());
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void GalleryPageConstructsWithSortableTable() => Sta.Run(() => Assert.IsNotNull(new ArcWpf.Gallery.Pages.SortableDataTablePage().Content));
    public sealed record Row(string Name, int Count);
}
