using ArcWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace ArcWpf.Tests;
[TestClass] public sealed class BottomSheetTests
{
    [TestMethod] public void BottomSheetDefaultsToBottomSide() => Sta.Run(() => Assert.AreEqual(DrawerSide.Bottom, new BottomSheet().Side));
    [TestMethod] public void SnapHeightIsBindable() => Sta.Run(() => { var sheet = new BottomSheet { SnapHeight = 520 }; Assert.AreEqual(520d, sheet.SnapHeight); });
    [TestMethod] public void CloseOptionsAreInherited() => Sta.Run(() => { var sheet = new BottomSheet { CloseOnEscape = true, CloseOnOverlayClick = true }; Assert.IsTrue(sheet.CloseOnEscape && sheet.CloseOnOverlayClick); });
    [TestMethod] public void SnapHeightControlsSurfaceAndClampsToHost() => Sta.Run(() =>
    {
        var sheet = new BottomSheet { Title = "Filters", SnapHeight = 420, Content = new TextBlock { Text = "Categories" } };
        var host = new DrawerHost { Width = 700, Height = 600, Content = new Border(), Drawer = sheet };
        var window = new Window { Content = host, Width = 780, Height = 680, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); sheet.IsOpen = true; Sta.Pump(); host.UpdateLayout();
            var surface = (FrameworkElement)sheet.Template.FindName("PART_Surface", sheet)!;
            Assert.AreEqual(420d, surface.ActualHeight, 1);
            sheet.SnapHeight = 300; host.UpdateLayout();
            Assert.AreEqual(300d, surface.ActualHeight, 1);
            host.Height = 220; host.UpdateLayout();
            Assert.AreEqual(220d, surface.ActualHeight, 1);
            ArcMotion.SetLevel(sheet, ArcMotionLevel.Off);
            sheet.Close();
            Assert.IsFalse(sheet.IsPresented);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void DefaultDetentsSetPeekGeometryAndGrabberKeyboardMovesBetweenStops() => Sta.Run(() =>
    {
        var sheet = new BottomSheet { Title = "Trip details", Content = new TextBlock { Text = "Flights and hotel" } };
        ArcMotion.SetLevel(sheet, ArcMotionLevel.Off);
        var host = new DrawerHost { Width = 800, Height = 600, Content = new Border(), Drawer = sheet };
        var window = new Window { Content = host, Width = 880, Height = 680, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); sheet.IsOpen = true; Sta.Pump(); host.UpdateLayout();
            var surface = (FrameworkElement)sheet.Template.FindName("PART_Surface", sheet)!;
            var overlay = (Border)sheet.Template.FindName("PART_Overlay", sheet)!;
            var grabber = (System.Windows.Controls.Button)sheet.Template.FindName("PART_Grabber", sheet)!;
            var move = (TranslateTransform)surface.RenderTransform;
            sheet.FocusInitial();
            Assert.IsTrue(surface.IsKeyboardFocused);
            Assert.AreEqual(576d, surface.ActualWidth, 1);
            Assert.AreEqual(552d, surface.ActualHeight, 1);
            Assert.AreEqual(282d, move.Y, 1);
            Assert.AreEqual(.78d, overlay.Opacity, .02);
            Assert.AreEqual(0, sheet.CurrentDetent);
            Assert.IsFalse(sheet.IsExpanded);
            int notifications = 0;
            sheet.CurrentDetentChanged += (_, _) => notifications++;
            var source = PresentationSource.FromVisual(grabber)!;
            grabber.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Up) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.AreEqual(1, sheet.CurrentDetent);
            Assert.IsTrue(sheet.IsExpanded);
            Assert.AreEqual(0d, move.Y, 1);
            Assert.AreEqual(1d, overlay.Opacity, .02);
            Assert.AreEqual(1, notifications);
            grabber.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, grabber));
            Assert.AreEqual(0, sheet.CurrentDetent);
            sheet.Close();
            Assert.IsFalse(sheet.IsPresented);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void DragProjectsToNextDetentAndDismissesBelowPeek() => Sta.Run(() =>
    {
        var sheet = new BottomSheet { Title = "Trip details" };
        ArcMotion.SetLevel(sheet, ArcMotionLevel.Off);
        var host = new DrawerHost { Width = 700, Height = 600, Content = new Border(), Drawer = sheet };
        var window = new Window { Content = host, Width = 780, Height = 680, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); sheet.IsOpen = true; Sta.Pump();
            sheet.BeginDrag(0); sheet.MoveDrag(-180); sheet.EndDrag();
            Assert.AreEqual(1, sheet.CurrentDetent);
            sheet.BeginDrag(0); sheet.MoveDrag(800); sheet.EndDrag();
            Assert.IsFalse(sheet.IsOpen);
            Assert.IsFalse(sheet.IsPresented);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void FullMotionCanBeInterruptedByOffAndGalleryConstructs() => Sta.Run(() =>
    {
        var page = new ArcWpf.Gallery.Pages.BottomSheetPage();
        Assert.IsNotNull(page.Content);
        var sheet = new BottomSheet { Title = "Trip details" };
        ArcMotion.SetLevel(sheet, ArcMotionLevel.Full);
        var host = new DrawerHost { Width = 700, Height = 600, Content = new Border(), Drawer = sheet };
        var window = new Window { Content = host, Width = 780, Height = 680, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); sheet.IsOpen = true; Sta.Pump();
            Assert.IsTrue(sheet.IsSheetMotionRunning);
            sheet.MoveToDetent(1);
            var surface = (FrameworkElement)sheet.Template.FindName("PART_Surface", sheet)!;
            var move = (TranslateTransform)surface.RenderTransform;
            ArcMotion.SetLevel(sheet, ArcMotionLevel.Off);
            Assert.IsFalse(sheet.IsSheetMotionRunning);
            Assert.AreEqual(0d, move.Y, 1);
            sheet.Close();
            Assert.IsFalse(sheet.IsPresented);
        }
        finally { window.Close(); }
    });
}
