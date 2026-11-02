using ArcWpf;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ThemeSwitchTests
{
    [TestMethod] public void ToggleChangesThemeAndRaisesEvent() => Sta.Run(() => { var c = new ThemeSwitch(); ThemeSwitchTheme? seen = null; c.ThemeChanged += (_, e) => seen = e.Theme; c.Toggle(); Assert.AreEqual(ThemeSwitchTheme.Dark, c.Theme); Assert.AreEqual(ThemeSwitchTheme.Dark, seen); });
    [TestMethod] public void ThemeSwitchSupportsAllVariants() => Sta.Run(() => { foreach (var variant in Enum.GetValues<ThemeSwitchVariant>()) { var c = new ThemeSwitch { Variant = variant }; c.Toggle(); Assert.AreEqual(variant, c.Variant); } });
}
