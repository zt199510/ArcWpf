using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ResizablePanelsTests
{
    [TestMethod] public void SplitRatioAndMinimumsAreBindable() => Sta.Run(() => { var panels = new ResizablePanels { SplitRatio = .4, MinLeft = 120, MinRight = 180 }; Assert.AreEqual(.4, panels.SplitRatio, .001); Assert.AreEqual(120d, panels.MinLeft); });
    [TestMethod] public void ContentPropertiesAreBindable() => Sta.Run(() => { var panels = new ResizablePanels { LeftContent = "Left", RightContent = "Right" }; Assert.AreEqual("Left", panels.LeftContent); Assert.AreEqual("Right", panels.RightContent); });
    [TestMethod] public void DefaultsAreStable() => Sta.Run(() => Assert.AreEqual(.5, new ResizablePanels().SplitRatio, .001));
}
