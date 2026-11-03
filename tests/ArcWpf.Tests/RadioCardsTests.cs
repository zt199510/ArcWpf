using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class RadioCardsTests
{
    private static IEnumerable<RadioCardItem> Cards(RadioCards cards) => ((System.Windows.Controls.Panel)cards.Template.FindName("PART_Items", cards)!).Children.OfType<RadioCardItem>();

    private static RadioCards Three(string? selected = "a") => new()
    {
        Options = new ObservableCollection<RadioCardOption> { new() { Value = "a", Label = "A" }, new() { Value = "b", Label = "B", Disabled = true }, new() { Value = "c", Label = "C" } },
        SelectedValue = selected,
    };

    [TestMethod]
    public void SelectsEnabledOptionAndIgnoresDisabled() => Sta.Run(() =>
    {
        var cards = Three();
        cards.SelectOption(cards.Options[1]);
        Assert.AreEqual("a", cards.SelectedValue);
        cards.SelectOption(cards.Options[2]);
        Assert.AreEqual("c", cards.SelectedValue);
    });

    [TestMethod]
    public void TabStopUsesSelectedOrFirstUsableOption() => Sta.Run(() =>
    {
        var cards = new RadioCards { Options = new ObservableCollection<RadioCardOption> { new() { Value = "disabled", Disabled = true }, new() { Value = "first" }, new() { Value = "second" } } };
        Assert.IsTrue(cards.IsOptionTabStop(cards.Options[1]));
        Assert.IsFalse(cards.IsOptionTabStop(cards.Options[2]));
        cards.SelectedValue = "second";
        Assert.IsTrue(cards.IsOptionTabStop(cards.Options[2]));
        cards.SelectedValue = "disabled";
        Assert.IsTrue(cards.IsOptionTabStop(cards.Options[1]));
    });

    [TestMethod]
    public void GroupDisabledRefreshesSelectionAndTabSemantics() => Sta.Run(() =>
    {
        var cards = new RadioCards { Options = new ObservableCollection<RadioCardOption> { new() { Value = "a" }, new() { Value = "b" } } };
        cards.IsGroupDisabled = true;
        Assert.IsFalse(cards.IsOptionTabStop(cards.Options[0]));
        cards.SelectOption(cards.Options[1]);
        Assert.IsNull(cards.SelectedValue);
    });

    [TestMethod]
    public void GridUsesAutoFillColumnsAndTheRingCoversTheSelectedCard() => Sta.Run(() =>
    {
        var cards = Three("c");
        var window = new Window { Content = cards, Width = 522 + 16, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); cards.UpdateLayout();
            var items = Cards(cards).ToList();
            Assert.AreEqual(items[0].Box.Y, items[1].Box.Y, "two columns fit a 522 wide group");
            Assert.AreNotEqual(items[0].Box.Y, items[2].Box.Y);
            Assert.AreEqual(Math.Round(items[2].Box.Y), cards.RingBox.Y, .01);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void RingGlidesOnSelectionAndSnapsOnResize() => Sta.Run(() =>
    {
        var cards = Three();
        var window = new Window { Content = cards, Width = 538, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); cards.UpdateLayout();
            var from = cards.RingBox;
            cards.SelectedValue = "c";
            cards.AdvanceRing(.05);
            Assert.AreNotEqual(from, cards.RingBox);
            for (int i = 0; i < 80; i++) { cards.AdvanceRing(.05); }
            var target = Cards(cards).Last().Box;
            Assert.AreEqual(Math.Round(target.X), cards.RingBox.X, .01);
            window.Width = 340;
            Sta.Pump(); cards.UpdateLayout();
            target = Cards(cards).Last().Box;
            Assert.AreEqual(Math.Round(target.Y), cards.RingBox.Y, .01, "a resize follows the layout at once");
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void ArrowKeysWrapPastDisabledCards() => Sta.Run(() =>
    {
        var cards = Three();
        var window = new Window { Content = cards, Width = 538, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); cards.UpdateLayout();
            var first = Cards(cards).First();
            first.Focus();
            first.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(first)!, 0, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.AreEqual("c", cards.SelectedValue, "the disabled card is skipped");
            var last = Cards(cards).Last();
            last.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(last)!, 0, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.AreEqual("a", cards.SelectedValue, "arrows wrap");
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void DisabledCardShowsItsReasonInPlaceOfTheDescription() => Sta.Run(() =>
    {
        var cards = new RadioCards { Options = new ObservableCollection<RadioCardOption> { new() { Value = "a", Label = "A", Description = "Usual", DisabledReason = "Back in October", Disabled = true } } };
        var window = new Window { Content = cards, Width = 538, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); cards.UpdateLayout();
            Assert.AreEqual("Back in October", System.Windows.Automation.AutomationProperties.GetHelpText(Cards(cards).Single()));
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void HomeAndEndJumpPastDisabledCards() => Sta.Run(() =>
    {
        var cards = new RadioCards { Options = new ObservableCollection<RadioCardOption> { new() { Value = "off", Disabled = true }, new() { Value = "a" }, new() { Value = "b" } }, SelectedValue = "a" };
        var window = new Window { Content = cards, Width = 538, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); cards.UpdateLayout();
            var card = Cards(cards).ElementAt(1);
            card.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(card)!, 0, Key.End) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.AreEqual("b", cards.SelectedValue);
            card.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(card)!, 0, Key.Home) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.AreEqual("a", cards.SelectedValue, "Home lands on the first usable card");
        }
        finally { window.Close(); }
    });
}
