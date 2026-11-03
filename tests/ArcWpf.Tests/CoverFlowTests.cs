using System.Windows;
using System.Windows.Media;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class CoverFlowTests
{
    private const double Stage = 521.625;

    private static CoverFlowItem[] Items() => Enumerable.Range(0, 7).Select(i => new CoverFlowItem { Title = "T" + i, Subtitle = "S" + i, Image = new DrawingImage(new GeometryDrawing(Brushes.Teal, null, new RectangleGeometry(new Rect(0, 0, 40, 50)))) }).ToArray();

    private static void With(Action<CoverFlow> action) => Sta.Run(() =>
    {
        var flow = new CoverFlow { Width = Stage, ItemsSource = Items(), SelectedIndex = 2, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var window = new Window { Content = flow, Width = 700, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(flow); }
        finally { window.Close(); }
    });

    [TestMethod] public void PosesMatchTheMeasuredReference()
    {
        double card = CoverFlowMath.CardWidth(Stage);
        Assert.AreEqual(161.7, card, .01);
        Assert.AreEqual(202.13, CoverFlowMath.CardHeight(Stage), .01);
        Assert.AreEqual(549.79, CoverFlowMath.Perspective(Stage), .01);
        Assert.AreEqual(252.44, CoverFlowMath.StageHeight(Stage), .01);
        (double D, double X, double Z, double Veil)[] rows = [(1, 129.6, -61.56, .42), (2, 189.22, -84.24, .58), (3, 210.08, -106.92, .74), (4, 230.95, -106.92, .74)];
        foreach (var row in rows)
        {
            var pose = CoverFlowMath.Pose(row.D, card);
            Assert.AreEqual(row.X, pose.X, .03);
            Assert.AreEqual(row.Z, pose.Z, .03);
            Assert.AreEqual(.7071, pose.Sin, .001);
            Assert.AreEqual(row.Veil, pose.Veil, .001);
            Assert.AreEqual(.55, pose.Reflection, .001);
            Assert.AreEqual(.45, pose.Edge, .001);
            Assert.AreEqual(-row.X, CoverFlowMath.Pose(-row.D, card).X, .03);
            Assert.AreEqual(-.7071, CoverFlowMath.Pose(-row.D, card).Sin, .001);
        }
        var center = CoverFlowMath.Pose(0, card);
        Assert.AreEqual(0d, center.X);
        Assert.AreEqual(0d, center.Veil);
        Assert.AreEqual(1d, center.Reflection);
    }

    [TestMethod] public void FarCardsFadeBetweenTheSecondPlaceAndNearlyTheThird()
    {
        double card = CoverFlowMath.CardWidth(Stage);
        (double D, double Opacity)[] rows = [(1, 1), (2, 1), (2.172, .752), (2.287, .591), (2.407, .421), (2.594, .15), (2.713, 0), (4, 0)];
        foreach (var row in rows) { Assert.AreEqual(row.Opacity, CoverFlowMath.Pose(row.D, card).Opacity, .01, $"at {row.D}"); }
    }

    [TestMethod] public void MidDragPosesMatchTheReferenceMatrices()
    {
        double card = CoverFlowMath.CardWidth(Stage);
        var going = CoverFlowMath.Pose(-.37, card);
        Assert.AreEqual(-48d, going.X, .1);
        Assert.AreEqual(-.2868, going.Sin, .001);
        var coming = CoverFlowMath.Pose(.63, card);
        Assert.AreEqual(81.6, coming.X, .2);
        Assert.AreEqual(.4745, coming.Sin, .001);
    }

    [TestMethod] public void ProjectedBoxesMatchTheBrowsersRects()
    {
        // Reference: card rects from getBoundingClientRect, relative to the stage.
        (double D, double X, double Y, double W, double H)[] rows = [(0, 179.95, 8, 161.7, 202.13), (1, 320.37, 9.6, 125.72, 200.52), (2, 365.84, 17.48, 129.79, 192.64), (3, 378.57, 24.77, 127.31, 185.35), (-1, 75.51, 9.6, 125.73, 200.52), (-2, 25.98, 17.48, 129.8, 192.64)];
        foreach (var row in rows)
        {
            var box = CoverFlowMath.FaceBounds(row.D, Stage);
            Assert.AreEqual(row.X, box.X, .05, $"x at {row.D}");
            Assert.AreEqual(row.Y, box.Y, .05, $"y at {row.D}");
            Assert.AreEqual(row.W, box.Width, .05, $"w at {row.D}");
            Assert.AreEqual(row.H, box.Height, .05, $"h at {row.D}");
        }
    }

    [TestMethod] public void StackingFollowsDistance()
    {
        Assert.AreEqual(100, CoverFlowMath.ZIndex(0));
        Assert.AreEqual(90, CoverFlowMath.ZIndex(-1));
        Assert.AreEqual(96, CoverFlowMath.ZIndex(.37));
        Assert.AreEqual(66, CoverFlowMath.ZIndex(-3.37));
    }

    [TestMethod] public void ReleaseLandsWithinOneCardOfTheThrow()
    {
        Assert.AreEqual(3, CoverFlowMath.Landing(3.1, 0, 7));
        Assert.AreEqual(5, CoverFlowMath.Landing(4.1, 20, 7));
        Assert.AreEqual(3, CoverFlowMath.Landing(4.1, -20, 7));
        Assert.AreEqual(6, CoverFlowMath.Landing(6, 20, 7));
        Assert.AreEqual(0, CoverFlowMath.Landing(0, -20, 7));
    }

    [TestMethod] public void TheRailResistsPastItsEnds()
    {
        Assert.AreEqual(3d, CoverFlowMath.Resist(3, 7));
        Assert.IsGreaterThan(-1d, CoverFlowMath.Resist(-100, 7));
        Assert.IsLessThan(0d, CoverFlowMath.Resist(-.5, 7));
        Assert.IsLessThan(7d, CoverFlowMath.Resist(100, 7));
        Assert.IsGreaterThan(6d, CoverFlowMath.Resist(6.5, 7));
    }

    [TestMethod] public void SelectingGlidesAndUpdatesCaptionThumbAndAnnouncement() => With(flow =>
    {
        Assert.AreEqual(Stage, flow.StageWidthForTest, .01);
        Assert.AreEqual(2d, flow.PositionForTest, .01);
        Assert.AreEqual(1d, flow.CaptionOpacityForTest(2), .01);
        Assert.AreEqual(0d, flow.CaptionOpacityForTest(3), .01);
        Assert.AreEqual(32d, flow.ThumbXForTest, .01);
        Assert.AreEqual(100, flow.ZIndexForTest(2));

        flow.SelectedIndex = 3;
        flow.FinishMotionForTest();
        Assert.AreEqual(3d, flow.PositionForTest, .01);
        Assert.AreEqual(1d, flow.CaptionOpacityForTest(3), .01);
        Assert.AreEqual(48d, flow.ThumbXForTest, .01);
        Assert.AreEqual("T3, 4 of 7", flow.StatusForTest);
        Assert.AreEqual(90, flow.ZIndexForTest(2));
    });

    [TestMethod] public void IndexIsClampedToTheItems() => With(flow =>
    {
        flow.SelectedIndex = 40;
        Assert.AreEqual(6, flow.SelectedIndex);
    });

    [TestMethod] public void DraggingOneCardWidthMovesOnePlaceAndAThrowAddsAnother() => With(flow =>
    {
        double card = CoverFlowMath.CardWidth(Stage);
        flow.DownForTest(300, 0);
        flow.MoveForTest(300 - card * .37, 16);
        Assert.AreEqual(2.37, flow.PositionForTest, .01);
        flow.UpForTest(new Point(200, 100), 10000);
        flow.FinishMotionForTest();
        Assert.AreEqual(2, flow.SelectedIndex);

        flow.DownForTest(300, 20000);
        flow.MoveForTest(300 - card, 20040);
        flow.MoveForTest(300 - card * 1.2, 20060);
        flow.UpForTest(new Point(100, 100), 20070);
        flow.FinishMotionForTest();
        Assert.AreEqual(4, flow.SelectedIndex);
    });

    [TestMethod] public void ClickingASideCardGoesToItAndTheTopmostCardWins() => With(flow =>
    {
        Assert.AreEqual(2, flow.HitForTest(new Point(260, 100)));
        Assert.AreEqual(3, flow.HitForTest(new Point(390, 100)));
        Assert.IsNull(flow.HitForTest(new Point(260, 240)));
        flow.DownForTest(390, 0);
        flow.UpForTest(new Point(390, 100), 50);
        Assert.AreEqual(3, flow.SelectedIndex);
    });

    [TestMethod] public void UnprojectInvertsProject()
    {
        double card = CoverFlowMath.CardWidth(Stage);
        foreach (double distance in new[] { 0, .37, 1, -1, 2.5, -3 })
        {
            var pose = CoverFlowMath.Pose(distance, card);
            foreach (double local in new[] { -card / 2, -10, 0, 33, card / 2 })
            {
                var at = CoverFlowMath.Project(pose, Stage, local, 100);
                var (back, unscale) = CoverFlowMath.Unproject(pose, Stage, at.X);
                Assert.AreEqual(local, back, 1e-6);
                Assert.AreEqual(100, (CoverFlowMath.Floor(Stage) - at.Y) * unscale, 1e-6);
            }
        }
    }

    [TestMethod] public void TheComposedFrameShowsTheFaceVeilsTheSidesAndLeavesTheBackgroundClear() => With(flow =>
    {
        uint Blue(uint p) => p & 255;
        uint center = flow.PixelForTest(new Point(Stage / 2, 100));
        uint side = flow.PixelForTest(new Point(345, 100));
        Assert.AreEqual(255u, center >> 24);
        Assert.AreEqual(255u, side >> 24);
        // The veil alone gives the bright end; the edge shade darkens further toward the center, by up to 22.5%.
        double veiled = Blue(center) * (1 - .62 * .42);
        Assert.IsLessThanOrEqualTo(veiled + 1, Blue(side));
        Assert.IsGreaterThanOrEqualTo(veiled * (1 - .225) - 1, Blue(side));
        Assert.AreEqual(0u, flow.PixelForTest(new Point(4, 4)));
        // The reflection and shadow hang below the floor of the centered card.
        Assert.AreNotEqual(0u, flow.PixelForTest(new Point(Stage / 2, CoverFlowMath.Floor(Stage) + 5)));
        var pose = flow.PoseForTest(3);
        Assert.AreEqual(.7071, pose.Sin, .001);
        Assert.AreEqual(129.6, pose.X, .03);
    });

    [TestMethod] public void GlidingRedrawsTheFrame() => With(flow =>
    {
        var right = new Point(420, 100);
        Assert.AreEqual(255u, flow.PixelForTest(right) >> 24);
        flow.SelectedIndex = 6;
        flow.FinishMotionForTest();
        Assert.AreEqual(255u, flow.PixelForTest(new Point(Stage / 2, 100)) >> 24);
        Assert.AreEqual(0u, flow.PixelForTest(right));
    });
}
