using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ElasticSliderTests
{
    // Width 424 gives a 400 rail: one percent is four pixels, and x = 12 + 4 * percent.
    private static void Run(Action<ElasticSlider> action, Action<ElasticSlider>? setup = null)
    {
        Sta.Run(() =>
        {
            var slider = new ElasticSlider { Label = "Volume", Value = 50, Width = 424, Format = value => $"{value}%" };
            setup?.Invoke(slider);
            var window = new Window { Content = slider, Width = 600, Height = 300, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(slider); }
            finally { window.Close(); }
        });
    }

    private static double X(double percent) => 12 + 4 * percent;
    private static void Speed(ElasticSlider slider) { slider.Minimum = 0; slider.Maximum = 100; slider.Marks = new ElasticSliderMarkCollection { new() { Value = 50, Label = "half" } }; }

    [TestMethod]
    public void GrabbedThumbMovesOneToOneWithoutJumping() => Run(slider =>
    {
        slider.DownForTest(X(50) + 10, 0);
        Assert.AreEqual(50, slider.ShownForTest, .001);
        slider.MoveForTest(X(60) + 10, 16);
        Assert.AreEqual(60, slider.ShownForTest, .001);
        Assert.AreEqual(60, slider.Value);
    });

    [TestMethod]
    public void PressedTrackPullsTheThumbOverOnASpring() => Run(slider =>
    {
        slider.DownForTest(X(80), 0);
        Assert.AreEqual(80, slider.Value);
        Assert.AreEqual(50, slider.ShownForTest, .001, "the thumb travels from where it was");
        slider.SettleForTest();
        Assert.AreEqual(80, slider.ShownForTest, .01);
        slider.UpForTest(10000);
    });

    [TestMethod]
    public void PullingPastAnEndStretchesAndSpringsBackWithOvershoot() => Run(slider =>
    {
        slider.DownForTest(X(50), 0);
        slider.MoveForTest(X(100) + 2000, 16);
        Assert.AreEqual(100, slider.Value);
        Assert.IsTrue(slider.OverForTest is > 20 and < 26, $"rubber band {slider.OverForTest}");
        slider.UpForTest(32);
        Assert.IsFalse(slider.DraggingForTest);
        double lowest = double.MaxValue;
        for (int index = 0; index < 240; index++) { slider.AdvanceForTest(1d / 60); lowest = Math.Min(lowest, slider.OverForTest); }
        Assert.IsTrue(lowest < 0, "the release bounces through rest");
        Assert.AreEqual(0, slider.OverForTest, .06);
    });

    [TestMethod]
    public void DetentHoldsAndReleases() => Run(slider =>
    {
        slider.DownForTest(X(0), 0);
        slider.MoveForTest(X(50) - 6, 16);
        Assert.AreEqual(50d, slider.DetentForTest);
        Assert.AreEqual(50, slider.Value);
        slider.MoveForTest(X(50) + 6, 32);
        Assert.AreEqual(50, slider.Value, "still inside the reach");
        slider.MoveForTest(X(50) + 8, 48);
        Assert.IsNull(slider.DetentForTest);
        Assert.AreEqual(52, slider.Value);
        slider.UpForTest(10000);
    }, slider => { Speed(slider); slider.Value = 0; });

    [TestMethod]
    public void CommitFiresOnceOnReleaseAndOnKeys() => Run(slider =>
    {
        int commits = 0;
        slider.ValueCommitted += (_, _) => commits++;
        slider.DownForTest(X(50), 0);
        slider.MoveForTest(X(60), 16);
        slider.MoveForTest(X(70), 32);
        Assert.AreEqual(0, commits);
        slider.UpForTest(10000);
        Assert.AreEqual(1, commits);
        slider.KeyForTest(Key.Right);
        Assert.AreEqual(2, commits);
    });

    [TestMethod]
    public void KeysFollowANativeRangeInput() => Run(slider =>
    {
        slider.KeyForTest(Key.Right);
        Assert.AreEqual(51, slider.Value);
        slider.KeyForTest(Key.Down);
        Assert.AreEqual(50, slider.Value);
        slider.KeyForTest(Key.PageUp);
        Assert.AreEqual(60, slider.Value);
        slider.KeyForTest(Key.Left, shift: true);
        Assert.AreEqual(50, slider.Value);
        slider.KeyForTest(Key.Home);
        Assert.AreEqual(0, slider.Value);
        slider.KeyForTest(Key.End);
        Assert.AreEqual(100, slider.Value);
    });

    [TestMethod]
    public void KeyAtALimitBumpsTheBand() => Run(slider =>
    {
        slider.KeyForTest(Key.End);
        slider.SettleForTest();
        slider.KeyForTest(Key.Right);
        Assert.AreEqual(100, slider.Value);
        Assert.AreEqual(260, slider.OverVelocityForTest, .01);
    });

    [TestMethod]
    public void TagShowsWhileDraggingAndAfterKeysUntilBlur() => Run(slider =>
    {
        Assert.IsFalse(slider.TagForTest);
        slider.KeyForTest(Key.Right);
        Assert.IsTrue(slider.TagForTest);
        slider.DownForTest(X(50), 0);
        Assert.IsTrue(slider.TagForTest);
        slider.UpForTest(10000);
        Assert.IsTrue(slider.TagForTest, "it lingers");
    });

    [TestMethod]
    public void StepsSnapAtTheStepsOwnPrecision() => Run(slider =>
    {
        slider.KeyForTest(Key.Right);
        slider.KeyForTest(Key.Right);
        Assert.AreEqual(1.1, slider.Value);
        Assert.AreEqual("1.1×", slider.ReadoutForTest);
    }, slider => { slider.Minimum = .25; slider.Maximum = 2.5; slider.Step = .05; slider.Value = 1; slider.Format = value => $"{value}×"; });

    [TestMethod]
    public void SpeaksAsASliderWithTheFormattedValue() => Run(slider =>
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(slider);
        Assert.AreEqual(AutomationControlType.Slider, peer.GetAutomationControlType());
        Assert.AreEqual("Volume", peer.GetName());
        Assert.AreEqual("50%", peer.GetItemStatus());
        ((IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue)).SetValue(70.4);
        Assert.AreEqual(70, slider.Value);
    });

    [TestMethod]
    public void RubberNeverPassesItsLimit()
    {
        Assert.AreEqual(0, GestureMath.Rubber(0, 26));
        Assert.IsTrue(GestureMath.Rubber(1e6, 26) < 26);
        Assert.AreEqual(-GestureMath.Rubber(40, 26), GestureMath.Rubber(-40, 26), 1e-12);
    }
}
