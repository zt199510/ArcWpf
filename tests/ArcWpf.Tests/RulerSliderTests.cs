using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class RulerSliderTests
{
    private static void Run(Action<RulerSlider> action, Action<RulerSlider>? setup = null)
    {
        Sta.Run(() =>
        {
            var slider = new RulerSlider { Label = "Height", Minimum = 120, Maximum = 220, Value = 172, Unit = "cm", Width = 420 };
            setup?.Invoke(slider);
            var window = new Window { Content = slider, Width = 600, Height = 300, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(slider); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void DragMovesTheTapeAgainstThePointerOneToOne() => Run(slider =>
    {
        slider.DownForTest(200, 0);
        slider.MoveForTest(150, 16);
        Assert.AreEqual(177, slider.PositionForTest, 1e-9);
        Assert.AreEqual(177, slider.Value);
        slider.UpForTest(150, 10000);
        slider.SettleForTest();
        Assert.AreEqual(177, slider.Value);
    });

    [TestMethod]
    public void SmallMovementIsATapThatGlides() => Run(slider =>
    {
        int commits = 0;
        slider.ValueCommitted += (_, _) => commits++;
        slider.DownForTest(310, 0);
        slider.MoveForTest(312, 16);
        Assert.IsFalse(slider.DraggingForTest);
        slider.UpForTest(310, 32);
        Assert.AreEqual(182, slider.Value, "100px right of center is ten ticks");
        Assert.AreEqual(1, commits);
        slider.SettleForTest();
        Assert.AreEqual(182, slider.PositionForTest, 1e-9);
    });

    [TestMethod]
    public void FlickCoastsOntoATickAndCommitsAtRest() => Run(slider =>
    {
        int commits = 0;
        slider.ValueCommitted += (_, _) => commits++;
        slider.DownForTest(300, 0);
        slider.MoveForTest(250, 20);
        slider.MoveForTest(200, 40);
        slider.UpForTest(200, 50);
        Assert.IsTrue(slider.CoastingForTest);
        Assert.AreEqual(0, commits);
        slider.AdvanceForTest(.05);
        double early = slider.Value;
        slider.SettleForTest();
        Assert.IsFalse(slider.FlyingForTest);
        Assert.AreEqual(1, commits);
        Assert.IsTrue(slider.Value > early, "the readout rolls while it coasts");
        // 10 units in 40ms is 250 units a second; .42 of that lands 105 past the release, which clamps at the end.
        Assert.AreEqual(Math.Min(220, 182 + 105), slider.Value);
        Assert.AreEqual(slider.Value, slider.PositionForTest, 1e-9);
    });

    [TestMethod]
    public void GrabbingACoastingTapeCatchesIt() => Run(slider =>
    {
        slider.DownForTest(300, 0);
        slider.MoveForTest(200, 40);
        slider.UpForTest(200, 50);
        slider.AdvanceForTest(.05);
        double caught = slider.PositionForTest;
        slider.DownForTest(100, 60);
        Assert.IsFalse(slider.FlyingForTest);
        Assert.AreEqual(caught, slider.PositionForTest);
    });

    [TestMethod]
    public void PullingPastAnEndRubberBandsHome() => Run(slider =>
    {
        slider.DownForTest(400, 0);
        slider.MoveForTest(-2000, 16);
        Assert.AreEqual(220, slider.Value);
        Assert.IsTrue(slider.PositionForTest is > 220 and < 224.4, $"position {slider.PositionForTest}");
        slider.UpForTest(-2000, 10000);
        slider.SettleForTest();
        Assert.AreEqual(220, slider.PositionForTest, 1e-9);
    });

    [TestMethod]
    public void KeysStepAndOutsideValuesGlide() => Run(slider =>
    {
        slider.KeyForTest(Key.Right);
        Assert.AreEqual(173, slider.Value);
        slider.KeyForTest(Key.PageUp);
        Assert.AreEqual(183, slider.Value);
        slider.KeyForTest(Key.Left, shift: true);
        Assert.AreEqual(173, slider.Value);
        slider.KeyForTest(Key.Home);
        Assert.AreEqual(120, slider.Value);
        slider.Value = 200;
        Assert.AreNotEqual(200, slider.PositionForTest);
        slider.SettleForTest();
        Assert.AreEqual(200, slider.PositionForTest, 1e-9);
    });

    [TestMethod]
    public void WheelMovesTheTapeThenLandsOnATick() => Run(slider =>
    {
        slider.WheelForTest(23);
        Assert.AreEqual(174.3, slider.PositionForTest, 1e-9);
        Assert.AreEqual(174, slider.Value);
        slider.WheelSettleForTest();
        slider.SettleForTest();
        Assert.AreEqual(174, slider.PositionForTest, 1e-9);
    });

    [TestMethod]
    public void HalfStepsSnapAndKeepTheirDecimal() => Run(slider =>
    {
        Assert.AreEqual("68.5", slider.RollingForTest.TextForTest);
        slider.KeyForTest(Key.Right);
        Assert.AreEqual(69, slider.Value);
        Assert.AreEqual("69.0", slider.RollingForTest.TextForTest);
    }, slider => { slider.Minimum = 30; slider.Maximum = 150; slider.Step = .5; slider.Spacing = 12; slider.Value = 68.5; slider.Unit = "kg"; });

    [TestMethod]
    public void DigitsKeepTheirPlaceAsTheNumberGrows() => Run(slider =>
    {
        CollectionAssert.AreEqual(new[] { "d2", "d1", "d0" }, slider.RollingForTest.KeysForTest.ToArray());
        slider.Value = 99;
        CollectionAssert.AreEqual(new[] { "d1", "d0" }, slider.RollingForTest.KeysForTest.ToArray());
        Assert.AreEqual(1, slider.RollingForTest.LeavingForTest, "the hundreds column closes on a spring");
    }, slider => { slider.Minimum = 0; slider.Maximum = 200; });

    [TestMethod]
    public void SpeaksTheValueWithItsUnit() => Run(slider =>
    {
        Assert.AreEqual("172 cm", slider.SpokenForTest);
        var peer = UIElementAutomationPeer.CreatePeerForElement(slider);
        Assert.AreEqual(AutomationControlType.Slider, peer.GetAutomationControlType());
        Assert.AreEqual("Height", peer.GetName());
        Assert.AreEqual("172 cm", peer.GetItemStatus());
    });
}
