using System.Windows;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class CitationListTests
{
    private static CitationSource[] Sources(int count) => [.. Enumerable.Range(1, count).Select(index => new CitationSource($"Title {index}", $"https://www.site{index}.com/a/b") { Snippet = "Passage" })];

    private static void Run(Action<CitationList> setup, Action<CitationList> action, double width = 520)
    {
        Sta.Run(() =>
        {
            var list = new CitationList();
            ArcMotion.SetLevel(list, ArcMotionLevel.Off);
            setup(list);
            var window = new Window { Content = list, Width = width, Height = 500, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(list); }
            finally { window.Close(); list.PreviewForTest.Hide(); }
        });
    }

    [TestMethod]
    public void TheDomainDropsWww() => Assert.AreEqual("site1.com", Sources(1)[0].Domain);

    [TestMethod]
    public void CardsPastTheMaximumCollapseIntoTheStack() => Run(list => { list.Sources = Sources(7); list.Total = 7; list.Status = CitationStatus.Done; }, list =>
    {
        var kinds = list.CardKindsForTest;
        Assert.AreEqual(4, kinds.Length);
        Assert.AreEqual(3, kinds.Count(kind => kind == "card"));
        list.IsExpanded = true;
        Sta.Pump();
        Assert.AreEqual(7, list.CardKindsForTest.Count(kind => kind == "card"));
    });

    [TestMethod]
    public void NarrowContainersUseTwoColumns()
    {
        Run(list => { list.Sources = Sources(4); list.Status = CitationStatus.Done; }, list => Assert.AreEqual(2, list.ColumnsForTest), 400);
        Run(list => { list.Sources = Sources(4); list.Status = CitationStatus.Done; }, list => Assert.AreEqual(4, list.ColumnsForTest), 700);
    }

    [TestMethod]
    public void SearchingCountsAndReservesItsSlots() => Run(list => { list.Status = CitationStatus.Searching; list.Total = 3; list.Sources = Sources(1); }, list =>
    {
        StringAssert.Contains(list.StatusForTest, "1");
        StringAssert.Contains(list.StatusForTest, "3");
        Assert.AreEqual(3, list.CardKindsForTest.Length);
    });

    [TestMethod]
    public void AFailedSearchKeepsSourcesAndOffersRetry() => Run(list => { list.Sources = Sources(2); list.Error = "No answer"; list.Status = CitationStatus.Error; }, list =>
    {
        int retries = 0;
        list.RetryRequested += (_, _) => retries++;
        Assert.AreEqual(2, list.CardKindsForTest.Count(kind => kind == "card"));
        list.StatusActionForTest!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(1, retries);
    });

    [TestMethod]
    public void HoverOpensThePreviewAfterTheIntentDelayAndLeavingClosesIt() => Run(list => { list.Sources = Sources(3); list.Status = CitationStatus.Done; }, list =>
    {
        list.HoverForTest(0, false);
        Assert.IsTrue(list.HoverPendingForTest);
        Assert.IsFalse(list.PreviewForTest.IsOpen);
        list.LeaveForTest();
        Assert.IsFalse(list.HoverPendingForTest);
        list.HoverForTest(1);
        Assert.IsTrue(list.PreviewForTest.IsOpen);
        Assert.AreEqual("Title 2", list.PreviewForTest.Source!.Title);
        list.HoverForTest(2);
        Assert.AreEqual("Title 3", list.PreviewForTest.Source!.Title);
        list.LeaveForTest();
        Assert.IsFalse(list.PreviewForTest.IsOpen);
    });

    [TestMethod]
    public void ACitationClickHighlightsItsCardAndThenLetsGo()
    {
        var chip = (SourceCitation?)null;
        Run(list =>
        {
            list.Sources = Sources(3);
            list.Status = CitationStatus.Done;
            chip = new SourceCitation { Number = 2 };
            list.Content = chip;
        }, list =>
        {
            Assert.IsTrue(chip!.IsVisible, "The answer content renders.");
            list.ClickCitationForTest(chip);
            Assert.AreEqual(2, list.Highlighted);
            Assert.IsTrue(chip.IsLit);
            list.UnlightNowForTest();
            Assert.IsNull(list.Highlighted);
            Assert.IsFalse(chip.IsLit);
        });
    }

    [TestMethod]
    public void ThePreviewSitsAboveItsTargetWithAGap() => Sta.Run(() =>
    {
        var target = new System.Windows.Controls.Button { Width = 200, Height = 80, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left };
        ArcMotion.SetLevel(target, ArcMotionLevel.Off);
        var window = new Window { Content = target, Left = 200, Top = 300, Width = 500, Height = 400, ShowActivated = false, ShowInTaskbar = false };
        var popover = new SourcePopover();
        try
        {
            window.Show();
            Sta.Pump();
            popover.Show(target, Sources(1)[0], 1);
            Sta.Pump();
            var surface = popover.SurfaceForTest;
            double bottom = surface.PointToScreen(new Point(0, surface.ActualHeight)).Y, top = target.PointToScreen(new Point()).Y;
            Assert.AreEqual(top - 8, bottom, 1.5);
            Assert.AreEqual(target.PointToScreen(new Point()).X, surface.PointToScreen(new Point()).X, 1.5);
        }
        finally { popover.Hide(); window.Close(); }
    });

    [TestMethod]
    public void TheStandalonePopoverShowsAndHides() => Sta.Run(() =>
    {
        var target = new System.Windows.Controls.Button { Width = 40, Height = 20 };
        var window = new Window { Content = target, Width = 400, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        var popover = new SourcePopover();
        try
        {
            window.Show();
            Sta.Pump();
            popover.Show(target, Sources(1)[0], 1);
            Assert.IsTrue(popover.IsOpen);
            Assert.AreSame(target, popover.Target);
            popover.Hide();
            Assert.IsFalse(popover.IsOpen);
        }
        finally { popover.Hide(); window.Close(); }
    });
}
