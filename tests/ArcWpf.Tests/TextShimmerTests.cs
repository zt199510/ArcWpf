using System.Windows;
using System.Windows.Media;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class TextShimmerTests
{
    private static void Run(Action<TextShimmer> action) => Sta.Run(() =>
    {
        var shimmer = new TextShimmer { Text = "Generating summary", FontSize = 18, FontWeight = FontWeights.Medium, LineRatio = 1.4, HorizontalAlignment = HorizontalAlignment.Left };
        var window = new Window { Content = shimmer, Width = 700, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(shimmer); } finally { window.Close(); }
    });

    private static double Luma(Color c) => c.R * .2126 + c.G * .7152 + c.B * .0722;

    [TestMethod]
    public void TheBandIsBrightestAtItsCentre() => Run(shimmer =>
    {
        shimmer.SetSweepForTest(.5);
        double centre = shimmer.WidthForTest / 2;
        var mid = shimmer.BandColorAtForTest(centre);
        var far = shimmer.BandColorAtForTest(centre + 2.2 * 18 + 2);
        Assert.IsTrue(Luma(mid) < Luma(far) - 40, "the highlight is the text color, darker than the base on a light theme");
        Assert.AreEqual(shimmer.BaseForTest, far);
    });

    [TestMethod]
    public void BandClearsTheTextAtBothEndsOfTheSweep() => Run(shimmer =>
    {
        shimmer.SetSweepForTest(0);
        Assert.AreEqual(shimmer.BaseForTest, shimmer.BandColorAtForTest(0));
        shimmer.SetSweepForTest(1);
        Assert.AreEqual(shimmer.BaseForTest, shimmer.BandColorAtForTest(shimmer.WidthForTest));
    });

    [TestMethod]
    public void FinishingSettlesToTheSolidTextColor() => Run(shimmer =>
    {
        shimmer.IsActive = false;
        shimmer.FinishMotionForTest();
        Assert.AreEqual(1, shimmer.SettleForTest, .001);
        Assert.AreEqual(((SolidColorBrush)shimmer.Foreground).Color, shimmer.BaseForTest);
    });

    [TestMethod]
    public void ANewLabelCrossesOverAndTheWidthSprings() => Run(shimmer =>
    {
        double before = shimmer.WidthForTest;
        shimmer.Text = "Done";
        Assert.AreEqual("Generating summary", shimmer.LeavingForTest);
        Assert.AreEqual(before, shimmer.WidthForTest, .01);
        Assert.IsTrue(shimmer.WidthTargetForTest < before);
        shimmer.FinishMotionForTest();
        Assert.IsNull(shimmer.LeavingForTest);
        Assert.AreEqual(shimmer.WidthTargetForTest, shimmer.WidthForTest, .01);
    });

    [TestMethod]
    public void ReducedMotionShowsTheSteadyBase() => Run(shimmer =>
    {
        ArcMotion.SetLevel(shimmer, ArcMotionLevel.Reduced);
        shimmer.SetSweepForTest(.5);
        shimmer.Duration = 2;
        Assert.AreEqual(0, shimmer.SweepForTest, .001);
    });

    [TestMethod]
    public void BusyStateIsExposed() => Run(shimmer =>
    {
        Assert.AreEqual("Busy", System.Windows.Automation.AutomationProperties.GetItemStatus(shimmer));
        shimmer.IsActive = false;
        Assert.AreEqual("", System.Windows.Automation.AutomationProperties.GetItemStatus(shimmer));
    });
}
