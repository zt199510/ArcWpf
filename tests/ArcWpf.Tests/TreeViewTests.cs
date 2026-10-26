using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ArcWpf.Controls;
using WpfButton = System.Windows.Controls.Button;
using ArcTreeView = ArcWpf.Controls.TreeView;

namespace ArcWpf.Tests;

[TestClass]
public sealed class TreeViewTests
{
    [TestMethod]
    public void DefaultsFlattenOnlyVisibleNodes() => Sta.Run(() =>
    {
        var tree = Create(); var app = new TreeNode { Id = "app", Label = "app" }; app.Children.Add(new TreeNode { Id = "page", Label = "page.tsx" }); tree.Nodes.Add(app); tree.Nodes.Add(new TreeNode { Id = "readme", Label = "README.md" }); Layout(tree);
        var host = (ItemsControl)tree.Template.FindName("PART_RowsHost", tree);
        Assert.AreEqual(2, host.Items.Count); Assert.AreEqual("File tree", tree.Label); Assert.IsNull(tree.SelectedNode);
    });

    [TestMethod]
    public void DefaultExpandedIdsExposeChildren() => Sta.Run(() =>
    {
        var tree = Create(); var app = new TreeNode { Id = "app", Label = "app" }; app.Children.Add(new TreeNode { Id = "page", Label = "page.tsx" }); tree.Nodes.Add(app); tree.DefaultExpandedIds.Add("app"); Layout(tree);
        var host = (ItemsControl)tree.Template.FindName("PART_RowsHost", tree); Assert.AreEqual(2, host.Items.Count);
        Assert.IsNotNull(FindButton(host, "page.tsx"));
    });

    [TestMethod]
    public void ClickingRowSelectsAndTogglesFolder() => Sta.Run(() =>
    {
        var tree = Create(); var app = new TreeNode { Id = "app", Label = "app" }; app.Children.Add(new TreeNode { Id = "page", Label = "page.tsx" }); tree.Nodes.Add(app); TreeNode? selected = null; tree.Selected += (_, e) => selected = e.Node; Layout(tree);
        FindButton((ItemsControl)tree.Template.FindName("PART_RowsHost", tree), "app")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); Sta.Pump();
        Assert.AreSame(app, selected); Assert.AreSame(app, tree.SelectedNode); Assert.AreEqual(2, ((ItemsControl)tree.Template.FindName("PART_RowsHost", tree)).Items.Count);
    });

    [TestMethod]
    public void DisabledRowsDoNotSelect() => Sta.Run(() =>
    {
        var tree = Create(); tree.Nodes.Add(new TreeNode { Id = "disabled", Label = "Disabled", IsEnabled = false }); Layout(tree); FindButton((ItemsControl)tree.Template.FindName("PART_RowsHost", tree), "Disabled")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); Assert.IsNull(tree.SelectedNode);
    });

    private static ArcTreeView Create()
    {
        var tree = new ArcTreeView(); tree.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/TreeView/Themes/TreeViewTheme.xaml", UriKind.Relative) }); return tree;
    }
    private static void Layout(FrameworkElement element) { element.Measure(new Size(600, 400)); element.Arrange(new Rect(0, 0, 600, 400)); element.UpdateLayout(); Sta.Pump(); }
    private static WpfButton? FindButton(DependencyObject root, string label) => FindVisual(root, item => item is WpfButton button && button.Visibility == Visibility.Visible && AutomationProperties.GetName(button) == label) as WpfButton;
    private static DependencyObject? FindVisual(DependencyObject root, Func<DependencyObject, bool> match) { if (match(root)) return root; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) if (FindVisual(VisualTreeHelper.GetChild(root, i), match) is { } result) return result; return null; }
}
