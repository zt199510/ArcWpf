using ArcWpf.Controls;
using System.Windows;
using System.Windows.Controls;
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
}
