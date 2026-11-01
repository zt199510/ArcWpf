using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class BottomSheetTests
{
    [TestMethod] public void BottomSheetDefaultsToBottomSide() => Sta.Run(() => Assert.AreEqual(DrawerSide.Bottom, new BottomSheet().Side));
    [TestMethod] public void SnapHeightIsBindable() => Sta.Run(() => { var sheet = new BottomSheet { SnapHeight = 520 }; Assert.AreEqual(520d, sheet.SnapHeight); });
    [TestMethod] public void CloseOptionsAreInherited() => Sta.Run(() => { var sheet = new BottomSheet { CloseOnEscape = true, CloseOnOverlayClick = true }; Assert.IsTrue(sheet.CloseOnEscape && sheet.CloseOnOverlayClick); });
}
