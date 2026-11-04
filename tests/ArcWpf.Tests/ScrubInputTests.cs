using System.Windows;
using System.Windows.Automation.Peers;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ScrubInputTests
{
    private static void Run(Action<ScrubInput> action, Action<ScrubInput>? setup = null)
    {
        Sta.Run(() =>
        {
            var input = new ScrubInput { Label = "X", Minimum = -240, Maximum = 240, Unit = "px", Width = 240, UsePointerLock = false };
            setup?.Invoke(input);
            var window = new Window { Content = input, Width = 400, Height = 200, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(input); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    [DataRow("12*2", 24d)]
    [DataRow("(100-24)/2", 38d)]
    [DataRow("2+3*4", 14d)]
    [DataRow("-5+1", -4d)]
    [DataRow("1,5", 1.5d)]
    [DataRow("3×4", 12d)]
    [DataRow("9÷3", 3d)]
    [DataRow("1e2", 100d)]
    public void ExpressionsEvaluate(string text, double expected) => Assert.AreEqual(expected, ScrubExpression.Evaluate(text, ["px", "%"])!.Value.Value, 1e-9);

    [TestMethod]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("1+")]
    [DataRow("(1")]
    [DataRow("1/0")]
    [DataRow("5em")]
    public void BadExpressionsAreRejected(string text) => Assert.IsNull(ScrubExpression.Evaluate(text, ["px", "%"]));

    [TestMethod]
    public void TypedUnitsResolveWithTheDegreeAlias()
    {
        Assert.AreEqual("%", ScrubExpression.Evaluate("50%", ["px", "%"])!.Value.Unit);
        Assert.AreEqual("°", ScrubExpression.Evaluate("90deg", ["°"])!.Value.Unit);
    }

    [TestMethod]
    public void ScrubStepsByTravelAndSlopHoldsItBack() => Run(input =>
    {
        ScrubInputSource? source = null;
        input.ValueChanged += (_, e) => source = e.Source;
        input.BeginForTest();
        input.ScrubForTest(2);
        Assert.IsFalse(input.ScrubbingForTest);
        input.ScrubForTest(38);
        Assert.IsTrue(input.ScrubbingForTest);
        Assert.AreEqual(19, input.Value, "travel inside the slop does not count");
        Assert.AreEqual(ScrubInputSource.Scrub, source);
        input.EndForTest();
    });

    [TestMethod]
    public void PressOnTheHandleStartsAScrubThroughRoutedInput() => Run(input =>
    {
        input.HandleForTest.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = UIElement.MouseDownEvent });
        Assert.IsTrue(input.PressedForTest);
        input.EndForTest();
    });

    [TestMethod]
    public void LockedPointerReadsTravelInDips() => Sta.Run(() =>
    {
        double dip = 1.5, middle = (SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth / 2) * dip;
        Assert.AreEqual(20, ScrubInput.LockedTravelForTest((int)middle, (int)middle + 30, dip), 1e-9);
    });

    [TestMethod]
    public void ModifiersChangeTheStepAndShowATag() => Run(input =>
    {
        input.BeginForTest();
        input.ScrubForTest(20);
        Assert.AreEqual(10, input.Value);
        input.ScrubForTest(2, shift: true);
        Assert.AreEqual("×10", input.TagForTest);
        Assert.AreEqual(20, input.Value);
        input.ScrubForTest(2, alt: true);
        Assert.AreEqual("×0.1", input.TagForTest);
        input.EndForTest();
    });

    [TestMethod]
    public void EscapeCancelsToTheStartValue() => Run(input =>
    {
        double? committed = null;
        input.ValueCommitted += (_, e) => committed = e.Value;
        input.Value = 15;
        input.BeginForTest();
        input.ScrubForTest(40);
        Assert.AreEqual(35, input.Value);
        input.EndForTest(cancel: true);
        Assert.AreEqual(15, input.Value);
        Assert.AreEqual(15, committed);
        Assert.IsFalse(input.ScrubbingForTest);
    });

    [TestMethod]
    public void StrainsPastALimitAndSpringsBack() => Run(input =>
    {
        input.Value = 240;
        input.BeginForTest();
        input.ScrubForTest(400);
        Assert.AreEqual(240, input.Value);
        Assert.IsTrue(input.StrainForTest is > 0 and < 12, $"strain {input.StrainForTest}");
        Assert.AreEqual(input.StrainForTest * .5, input.LeanForTest, 1e-9);
        input.EndForTest();
        input.SettleForTest();
        Assert.AreEqual(0, input.StrainForTest, .02);
    });

    [TestMethod]
    public void ReversingAfterAnOvershootMovesSoon() => Run(input =>
    {
        input.Value = 240;
        input.BeginForTest();
        input.ScrubForTest(5000);
        input.ScrubForTest(-100);
        Assert.IsTrue(input.Value < 240, "the stored overshoot is capped at 90px");
        input.EndForTest();
    });

    [TestMethod]
    public void WrapGoesRoundInsteadOfStopping() => Run(input =>
    {
        input.StepForTest(1);
        Assert.AreEqual(0, input.Value);
        input.StepForTest(-1);
        Assert.AreEqual(359, input.Value);
    }, input => { input.Minimum = 0; input.Maximum = 360; input.Wrap = true; input.Value = 359; input.Unit = "°"; });

    [TestMethod]
    public void TypingCommitsAndKeepsLimits() => Run(input =>
    {
        double? committed = null;
        input.ValueCommitted += (_, e) => committed = e.Value;
        input.TypeForTest("12*2");
        input.CommitForTest();
        Assert.AreEqual(24, input.Value);
        Assert.AreEqual(24, committed);
        Assert.AreEqual("24", input.TextForTest);
        input.TypeForTest("9000");
        input.CommitForTest();
        Assert.AreEqual(240, input.Value);
        Assert.AreEqual(260, Math.Abs(ReadVelocity(input)), .01);
    });

    private static double ReadVelocity(ScrubInput input) => ((GestureSpring)typeof(ScrubInput).GetField("_strain", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(input)!).Velocity;

    [TestMethod]
    public void InvalidEntryRestoresAndFlags() => Run(input =>
    {
        input.Value = 7;
        input.TypeForTest("nope");
        input.CommitForTest();
        Assert.AreEqual(7, input.Value);
        Assert.AreEqual("7", input.TextForTest);
        Assert.IsTrue(input.InvalidForTest);
    });

    [TestMethod]
    public void EscapeDropsTheDraft() => Run(input =>
    {
        input.Value = 7;
        input.TypeForTest("99");
        input.EscapeForTest();
        Assert.IsNull(input.DraftForTest);
        Assert.AreEqual("7", input.TextForTest);
        Assert.AreEqual(7, input.Value);
    });

    [TestMethod]
    public void CoarseStepsLandOnTheCoarseGrid() => Run(input =>
    {
        input.Value = 13;
        input.StepForTest(1, shift: true);
        Assert.AreEqual(20, input.Value);
        input.StepForTest(-1, shift: true);
        Assert.AreEqual(10, input.Value);
        input.StepForTest(1, alt: true);
        Assert.AreEqual(10.1, input.Value);
    });

    [TestMethod]
    public void ResetReturnsToItsValue() => Run(input =>
    {
        ScrubInputSource? source = null;
        input.ValueCommitted += (_, e) => source = e.Source;
        input.Value = 90;
        input.ResetForTest();
        Assert.AreEqual(12, input.Value);
        Assert.AreEqual(ScrubInputSource.Reset, source);
    }, input => input.ResetValue = 12);

    [TestMethod]
    public void UnitsCycleAndTypedUnitsSwitch() => Run(input =>
    {
        Assert.IsTrue(input.CyclesForTest);
        string? changed = null;
        input.UnitChanged += (_, unit) => changed = unit;
        input.CycleForTest();
        Assert.AreEqual("%", changed);
        Assert.AreEqual("%", input.EffectiveUnitForTest);
        input.TypeForTest("10px");
        input.CommitForTest();
        Assert.AreEqual("px", input.EffectiveUnitForTest);
    }, input => { input.Units = ["px", "%"]; input.Unit = "px"; });

    [TestMethod]
    public void TrackNeedsBothLimitsAndFollowsTheValue() => Run(input =>
    {
        input.Value = 120;
        input.SettleForTest();
        Assert.AreEqual(.75, input.FillForTest, .001);
    });

    [TestMethod]
    public void FieldSpeaksAsASpinnerWithTheUnitSpelledOut() => Run(input =>
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(input.FieldForTest);
        Assert.AreEqual(AutomationControlType.Spinner, peer.GetAutomationControlType());
        Assert.AreEqual("Opacity", peer.GetName());
        Assert.AreEqual("40 percent", peer.GetItemStatus());
        Assert.IsNotNull(peer.GetPattern(PatternInterface.RangeValue));
    }, input => { input.Label = "O"; input.Value = 40; input.Unit = "%"; System.Windows.Automation.AutomationProperties.SetName(input, "Opacity"); });
}
