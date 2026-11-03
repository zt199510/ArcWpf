using System.Windows;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class CookieConsentTests
{
    private static Window Host(CookieConsent consent, double width = 522) => new() { Content = consent, Width = width, Height = 560, ShowInTaskbar = false };

    [TestMethod]
    public void AcceptAllMarksRequiredAndOptional() => Sta.Run(() =>
    {
        var consent = new CookieConsent();
        int calls = 0;
        consent.ConsentChanged += (_, _) => calls++;
        consent.CommitForTest(true);
        Assert.IsTrue(consent.Consent!.Values.All(value => value));
        Assert.AreEqual(1, calls);
    });

    [TestMethod]
    public void RejectAllKeepsRequiredOn() => Sta.Run(() =>
    {
        var consent = new CookieConsent();
        consent.CommitForTest(false);
        Assert.IsTrue(consent.Consent!["necessary"]);
        Assert.IsFalse(consent.Consent["analytics"]);
    });

    [TestMethod]
    public void SurfaceTakesEachShapeAndFoldsToTheChip() => Sta.Run(() =>
    {
        var consent = new CookieConsent();
        var window = Host(consent);
        try
        {
            window.Show(); Sta.Pump(); consent.UpdateLayout();
            Assert.AreEqual(CookieMode.Bar, consent.Mode);
            for (int i = 0; i < 80; i++) { consent.AdvanceMotion(.05); }
            Assert.AreEqual(22d, consent.SurfaceRadius, .01);
            consent.OpenPreferences();
            Assert.AreEqual(CookieMode.Panel, consent.Mode);
            for (int i = 0; i < 80; i++) { consent.AdvanceMotion(.05); }
            Assert.AreEqual(420d, consent.SurfaceBox.Width, .01);
            Assert.AreEqual(26d, consent.SurfaceRadius, .01);
            Assert.AreEqual(ControlSpring.Physical(.46), consent.GrowTuning, 1e-6, "growing uses the slower spring");
            consent.CommitForTest(false);
            Assert.AreEqual(CookieMode.Chip, consent.Mode);
            for (int i = 0; i < 80; i++) { consent.AdvanceMotion(.05); }
            Assert.AreEqual(44d, consent.SurfaceBox.Width, .01);
            Assert.AreEqual(44d, consent.SurfaceBox.Height, .01);
            Assert.AreEqual(22d, consent.SurfaceRadius, .01);
            Assert.AreEqual(ControlSpring.Physical(.38), consent.GrowTuning, 1e-6, "shrinking uses the quicker spring");
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void ReopeningAPanelSitsOnTheChipSide() => Sta.Run(() =>
    {
        var consent = new CookieConsent();
        var window = Host(consent);
        try
        {
            window.Show(); Sta.Pump(); consent.UpdateLayout();
            consent.CommitForTest(true);
            consent.OpenPreferences();
            for (int i = 0; i < 80; i++) { consent.AdvanceMotion(.05); }
            Assert.AreEqual(0d, consent.SurfaceBox.X, .01);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void HidingTheReopenButtonLeavesNoSurface() => Sta.Run(() =>
    {
        var consent = new CookieConsent { ShowReopenButton = false };
        var window = Host(consent);
        try
        {
            window.Show(); Sta.Pump();
            consent.CommitForTest(true);
            Assert.AreEqual(CookieMode.None, consent.Mode);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void DraftOnlyReachesConsentWhenSaved() => Sta.Run(() =>
    {
        var consent = new CookieConsent();
        var window = Host(consent);
        try
        {
            window.Show(); Sta.Pump();
            consent.CommitForTest(false);
            consent.OpenPreferences();
            consent.Categories.First(category => category.Id == "analytics").DraftEnabled = true;
            consent.ClosePreferences();
            Assert.IsFalse(consent.Consent!["analytics"]);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void StoredChoiceStartsFoldedAndResetAsksAgain() => Sta.Run(() =>
    {
        string key = "arcwpf-test-" + Guid.NewGuid().ToString("N");
        var first = new CookieConsent { StorageKey = key };
        first.CommitForTest(true);
        var second = new CookieConsent { StorageKey = key };
        var window = Host(second);
        try
        {
            window.Show(); Sta.Pump();
            Assert.AreEqual(CookieMode.Chip, second.Mode);
            second.Reset();
            Assert.AreEqual(CookieMode.Bar, second.Mode);
            Assert.IsNull(second.Consent);
        }
        finally { window.Close(); second.Reset(); }
    });

    [TestMethod]
    public void BarStacksBelowItsBreakpointAndSitsOnOneRowAboveIt() => Sta.Run(() =>
    {
        var narrow = new CookieConsent();
        var wide = new CookieConsent();
        var one = Host(narrow, 522);
        var two = Host(wide, 900);
        try
        {
            one.Show(); two.Show(); Sta.Pump(); narrow.UpdateLayout(); wide.UpdateLayout();
            for (int i = 0; i < 80; i++) { narrow.AdvanceMotion(.05); wide.AdvanceMotion(.05); }
            Assert.IsGreaterThan(wide.SurfaceBox.Height, narrow.SurfaceBox.Height, "a stacked bar is taller than a single row");
            Assert.AreEqual(660d, wide.SurfaceBox.Width, .01);
            Assert.AreEqual(44d, CookieChipHeight(wide), .01);
        }
        finally { one.Close(); two.Close(); }
    });

    private static double CookieChipHeight(CookieConsent consent)
    {
        consent.CommitForTest(true);
        for (int i = 0; i < 80; i++) { consent.AdvanceMotion(.05); }
        Assert.AreEqual(44d, consent.CurrentFace!.Width, .01, "the chip face has a real width, or its icon does not draw");
        return consent.SurfaceBox.Height;
    }
}
