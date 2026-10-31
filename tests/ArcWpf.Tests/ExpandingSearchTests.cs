using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ExpandingSearchTests
{
    [TestMethod]
    public void TriggerExpandsAndReturnsFocusOnEscape()
    {
        Sta.Run(() => InWindow(search =>
        {
            var trigger = (System.Windows.Controls.Button)search.Template.FindName("PART_Trigger", search)!;
            trigger.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Sta.Pump(); Assert.IsTrue(search.IsExpanded);
            var input = (System.Windows.Controls.TextBox)search.Template.FindName("PART_Input", search)!; input.Text = "abc"; RaiseKey(input, Key.Escape); Sta.Pump();
            Assert.AreEqual(string.Empty, search.Query); Assert.IsTrue(search.IsExpanded); RaiseKey(input, Key.Escape); Sta.Pump(); Assert.IsFalse(search.IsExpanded);
        }));
    }

    [TestMethod]
    public void ClearButtonClearsQueryWithoutCollapsing()
    {
        Sta.Run(() => InWindow(search =>
        {
            search.IsExpanded = true; search.Query = "abc"; Sta.Pump();
            var clear = (System.Windows.Controls.Button)search.Template.FindName("PART_Clear", search)!; clear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.AreEqual(string.Empty, search.Query); Assert.IsTrue(search.IsExpanded);
        }));
    }

    [TestMethod]
    public void EnterRaisesSubmittedEvent()
    {
        Sta.Run(() => InWindow(search =>
        {
            var submitted = false; search.Submitted += (_, _) => submitted = true; search.IsExpanded = true; search.Query = "arc"; Sta.Pump();
            var input = (System.Windows.Controls.TextBox)search.Template.FindName("PART_Input", search)!; RaiseKey(input, Key.Enter); Assert.IsTrue(submitted);
        }));
    }

    [TestMethod]
    public void ExpandedSearchShowsRecentPanelWhenItemsExist()
    {
        Sta.Run(() => InWindow(search =>
        {
            search.Items.Add(new ExpandingSearchItem { Title = "Release checklist", Meta = "Engineering · Edited Sep 18" });
            search.IsExpanded = true; Sta.Pump();
            var results = (Border)search.Template.FindName("PART_Results", search)!;
            Assert.AreEqual(Visibility.Visible, results.Visibility);
            Assert.IsGreaterThan(0, results.ActualWidth);
        }));
    }

    private static void InWindow(Action<ExpandingSearch> action)
    {
        var search = new ExpandingSearch { WidthExpanded = 320, Width = 320 };
        var window = new Window { Content = search, Width = 420, Height = 120, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(search); }
        finally { window.Close(); Sta.Pump(); }
    }
    private static void RaiseKey(UIElement element, Key key) { var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = element }; element.RaiseEvent(args); }
}
