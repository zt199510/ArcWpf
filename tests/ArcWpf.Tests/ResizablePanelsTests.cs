using ArcWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
namespace ArcWpf.Tests;
[TestClass] public sealed class ResizablePanelsTests
{
    [TestMethod] public void SplitRatioAndMinimumsAreBindable() => Sta.Run(() => { var panels = new ResizablePanels { SplitRatio = .4, MinLeft = 120, MinRight = 180 }; Assert.AreEqual(.4, panels.SplitRatio, .001); Assert.AreEqual(120d, panels.MinLeft); });
    [TestMethod] public void ContentPropertiesAreBindable() => Sta.Run(() => { var panels = new ResizablePanels { LeftContent = "Left", RightContent = "Right" }; Assert.AreEqual("Left", panels.LeftContent); Assert.AreEqual("Right", panels.RightContent); });
    [TestMethod] public void DefaultsAreStable() => Sta.Run(() => Assert.AreEqual(.5, new ResizablePanels().SplitRatio, .001));
    [TestMethod] public void RatioChangesMeasuredColumns() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 600, Height = 160, SplitRatio = .3, MinLeft = 100, MinRight = 100 };
        var window = new Window { Content = panels, Width = 660, Height = 220, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            Assert.AreEqual(.3, grid.ColumnDefinitions[0].ActualWidth / (grid.ActualWidth - 6), .03);
            panels.SplitRatio = .7; panels.UpdateLayout();
            Assert.AreEqual(.7, grid.ColumnDefinitions[0].ActualWidth / (grid.ActualWidth - 6), .03);
            panels.SplitRatio = 0; panels.UpdateLayout();
            Assert.IsGreaterThanOrEqualTo(100, grid.ColumnDefinitions[0].ActualWidth);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void BoundaryDragReboundsWithoutChangingMinimum() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 420, Height = 160, SplitRatio = 0, MinLeft = 160, MinRight = 160 };
        var window = new Window { Content = panels, Width = 500, Height = 220, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            var splitter = (GridSplitter)panels.Template.FindName("PART_Splitter", panels)!;
            Assert.AreEqual(160d, grid.ColumnDefinitions[0].ActualWidth, .5);
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            splitter.RaiseEvent(new DragDeltaEventArgs(-20, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            var offset = (TranslateTransform)splitter.RenderTransform;
            Assert.IsTrue(offset.X < 0);
            Assert.AreEqual(160d, grid.ColumnDefinitions[0].ActualWidth, .5);
            splitter.RaiseEvent(new DragCompletedEventArgs(-20, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            Assert.IsTrue(offset.HasAnimatedProperties);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual(0d, offset.X, .01);
            Assert.IsFalse(offset.HasAnimatedProperties);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void KeyboardResizeUpdatesBoundRatio() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 500, Height = 160, SplitRatio = .5, MinLeft = 100, MinRight = 100 };
        var window = new Window { Content = panels, Width = 560, Height = 220, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var splitter = (GridSplitter)panels.Template.FindName("PART_Splitter", panels)!;
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            splitter.Focus();
            var source = PresentationSource.FromVisual(splitter)!;
            splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Right) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Sta.Pump(); panels.UpdateLayout();
            Assert.AreEqual(grid.ColumnDefinitions[0].ActualWidth / (grid.ActualWidth - 6), panels.SplitRatio, .02);
            Assert.IsGreaterThan(.5, panels.SplitRatio);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void CollapseRestoreAndResetKeepRatioAndContentHitState() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 500, Height = 160, SplitRatio = .3, DefaultSplitRatio = .6, MinLeft = 100, MinRight = 100, CanCollapseLeft = true, LeftContent = "Folders", RightContent = "Messages" };
        ResizablePanelsLayoutChangedEventArgs? change = null;
        panels.LayoutChanged += (_, e) => change = e;
        var window = new Window { Content = panels, Width = 560, Height = 220, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            var left = (ContentPresenter)panels.Template.FindName("PART_LeftPresenter", panels)!;
            var restore = (System.Windows.Controls.Button)panels.Template.FindName("PART_RestoreButton", panels)!;
            var splitter = (GridSplitter)panels.Template.FindName("PART_Splitter", panels)!;
            panels.IsLeftCollapsed = true; panels.UpdateLayout();
            Assert.AreEqual(0d, grid.ColumnDefinitions[0].ActualWidth, .5);
            Assert.AreEqual(Visibility.Collapsed, left.Visibility);
            Assert.AreEqual(Visibility.Visible, restore.Visibility);
            Assert.AreEqual(.3, panels.SplitRatio, .001);
            Assert.IsNotNull(change);
            Assert.AreEqual(0d, change.Shares[0], .001);
            restore.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, restore)); panels.UpdateLayout();
            Assert.IsFalse(panels.IsLeftCollapsed);
            Assert.AreEqual(Visibility.Visible, left.Visibility);
            Assert.AreEqual(.3, panels.SplitRatio, .001);
            splitter.Focus();
            var source = PresentationSource.FromVisual(splitter)!;
            splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Home) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Left) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.IsTrue(panels.IsLeftCollapsed);
            splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.IsFalse(panels.IsLeftCollapsed);
            splitter.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = System.Windows.Controls.Control.MouseDoubleClickEvent });
            panels.UpdateLayout();
            Assert.AreEqual(.6, panels.SplitRatio, .001);
            Assert.AreEqual(60d, panels.Shares[0], .001);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void DragPastMinimumCollapsesConfiguredPane() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 420, Height = 160, SplitRatio = 0, MinLeft = 100, MinRight = 100, CanCollapseLeft = true };
        var window = new Window { Content = panels, Width = 500, Height = 220, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var splitter = (GridSplitter)panels.Template.FindName("PART_Splitter", panels)!;
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            splitter.RaiseEvent(new DragDeltaEventArgs(-60, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            splitter.RaiseEvent(new DragCompletedEventArgs(-60, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            panels.UpdateLayout();
            Assert.IsTrue(panels.IsLeftCollapsed);
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            Assert.AreEqual(0d, grid.ColumnDefinitions[0].ActualWidth, .5);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void NarrowHostScalesBothMinimumsInsteadOfFavoringLeft() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 206, Height = 120, MinLeft = 160, MinRight = 160 };
        var window = new Window { Content = panels, Width = 300, Height = 180, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            Assert.AreEqual(100d, grid.ColumnDefinitions[0].ActualWidth, 1);
            Assert.AreEqual(100d, grid.ColumnDefinitions[2].ActualWidth, 1);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void ThreePanelsResizeCollapseRestoreAndResetAdjacentPair() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 780, Height = 220, Label = "Mail" };
        panels.Panels.Add(new ResizablePanel { Id = "folders", Label = "Folders", DefaultSize = 22, MinSize = 160, Collapsible = true, Content = "Folders" });
        panels.Panels.Add(new ResizablePanel { Id = "messages", Label = "Messages", DefaultSize = 45, MinSize = 120, Content = "Messages" });
        panels.Panels.Add(new ResizablePanel { Id = "preview", Label = "Preview", DefaultSize = 33, MinSize = 100, Content = "Preview" });
        var window = new Window { Content = panels, Width = 850, Height = 280, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            Assert.HasCount(5, grid.ColumnDefinitions);
            var splitters = grid.Children.OfType<GridSplitter>().ToArray();
            Assert.HasCount(2, splitters);
            double third = grid.ColumnDefinitions[4].ActualWidth;
            double first = grid.ColumnDefinitions[0].ActualWidth;
            splitters[0].Focus();
            var source = PresentationSource.FromVisual(splitters[0])!;
            splitters[0].RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Right) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            panels.UpdateLayout();
            Assert.IsTrue(grid.ColumnDefinitions[0].ActualWidth > first + 10);
            Assert.AreEqual(third, grid.ColumnDefinitions[4].ActualWidth, 1);
            splitters[0].RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            panels.UpdateLayout();
            Assert.IsTrue(panels.Panels[0].IsCollapsed);
            Assert.AreEqual(0d, grid.ColumnDefinitions[0].ActualWidth, .5);
            Assert.AreEqual(Visibility.Collapsed, grid.Children.OfType<Border>().First(host => ReferenceEquals(host.Child, panels.Panels[0])).Visibility);
            var restore = grid.Children.OfType<System.Windows.Controls.Button>().Single(button => button.Visibility == Visibility.Visible);
            restore.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, restore));
            panels.UpdateLayout();
            Assert.IsFalse(panels.Panels[0].IsCollapsed);
            splitters[0].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = System.Windows.Controls.Control.MouseDoubleClickEvent });
            panels.UpdateLayout();
            Assert.AreEqual(.22, grid.ColumnDefinitions[0].ActualWidth / (grid.ActualWidth - 12), .03);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void DefaultCollapsedPanelRestoresConfiguredShare() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 780, Height = 180 };
        panels.Panels.Add(new ResizablePanel { Id = "folders", Label = "Folders", DefaultSize = 22, MinSize = 160, Collapsible = true, DefaultCollapsed = true, Content = "Folders" });
        panels.Panels.Add(new ResizablePanel { Id = "messages", Label = "Messages", DefaultSize = 45, Content = "Messages" });
        panels.Panels.Add(new ResizablePanel { Id = "preview", Label = "Preview", DefaultSize = 33, Content = "Preview" });
        var window = new Window { Content = panels, Width = 850, Height = 250, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            Assert.IsTrue(panels.Panels[0].IsCollapsed);
            Assert.AreEqual(0d, grid.ColumnDefinitions[0].ActualWidth, .5);
            var restore = grid.Children.OfType<System.Windows.Controls.Button>().First(button => button.Visibility == Visibility.Visible);
            restore.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, restore)); panels.UpdateLayout();
            Assert.IsFalse(panels.Panels[0].IsCollapsed);
            Assert.AreEqual(.22, grid.ColumnDefinitions[0].ActualWidth / (grid.ActualWidth - 12), .05);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void ThreeNarrowPanelsScaleAllMinimums() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 206, Height = 160 };
        for (int index = 0; index < 3; index++) panels.Panels.Add(new ResizablePanel { Id = $"pane-{index}", Label = $"Pane {index}", DefaultSize = 1, MinSize = 160, Content = $"Pane {index}" });
        var window = new Window { Content = panels, Width = 290, Height = 230, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            Assert.AreEqual(194d / 3, grid.ColumnDefinitions[0].ActualWidth, 1);
            Assert.AreEqual(194d / 3, grid.ColumnDefinitions[2].ActualWidth, 1);
            Assert.AreEqual(194d / 3, grid.ColumnDefinitions[4].ActualWidth, 1);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void SecondDividerDragKeepsFirstPaneAndPublishesShares() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 780, Height = 200 };
        panels.Panels.Add(new ResizablePanel { Id = "one", Label = "One", DefaultSize = 30, MinSize = 80, Content = "One" });
        panels.Panels.Add(new ResizablePanel { Id = "two", Label = "Two", DefaultSize = 40, MinSize = 80, Content = "Two" });
        panels.Panels.Add(new ResizablePanel { Id = "three", Label = "Three", DefaultSize = 30, MinSize = 80, Content = "Three" });
        ResizablePanelsLayoutChangedEventArgs? change = null;
        panels.LayoutChanged += (_, e) => change = e;
        var window = new Window { Content = panels, Width = 850, Height = 270, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            var divider = grid.Children.OfType<GridSplitter>().ElementAt(1);
            double first = grid.ColumnDefinitions[0].ActualWidth;
            double second = grid.ColumnDefinitions[2].ActualWidth;
            double third = grid.ColumnDefinitions[4].ActualWidth;
            divider.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            divider.RaiseEvent(new DragDeltaEventArgs(40, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            divider.RaiseEvent(new DragCompletedEventArgs(40, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            panels.UpdateLayout();
            Assert.AreEqual(first, grid.ColumnDefinitions[0].ActualWidth, 1);
            Assert.IsTrue(grid.ColumnDefinitions[2].ActualWidth > second + 20);
            Assert.IsTrue(grid.ColumnDefinitions[4].ActualWidth < third - 20);
            Assert.IsNotNull(change);
            Assert.HasCount(3, change.Shares);
            Assert.AreEqual(100d, change.Shares.Sum(), 1);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void MultiDividerOvershootCollapsesAndCanReturnToLegacyTemplate() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 780, Height = 180, LeftContent = "Legacy left", RightContent = "Legacy right" };
        panels.Panels.Add(new ResizablePanel { Id = "folders", Label = "Folders", DefaultSize = 22, MinSize = 160, Collapsible = true, Content = "Folders" });
        panels.Panels.Add(new ResizablePanel { Id = "messages", Label = "Messages", DefaultSize = 78, MinSize = 80, Content = "Messages" });
        var window = new Window { Content = panels, Width = 850, Height = 250, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); panels.UpdateLayout();
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            var splitter = grid.Children.OfType<GridSplitter>().Single();
            Assert.IsNotNull(splitter.Template);
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            splitter.RaiseEvent(new DragDeltaEventArgs(-100, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            Assert.IsTrue(((TranslateTransform)splitter.RenderTransform).X < 0);
            splitter.RaiseEvent(new DragCompletedEventArgs(-100, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            panels.UpdateLayout();
            Assert.IsTrue(panels.Panels[0].IsCollapsed);
            Assert.AreEqual(0d, grid.ColumnDefinitions[0].ActualWidth, .5);
            panels.Panels.Clear(); panels.UpdateLayout();
            Assert.HasCount(3, grid.ColumnDefinitions);
            Assert.AreEqual("Legacy left", ((ContentPresenter)panels.Template.FindName("PART_LeftPresenter", panels)!).Content);
            Assert.AreEqual("Legacy right", ((ContentPresenter)panels.Template.FindName("PART_RightPresenter", panels)!).Content);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void ReapplyingMultiTemplateKeepsPanelInstancesAndLayout() => Sta.Run(() =>
    {
        var panels = new ResizablePanels { Width = 520, Height = 160 };
        var first = new ResizablePanel { Id = "first", Label = "First", DefaultSize = 40, Content = new Border() };
        var second = new ResizablePanel { Id = "second", Label = "Second", DefaultSize = 60, Content = new Border() };
        first.Visibility = Visibility.Hidden;
        second.IsHitTestVisible = false;
        panels.Panels.Add(first); panels.Panels.Add(second);
        var window = new Window { Content = panels, Width = 590, Height = 230, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var original = panels.Template;
            panels.Template = null;
            panels.ApplyTemplate();
            panels.Template = original;
            panels.ApplyTemplate(); Sta.Pump(); panels.UpdateLayout();
            var grid = (Grid)panels.Template.FindName("PART_Layout", panels)!;
            Assert.IsTrue(grid.Children.OfType<Border>().Any(host => ReferenceEquals(host.Child, first)));
            Assert.IsTrue(grid.Children.OfType<Border>().Any(host => ReferenceEquals(host.Child, second)));
            Assert.AreEqual(.4, grid.ColumnDefinitions[0].ActualWidth / (grid.ActualWidth - 6), .03);
            Assert.AreEqual(Visibility.Hidden, first.Visibility);
            Assert.IsFalse(second.IsHitTestVisible);
        }
        finally { window.Close(); }
    });
}
