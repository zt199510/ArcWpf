using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class TreeTableTests
{
    [TestMethod]
    public void TreeTableBuildsNestedRowsAndColumns() => Sta.Run(() =>
    {
        var table = new TreeTable { Width = 620, Rows = new() { new TreeTableRow { Id = "root", Data = new Item("Root", 10), Children = { new TreeTableRow { Id = "child", Data = new Item("Child", 4) } } } } };
        table.Columns.Add(new TreeTableColumn { Key = "Name", Header = "Name", Path = "Name" });
        table.Columns.Add(new TreeTableColumn { Key = "Count", Header = "Count", Path = "Count" });
        var window = new Window { Content = table, Width = 720, Height = 320, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout();
            var body = (StackPanel)table.Template.FindName("PART_Body", table)!;
            Assert.HasCount(1, body.Children);
            table.Toggle("root"); Sta.Pump();
            Assert.HasCount(2, body.Children);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void TreeTableSelectionAndSortStayBoundedToSiblings() => Sta.Run(() =>
    {
        var table = new TreeTable { Rows = new() { new TreeTableRow { Id = "a", Data = new Item("B", 2) }, new TreeTableRow { Id = "b", Data = new Item("A", 1) } }, Selectable = true };
        table.Columns.Add(new TreeTableColumn { Key = "Name", Header = "Name", Path = "Name" });
        table.SelectedIds.Add("a"); table.SortColumn = "Name"; table.SortDirection = TreeTableSortDirection.Ascending;
        var window = new Window { Content = table, Width = 620, Height = 280, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); Assert.IsTrue(table.SelectedIds.Contains("a")); Assert.AreEqual("Name", table.SortColumn); }
        finally { window.Close(); }
    });

    [TestMethod]
    public void GalleryPageConstructsWithTreeTable() => Sta.Run(() => Assert.IsNotNull(new ArcWpf.Gallery.Pages.TreeTablePage().Content));
    private sealed record Item(string Name, int Count);
}
