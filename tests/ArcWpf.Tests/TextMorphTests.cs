using System.Windows;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class TextMorphTests
{
    private static void Run(Action<TextMorph, Window> action) => Sta.Run(() =>
    {
        var morph = new TextMorph { Text = "Publish", FontSize = 72, LineRatio = 1.1, Tracking = -.03, HorizontalAlignment = HorizontalAlignment.Left };
        var window = new Window { Content = morph, Width = 700, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(morph, window); } finally { window.Close(); }
    });

    [TestMethod]
    public void RepeatedLettersAreKeyedByOccurrence() => Sta.Run(() =>
    {
        var morph = new TextMorph { Text = "aba" };
        var window = new Window { Content = morph, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); CollectionAssert.AreEqual(new[] { "a-0", "b-0", "a-1" }, morph.GlyphKeysForTest.ToArray()); } finally { window.Close(); }
    });

    [TestMethod]
    public void ChangingTextKeepsSharedLettersAndEntersTheRest() => Run((morph, _) =>
    {
        double before = morph.GlyphXForTest("P-0");
        morph.Text = "Publishing";
        CollectionAssert.Contains(morph.GlyphKeysForTest.ToList(), "n-0");
        Assert.AreEqual(before, morph.GlyphXForTest("P-0"), .01);
        Assert.AreEqual(0, morph.GlyphOpacityForTest("n-0"), .01, "a new letter starts hidden");
        Assert.IsTrue(morph.FrameTargetForTest > morph.FrameWidthForTest, "the frame springs towards the longer label");
        Assert.IsTrue(morph.IsMaskedForTest, "the frame feathers its edge while moving");
    });

    [TestMethod]
    public void RemovedLettersLeaveInPlaceAndAreDiscarded() => Run((morph, _) =>
    {
        morph.Text = "Publishing";
        morph.FinishMotionForTest();
        morph.Text = "Published";
        CollectionAssert.Contains(morph.LeavingKeysForTest.ToList(), "n-0");
        CollectionAssert.DoesNotContain(morph.GlyphKeysForTest.ToList(), "n-0");
        morph.FinishMotionForTest();
        Assert.AreEqual(0, morph.LeavingKeysForTest.Count);
        Assert.IsFalse(morph.IsMaskedForTest, "a resting frame carries no mask");
        Sta.Pump();
        Assert.AreEqual(morph.FrameTargetForTest, morph.ActualWidth, .6);
    });

    [TestMethod]
    public void ARemovedLetterThatComesBackIsRevived() => Run((morph, _) =>
    {
        morph.Text = "Publi";
        morph.Text = "Publish";
        Assert.AreEqual(0, morph.LeavingKeysForTest.Count);
        CollectionAssert.Contains(morph.GlyphKeysForTest.ToList(), "h-0");
    });

    [TestMethod]
    public void LayoutAddsTrackingAfterEveryLetter() => Run((morph, _) =>
    {
        double tracked = morph.ActualWidth;
        morph.Tracking = 0;
        Sta.Pump();
        Assert.AreEqual(7 * .03 * 72, morph.ActualWidth - tracked, .6);
    });

    [TestMethod]
    public void OffMotionSwapsInstantly() => Run((morph, _) =>
    {
        ArcMotion.SetLevel(morph, ArcMotionLevel.Off);
        morph.Text = "Published";
        Assert.AreEqual(0, morph.LeavingKeysForTest.Count);
        Assert.AreEqual(morph.FrameTargetForTest, morph.FrameWidthForTest, .01);
        Assert.AreEqual(1, morph.GlyphOpacityForTest("e-0"), .01);
    });

    [TestMethod]
    public void AutomationReadsThePlainText() => Run((morph, _) =>
    {
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(morph);
        Assert.AreEqual("Publish", peer.GetName());
    });
}
