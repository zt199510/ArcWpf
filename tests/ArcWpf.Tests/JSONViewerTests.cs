using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ArcWpf.Controls;
using ArcTreeView = ArcWpf.Controls.TreeView;

namespace ArcWpf.Tests;

[TestClass]
public sealed class JSONViewerTests
{
    [TestMethod]
    public void DefaultsExposeSearchAndCopyContract() => Sta.Run(() =>
    {
        var viewer = new JSONViewer { Data = new Dictionary<string, object?> { ["name"] = "Arc", ["count"] = 2 } };
        Assert.AreEqual("root", viewer.RootName);
        Assert.IsTrue(viewer.Searchable);
        Assert.IsTrue(viewer.Copyable);
        Assert.AreEqual(50, viewer.PageSize);
    });

    [TestMethod]
    public void WindowBuildsTreeAndCopiesSelectedPathAndValue() => Sta.Run(() =>
    {
        var viewer = new JSONViewer { Width = 520, Data = new Dictionary<string, object?> { ["user"] = new Dictionary<string, object?> { ["name"] = "Arc" } } };
        var window = new Window { Content = viewer, Width = 620, Height = 380, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); viewer.UpdateLayout();
            var tree = (ArcTreeView)viewer.Template.FindName("PART_Tree", viewer)!;
            Assert.HasCount(1, tree.Nodes);
            var root = tree.Nodes[0];
            Assert.HasCount(1, root.Children);
            viewer.CopyPath();
            Assert.AreEqual("root", Clipboard.GetText());
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void SearchExpandsMatchingAncestors() => Sta.Run(() =>
    {
        var viewer = new JSONViewer { Width = 520, Data = new Dictionary<string, object?> { ["user"] = new Dictionary<string, object?> { ["name"] = "Arc" } } };
        var window = new Window { Content = viewer, Width = 620, Height = 380, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); viewer.Query = "Arc"; viewer.UpdateLayout();
            var tree = (ArcTreeView)viewer.Template.FindName("PART_Tree", viewer)!;
            Assert.IsTrue(tree.DefaultExpandedIds.Contains("root"));
            Assert.IsTrue(tree.DefaultExpandedIds.Contains("root.user"));
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void ChromeFollowsArcGeometry() => Sta.Run(() =>
    {
        var viewer = new JSONViewer { Width = 521.6, VerticalAlignment = VerticalAlignment.Top, MaxHeight = 380, DefaultExpandDepth = 2, Data = LongData() };
        var window = new Window { Content = viewer, Width = 620, Height = 620, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); viewer.UpdateLayout();
            Assert.AreEqual(472, viewer.ActualHeight, .51);
            Assert.AreEqual(47, ((FrameworkElement)viewer.Template.FindName("PART_Toolbar", viewer)).ActualHeight, .01);
            Assert.AreEqual(43, ((FrameworkElement)viewer.Template.FindName("PART_PathBar", viewer)).ActualHeight, .01);
            Assert.AreEqual(380, ((FrameworkElement)viewer.Template.FindName("PART_Tree", viewer)).ActualHeight, .01);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void LongBranchesPageAndSearchReachesPastThePage() => Sta.Run(() =>
    {
        var viewer = new JSONViewer { Width = 520, DefaultExpandDepth = 2, PageSize = 5, Data = LongData() };
        var window = new Window { Content = viewer, Width = 620, Height = 620, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); viewer.UpdateLayout();
            var tree = (ArcTreeView)viewer.Template.FindName("PART_Tree", viewer)!;
            var list = tree.Nodes[0].Children.Single(node => node.Id == "root.list");
            Assert.HasCount(6, list.Children);
            Assert.AreEqual("root.list::more", list.Children[5].Id);
            viewer.Query = "value 12";
            list = tree.Nodes[0].Children.Single(node => node.Id == "root.list");
            Assert.IsTrue(list.Children.Any(node => node.Id == "root.list[12]"));
            Assert.AreEqual("root.list[12]", viewer.SelectedPath);
            viewer.Query = string.Empty;
            Assert.HasCount(6, tree.Nodes[0].Children.Single(node => node.Id == "root.list").Children);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void PathsFollowArcRulesAndCollapseAllKeepsTheRootOpen() => Sta.Run(() =>
    {
        var viewer = new JSONViewer { Width = 520, DefaultExpandDepth = 3, Data = new Dictionary<string, object?> { ["gift wrap"] = true, ["a"] = new List<object?> { 1 } } };
        var window = new Window { Content = viewer, Width = 620, Height = 420, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); viewer.UpdateLayout();
            var tree = (ArcTreeView)viewer.Template.FindName("PART_Tree", viewer)!;
            Assert.AreEqual("root[\"gift wrap\"]", tree.Nodes[0].Children[0].Id);
            Assert.AreEqual("root.a[0]", tree.Nodes[0].Children[1].Children[0].Id);
            viewer.CollapseAll();
            CollectionAssert.AreEquivalent(new[] { "root" }, viewer.ExpandedIds);
            Assert.AreEqual("root", viewer.SelectedPath);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void ClickingARowTogglesAndTheCopyActionDoesNot() => Sta.Run(() =>
    {
        var viewer = new JSONViewer { Width = 520, Data = new Dictionary<string, object?> { ["user"] = new Dictionary<string, object?> { ["name"] = "Arc" } } };
        var window = new Window { Content = viewer, Width = 620, Height = 420, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); viewer.UpdateLayout();
            var tree = (ArcTreeView)viewer.Template.FindName("PART_Tree", viewer)!;
            Press(RowButton(tree, "root.user")); Sta.Pump();
            Assert.IsTrue(viewer.ExpandedIds.Contains("root.user"));
            Assert.AreEqual("root.user", viewer.SelectedPath);
            var copy = Descendants(RowButton(tree, "root.user.name")).OfType<JsonActionButton>().First(action => action.Kind == "path");
            Press(copy);
            Assert.IsTrue(viewer.ExpandedIds.Contains("root.user"));
            Assert.AreEqual("root.user", viewer.SelectedPath);
            copy.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, copy));
            Assert.AreEqual("root.user.name", Clipboard.GetText());
            Assert.AreEqual("done", copy.State);
            Assert.IsTrue(viewer.ExpandedIds.Contains("root.user"));
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void ScrollPositionSurvivesARebuild() => Sta.Run(() =>
    {
        var viewer = new JSONViewer { Width = 520, MaxHeight = 200, DefaultExpandDepth = 2, Data = LongData() };
        var window = new Window { Content = viewer, Width = 620, Height = 420, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); viewer.UpdateLayout();
            var tree = (ArcTreeView)viewer.Template.FindName("PART_Tree", viewer)!;
            var scroll = Descendants(tree).OfType<ScrollViewer>().First();
            scroll.ScrollToVerticalOffset(300); Sta.Pump();
            double before = scroll.VerticalOffset;
            Assert.IsGreaterThan(200, before);
            viewer.Copyable = false; Sta.Pump();
            Assert.AreEqual(before, scroll.VerticalOffset, 2);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void JsonElementNormalizesToTheSameTree() => Sta.Run(() =>
    {
        using var document = System.Text.Json.JsonDocument.Parse("{\"a\":[1,true,null],\"b c\":\"x\"}");
        var viewer = new JSONViewer { Width = 520, DefaultExpandDepth = 2, Data = document.RootElement.Clone() };
        var window = new Window { Content = viewer, Width = 620, Height = 420, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); viewer.UpdateLayout();
            var root = ((ArcTreeView)viewer.Template.FindName("PART_Tree", viewer)!).Nodes[0];
            Assert.HasCount(2, root.Children);
            Assert.HasCount(3, root.Children[0].Children);
            Assert.AreEqual("root[\"b c\"]", root.Children[1].Id);
        }
        finally { window.Close(); }
    });

    private static void Press(UIElement target) => target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent, Source = target });
    private static System.Windows.Controls.Button RowButton(ArcTreeView tree, string id) => Descendants(tree).OfType<System.Windows.Controls.Button>().First(button => button.Tag is not null && button.DataContext?.GetType().Name == "TreeRow" && ((dynamic)button.DataContext).Node.Id == id);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static Dictionary<string, object?> LongData() => new() { ["list"] = Enumerable.Range(0, 40).Select(index => (object?)$"value {index}").ToList() };

    [TestMethod]
    public void GalleryPageConstructsWithJsonViewer() => Sta.Run(() =>
    {
        var page = new ArcWpf.Gallery.Pages.JSONViewerPage();
        Assert.IsNotNull(page.Content);
    });
}
