using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class BillingToggleTests
{
    private static void Show(FrameworkElement element, Action action)
    {
        var window = new Window { Content = element, Width = 600, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(); }
        finally { window.Close(); }
    }

    private static BillingToggle Toggle() => new()
    {
        Value = "yearly", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        Options = new[] { new BillingToggleOption { Value = "monthly", Label = "Monthly" }, new BillingToggleOption { Value = "yearly", Label = "Yearly", Badge = "Save 20%", ActiveBadge = "Saving 20%" } },
    };

    [TestMethod] public void BadgeSaysSavingOnlyWhileItsOptionIsSelected() => Sta.Run(() =>
    {
        var toggle = Toggle();
        Show(toggle, () =>
        {
            Assert.AreEqual("Saving 20%", toggle.BadgeTextForTest(1));
            toggle.Value = "monthly";
            Sta.Pump();
            Assert.AreEqual("Save 20%", toggle.BadgeTextForTest(1));
        });
    });

    [TestMethod] public void ChangingValueRaisesValueChangedOnceAndMovesTheThumb() => Sta.Run(() =>
    {
        var toggle = Toggle();
        Show(toggle, () =>
        {
            var seen = new List<(string Old, string New)>();
            toggle.ValueChanged += (_, e) => seen.Add((e.OldValue, e.NewValue));
            double before = toggle.ThumbTargetXForTest;
            toggle.Value = "monthly";
            Sta.Pump();
            CollectionAssert.AreEqual(new[] { ("yearly", "monthly") }, seen);
            Assert.IsLessThan(before, toggle.ThumbTargetXForTest);
        });
    });

    [TestMethod] public void CheckingAnOptionSelectsIt() => Sta.Run(() =>
    {
        var toggle = Toggle();
        Show(toggle, () =>
        {
            toggle.OptionForTest(0).IsChecked = true;
            Sta.Pump();
            Assert.AreEqual("monthly", toggle.Value);
        });
    });

    [TestMethod] public void PriceShowsWasOnlyWhenHigherThanAmount() => Sta.Run(() =>
    {
        var price = new BillingPrice { Currency = "$", Amount = 8, Was = 10, Period = "/ mo" };
        Show(price, () =>
        {
            Assert.IsTrue(price.WasVisibleForTest);
            Assert.AreEqual("$10", price.WasTextForTest);
            Assert.AreEqual("/ mo", price.PeriodTextForTest);
            price.Was = 8;
            Sta.Pump();
            Assert.IsFalse(price.WasVisibleForTest);
        });
    });
}
