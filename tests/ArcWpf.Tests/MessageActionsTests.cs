using System.Windows;
using System.Windows.Controls.Primitives;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class MessageActionsTests
{
    private static void With(Action<MessageActions> action) => Sta.Run(() =>
    {
        var row = new MessageActions { Text = "The reply" };
        row.WriteForTest = _ => { };
        ArcMotion.SetLevel(row, ArcMotionLevel.Off);
        var window = new Window { Content = row, Width = 520, Height = 420, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(row); }
        finally { window.Close(); }
    });

    private static void Press(System.Windows.Controls.Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    [TestMethod]
    public void CopyPutsTheTextOnTheClipboard() => With(row =>
    {
        row.CopyForTest.Copy();
        Assert.AreEqual("The reply", row.CopyForTest.CopiedForTest);
        Assert.IsTrue(row.CopyForTest.ShowsCheckForTest);
    });

    [TestMethod]
    public void NoTextMeansNoCopy() => With(row =>
    {
        row.Text = null;
        row.CanRegenerate = false; // Chrome runs on any flag change.
        Assert.AreEqual(Visibility.Collapsed, row.CopyForTest.Visibility);
    });

    [TestMethod]
    public void RegenerateTurnsIntoStopWhileGenerating() => With(row =>
    {
        int regenerated = 0, stopped = 0;
        row.RegenerateRequested += (_, _) => regenerated++;
        row.StopRequested += (_, _) => stopped++;
        Assert.IsFalse(row.ShowsStopForTest);
        Press(row.RegenerateForTest);
        Assert.AreEqual(1, regenerated);
        row.IsGenerating = true;
        Sta.Pump();
        Assert.IsTrue(row.ShowsStopForTest);
        Assert.IsFalse(row.UpForTest.IsEnabled);
        Assert.AreEqual(Visibility.Collapsed, row.QuoteForTest.Visibility);
        Press(row.StopForTest);
        Assert.AreEqual(1, stopped);
        row.IsGenerating = false;
        Sta.Pump();
        Assert.IsFalse(row.ShowsStopForTest);
        Assert.IsTrue(row.UpForTest.IsEnabled);
    });

    [TestMethod]
    public void QuoteCarriesTheText() => With(row =>
    {
        string? quoted = null;
        row.QuoteRequested += (_, text) => quoted = text;
        Press(row.QuoteForTest);
        Assert.AreEqual("The reply", quoted);
    });

    [TestMethod]
    public void ThumbsToggleAndExcludeEachOther() => With(row =>
    {
        var seen = new List<MessageRating>();
        row.RatingChanged += (_, rating) => seen.Add(rating);
        row.AsksWhy = false;
        Press(row.UpForTest);
        Assert.AreEqual(1, row.FilledOpacityForTest(true));
        Press(row.DownForTest);
        Assert.AreEqual(0, row.FilledOpacityForTest(true));
        Assert.AreEqual(1, row.FilledOpacityForTest(false));
        Press(row.DownForTest);
        CollectionAssert.AreEqual(new[] { MessageRating.Up, MessageRating.Down, MessageRating.None }, seen);
        Assert.AreEqual(MessageRating.None, row.Rating);
        Assert.IsFalse(row.PanelOpenForTest);
    });

    [TestMethod]
    public void ThumbDownOpensThePanelAndSubmitWaitsForAnAnswer() => With(row =>
    {
        Press(row.DownForTest);
        Sta.Pump();
        Assert.IsTrue(row.PanelOpenForTest);
        Assert.AreEqual("down", row.PanelKindForTest);
        Assert.HasCount(5, row.ChipsForTest);
        Assert.IsFalse(row.SubmitForTest!.IsEnabled);
        Press(row.ChipsForTest[0]);
        Assert.IsTrue(row.SubmitForTest.IsEnabled);
        Press(row.ChipsForTest[0]);
        Assert.IsFalse(row.SubmitForTest.IsEnabled);
        row.NoteForTest!.Text = "too vague";
        Assert.IsTrue(row.SubmitForTest.IsEnabled);
    });

    [TestMethod]
    public void SubmitThanksThenFoldsAndKeepsTheRating() => With(row =>
    {
        MessageFeedback? got = null;
        row.FeedbackSubmitted += (_, feedback) => got = feedback;
        Press(row.DownForTest);
        Sta.Pump();
        Press(row.ChipsForTest[1]);
        row.NoteForTest!.Text = " note ";
        Press(row.SubmitForTest!);
        Sta.Pump();
        Assert.IsNotNull(got);
        Assert.AreEqual(MessageRating.Down, got.Rating);
        CollectionAssert.AreEqual(new[] { "Not helpful" }, got.Reasons.ToArray());
        Assert.AreEqual("note", got.Note);
        Assert.AreEqual("thanks", row.PanelKindForTest);
        row.ExpireThanksForTest();
        Sta.Pump();
        Assert.IsFalse(row.PanelOpenForTest);
        Assert.AreEqual(MessageRating.Down, row.Rating);
    });

    [TestMethod]
    public void FailedSubmitOffersTryAgainAndSecondSendSucceeds() => With(row =>
    {
        int attempts = 0;
        row.SubmitFeedback = _ => ++attempts == 1 ? Task.FromException(new InvalidOperationException("nope")) : Task.CompletedTask;
        Press(row.UpForTest);
        Sta.Pump();
        Press(row.ChipsForTest[0]);
        Press(row.SubmitForTest!);
        Sta.Pump();
        Assert.AreEqual("up", row.PanelKindForTest);
        Assert.AreEqual("Try again", row.SubmitLabelForTest);
        Assert.IsNotNull(row.ErrorForTest);
        Press(row.SubmitForTest!);
        Sta.Pump();
        Assert.AreEqual(2, attempts);
        Assert.AreEqual("thanks", row.PanelKindForTest);
    });

    [TestMethod]
    public void PanelCanOpenAgainAfterFolding() => With(row =>
    {
        Press(row.UpForTest);
        Sta.Pump();
        Press(row.UpForTest);
        Sta.Pump();
        Assert.IsFalse(row.PanelOpenForTest);
        Press(row.DownForTest);
        Sta.Pump();
        Assert.AreEqual("down", row.PanelKindForTest);
        Assert.IsTrue(row.PanelForTest.ActualHeight > 100);
    });

    [TestMethod]
    public void GeneratingFoldsAnOpenPanel() => With(row =>
    {
        Press(row.DownForTest);
        Sta.Pump();
        row.IsGenerating = true;
        Sta.Pump();
        Assert.IsFalse(row.PanelOpenForTest);
    });

    [TestMethod]
    public void NoPositiveReasonsSkipsTheFollowUp() => With(row =>
    {
        row.PositiveReasons = [];
        Press(row.UpForTest);
        Sta.Pump();
        Assert.AreEqual(MessageRating.Up, row.Rating);
        Assert.IsFalse(row.PanelOpenForTest);
    });
}
