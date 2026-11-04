using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class InViewTitleTests
{
    private static InViewTitle Title(InViewTitleVariant variant = InViewTitleVariant.Blur) => new() { Text = "Make progress visible", FontSize = 44, Tracking = -.03, LineRatio = 1.1, Variant = variant };

    private static void Run(Func<InViewTitle> make, Action<InViewTitle> action) => Sta.Run(() =>
    {
        var title = make();
        var window = new Window { Content = title, Width = 700, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(title); } finally { window.Close(); }
    });

    /// <summary>Scrolls a title through a ScrollArea, the host this control is designed to sit in.</summary>
    private static void InScrollArea(Action<InViewTitle, ScrollViewer> action, bool once = true, double spacer = 600) => Sta.Run(() =>
    {
        var title = Title();
        title.Once = once;
        var content = new StackPanel { Children = { new Border { Height = spacer }, title, new Border { Height = 900 } } };
        var area = new ScrollArea { Content = content, Height = 200 };
        var window = new Window { Content = area, Width = 700, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var scroller = Find<ScrollViewer>(area)!;
            action(title, scroller);
        }
        finally { window.Close(); }
    });

    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T found) { return found; }
            if (Find<T>(child) is { } deeper) { return deeper; }
        }
        return null;
    }

    [TestMethod]
    public void ATitleInViewRevealsAndRestsPlain() => Run(() => Title(), title =>
    {
        Assert.IsTrue(title.IsRevealedForTest);
        title.FinishMotionForTest();
        Assert.AreEqual(1, title.PartOpacityForTest(2), .01);
        Assert.AreEqual(0, title.PartYForTest(2), .01);
        Assert.IsFalse(title.PartBlurredForTest(2));
    });

    [TestMethod]
    public void ScrollAreaRevealsTheTitleOnlyOnceInView() => InScrollArea((title, scroller) =>
    {
        Assert.IsFalse(title.IsRevealedForTest, "below the fold it stays hidden");
        Assert.AreEqual(0, title.PartOpacityForTest(0), .01);
        scroller.ScrollToVerticalOffset(550);
        Sta.Pump();
        Assert.IsTrue(title.IsRevealedForTest);
    });

    [TestMethod]
    public void OnceKeepsItAfterLeavingAndFalseHidesAgain()
    {
        InScrollArea((title, scroller) =>
        {
            scroller.ScrollToVerticalOffset(550); Sta.Pump();
            scroller.ScrollToVerticalOffset(0); Sta.Pump();
            Assert.IsTrue(title.IsRevealedForTest);
        });
        InScrollArea((title, scroller) =>
        {
            scroller.ScrollToVerticalOffset(550); Sta.Pump();
            scroller.ScrollToVerticalOffset(0); Sta.Pump();
            Assert.IsFalse(title.IsRevealedForTest);
        }, once: false);
    }

    [TestMethod]
    public void ATitleAlreadyAboveTheViewportShows() => InScrollArea((title, scroller) =>
    {
        scroller.ScrollToVerticalOffset(1000); Sta.Pump();
        Assert.IsTrue(title.IsRevealedForTest);
    });

    [TestMethod]
    public void WordStaggerAndHideDistances() => Run(() => Title(InViewTitleVariant.Word), title =>
    {
        Assert.AreEqual(3, title.PartCountForTest);
        Assert.AreEqual(.06, title.PartDelayForTest(1), 1e-6);
        title.Replay();
        Assert.AreEqual(.9 * 44, title.PartYForTest(0), .01);
    });

    [TestMethod]
    public void LineVariantStepsByLineHeightPlusItsGap() => Sta.Run(() =>
    {
        var title = Title(InViewTitleVariant.Line);
        title.Lines = ["Make progress", "visible"];
        var window = new Window { Content = title, Width = 700, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); title.FinishMotionForTest(); Sta.Pump();
            Assert.AreEqual(2, title.PartCountForTest);
            Assert.AreEqual(.08, title.PartDelayForTest(1), 1e-6);
            Assert.AreEqual(44 * 1.1 + .06 * 44, title.PartAtForTest(1).Y - title.PartAtForTest(0).Y, .1);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void TrackingSpreadsLettersAroundTheWordCentre() => Run(() => Title(InViewTitleVariant.Tracking), title =>
    {
        title.Replay();
        Assert.AreEqual(1.5 * .06 * 44, title.PartXForTest(0), .01, "first letter of Make");
        Assert.AreEqual(-1.5 * .06 * 44, title.PartXForTest(3), .01, "last letter of Make");
    });

    [TestMethod]
    public void WipeMasksWhileMovingAndNotAtRest() => Run(() => Title(InViewTitleVariant.Wipe), title =>
    {
        title.Replay();
        Assert.AreEqual(1, title.WipeForTest, .01);
        Assert.IsTrue(title.IsWipeMaskedForTest);
        Sta.Pump();
        title.FinishMotionForTest();
        Assert.AreEqual(0, title.WipeForTest, .01);
        Assert.IsFalse(title.IsWipeMaskedForTest);
    });

    [TestMethod]
    public void ReducedMotionIsVisibleImmediately() => Sta.Run(() =>
    {
        var title = Title();
        ArcMotion.SetLevel(title, ArcMotionLevel.Reduced);
        var window = new Window { Content = title, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            Assert.IsTrue(title.IsRevealedForTest);
            Assert.AreEqual(1, title.PartOpacityForTest(0), .01);
        }
        finally { window.Close(); }
    });
}
