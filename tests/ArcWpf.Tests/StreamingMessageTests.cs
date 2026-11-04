using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class StreamingMessageTests
{
    private static void Run(Action<StreamingMessage> action, bool still = false, Action<StreamingMessage>? setup = null)
    {
        Sta.Run(() =>
        {
            var message = new StreamingMessage { ModelName = "Claude" };
            if (still) { ArcMotion.SetLevel(message, ArcMotionLevel.Off); }
            setup?.Invoke(message);
            var window = new Window { Content = message, Width = 560, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(message); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void TheMarkdownCoversTheSmallSet()
    {
        var blocks = StreamingMarkdown.Parse("# Title\n\nOne **two** `three` [1].\n\n1. first\n2. second\n\n- dash\n\n```sql\nselect 1;\nselect 2;\n```");
        CollectionAssert.AreEqual(new[] { StreamingBlockKind.Heading, StreamingBlockKind.Paragraph, StreamingBlockKind.Ordered, StreamingBlockKind.Ordered, StreamingBlockKind.Bullet, StreamingBlockKind.Code }, blocks.Select(block => block.Kind).ToArray());
        var spans = blocks[1].Spans;
        Assert.IsTrue(spans.Any(span => span is { Kind: StreamingSpanKind.Bold, Text: "two" }));
        Assert.IsTrue(spans.Any(span => span is { Kind: StreamingSpanKind.Code, Text: "three" }));
        Assert.IsTrue(spans.Any(span => span is { Kind: StreamingSpanKind.Citation, Number: 1 }));
        Assert.AreEqual("2.", blocks[3].Marker);
        Assert.AreEqual("sql", blocks[5].Language);
        Assert.AreEqual("select 1;\nselect 2;", blocks[5].PlainText);
    }

    [TestMethod]
    [DataRow("Hello **wor", "Hello wor")]
    [DataRow("Hello *", "Hello ")]
    [DataRow("See [1", "See ")]
    [DataRow("Run `cod", "Run cod")]
    public void HalfMarkersNeverShowRaw(string text, string plain) => Assert.AreEqual(plain, StreamingMarkdown.Parse(text)[0].PlainText);

    [TestMethod]
    public void AnUnclosedFenceRunsToTheEndAndAListDigitWaits()
    {
        Assert.AreEqual("select", StreamingMarkdown.Parse("```sql\nselect")[0].PlainText);
        Assert.HasCount(1, StreamingMarkdown.Parse("Line\n\n1"));
    }

    [TestMethod]
    public void AFinishedReplyArrivesWhole() => Run(message =>
    {
        message.Text = "One two three four five six seven eight";
        Sta.Pump();
        Assert.AreEqual(message.Text, message.RevealedForTest);
        Assert.IsTrue(message.FootShownForTest);
        Assert.IsFalse(message.LoopRunningForTest);
    });

    [TestMethod]
    public void ARunningReplyRevealsWholeWordsAndOnlyAddsNewRuns() => Run(message =>
    {
        message.Text = "One two three four five six seven eight nine";
        Assert.AreEqual("", message.RevealedForTest);
        message.AdvanceForTest(.034);
        Assert.AreEqual("One two three", message.RevealedForTest);
        var first = message.InlineForTest(0, 0);
        message.AdvanceForTest(.034);
        Assert.AreEqual("One two three four five six", message.RevealedForTest);
        Assert.AreEqual(2, message.RunCountForTest(0));
        Assert.AreSame(first, message.InlineForTest(0, 0), "Text on screen is not rebuilt.");
        message.AdvanceForTest(1);
        Assert.IsFalse(message.IsRevealing);
        Assert.IsFalse(message.FootShownForTest, "The foot waits for the end of the stream.");
        message.Status = StreamingStatus.Done;
        Sta.Pump();
        Assert.IsTrue(message.FootShownForTest);
    }, false, message => message.Status = StreamingStatus.Streaming);

    [TestMethod]
    public void ThinkingShowsUntilTheFirstWords() => Run(message =>
    {
        Assert.IsTrue(message.ThinkingShownForTest);
        message.Status = StreamingStatus.Streaming;
        message.Append("Hello there friend");
        message.AdvanceForTest(.034);
        Sta.Pump();
        Assert.IsFalse(message.ThinkingShownForTest);
    }, false, message => message.Status = StreamingStatus.Thinking);

    [TestMethod]
    public void AReplayFromAFinishedReplyShowsThinkingAgain() => Run(message =>
    {
        message.Text = "Old reply";
        Sta.Pump();
        message.Status = StreamingStatus.Thinking;
        message.Text = "";
        Assert.IsTrue(message.ThinkingShownForTest);
        Assert.IsFalse(message.FootShownForTest);
        Assert.AreEqual(0, message.ViewCountForTest);
    });

    [TestMethod]
    public void StopCutsWhereItStandsAndLaterTextIsIgnored() => Run(message =>
    {
        message.Text = "One two three four five six";
        message.AdvanceForTest(.034);
        message.Status = StreamingStatus.Stopped;
        Sta.Pump();
        Assert.AreEqual("One two three", message.RevealedForTest);
        Assert.IsTrue(message.StoppedShownForTest);
        Assert.IsTrue(message.FootShownForTest);
        message.Append(" late");
        Sta.Pump();
        Assert.AreEqual("One two three", message.RevealedForTest);
    }, false, message => message.Status = StreamingStatus.Streaming);

    [TestMethod]
    public void AnErrorOffersRetryAndARetryStartsOver() => Run(message =>
    {
        int retries = 0;
        message.RetryRequested += (_, _) => retries++;
        message.Text = "Partial";
        message.Error = "Connection closed.";
        message.Status = StreamingStatus.Error;
        Sta.Pump();
        Assert.IsTrue(message.ErrorShownForTest);
        StringAssert.Contains(message.ErrorTextForTest, "Connection closed.");
        Assert.AreEqual("Partial", message.RevealedForTest);
        message.RetryForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(1, retries);
        message.Status = StreamingStatus.Thinking;
        message.Text = "";
        Sta.Pump();
        Assert.AreEqual(0, message.ViewCountForTest);
        Assert.IsFalse(message.ErrorShownForTest);
        Assert.IsTrue(message.ThinkingShownForTest);
    }, true);

    [TestMethod]
    public void NoMotionShowsEverythingAtOnce() => Run(message =>
    {
        message.Text = "One two three four five";
        Sta.Pump();
        Assert.AreEqual(message.Text, message.RevealedForTest);
        Assert.IsFalse(message.LoopRunningForTest);
    }, true, message => message.Status = StreamingStatus.Streaming);

    [TestMethod]
    public void CitationsBindToSourcesAndUnknownOnesStayPlain() => Run(message =>
    {
        message.Text = "Known [1] unknown [9].";
        Sta.Pump();
        var containers = Enumerable.Range(0, message.RunCountForTest(0)).Select(index => message.InlineForTest(0, index)).OfType<InlineUIContainer>().ToList();
        Assert.HasCount(1, containers);
        Assert.IsInstanceOfType<SourceCitation>(containers[0].Child);
        StringAssert.Contains(message.BlockTextForTest(0), "9");
    }, true, message => message.Sources = [new CitationSource("One", "https://one.example/a")]);

    [TestMethod]
    public void ThumbsRateOnlyAFinishedReplyAndCanBeTakenBack() => Run(message =>
    {
        var ratings = new List<bool?>();
        message.FeedbackGiven += (_, rating) => ratings.Add(rating);
        message.Text = "Done";
        Sta.Pump();
        Assert.IsTrue(message.ThumbUpForTest.IsVisible);
        message.ThumbUpForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        message.ThumbUpForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        CollectionAssert.AreEqual(new bool?[] { true, null }, ratings);
        message.Status = StreamingStatus.Stopped;
        Sta.Pump();
        Assert.IsFalse(message.ThumbUpForTest.IsVisible);
    }, true, message => message.ShowFeedback = true);

    [TestMethod]
    public void CopyTakesTheWholeReply() => Run(message =>
    {
        message.Text = "Reply text";
        Sta.Pump();
        string? written = null;
        message.CopyForTest.WriteForTest = value => written = value;
        message.CopyForTest.Copy();
        Assert.AreEqual("Reply text", written);
    }, true);

    private static void Host(Action<StreamingScrollHost, Border> action, bool still = true)
    {
        Sta.Run(() =>
        {
            var content = new Border { Height = 300 };
            var host = new StreamingScrollHost { Content = content, Height = 200 };
            if (still) { ArcMotion.SetLevel(host, ArcMotionLevel.Off); }
            var window = new Window { Content = host, Width = 400, Height = 300, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(host, content); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void AReleaseNearTheBottomDoesNotStickAgainUntilTheReaderMovesDown() => Host((host, content) =>
    {
        content.Height = 800;
        Sta.Pump();
        host.ReleaseForTest();
        content.Height = 805;
        Sta.Pump();
        Assert.IsFalse(host.IsStuckToBottom);
        host.ViewerForTest.ScrollToVerticalOffset(0);
        Sta.Pump();
        host.ViewerForTest.ScrollToVerticalOffset(host.ScrollableForTest);
        Sta.Pump();
        Assert.IsTrue(host.IsStuckToBottom);
    });

    [TestMethod]
    public void TheHostFollowsNewLinesUntilTheReaderScrollsUp() => Host((host, content) =>
    {
        Assert.IsTrue(host.IsStuckToBottom);
        content.Height = 800;
        Sta.Pump();
        Assert.AreEqual(host.ScrollableForTest, host.OffsetForTest, 1);
        host.ReleaseForTest();
        host.ViewerForTest.ScrollToVerticalOffset(0);
        Sta.Pump();
        Assert.IsFalse(host.IsStuckToBottom);
        Assert.IsTrue(host.JumpShownForTest);
        content.Height = 1200;
        Sta.Pump();
        Assert.AreEqual(0, host.OffsetForTest, 1, "A released reader is left where they are.");
        host.JumpForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.IsTrue(host.IsStuckToBottom);
        Assert.AreEqual(host.ScrollableForTest, host.OffsetForTest, 1);
        Assert.IsFalse(host.JumpShownForTest);
    });
}
