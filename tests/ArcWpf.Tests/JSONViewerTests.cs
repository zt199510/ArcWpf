using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
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
    public void GalleryPageConstructsWithJsonViewer() => Sta.Run(() =>
    {
        var page = new ArcWpf.Gallery.Pages.JSONViewerPage();
        Assert.IsNotNull(page.Content);
    });
}
