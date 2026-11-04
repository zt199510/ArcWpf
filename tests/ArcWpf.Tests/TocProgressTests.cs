using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class TocProgressTests
{
    private static void Run(Action<TocProgress, ScrollViewer> action, bool area = false)
    {
        Sta.Run(() =>
        {
            var article = new StackPanel();
            var items = new TocProgressItemCollection();
            for (int i = 0; i < 6; i++)
            {
                var heading = new TextBlock { Text = $"Section {i}", Height = 40 };
                article.Children.Add(heading);
                article.Children.Add(new Border { Height = 400 });
                items.Add(new TocProgressItem { Id = $"s{i}", Label = $"Section {i}", Level = i is 2 ? 2 : i is 3 ? 3 : 1, Target = heading });
            }
            var viewer = new ScrollViewer { Content = article, Height = 400 };
            var wrap = new ScrollArea { Height = 400, Content = article };
            var toc = new TocProgress { Items = items, ReadingTime = 6, Title = "On this page", Width = 300, VerticalAlignment = VerticalAlignment.Top };
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition() } };
            grid.Children.Add(toc);
            FrameworkElement reader = area ? wrap : viewer;
            if (area) { viewer.Content = null; } else { wrap.Content = null; viewer.Content = article; }
            Grid.SetColumn(reader, 1);
            grid.Children.Add(reader);
            toc.ScrollContainer = reader;
            var window = new Window { Content = grid, Width = 900, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); toc.MeasureForTest(); action(toc, area ? wrap.Viewport! : viewer); }
            finally { window.Close(); }
        });
    }

    private static void Settle(TocProgress toc) { Sta.Pump(); for (int i = 0; i < 300; i++) { toc.AdvanceForTest(1d / 60); } Sta.Pump(); }

    [TestMethod]
    public void StartsAtTheFirstHeadingWithTheFullReadingTime() => Run((toc, viewer) =>
    {
        Assert.AreEqual(0, toc.CurrentForTest);
        Assert.AreEqual("6 min left", toc.TimeForTest);
        Assert.AreEqual(0, toc.PositionForTest, .05);
    });

    [TestMethod]
    public void ScrollingMovesTheLineAndTheCurrentHeadingOnlyForward() => Run((toc, viewer) =>
    {
        double progress = toc.ProgressForTest;
        viewer.ScrollToVerticalOffset(900);
        Settle(toc);
        Assert.AreEqual(2, toc.CurrentForTest);
        Assert.IsGreaterThan(progress, toc.ProgressForTest);
        viewer.ScrollToVerticalOffset(0);
        Settle(toc);
        Assert.AreEqual(0, toc.CurrentForTest);
    });

    [TestMethod]
    public void TheBottomOfTheArticleFinishesTheLine() => Run((toc, viewer) =>
    {
        viewer.ScrollToBottom();
        Settle(toc);
        Assert.AreEqual(5, toc.CurrentForTest);
        Assert.AreEqual("Finished", toc.TimeForTest);
        Assert.AreEqual(toc.TotalLengthForTest, toc.ProgressForTest, .5);
    });

    [TestMethod]
    public void ChoosingAHeadingScrollsThereAndReports() => Run((toc, viewer) =>
    {
        string? navigated = null, active = null;
        toc.Navigate += (_, e) => navigated = e.Id;
        toc.ActiveChanged += (_, e) => active = e.Id;
        toc.ChooseForTest(3);
        for (int i = 0; i < 300 && toc.ScrollingForTest; i++) { toc.AdvanceForTest(1d / 60); }
        Settle(toc);
        Assert.AreEqual("s3", navigated);
        Assert.AreEqual("s3", active);
        Assert.AreEqual(3, toc.CurrentForTest);
        Assert.IsGreaterThan(1000d, viewer.VerticalOffset);
    });

    [TestMethod]
    public void AScrollAreaIsFollowedThroughItsViewport() => Run((toc, viewer) =>
    {
        viewer.ScrollToVerticalOffset(900);
        Settle(toc);
        Assert.AreEqual(2, toc.CurrentForTest);
    }, true);

    [TestMethod]
    public void TheLineBendsIntoTheLaneOfEachLevel() => Run((toc, viewer) =>
    {
        var xs = toc.LinePointsForTest.Select(point => Math.Round(point.X)).Distinct().ToList();
        CollectionAssert.Contains(xs, 5d);
        CollectionAssert.Contains(xs, 17d);
        CollectionAssert.Contains(xs, 29d);
    });

    [TestMethod]
    public void ArrowKeysMoveAmongHeadings() => Run((toc, viewer) =>
    {
        toc.RowForTest(0).Focus();
        toc.KeyForTest(0, Key.Down);
        Assert.IsTrue(toc.RowForTest(1).IsFocused || toc.RowForTest(1).IsKeyboardFocused);
    });
}
