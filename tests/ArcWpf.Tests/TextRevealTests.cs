using System.Windows;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class TextRevealTests
{
    private const string Heading = "Small details make every interaction feel considered";

    private static void Run(Func<TextReveal> make, Action<TextReveal> action) => Sta.Run(() =>
    {
        var reveal = make();
        var window = new Window { Content = new System.Windows.Controls.Grid { Children = { reveal }, Width = 521.625 }, Width = 700, Height = 400, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(reveal); } finally { window.Close(); }
    });

    private static TextReveal Title() => new() { Text = Heading, FontSize = 52, LineRatio = 1.1, Tracking = -.03, VerticalAlignment = VerticalAlignment.Top };

    [TestMethod]
    public void HeadingBalancesIntoThreeLines() => Run(Title, reveal =>
    {
        reveal.FinishMotionForTest();
        Assert.AreEqual(3, reveal.LineCountForTest);
        Assert.AreEqual(3 * 57.2, reveal.ActualHeight, .1);
    });

    [TestMethod]
    public void StaggerFollowsPositionAndCapsAtTheTotal() => Run(Title, reveal =>
    {
        Assert.AreEqual(7, reveal.WordCountForTest);
        Assert.AreEqual(.04, reveal.DelayForTest(1), 1e-6);
        Assert.AreEqual(.24, reveal.DelayForTest(6), 1e-6);
    });

    [TestMethod]
    public void LongTextShrinksTheStepAndDelayOffsetsIt() => Run(() => new TextReveal { Delay = .32, Kind = TextRevealKind.Paragraph, MaxCharacters = 0, Text = "Each word rises out of its line and sharpens as it lands, once, when the title appears." }, reveal =>
    {
        Assert.AreEqual(17, reveal.WordCountForTest);
        Assert.AreEqual(.32, reveal.DelayForTest(0), 1e-6);
        Assert.AreEqual(.32 + .48 / 17, reveal.DelayForTest(1), 1e-6);
    });

    [TestMethod]
    public void ALineFeedBreaksAndAddsAStep() => Run(() => new TextReveal { Text = "one two\nthree", FontSize = 20 }, reveal =>
    {
        Assert.AreEqual(2, reveal.LineCountForTest);
        Assert.AreEqual(3 * .04, reveal.DelayForTest(2), 1e-6);
        Assert.IsTrue(reveal.WordAtForTest(2).Y > reveal.WordAtForTest(0).Y);
    });

    [TestMethod]
    public void WordsStartHiddenBelowAndRestPlain() => Run(Title, reveal =>
    {
        reveal.Replay();
        Assert.AreEqual(0, reveal.OpacityForTest(6), .01);
        Assert.AreEqual(.62 * 52, reveal.RiseForTest(6), .01);
        Assert.IsTrue(reveal.IsBlurredForTest(6));
        Assert.IsTrue(reveal.IsMaskedForTest(6));
        reveal.FinishMotionForTest();
        Assert.AreEqual(1, reveal.OpacityForTest(6), .01);
        Assert.AreEqual(0, reveal.RiseForTest(6), .01);
        Assert.IsFalse(reveal.IsBlurredForTest(6));
        Assert.IsFalse(reveal.IsMaskedForTest(6));
    });

    [TestMethod]
    public void ReducedMotionOnlyFades() => Run(Title, reveal =>
    {
        ArcMotion.SetLevel(reveal, ArcMotionLevel.Reduced);
        reveal.Replay();
        Assert.AreEqual(0, reveal.RiseForTest(0), .01);
        Assert.IsFalse(reveal.IsBlurredForTest(0));
        Assert.IsFalse(reveal.IsMaskedForTest(0));
    });

    [TestMethod]
    public void HeadingAndParagraphExposeDifferentLevels() => Run(Title, reveal =>
    {
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(reveal);
        Assert.AreEqual(Heading, peer.GetName());
        Assert.AreEqual(System.Windows.Automation.AutomationHeadingLevel.Level2, System.Windows.Automation.AutomationProperties.GetHeadingLevel(reveal));
        reveal.Kind = TextRevealKind.Paragraph;
        Assert.AreEqual(System.Windows.Automation.AutomationHeadingLevel.None, System.Windows.Automation.AutomationProperties.GetHeadingLevel(reveal));
    });
}
