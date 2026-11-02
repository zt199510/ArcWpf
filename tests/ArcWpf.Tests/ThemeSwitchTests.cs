using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass]
public sealed class ThemeSwitchTests
{
    private static void Shown(FrameworkElement content, Action<Window> body)
    {
        var window = new Window { Width = 400, Height = 300, Content = content, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        try { window.Show(); window.UpdateLayout(); Sta.Pump(); body(window); }
        finally { window.Close(); }
    }

    [TestMethod]
    public void ToggleChangesThemeAndRaisesEvent() => Sta.Run(() =>
    {
        var c = new ThemeSwitch();
        ThemeSwitchTheme? seen = null;
        c.ThemeChanged += (_, e) => seen = e.Theme;
        c.Toggle();
        Assert.AreEqual(ThemeSwitchTheme.Dark, c.Theme);
        Assert.AreEqual(ThemeSwitchTheme.Dark, seen);
    });

    [TestMethod]
    public void ThemeSwitchSupportsAllVariants() => Sta.Run(() =>
    {
        foreach (var variant in Enum.GetValues<ThemeSwitchVariant>())
        {
            var c = new ThemeSwitch { Variant = variant };
            c.Toggle();
            Assert.AreEqual(variant, c.Variant);
        }
    });

    [TestMethod]
    public void MatchesSourceGeometry() => Sta.Run(() =>
    {
        var labelled = new ThemeSwitch();
        Shown(labelled, _ =>
        {
            Assert.AreEqual(36, labelled.ActualHeight);
            var wrap = (Panel)labelled.Template.FindName("PART_IconWrap", labelled);
            Assert.AreEqual(20, wrap.ActualWidth);
            Assert.AreEqual(13, wrap.TransformToAncestor(labelled).Transform(new Point()).X, "border 1 + padding 12");
        });
        var iconOnly = new ThemeSwitch { IconOnly = true };
        Shown(iconOnly, _ =>
        {
            Assert.AreEqual(38, iconOnly.ActualWidth);
            Assert.AreEqual(38, iconOnly.ActualHeight);
            var surface = (Border)iconOnly.Template.FindName("PART_Surface", iconOnly);
            Assert.AreEqual(12, surface.CornerRadius.TopLeft);
        });
    });

    [TestMethod]
    public void NameFollowsThemeUnlessLabelled() => Sta.Run(() =>
    {
        var c = new ThemeSwitch();
        Assert.AreEqual("Switch to dark mode", AutomationProperties.GetName(c));
        c.Toggle();
        Assert.AreEqual("Switch to light mode", AutomationProperties.GetName(c));
        c.Label = "Theme";
        Assert.AreEqual("Theme", AutomationProperties.GetName(c));
    });

    [TestMethod]
    public void PressedStateIsExposedAsToggle() => Sta.Run(() =>
    {
        var c = new ThemeSwitch();
        var toggle = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(c).GetPattern(PatternInterface.Toggle);
        Assert.AreEqual(ToggleState.Off, toggle.ToggleState);
        c.Toggle();
        Assert.AreEqual(ToggleState.On, toggle.ToggleState);
    });

    [TestMethod]
    public void DarkThemeNudgesIconPerVariant() => Sta.Run(() =>
    {
        var c = new ThemeSwitch { Variant = ThemeSwitchVariant.Rise };
        ArcMotion.SetLevel(c, ArcMotionLevel.Off);
        Shown(c, _ =>
        {
            c.Toggle();
            var wrap = (Panel)c.Template.FindName("PART_IconWrap", c);
            var move = ((TransformGroup)wrap.RenderTransform).Children.OfType<TranslateTransform>().Single();
            Assert.AreEqual(-2, move.Y);
            c.Toggle();
            Assert.AreEqual(0, move.Y);
        });
    });
}
