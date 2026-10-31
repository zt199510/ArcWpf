using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ExpandingSearchTests
{
    private static ExpandingSearchItem Item(string id, string title, string? group = null, string? meta = null, params string[] keywords) => new() { Id = id, Title = title, Group = group, Meta = meta, Keywords = keywords };

    [TestMethod]
    public void RankPrefersTitleStartThenWordStartThenInsideThenKeywords()
    {
        var items = new[] { Item("a", "Keyword only", keywords: "plan"), Item("b", "Display plans"), Item("c", "Q4-planning"), Item("d", "Planning board") };
        CollectionAssert.AreEqual(new[] { "d", "b", "c", "a" }, ExpandingSearch.Rank(items, "PLAN", 6).Select(i => i.Id).ToArray());
        Assert.HasCount(2, ExpandingSearch.Rank(items, "plan", 2));
    }

    [TestMethod]
    public void TriggerExpandsAndEscapeFoldsBackToTheButton()
    {
        Sta.Run(() => InWindow(search =>
        {
            var trigger = (System.Windows.Controls.Button)search.Template.FindName("PART_Trigger", search)!;
            trigger.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Sta.Pump();
            Assert.IsTrue(search.IsExpanded);
            var input = (TextBox)search.Template.FindName("PART_Input", search)!;
            Assert.IsTrue(input.IsKeyboardFocused);
            input.Text = "bill"; Sta.Pump();
            Assert.AreEqual("bill", search.Query);
            RaiseKey(input, Key.Escape); Sta.Pump();
            Assert.IsFalse(search.IsExpanded);
            Assert.AreEqual(string.Empty, search.Query);
            Assert.IsTrue(trigger.IsKeyboardFocused);
        }));
    }

    [TestMethod]
    public void ArrowsWrapAndEnterChoosesTheActiveResult()
    {
        Sta.Run(() => InWindow(search =>
        {
            search.Items.Add(Item("p", "Billing migration", "Projects")); search.Items.Add(Item("d", "Billing FAQ", "Docs"));
            ExpandingSearchItem? chosen = null; search.ItemSelected += (_, e) => chosen = e.Item;
            search.IsExpanded = true; Sta.Pump();
            var input = (TextBox)search.Template.FindName("PART_Input", search)!;
            input.Text = "billing"; Sta.Pump();
            RaiseKey(input, Key.Up); // from the first result, Up wraps to the last
            RaiseKey(input, Key.Enter); Sta.Pump();
            Assert.AreEqual("d", chosen?.Id);
            Assert.IsFalse(search.IsExpanded);
        }));
    }

    [TestMethod]
    public void ClearEmptiesQueryAndKeepsTheFieldOpen()
    {
        Sta.Run(() => InWindow(search =>
        {
            search.IsExpanded = true; search.Query = "abc"; Sta.Pump();
            var clear = (System.Windows.Controls.Button)search.Template.FindName("PART_Clear", search)!;
            clear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Sta.Pump();
            Assert.AreEqual(string.Empty, search.Query);
            Assert.IsTrue(search.IsExpanded);
        }));
    }

    [TestMethod]
    public void ShellWidthFollowsExpandedWidthCappedByTheLane()
    {
        Sta.Run(() => InWindow(search =>
        {
            ArcMotion.SetLevel(search, ArcMotionLevel.Off);
            var shell = (Border)search.Template.FindName("PART_Shell", search)!;
            Sta.Pump(); Assert.AreEqual(44, shell.ActualWidth, .5);
            search.IsExpanded = true; Sta.Pump(); Assert.AreEqual(320, shell.ActualWidth, .5); // lane 320 < ExpandedWidth 360
            Assert.AreEqual(18, shell.CornerRadius.TopLeft, .01);
        }));
    }

    private static void InWindow(Action<ExpandingSearch> action)
    {
        var search = new ExpandingSearch { Width = 320 };
        var window = new Window { Content = search, Width = 420, Height = 200, ShowInTaskbar = false };
        try { window.Show(); window.Activate(); Sta.Pump(); action(search); }
        finally { window.Close(); Sta.Pump(); }
    }
    private static void RaiseKey(UIElement element, Key key) => element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = element });
}
