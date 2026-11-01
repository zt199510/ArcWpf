using ArcWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace ArcWpf.Tests;
[TestClass] public sealed class ScrollBarThemeTests
{
    private static IEnumerable<DependencyObject> Walk(DependencyObject node) { yield return node; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) foreach (var child in Walk(VisualTreeHelper.GetChild(node, i))) yield return child; }
    private static StackPanel Tall() { var rows = new StackPanel(); for (int i = 0; i < 40; i++) rows.Children.Add(new TextBlock { Text = $"Row {i}", Height = 30 }); return rows; }
    private static Border Pill(ScrollBar bar) => Walk(bar).OfType<Border>().Single(item => item.Name == "Pill");

    private static void AssertArcBar(ScrollBar bar)
    {
        Assert.AreEqual(11d, bar.Orientation == Orientation.Vertical ? bar.ActualWidth : bar.ActualHeight, .5);
        Assert.AreEqual(5d, bar.Orientation == Orientation.Vertical ? Pill(bar).ActualWidth : Pill(bar).ActualHeight, .5);
        Assert.IsFalse(Walk(bar).OfType<RepeatButton>().Any(item => item.ActualHeight > 0 && item.Content is not null), "Arc bars have no arrow buttons.");
    }

    [TestMethod] public void EveryControlWithAVisibleScrollBarSharesTheArcBar() => Sta.Run(() =>
    {
        var items = Enumerable.Range(0, 40).Select(index => new ListItem { Title = $"Item {index}" }).ToList();
        var list = new List { Height = 180, ItemsSource = items };
        var box = new System.Windows.Controls.TextBox { Height = 120, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Text = string.Join("\n", Enumerable.Range(0, 40)) };
        var plain = new ScrollViewer { Height = 120, Content = Tall() };
        var wide = new ScrollViewer { Height = 80, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = new Border { Width = 2000, Height = 40 } };
        var root = new StackPanel { Children = { list, box, plain, wide } };
        var window = new Window { Content = root, Width = 500, Height = 700, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); root.UpdateLayout();
            foreach (var host in new FrameworkElement[] { list, box, plain, wide })
            {
                var bars = Walk(host).OfType<ScrollBar>().Where(item => item.Visibility == Visibility.Visible).ToList();
                Assert.AreEqual(1, bars.Count, host.GetType().Name);
                AssertArcBar(bars[0]);
            }
            Assert.AreEqual(Orientation.Horizontal, Walk(wide).OfType<ScrollBar>().Single(item => item.Visibility == Visibility.Visible).Orientation);
        }
        finally { window.Close(); }
    });

    [TestMethod] public void FloatingSurfacesShareTheArcBar() => Sta.Run(() =>
    {
        var select = new Select { Width = 240 };
        for (int i = 0; i < 40; i++) select.Items.Add(new SelectItem { Content = $"Option {i}" });
        var drawer = new Drawer { Title = "Details", Content = Tall() };
        ArcMotion.SetLevel(drawer, ArcMotionLevel.Off);
        var host = new DrawerHost { Content = select, Drawer = drawer };
        var window = new Window { Content = host, Width = 700, Height = 500, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            select.IsDropDownOpen = true; Sta.Pump();
            var list = (ScrollViewer)select.Template.FindName("DropDownScrollViewer", select);
            list.UpdateLayout();
            AssertArcBar(Walk(list).OfType<ScrollBar>().Single(item => item.Orientation == Orientation.Vertical && item.Visibility == Visibility.Visible));
            select.IsDropDownOpen = false; Sta.Pump();
            drawer.IsOpen = true; Sta.Pump(); host.UpdateLayout();
            AssertArcBar(Walk(drawer).OfType<ScrollBar>().Single(item => item.Orientation == Orientation.Vertical && item.Visibility == Visibility.Visible));
        }
        finally { window.Close(); }
    });

    [TestMethod] public void ScrollBarStillPagesAndFollowsTheThemeBrush() => Sta.Run(() =>
    {
        var plain = new ScrollViewer { Content = Tall() };
        var window = new Window { Content = plain, Width = 300, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); plain.UpdateLayout();
            var bar = Walk(plain).OfType<ScrollBar>().Single(item => item.Orientation == Orientation.Vertical);
            ScrollBar.PageDownCommand.Execute(null, bar); Sta.Pump();
            Assert.IsTrue(plain.VerticalOffset > 0);
            var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
            var light = ((SolidColorBrush)Pill(bar).Background).Color;
            theme.Mode = ArcThemeMode.Dark; Sta.Pump();
            Assert.AreNotEqual(light, ((SolidColorBrush)Pill(bar).Background).Color);
        }
        finally { window.Close(); }
    });

    [TestMethod] public void ApplicationOverrideWinsOverTheThemeBar() => Sta.Run(() =>
    {
        var custom = new Style(typeof(ScrollBar)); custom.Setters.Add(new Setter(FrameworkElement.WidthProperty, 30d));
        var plain = new ScrollViewer { Content = Tall() };
        var window = new Window { Content = plain, Width = 300, Height = 300, ShowInTaskbar = false };
        Application.Current.Resources[typeof(ScrollBar)] = custom;
        try
        {
            window.Show(); Sta.Pump(); plain.UpdateLayout();
            Assert.AreEqual(30d, Walk(plain).OfType<ScrollBar>().Single(item => item.Orientation == Orientation.Vertical).ActualWidth, .5);
        }
        finally { window.Close(); }
    });
}
