using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ArcWpf;
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

    [TestMethod]
    public void LazyBranchOpensAsFolderLoadsOnceAndShowsItsChildren() => Sta.Run(() =>
    {
        var pending = new TaskCompletionSource<IEnumerable<TreeNode>>(); int calls = 0;
        var tree = Still(); var src = new TreeNode { Id = "src", Label = "src", HasLazyChildren = true }; tree.Nodes.Add(src);
        tree.ChildrenLoader = (_, _) => { calls++; return pending.Task; }; Layout(tree);
        var host = Host(tree); var row = FindButton(host, "src")!;
        Assert.AreEqual(Visibility.Visible, Named<FrameworkElement>(row, "Chevron").Visibility, "An unloaded lazy node must already look like a folder.");
        Assert.AreEqual(0, calls); Assert.AreEqual(TreeLoadState.NotLoaded, src.LoadState);

        row.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); tree.UpdateLayout();
        row = FindButton(host, "src")!;
        Assert.AreEqual(TreeLoadState.Loading, src.LoadState); Assert.AreEqual(1, calls); Assert.AreEqual(1, host.Items.Count);
        Assert.AreEqual(Visibility.Visible, Named<FrameworkElement>(row, "Spinner").Visibility); Assert.AreEqual(Visibility.Collapsed, Named<FrameworkElement>(row, "Chevron").Visibility);

        pending.SetResult([new TreeNode { Id = "a", Label = "a.cs" }, new TreeNode { Id = "b", Label = "b.cs" }]);
        PumpUntil(() => src.LoadState == TreeLoadState.Loaded); tree.UpdateLayout();
        Assert.AreEqual(3, host.Items.Count); Assert.IsNotNull(FindButton(host, "b.cs")); Assert.AreEqual(2, src.Children.Count);
        Assert.AreEqual(Visibility.Collapsed, Named<FrameworkElement>(FindButton(host, "src")!, "Spinner").Visibility);

        FindButton(host, "src")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); tree.UpdateLayout(); Assert.AreEqual(1, host.Items.Count);
        FindButton(host, "src")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); tree.UpdateLayout(); Assert.AreEqual(3, host.Items.Count);
        Assert.AreEqual(1, calls, "Loaded children are kept; reopening must not fetch again.");
    });

    [TestMethod]
    public void ClosingWhileLoadingCancelsAndDropsTheLateResult() => Sta.Run(() =>
    {
        var pending = new TaskCompletionSource<IEnumerable<TreeNode>>(); CancellationToken seen = default;
        var tree = Still(); var src = new TreeNode { Id = "src", Label = "src", HasLazyChildren = true }; tree.Nodes.Add(src);
        tree.ChildrenLoader = (_, token) => { seen = token; return pending.Task; }; Layout(tree); var host = Host(tree);
        FindButton(host, "src")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); tree.UpdateLayout(); Assert.AreEqual(TreeLoadState.Loading, src.LoadState);
        FindButton(host, "src")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); tree.UpdateLayout();
        Assert.IsTrue(seen.IsCancellationRequested); Assert.AreEqual(TreeLoadState.NotLoaded, src.LoadState);
        pending.SetResult([new TreeNode { Id = "late", Label = "late.cs" }]); Thread.Sleep(50); PumpUntil(() => false, 150); tree.UpdateLayout();
        Assert.AreEqual(0, src.Children.Count); Assert.AreEqual(1, host.Items.Count); Assert.AreEqual(TreeLoadState.NotLoaded, src.LoadState);
    });

    [TestMethod]
    public void FailedLoadClosesTheBranchReportsTheErrorAndRetriesOnReopen() => Sta.Run(() =>
    {
        int calls = 0; TreeLoadFailedEventArgs? failed = null; IReadOnlyCollection<string>? open = null;
        var tree = Still(); var src = new TreeNode { Id = "src", Label = "src", HasLazyChildren = true }; tree.Nodes.Add(src);
        tree.ChildrenLoader = (_, _) => ++calls == 1 ? Task.FromException<IEnumerable<TreeNode>>(new UnauthorizedAccessException("Access denied")) : Task.FromResult<IEnumerable<TreeNode>>([new TreeNode { Id = "a", Label = "a.cs" }]);
        tree.ChildrenLoadFailed += (_, e) => failed = e; tree.ExpandedChanged += (_, e) => open = e.ExpandedIds; Layout(tree); var host = Host(tree);
        FindButton(host, "src")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); PumpUntil(() => src.LoadState == TreeLoadState.Failed); tree.UpdateLayout();
        Assert.AreSame(src, failed?.Node); Assert.IsInstanceOfType<UnauthorizedAccessException>(failed!.Error); Assert.AreEqual("Access denied", src.LoadError);
        Assert.AreEqual(0, open!.Count, "A failed branch closes so it does not load again by itself."); Assert.AreEqual(1, host.Items.Count); Assert.AreEqual(1, calls);
        var row = FindButton(host, "src")!; Assert.AreEqual("Access denied", row.ToolTip); Assert.AreEqual("Access denied", AutomationProperties.GetHelpText(row));

        row.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); PumpUntil(() => src.LoadState == TreeLoadState.Loaded); tree.UpdateLayout();
        Assert.AreEqual(2, calls); Assert.AreEqual(2, host.Items.Count); Assert.IsNull(src.LoadError);
    });

    [TestMethod]
    public void RestoredOpenLazyBranchesLoadInTurnAndReloadKeepsRowsUntilTheNewOnesArrive() => Sta.Run(() =>
    {
        var order = new List<string>(); int generation = 0;
        var tree = Still(); tree.Nodes.Add(new TreeNode { Id = "src", Label = "src", HasLazyChildren = true }); tree.DefaultExpandedIds.Add("src"); tree.DefaultExpandedIds.Add("src/lib");
        tree.ChildrenLoader = (node, _) =>
        {
            order.Add(node.Id);
            IEnumerable<TreeNode> children = node.Id == "src" ? [new TreeNode { Id = "src/lib", Label = "lib", HasLazyChildren = true }] : [new TreeNode { Id = $"file{generation}", Label = $"file{generation}.cs" }];
            return Task.FromResult(children);
        };
        Layout(tree); var host = Host(tree); PumpUntil(() => host.Items.Count == 3); tree.UpdateLayout();
        CollectionAssert.AreEqual(new[] { "src", "src/lib" }, order); Assert.IsNotNull(FindButton(host, "file0.cs"));

        var gate = new TaskCompletionSource<IEnumerable<TreeNode>>(); tree.ChildrenLoader = (_, _) => gate.Task; tree.UpdateLayout();
        Assert.AreEqual(3, host.Items.Count, "Swapping the loader must not refetch branches that are already loaded.");
        var lib = tree.Nodes[0].Children[0]; tree.Reload(lib); tree.UpdateLayout();
        Assert.AreEqual(TreeLoadState.Loading, lib.LoadState); Assert.IsNotNull(FindButton(host, "file0.cs"), "Old rows stay while the reload runs.");
        gate.SetResult([new TreeNode { Id = "fresh", Label = "fresh.cs" }]); PumpUntil(() => lib.LoadState == TreeLoadState.Loaded); tree.UpdateLayout();
        Assert.IsNull(FindButton(host, "file0.cs")); Assert.IsNotNull(FindButton(host, "fresh.cs"));
    });

    [TestMethod]
    public void ChildrenAddedToAnOpenBranchAppearWithoutBeingAskedFor() => Sta.Run(() =>
    {
        var tree = Create(); var app = new TreeNode { Id = "app", Label = "app" }; app.Children.Add(new TreeNode { Id = "page", Label = "page.tsx" }); tree.Nodes.Add(app); tree.DefaultExpandedIds.Add("app"); Layout(tree);
        var host = Host(tree); Assert.AreEqual(2, host.Items.Count);
        for (int i = 0; i < 3; i++) app.Children.Add(new TreeNode { Id = $"new{i}", Label = $"new{i}.tsx" });
        Sta.Pump(); tree.UpdateLayout(); Assert.AreEqual(5, host.Items.Count); Assert.IsNotNull(FindButton(host, "new2.tsx"));
        app.Children.RemoveAt(0); Sta.Pump(); tree.UpdateLayout(); Assert.AreEqual(4, host.Items.Count); Assert.IsNull(FindButton(host, "page.tsx"));
    });

    [TestMethod]
    public void BoundedHeightBuildsOnlyTheRowsInViewAndKeepsTheScrollPositionAcrossSelection() => Sta.Run(() =>
    {
        var tree = Create(); for (int i = 0; i < 5000; i++) tree.Nodes.Add(new TreeNode { Id = $"n{i}", Label = $"file-{i}.cs" }); Layout(tree);
        var host = Host(tree); int built = Buttons(host).Count;
        Assert.AreEqual(5000, host.Items.Count); Assert.IsTrue(built is > 5 and < 40, $"Expected a viewport of rows, built {built}.");
        var scroll = (ScrollViewer)FindVisual(host, item => item is ScrollViewer)!;
        Assert.AreEqual(5000 * 38d, scroll.ExtentHeight, 1, "Pixel scrolling: the extent is every row's height.");
        scroll.ScrollToVerticalOffset(38d * 3000); Layout(tree);
        var row = FindButton(host, "file-3002.cs"); Assert.IsNotNull(row, "Rows at the new offset are built on demand.");
        row.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); Layout(tree);
        Assert.AreEqual("n3002", tree.SelectedNode?.Id); Assert.AreEqual(38d * 3000, scroll.VerticalOffset, 1, "Selecting rebuilds the rows; the view must not jump.");
        Assert.IsTrue(Buttons(host).Count < 40); Assert.IsNotNull(FindButton(host, "file-3002.cs"));
    });

    [TestMethod]
    public void EndKeyScrollsTheLastVirtualizedRowIntoViewAndFocusesIt() => Sta.Run(() =>
    {
        var tree = Create(); for (int i = 0; i < 2000; i++) tree.Nodes.Add(new TreeNode { Id = $"n{i}", Label = $"file-{i}.cs" });
        var window = new Window { Content = tree, Width = 360, Height = 320, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        window.Show();
        try
        {
            window.Activate(); Sta.Pump(); var host = Host(tree); var first = FindButton(host, "file-0.cs")!; first.Focus(); Sta.Pump();
            Assert.IsNull(FindButton(host, "file-1999.cs"));
            var current = (WpfButton)FindVisual(host, item => item is WpfButton { IsTabStop: true })!;
            current.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(current)!, 0, Key.End) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); Sta.Pump(); Sta.Pump();
            var last = FindButton(host, "file-1999.cs"); Assert.IsNotNull(last, "The last row must be built once End moves to it.");
            Assert.IsTrue(last.IsTabStop); Assert.IsTrue(last.IsKeyboardFocused, "Focus must land on the row that was scrolled in.");
            var scroll = (ScrollViewer)FindVisual(host, item => item is ScrollViewer)!; Assert.AreEqual(scroll.ScrollableHeight, scroll.VerticalOffset, 1);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void RightKeyStaysOnAnOpenFolderThatHasNoRowsYet() => Sta.Run(() =>
    {
        var tree = Still(); tree.Nodes.Add(new TreeNode { Id = "src", Label = "src", HasLazyChildren = true }); tree.Nodes.Add(new TreeNode { Id = "readme", Label = "README.md" });
        tree.ChildrenLoader = (_, _) => new TaskCompletionSource<IEnumerable<TreeNode>>().Task;
        var window = new Window { Content = tree, Width = 360, Height = 320, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        window.Show();
        try
        {
            window.Activate(); Sta.Pump(); var host = Host(tree); FindButton(host, "src")!.Focus(); Sta.Pump();
            for (int press = 0; press < 2; press++)
            {
                var row = FindButton(host, "src")!;
                row.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(row)!, 0, Key.Right) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); Sta.Pump();
            }
            Assert.AreEqual(TreeLoadState.Loading, tree.Nodes[0].LoadState); Assert.IsTrue(FindButton(host, "src")!.IsTabStop, "The sibling is not a child; Right must not move onto it."); Assert.IsFalse(FindButton(host, "README.md")!.IsTabStop);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void KeyboardToggleAndALateLoadLeaveFocusOnTheRow() => Sta.Run(() =>
    {
        var pending = new TaskCompletionSource<IEnumerable<TreeNode>>();
        var tree = Still(); var app = new TreeNode { Id = "app", Label = "app" }; app.Children.Add(new TreeNode { Id = "page", Label = "page.tsx" }); tree.Nodes.Add(app); tree.Nodes.Add(new TreeNode { Id = "src", Label = "src", HasLazyChildren = true });
        tree.DefaultExpandedIds.Add("app"); tree.ChildrenLoader = (_, _) => pending.Task;
        var window = new Window { Content = tree, Width = 360, Height = 320, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        window.Show();
        try
        {
            window.Activate(); Sta.Pump(); var host = Host(tree); FindButton(host, "app")!.Focus(); Sta.Pump();
            void Press(string label, Key key) { var row = FindButton(host, label)!; row.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(row)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); Sta.Pump(); }
            Press("app", Key.Left); Assert.AreEqual(2, host.Items.Count);
            Assert.IsTrue(FindButton(host, "app")!.IsKeyboardFocused, "Closing a folder rebuilds its row; the keyboard must stay on it.");
            Press("app", Key.Right); Assert.AreEqual(3, host.Items.Count); Assert.IsTrue(FindButton(host, "app")!.IsKeyboardFocused);
            Press("app", Key.End); Press("src", Key.Right); Assert.IsTrue(FindButton(host, "src")!.IsKeyboardFocused);
            pending.SetResult([new TreeNode { Id = "a", Label = "a.cs" }]); PumpUntil(() => host.Items.Count == 4); Sta.Pump();
            Assert.IsTrue(FindButton(host, "src")!.IsKeyboardFocused, "Rows arriving from a load must not take the keyboard away.");
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void SpinnerTurnsWhileLoadingUnlessMotionIsOff() => Sta.Run(() =>
    {
        var tree = Create(); Assert.IsTrue(tree.IsSpinning);
        ArcMotion.SetLevel(tree, ArcMotionLevel.Reduced); Assert.IsTrue(tree.IsSpinning);
        ArcMotion.SetLevel(tree, ArcMotionLevel.Off); Assert.IsFalse(tree.IsSpinning);
        ArcMotion.SetLevel(tree, ArcMotionLevel.Full); Assert.IsTrue(tree.IsSpinning);
    });

    [TestMethod]
    public void GalleryPageConstructsWithLazyAndLargeTrees() => Sta.Run(() =>
    {
        var page = new ArcWpf.Gallery.Pages.TreeViewPage(); ArcMotion.SetLevel(page, ArcMotionLevel.Off); page.Measure(new Size(800, 2000)); page.Arrange(new Rect(0, 0, 800, page.DesiredSize.Height)); page.UpdateLayout();
        var trees = Descendants(page).OfType<ArcTreeView>().ToList(); Assert.IsTrue(trees.Count >= 4);
        var large = (ArcTreeView)page.FindName("Large"); int built = Buttons(large).Count;
        Assert.AreEqual(10100, Host(large).Items.Count); Assert.IsTrue(built is > 5 and < 40, $"The large sample must be height-bounded so it virtualizes; built {built}.");
    });

    [TestMethod]
    public void OpeningAFolderGrowsAndStaggersItsRowsAndClosingFoldsThemBeforeTheyGo() => Sta.Run(() =>
    {
        var tree = Create(); var app = Folder("app", "page.tsx", "layout.tsx", "route.ts"); tree.Nodes.Add(app); tree.Nodes.Add(new TreeNode { Id = "readme", Label = "README.md" });
        Shown(tree, () =>
        {
            var host = Host(tree);
            FindButton(host, "app")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent));
            var first = Container(host, "page.tsx"); var third = Container(host, "route.ts");
            Assert.IsTrue(first.HasAnimatedProperties, "A mounting row opens its height on the smooth spring.");
            Assert.IsTrue(first.ClipToBounds, "The row clips only while its height moves.");
            Frame();
            Assert.IsTrue(first.ActualHeight < 38, $"The first frame starts the row short, got {first.ActualHeight}.");
            Assert.IsTrue(third.Opacity < 1, "The third child waits two stagger steps before it has faded in."); Assert.IsTrue(third.Opacity <= first.Opacity + .001, "Later rows trail earlier ones.");
            PumpUntil(() => !first.HasAnimatedProperties && !third.HasAnimatedProperties, 3000);
            Assert.AreEqual(38, first.ActualHeight, .5); Assert.AreEqual(1, third.Opacity, .001); Assert.IsFalse(first.ClipToBounds);
            Assert.IsTrue(double.IsNaN(first.Height), "The settled row is back on its own height.");

            FindButton(host, "app")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent));
            Assert.AreEqual(5, host.Items.Count, "Closing keeps the children until their exit has played.");
            Assert.IsFalse(first.IsHitTestVisible, "A leaving row takes no input.");
            Assert.IsFalse(FindButton(host, "page.tsx")!.IsTabStop, "A leaving row is no longer a stop in the tree.");
            PumpUntil(() => host.Items.Count == 2, 3000);
            Assert.AreEqual(2, host.Items.Count);
        });
    });

    [TestMethod]
    public void ReopeningAClosingFolderGrowsTheSameRowsBack() => Sta.Run(() =>
    {
        var tree = Create(); tree.Nodes.Add(Folder("app", "page.tsx", "layout.tsx")); tree.DefaultExpandedIds.Add("app");
        Shown(tree, () =>
        {
            var host = Host(tree); var page = Container(host, "page.tsx");
            FindButton(host, "app")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); Sta.Pump();
            FindButton(host, "app")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent));
            Assert.AreEqual(3, host.Items.Count, "The leaving rows are taken back, not doubled.");
            Assert.AreSame(page, Container(host, "page.tsx"), "The same container grows back.");
            Assert.IsTrue(page.IsHitTestVisible);
            PumpUntil(() => !page.HasAnimatedProperties, 3000); Sta.Pump();
            Assert.AreEqual(3, host.Items.Count); Assert.AreEqual(38, page.ActualHeight, .5); Assert.AreEqual(1, page.Opacity, .001);
        });
    });

    [TestMethod]
    public void SelectionGlidesFromTheOldRowAndSettlesOnTheNewOne() => Sta.Run(() =>
    {
        var tree = Create(); foreach (var name in new[] { "a.md", "b.md", "c.md" }) tree.Nodes.Add(new TreeNode { Id = name, Label = name });
        Shown(tree, () =>
        {
            var host = Host(tree);
            FindButton(host, "a.md")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); Sta.Pump();
            FindButton(host, "c.md")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent));
            var target = Container(host, "c.md"); var fill = Named<FrameworkElement>(FindButton(host, "c.md")!, "Selection");
            var slide = (TranslateTransform)fill.RenderTransform;
            Assert.IsTrue(slide.HasAnimatedProperties, "The new fill glides in."); Frame();
            Assert.IsTrue(slide.Y is < -30 and >= -76.5, $"The fill starts up to two rows up, where the old one was; got {slide.Y}.");
            Assert.AreEqual(-1, Panel.GetZIndex(target), "The gliding row passes under the other labels.");
            Assert.AreEqual(Visibility.Collapsed, Named<FrameworkElement>(FindButton(host, "a.md")!, "Selection").Visibility, "Only one fill is drawn.");
            PumpUntil(() => !slide.HasAnimatedProperties, 3000);
            Assert.AreEqual(0, slide.Y, .001); Assert.AreEqual(0, Panel.GetZIndex(target));
        });
    });

    [TestMethod]
    public void ChevronTurnsAndTheFolderGlyphSwapsThenRestOnTheOpenState() => Sta.Run(() =>
    {
        var tree = Create(); tree.Nodes.Add(Folder("app", "page.tsx"));
        Shown(tree, () =>
        {
            var host = Host(tree); var row = FindButton(host, "app")!;
            Assert.AreEqual(1, Named<FrameworkElement>(row, "Folder").Opacity); Assert.AreEqual(0, Named<FrameworkElement>(row, "FolderOpen").Opacity);
            row.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent));
            var turn = (RotateTransform)Named<FrameworkElement>(row, "Chevron").RenderTransform; var open = Named<FrameworkElement>(row, "FolderOpen");
            Assert.IsTrue(turn.HasAnimatedProperties, "The chevron turns on the snappy spring."); Assert.IsTrue(open.HasAnimatedProperties);
            Assert.IsInstanceOfType(open.Effect, typeof(System.Windows.Media.Effects.BlurEffect), "The open glyph sharpens out of a 2px blur.");
            PumpUntil(() => !turn.HasAnimatedProperties && !open.HasAnimatedProperties && open.Effect is null, 3000);
            Assert.AreEqual(90, turn.Angle, .001); Assert.AreEqual(1, open.Opacity, .001); Assert.AreEqual(0, Named<FrameworkElement>(row, "Folder").Opacity, .001);
            Assert.AreEqual(1, Named<FrameworkElement>(row, "LabelStrong").Opacity, .001, "The selected folder's label is foreground.");
            Assert.AreEqual(1, Named<FrameworkElement>(row, "FolderStrong").Opacity, .001);
        });
    });

    [TestMethod]
    public void ReducedMotionPlacesRowsAndStatesAtOnce() => Sta.Run(() =>
    {
        var tree = Create(); ArcMotion.SetLevel(tree, ArcMotionLevel.Reduced); tree.Nodes.Add(Folder("app", "page.tsx")); tree.Nodes.Add(new TreeNode { Id = "b", Label = "b.md" });
        Shown(tree, () =>
        {
            var host = Host(tree); var row = FindButton(host, "app")!;
            row.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); tree.UpdateLayout();
            Assert.AreEqual(3, host.Items.Count); Assert.IsFalse(Container(host, "page.tsx").HasAnimatedProperties);
            Assert.AreEqual(90, ((RotateTransform)Named<FrameworkElement>(row, "Chevron").RenderTransform).Angle); Assert.IsFalse(Named<FrameworkElement>(row, "Chevron").RenderTransform.HasAnimatedProperties);
            Assert.AreEqual(1, Named<FrameworkElement>(row, "LabelStrong").Opacity); Assert.IsFalse(Named<FrameworkElement>(row, "LabelStrong").HasAnimatedProperties);
            FindButton(host, "b.md")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); tree.UpdateLayout();
            Assert.IsFalse(Named<FrameworkElement>(FindButton(host, "b.md")!, "Selection").RenderTransform is TranslateTransform { Y: not 0 }, "Reduced motion moves the fill without gliding.");
            FindButton(host, "app")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent));
            Assert.AreEqual(2, host.Items.Count, "Reduced motion removes closed rows at once.");
        });
    });

    [TestMethod]
    public void DroppingToOffMidExitRemovesTheLeavingRowsAndStopsTheMotion() => Sta.Run(() =>
    {
        var tree = Create(); tree.Nodes.Add(Folder("app", "page.tsx", "layout.tsx")); tree.DefaultExpandedIds.Add("app");
        Shown(tree, () =>
        {
            var host = Host(tree);
            FindButton(host, "app")!.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); Assert.AreEqual(3, host.Items.Count);
            ArcMotion.SetLevel(tree, ArcMotionLevel.Off);
            Assert.AreEqual(1, host.Items.Count); var app = Container(host, "app");
            Assert.IsFalse(app.HasAnimatedProperties); Assert.IsFalse(Named<FrameworkElement>(FindButton(host, "app")!, "Chevron").RenderTransform.HasAnimatedProperties);
        });
    });

    [TestMethod]
    public void RowsAreFullWidthWithTheDepthIndentingOnlyTheirContent() => Sta.Run(() =>
    {
        var tree = Still(); tree.Nodes.Add(Folder("app", "page.tsx")); tree.DefaultExpandedIds.Add("app");
        Shown(tree, () =>
        {
            var host = Host(tree); var root = FindButton(host, "app")!; var child = FindButton(host, "page.tsx")!;
            Assert.AreEqual(root.ActualWidth, child.ActualWidth, .01, "tree-view.module.css .item is 100% wide at every depth.");
            Assert.AreEqual(root.TranslatePoint(new Point(), tree).X, child.TranslatePoint(new Point(), tree).X, .01);
            double Icon(WpfButton row) => Named<FrameworkElement>(row, "Glyphs").TranslatePoint(new Point(), row).X;
            Assert.AreEqual(18, Icon(child) - Icon(root), .01, "Each level indents the content 18.");
            Assert.AreEqual(10 + 14 + 8, Icon(root), .01, ".item padding 10, chevron 14, gap 8.");
            var branch = Named<FrameworkElement>(child, "Branch");
            Assert.AreEqual(Visibility.Visible, branch.Visibility); Assert.AreEqual(16, branch.TranslatePoint(new Point(), child).X, .01);
            Assert.AreEqual(Visibility.Collapsed, Named<FrameworkElement>(root, "Branch").Visibility);
            Assert.AreEqual(Application.Current.Resources["ArcWpf.Brush.TreeFileTypeScript"], ((System.Windows.Shapes.Path)Named<FrameworkElement>(child, "FilePath")).Stroke, "A .tsx file takes the TypeScript tone.");
        });
    });

    [TestMethod]
    public void AutomationSeesATreeOfItemsThatExpandAndSelect() => Sta.Run(() =>
    {
        var tree = Still(); tree.Nodes.Add(Folder("app", "page.tsx", "layout.tsx")); tree.Nodes.Add(new TreeNode { Id = "readme", Label = "README.md" });
        Shown(tree, () =>
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(tree)!;
            Assert.AreEqual(AutomationControlType.Tree, peer.GetAutomationControlType()); Assert.AreEqual("File tree", peer.GetName());
            var items = peer.GetChildren(); Assert.AreEqual(2, items.Count);
            var app = items[0]; Assert.AreEqual(AutomationControlType.TreeItem, app.GetAutomationControlType()); Assert.AreEqual("app", app.GetName());
            Assert.AreEqual(1, app.GetPositionInSet()); Assert.AreEqual(2, app.GetSizeOfSet());
            var expander = (IExpandCollapseProvider)app.GetPattern(PatternInterface.ExpandCollapse);
            Assert.AreEqual(ExpandCollapseState.Collapsed, expander.ExpandCollapseState);
            Assert.IsNull(items[1].GetPattern(PatternInterface.ExpandCollapse), "A file has nothing to expand.");
            // A UIA client is told through InvalidatePeer; with none listening the test refreshes the cache itself.
            expander.Expand(); tree.UpdateLayout(); Sta.Pump(); peer.ResetChildrenCache();
            Assert.AreEqual(ExpandCollapseState.Expanded, expander.ExpandCollapseState); Assert.AreEqual(4, peer.GetChildren().Count);
            var page = peer.GetChildren()[1]; Assert.AreEqual(1, page.GetPositionInSet()); Assert.AreEqual(2, page.GetSizeOfSet());
            ((ISelectionItemProvider)page.GetPattern(PatternInterface.SelectionItem)).Select(); Sta.Pump();
            Assert.AreEqual("page.tsx", tree.SelectedNode?.Id);
            var selection = (ISelectionProvider)peer.GetPattern(PatternInterface.Selection); Assert.IsFalse(selection.CanSelectMultiple); Assert.AreEqual(1, selection.GetSelection().Length);
            Assert.IsNull(app.GetPattern(PatternInterface.Invoke), "A tree item is not a button to screen readers.");
        });
    });

    private static TreeNode Folder(string id, params string[] files) { var folder = new TreeNode { Id = id, Label = id }; foreach (var file in files) folder.Children.Add(new TreeNode { Id = file, Label = file }); return folder; }
    private static FrameworkElement Container(ItemsControl host, string label) => (FrameworkElement)host.ItemContainerGenerator.ContainerFromItem(host.Items.Cast<TreeRow>().First(row => row.Node.Label == label));
    /// <summary>Lets the animation clock tick once, so animated values are the first frame's.</summary>
    private static void Frame()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    private static void Shown(ArcTreeView tree, Action body)
    {
        var window = new Window { Content = tree, Width = 360, Height = 400, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        window.Show();
        try { window.Activate(); Sta.Pump(); body(); }
        finally { window.Close(); }
    }

    // Motion Off keeps a lingering spinner from repainting forever, which would starve the idle pump these tests wait on.
    private static ArcTreeView Still() { var tree = Create(); ArcMotion.SetLevel(tree, ArcMotionLevel.Off); return tree; }
    private static ItemsControl Host(ArcTreeView tree) => (ItemsControl)tree.Template.FindName("PART_RowsHost", tree);
    private static List<WpfButton> Buttons(DependencyObject root) => Descendants(root).OfType<WpfButton>().Where(button => button.Tag is not null).ToList();
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root) { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var next in Descendants(child)) yield return next; } }
    private static T Named<T>(FrameworkElement root, string name) where T : FrameworkElement => (T)FindVisual(root, item => item is T element && element.Name == name)!;
    private static void PumpUntil(Func<bool> done, int milliseconds = 3000)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!done() && watch.ElapsedMilliseconds < milliseconds)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame); Thread.Sleep(5);
        }
    }

    private static ArcTreeView Create()
    {
        var tree = new ArcTreeView(); tree.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/TreeView/Themes/TreeViewTheme.xaml", UriKind.Relative) }); return tree;
    }
    private static void Layout(FrameworkElement element) { element.Measure(new Size(600, 400)); element.Arrange(new Rect(0, 0, 600, 400)); element.UpdateLayout(); Sta.Pump(); }
    private static WpfButton? FindButton(DependencyObject root, string label) => FindVisual(root, item => item is WpfButton button && button.Visibility == Visibility.Visible && AutomationProperties.GetName(button) == label) as WpfButton;
    private static DependencyObject? FindVisual(DependencyObject root, Func<DependencyObject, bool> match) { if (match(root)) return root; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) if (FindVisual(VisualTreeHelper.GetChild(root, i), match) is { } result) return result; return null; }
}
