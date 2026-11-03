using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class CardStackTests
{
    private static readonly object[] Cards = ["a", "b", "c", "d", "e", "f"];

    private static void With(Action<CardStack> action) => Sta.Run(() =>
    {
        var stack = new CardStack { Width = 320, ItemsSource = Cards, LeftLabel = "Skip", RightLabel = "Keep", RightOutcome = "kept", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var window = new Window { Content = stack, Width = 700, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(stack); }
        finally { window.Close(); }
    });

    [TestMethod] public void ResistanceIsFreeThenStretchesAndInverts()
    {
        Assert.AreEqual(30d, CardStackMath.ResistY(30), 1e-9);
        Assert.IsLessThan(48d + 140, CardStackMath.ResistY(100000));
        Assert.AreEqual(-CardStackMath.ResistY(200), CardStackMath.ResistY(-200), 1e-9);
        Assert.AreEqual(200d, CardStackMath.UnresistY(CardStackMath.ResistY(200)), 1e-6);
    }

    [TestMethod] public void ReleaseDecidesOnAFlickOrFarDragButNotOnAFlickBack()
    {
        Assert.IsTrue(CardStackMath.Decides(20, 600, 320));
        Assert.IsFalse(CardStackMath.Decides(10, 600, 320));
        Assert.IsTrue(CardStackMath.Decides(113, 0, 320));
        Assert.IsFalse(CardStackMath.Decides(111, 0, 320));
        Assert.IsFalse(CardStackMath.Decides(120, -400, 320));
        Assert.IsTrue(CardStackMath.Decides(141, 0, 1000));
        Assert.IsFalse(CardStackMath.Decides(0, 900, 320));
    }

    [TestMethod] public void VelocityUsesOnlyTheLast80Milliseconds()
    {
        (int, double, double)[] samples = [(0, 0, 0), (100, 500, 0), (140, 540, 0), (180, 580, 0)];
        Assert.AreEqual(1000d, CardStackMath.Velocity(samples, 180).X, 1e-6);
        Assert.AreEqual(default, CardStackMath.Velocity(samples, 300));
    }

    [TestMethod] public void ExitLeavesTheChosenWayEvenFromASteepThrow()
    {
        var exit = CardStackMath.Exit(1, 0, 0, new Vector(50, -2000), 320);
        Assert.IsGreaterThan(0d, exit.X);
        var reach = Math.Sqrt(exit.X * exit.X + exit.Y * exit.Y);
        Assert.AreEqual(320 * 1.6, reach, 1);
    }

    [TestMethod] public void RestingStackMatchesReferenceSlots() => With(stack =>
    {
        Assert.AreEqual(4, stack.RenderedCountForTest);
        Assert.AreEqual(328d, stack.StageForTest!.ActualHeight, .01);
        Assert.AreEqual(1d, stack.CardForTest("b").Depth, .01);
        Assert.IsTrue(stack.CardForTest("a").Top);
        Assert.IsFalse(stack.CardForTest("b").Top);
        Assert.AreEqual(4d, stack.CardForTest("a").ZIndex);
        Assert.AreEqual(3d, stack.CardForTest("b").ZIndex);
        // Reference: the second card is scaled .95 from its bottom edge and lowered 12, so its box starts at 8,27.2.
        var slot = (FrameworkElement)VisualTreeHelper(stack, "b");
        var at = slot.TranslatePoint(new Point(), stack.StageForTest);
        Assert.AreEqual(8d, at.X, .01);
        Assert.AreEqual(27.2, at.Y, .01);
    });

    private static object VisualTreeHelper(CardStack stack, object item) => typeof(CardStack).GetField("_cards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(stack) is System.Collections.IDictionary map ? map[item]!.GetType().GetProperty("Slot")!.GetValue(map[item])! : throw new InvalidOperationException();

    [TestMethod] public void DraggingTiltsByGrabHalfShowsTheStampAndLiftsTheStack() => With(stack =>
    {
        stack.DownForTest(new Point(160, 60), 0);
        stack.MoveForTest(new Point(240, 60), 16);
        var top = stack.CardForTest("a");
        Assert.AreEqual(80d, top.X, .01);
        Assert.AreEqual(80 * .065, top.Rotation, .01);
        Assert.AreEqual(.8, top.Keep, .01);
        Assert.AreEqual(0d, top.Pass, .01);
        Assert.AreEqual(.5, stack.LiftForTest, .01);
        stack.UpForTest(10000);
        stack.FinishMotionForTest();
        Assert.AreEqual(0d, stack.CardForTest("a").X, .01);
        Assert.AreEqual(0, stack.Position);

        stack.DownForTest(new Point(160, 250), 20000);
        stack.MoveForTest(new Point(240, 250), 20016);
        Assert.AreEqual(-80 * .065, stack.CardForTest("a").Rotation, .01);
        stack.UpForTest(30000);
    });

    [TestMethod] public void DraggingFarDecidesInThatDirection() => With(stack =>
    {
        CardDecision? decided = null;
        stack.Decided += (_, e) => decided = e.Decision;
        stack.DownForTest(new Point(160, 60), 0);
        stack.MoveForTest(new Point(20, 60), 16);
        stack.UpForTest(10000, "a");
        Assert.AreEqual(CardDecision.Left, decided);
        Assert.AreEqual(1, stack.Position);
    });

    [TestMethod] public void DecideAnnouncesThenUndoRestores() => With(stack =>
    {
        stack.Decide(CardDecision.Right);
        Assert.AreEqual("a kept. 5 left.", stack.StatusForTest);
        Assert.AreEqual(5, stack.Remaining);
        Assert.IsTrue(stack.CardForTest("b").Top);
        stack.Undo();
        Assert.AreEqual(0, stack.Position);
        Assert.AreEqual("a restored.", stack.StatusForTest);
        Assert.IsTrue(stack.CardForTest("a").Top);
    });

    [TestMethod] public void ThrownCardsLeaveAndTheWindowSlides() => With(stack =>
    {
        stack.Decide(CardDecision.Left);
        Assert.AreEqual(5, stack.RenderedCountForTest);
        stack.FinishMotionForTest();
        Assert.AreEqual(4, stack.RenderedCountForTest);
    });

    [TestMethod] public void EveryCardDecidedShowsTheEmptyStateAndResetDealsThemBack() => With(stack =>
    {
        foreach (var _ in Cards) { stack.Decide(CardDecision.Left); stack.FinishMotionForTest(); }
        Assert.IsTrue(stack.IsEmptyShownForTest);
        Assert.IsTrue(stack.IsDisabledForTest("left"));
        Assert.IsFalse(stack.IsDisabledForTest("undo"));
        bool reset = false;
        stack.ResetCompleted += (_, _) => reset = true;
        stack.Reset();
        Assert.IsTrue(reset);
        Assert.IsFalse(stack.IsEmptyShownForTest);
        Assert.AreEqual(0, stack.Position);
        Assert.IsTrue(stack.IsDisabledForTest("undo"));
    });
}
