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
}
