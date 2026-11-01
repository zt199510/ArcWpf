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
            splitter.RaiseEvent(new DragDeltaEventArgs(-20, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            var offset = (TranslateTransform)splitter.RenderTransform;
            Assert.IsTrue(offset.HasAnimatedProperties);
            Assert.AreEqual(160d, grid.ColumnDefinitions[0].ActualWidth, .5);
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
}
