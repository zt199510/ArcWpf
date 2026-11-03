using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class ImageCompareTests
{
    private static void With(Action<ImageCompare> action) => Sta.Run(() =>
    {
        var compare = new ImageCompare { Width = 520, Before = new Border(), After = new Border(), AspectRatio = 1.5, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var window = new Window { Content = compare, Width = 600, Height = 500, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(compare); }
        finally { window.Close(); }
    });

    [TestMethod] public void RubberBandNeverReachesItsLimit()
    {
        Assert.AreEqual(0d, ImageCompareMath.Rubber(0));
        Assert.IsLessThan(40d, ImageCompareMath.Rubber(10000, 40));
        Assert.AreEqual(-ImageCompareMath.Rubber(30, 40), ImageCompareMath.Rubber(-30, 40), 1e-9);
    }

    [TestMethod] public void FadeReachesFullAt28()
    {
        Assert.AreEqual(0d, ImageCompareMath.Fade(0));
        Assert.AreEqual(1d, ImageCompareMath.Fade(28));
        Assert.AreEqual(1d, ImageCompareMath.Fade(400));
    }

    [TestMethod] public void HalfPlaneAtTheCenterCutsTheFrameInTwo()
    {
        var points = ImageCompareMath.HalfPlane(100, 100, new Point(50, 50), 0);
        Assert.IsGreaterThanOrEqualTo(3, points.Count);
        double area = 0;
        for (int i = 0; i < points.Count; i++) { var a = points[i]; var b = points[(i + 1) % points.Count]; area += a.X * b.Y - b.X * a.Y; }
        Assert.AreEqual(5000d, Math.Abs(area) / 2, 1);
    }

    [TestMethod] public void DraggingSetsPositionAndReleasingEndsTheDrag() => With(compare =>
    {
        // The handle keeps the grab offset, so a press off its center doesn't jump.
        compare.PressForTest(new Point(250, 100), true);
        Assert.IsTrue(compare.DraggingForTest);
        compare.MoveForTest(new Point(146, 100));
        compare.ReleaseForTest(new Point(146, 100));
        compare.FinishForTest();
        Sta.Pump();
        Assert.IsFalse(compare.DraggingForTest);
        Assert.AreEqual(30d, compare.Position, 1);
    });

    [TestMethod] public void HandleReportsTheShareShowingAfter() => With(compare =>
    {
        compare.Position = 30;
        Sta.Pump();
        Assert.AreEqual(70d, compare.HandleForTest.Value, 1e-6);
    });

    [TestMethod] public void TurningToVerticalMovesTheAngle() => With(compare =>
    {
        double before = compare.ThetaForTest;
        compare.Orientation = Orientation.Vertical;
        Sta.Pump();
        compare.FinishForTest();
        Assert.AreNotEqual(before, compare.ThetaForTest);
    });
}
