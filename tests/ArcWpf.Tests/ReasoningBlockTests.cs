using System.Windows;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ReasoningBlockTests
{
    private const string Text = "**Reading**\nThe handler parses first.\n\n**Fixing**\nStore the event id.";

    private static void Run(Action<ReasoningBlock> setup, Action<ReasoningBlock> action, bool still = true)
    {
        Sta.Run(() =>
        {
            var block = new ReasoningBlock();
            if (still) { ArcMotion.SetLevel(block, ArcMotionLevel.Off); }
            setup(block);
            var window = new Window { Content = block, Width = 520, Height = 500, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(block); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void TheParserSplitsHeadingsAndHoldsBackOneStillArriving()
    {
        var steps = ReasoningParser.Parse(Text);
        Assert.HasCount(2, steps);
        Assert.AreEqual("Reading", steps[0].Title);
        Assert.AreEqual("Store the event id.", steps[1].Text);
        var partial = ReasoningParser.Parse("First line\n\n**Half");
        Assert.HasCount(1, partial);
        Assert.AreEqual("First line", partial[0].Text);
        Assert.IsEmpty(ReasoningParser.Parse("  "));
    }

    [TestMethod]
    public void ThinkingStreamsLiveAndFoldsWhenDone() => Run(block => { block.Reasoning = Text; block.Status = ReasoningStatus.Thinking; }, block =>
    {
        Assert.AreEqual("Thinking", block.LabelForTest);
        Assert.IsTrue(block.LiveShownForTest);
        StringAssert.Contains(block.LiveForTest, "Store the event id.");
        block.Seconds = 7;
        block.Status = ReasoningStatus.Done;
        Sta.Pump();
        Assert.IsFalse(block.LiveShownForTest);
        Assert.IsFalse(block.PanelShownForTest);
        StringAssert.Contains(block.LabelForTest + block.CounterForTest, "Thought");
    });

    [TestMethod]
    public void OpeningShowsEveryStepAndTheFoot() => Run(block => { block.Reasoning = Text; block.Seconds = 7; block.Status = ReasoningStatus.Done; }, block =>
    {
        block.IsOpen = true;
        Sta.Pump();
        Assert.IsTrue(block.PanelShownForTest);
        Assert.AreEqual(2, block.StepCountForTest);
        Assert.AreEqual("Fixing", block.StepTitleForTest(1));
        Assert.IsTrue(block.FootShownForTest);
        Assert.IsTrue(block.FrameHeightForTest > 100);
    });

    [TestMethod]
    public void TheCounterFollowsTheClockWhileThinking()
    {
        var now = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);
        Run(block => { block.ClockForTest = () => now; block.Status = ReasoningStatus.Thinking; }, block =>
        {
            now = now.AddSeconds(5);
            block.TickForTest();
            StringAssert.Contains(block.CounterForTest, "5");
        });
    }

    [TestMethod]
    public void StopAndRetryRaiseTheirEvents() => Run(block => { block.Status = ReasoningStatus.Thinking; block.CanStop = true; block.CanRetry = true; }, block =>
    {
        int stops = 0, retries = 0;
        block.StopRequested += (_, _) => stops++;
        block.RetryRequested += (_, _) => retries++;
        block.ActionForTest!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(1, stops);
        block.Error = "Timed out";
        block.Status = ReasoningStatus.Error;
        Sta.Pump();
        block.ActionForTest!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(1, retries);
    });

    [TestMethod]
    public void CopyTakesTheWholeReasoning() => Run(block => { block.Reasoning = Text; block.Status = ReasoningStatus.Done; block.Seconds = 2; block.IsOpen = true; }, block =>
    {
        string? written = null;
        block.CopyForTest.WriteForTest = value => written = value;
        block.CopyForTest.Copy();
        StringAssert.Contains(written!, "Reading");
        StringAssert.Contains(written!, "Store the event id.");
        Assert.IsTrue(block.CopyForTest.ShowsCheckForTest);
    });
}
