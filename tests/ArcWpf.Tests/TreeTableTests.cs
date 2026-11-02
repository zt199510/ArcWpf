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

    private static System.Windows.Controls.Button FindButton(DependencyObject root)
    {
        if (root is System.Windows.Controls.Button button) return button;
        for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            try { return FindButton(System.Windows.Media.VisualTreeHelper.GetChild(root, index)); }
            catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("No button.");
    }

    private static TreeTable Sample()
    {
        var table = new TreeTable { Width = 521.6, VerticalAlignment = VerticalAlignment.Top, Selectable = true };
        table.Columns.Add(new TreeTableColumn { Key = "Name", Header = "Name", Path = "Name" });
        table.Columns.Add(new TreeTableColumn { Key = "Count", Header = "Count", Path = "Count", Numeric = true, Width = new GridLength(96) });
        var root = new TreeTableRow { Id = "root", Data = new Item("Root", 3) };
        root.Children.Add(new TreeTableRow { Id = "b", Data = new Item("B", 2) });
        root.Children.Add(new TreeTableRow { Id = "a", Data = new Item("A", 1) });
        table.Rows.Add(root);
        table.ExpandedIds.Add("root");
        return table;
    }

    [TestMethod]
    public void RowsFollowArcGeometry() => Sta.Run(() =>
    {
        var table = Sample();
        var window = new Window { Content = table, Width = 620, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout();
            var body = (StackPanel)table.Template.FindName("PART_Body", table)!;
            Assert.HasCount(3, body.Children);
            foreach (FrameworkElement row in body.Children) Assert.AreEqual(44, row.ActualHeight, .01);
            Assert.AreEqual(2 + 40 + 3 * 44 + 6, table.ActualHeight, .51);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void CheckingAParentChecksChildrenAndPartialIsMixed() => Sta.Run(() =>
    {
        var table = Sample();
        var window = new Window { Content = table, Width = 620, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout();
            var body = (StackPanel)table.Template.FindName("PART_Body", table)!;
            ArcWpf.Controls.CheckBox CheckOf(int index) => (ArcWpf.Controls.CheckBox)((Grid)((Border)body.Children[index]).Child).Children[0];
            CheckOf(0).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, CheckOf(0)));
            CollectionAssert.AreEquivalent(new[] { "root", "a", "b" }, table.SelectedIds);
            CheckOf(1).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, CheckOf(1)));
            CollectionAssert.DoesNotContain(table.SelectedIds, "root");
            Assert.IsNull(CheckOf(0).IsChecked);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void SortStaysInsideSiblingsAndCyclesBackToNone() => Sta.Run(() =>
    {
        var table = Sample();
        table.SortColumn = "Name";
        var window = new Window { Content = table, Width = 620, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout();
            var body = (StackPanel)table.Template.FindName("PART_Body", table)!;
            CollectionAssert.AreEqual(new[] { "root", "a", "b" }, body.Children.Cast<FrameworkElement>().Select(row => ((TreeTableRow)row.DataContext).Id).ToList());
            table.SortDirection = TreeTableSortDirection.Descending; Sta.Pump();
            CollectionAssert.AreEqual(new[] { "root", "b", "a" }, body.Children.Cast<FrameworkElement>().Select(row => ((TreeTableRow)row.DataContext).Id).ToList());
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void LazyChildrenFailThenRetrySucceeds() => Sta.Run(() =>
    {
        var table = new TreeTable { Width = 521.6 };
        table.Columns.Add(new TreeTableColumn { Key = "Name", Header = "Name", Path = "Name" });
        table.Rows.Add(new TreeTableRow { Id = "lazy", Data = new Item("Lazy", 0), HasChildren = true });
        int calls = 0;
        table.LoadChildren = _ => ++calls == 1 ? Task.FromException<IEnumerable<TreeTableRow>>(new InvalidOperationException()) : Task.FromResult<IEnumerable<TreeTableRow>>([new TreeTableRow { Id = "kid", Data = new Item("Kid", 1) }]);
        var window = new Window { Content = table, Width = 620, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); table.UpdateLayout();
            var body = (StackPanel)table.Template.FindName("PART_Body", table)!;
            table.Toggle("lazy"); Sta.Pump(); Sta.Pump();
            Assert.AreEqual(1, calls);
            Assert.HasCount(2, body.Children);
            Assert.AreEqual("pending", ((FrameworkElement)body.Children[1]).Tag);
            var retry = FindButton(body.Children[1]);
            Assert.AreEqual("Retry", retry.Content);
            retry.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, retry)); Sta.Pump(); Sta.Pump();
            Assert.AreEqual(2, calls);
            Assert.HasCount(2, body.Children);
            Assert.AreEqual("kid", ((TreeTableRow)((FrameworkElement)body.Children[1]).DataContext).Id);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void GalleryPageConstructsWithTreeTable() => Sta.Run(() => Assert.IsNotNull(new ArcWpf.Gallery.Pages.TreeTablePage().Content));
    public sealed record Item(string Name, int Count);
}
