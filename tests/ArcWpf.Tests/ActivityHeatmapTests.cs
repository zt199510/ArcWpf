using System.Windows;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ActivityHeatmapTests
{
    private static ActivityDay[] Year(int year, Func<int, int> count)
    {
        var start = new DateOnly(year, 1, 1);
        int days = new DateOnly(year + 1, 1, 1).DayNumber - start.DayNumber;
        return [.. Enumerable.Range(0, days).Select(index => new ActivityDay(start.AddDays(index), count(index)))];
    }

    private static void Run(Action<ActivityHeatmap> action, double width = 520)
    {
        Sta.Run(() =>
        {
            var chart = new ActivityHeatmap { Label = "Contributions", Period = "2025", Thresholds = [3, 6, 10], Days = Year(2025, index => index % 13) };
            var window = new Window { Content = chart, Width = width, Height = 400, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); chart.RevealForTest(); chart.AdvanceForTest(2); action(chart); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void ModelLaysDaysIntoWeeksAndLevels()
    {
        var model = HeatmapModel.Build(Year(2025, index => index % 13), DayOfWeek.Sunday, (3, 6, 10));
        Assert.AreEqual(365, model.Length);
        Assert.AreEqual(3, model.Lead, "January 1, 2025 was a Wednesday");
        Assert.AreEqual(53, model.Weeks);
        Assert.AreEqual(0, model.Levels[0]);
        Assert.AreEqual(1, model.Levels[3]);
        Assert.AreEqual(2, model.Levels[4]);
        Assert.AreEqual(4, model.Levels[11]);
        Assert.AreEqual(365, model.PerLevel.Sum());
    }

    [TestMethod]
    public void DefaultBoundsAreQuartersOfTheBusiestDay()
    {
        var model = HeatmapModel.Build(Year(2025, index => index == 9 ? 20 : 1), DayOfWeek.Monday, null);
        Assert.AreEqual((5, 10, 15), model.Bounds);
        Assert.AreEqual(2, model.Lead, "Monday first shifts the lead");
    }

    [TestMethod]
    public void APartialFirstMonthLosesItsLabelWhenCrowded()
    {
        var model = HeatmapModel.Build(Year(2025, _ => 0).Skip(25).ToArray(), DayOfWeek.Sunday, null);
        Assert.AreEqual("Feb", model.Months[0].Label);
    }

    [TestMethod]
    public void CellsClampAndRestOnTheLatestWeeks() => Run(chart =>
    {
        Assert.AreEqual(9, chart.CellForTest);
        Assert.IsTrue(chart.OffsetForTest > 0, "a narrow card rests at its right edge");
        Assert.AreEqual(53 * 7, chart.CellsForTest);
    });

    [TestMethod]
    public void HoverOpensABubbleAndLeavingClosesIt() => Run(chart =>
    {
        chart.HoverForTest(chart.CenterOfForTest(300));
        Assert.IsTrue(chart.TipOpenForTest);
        StringAssert.Contains(chart.TipForTest, "1 contribution|");
        StringAssert.Contains(chart.TipForTest, "Tue, Oct 28, 2025");
        chart.HideNowForTest();
        Assert.IsFalse(chart.TipOpenForTest);
    });

    [TestMethod]
    public void LegendPreviewDimsOtherLevelsAndCaptions() => Run(chart =>
    {
        chart.PreviewForTest(2);
        Assert.AreEqual(2, chart.HighlightForTest);
        Assert.AreEqual(.2, chart.OpacityTargetOfForTest(0), 1e-9);
        Assert.AreEqual(1, chart.OpacityTargetOfForTest(4), 1e-9);
        StringAssert.Contains(chart.CaptionForTest, "days with 4–6 contributions");
        chart.PinForTest(4);
        chart.PinForTest(4);
        Assert.AreEqual(2, chart.HighlightForTest);
    });

    [TestMethod]
    public void ArrowsMoveByDayAndWeekAndEnterChooses() => Run(chart =>
    {
        DateOnly? chosen = null;
        chart.DateChosen += (_, e) => chosen = e.Date;
        chart.FocusIndexForTest(100);
        chart.KeyForTest(Key.Down);
        Assert.AreEqual(101, chart.FocusForTest);
        chart.KeyForTest(Key.Right);
        Assert.AreEqual(108, chart.FocusForTest);
        chart.KeyForTest(Key.Enter);
        Assert.AreEqual(new DateOnly(2025, 1, 1).AddDays(108), chosen);
        Assert.AreEqual(chosen, chart.SelectedDate);
        chart.KeyForTest(Key.Home);
        Assert.AreEqual(0, chart.FocusForTest);
        chart.KeyForTest(Key.Escape);
        Assert.IsFalse(chart.TipOpenForTest);
    });

    [TestMethod]
    public void ANewRangeRecolorsAndUpdatesTheSummary() => Run(chart =>
    {
        chart.Days = Year(2024, _ => 11);
        chart.Period = "2024";
        Assert.AreEqual(chart.LevelColorForTest(4), chart.ColorOfForTest(10));
        Assert.AreEqual($"{4026:#,0} contributions in 2024", chart.SummaryForTest);
        Assert.AreEqual(366, chart.ModelForTest.Length);
    });

    [TestMethod]
    public void WideCardsGrowTheCellsToTheirLimit() => Run(chart => Assert.AreEqual(15, chart.CellForTest), 1200);

    [TestMethod]
    public void CellsWaveInAlongTheDiagonalOnceSeen() => ChartHost.BelowTheFold(() => new ActivityHeatmap { Label = "Contributions", Period = "2025", Days = Year(2025, index => index % 13) }, chart =>
    {
        Assert.AreEqual(0, chart.OpacityOfForTest(0), "hidden until seen");
        chart.RevealForTest();
        chart.AdvanceForTest(.05);
        Assert.IsTrue(chart.OpacityOfForTest(0) > 0, "the first day starts at once");
        Assert.AreEqual(0, chart.OpacityOfForTest(364), "the last waits its turn");
        chart.AdvanceForTest(2);
        Assert.AreEqual(1, chart.OpacityOfForTest(364));
    });

    [TestMethod]
    public void WithoutMotionItSettlesAsSoonAsItLoads() => Sta.Run(() =>
    {
        var chart = new ActivityHeatmap { Label = "Contributions", Period = "2025", Days = Year(2025, index => index % 13) };
        ArcMotion.SetLevel(chart, ArcMotionLevel.Off);
        var window = new Window { Content = chart, Width = 520, Height = 400, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            Assert.AreEqual(1, chart.OpacityOfForTest(364));
        }
        finally { window.Close(); }
    });
}
