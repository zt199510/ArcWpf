using System.Windows;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class DonutChartTests
{
    private static readonly DonutChartDatum[] Month = [new("search", "Search", 16840), new("direct", "Direct", 8120), new("newsletter", "Newsletter", 5260), new("referrals", "Referrals", 6430), new("social", "Social", 2910), new("podcasts", "Podcasts", 640), new("partners", "Partners", 410)];

    private static void Run(Action<DonutChart> action)
    {
        Sta.Run(() =>
        {
            var chart = new DonutChart { Label = "Visits", TotalLabel = "Visits", Data = Month, LegendPlacement = DonutLegendPlacement.Below };
            var window = new Window { Content = chart, Width = 520, Height = 700, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(chart); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void SmallPartsGroupOnlyWhenAtLeastTwoWould()
    {
        var grouped = DonutMath.Slices(Month, .04, 6, "Other");
        Assert.AreEqual(DonutChart.OtherKey, grouped[^1].Key);
        Assert.AreEqual(1050, grouped[^1].Value);
        CollectionAssert.AreEqual(new[] { "Podcasts", "Partners" }, grouped[^1].Members!.Select(member => member.Label).ToArray());
        var single = DonutMath.Slices([new("a", "A", 100), new("b", "B", 1)], .04, 6, "Other");
        Assert.AreEqual(2, single.Length, "one small part stays itself");
        Assert.AreEqual("b", single[1].Key);
    }

    [TestMethod]
    public void ShareTextMatchesIntlWithASliver()
    {
        Assert.AreEqual("41%", DonutMath.Share(.4147));
        Assert.AreEqual("<1%", DonutMath.Share(.004));
        Assert.AreEqual("0%", DonutMath.Share(0));
    }

    [TestMethod]
    public void SectorNarrowsToNothingAndCompletesARing()
    {
        Assert.IsNull(DonutMath.Sector(104, 97, 73, .2, .2, 3, 4));
        Assert.IsNotNull(DonutMath.Sector(104, 97, 73, .1, .3, 3, 4));
        Assert.IsNotNull(DonutMath.Sector(104, 97, 73, 0, 1, 3, 4));
        Assert.IsTrue(double.IsPositiveInfinity(DonutMath.Breadth(97, 0, 1, 3)));
        Assert.IsTrue(DonutMath.Breadth(97, 0, .001, 3) < 1);
    }

    [TestMethod]
    public void ArcsSweepInThenFillTheRing() => Run(chart =>
    {
        chart.ShowForTest();
        chart.AdvanceForTest(3);
        Assert.AreEqual(1, chart.ArcForTest(DonutChart.OtherKey).End, .001);
        Assert.AreEqual(16840d / 40610, chart.ArcForTest("search").End, .001);
        Assert.AreEqual("40,610", chart.CenterValueForTest);
    });

    [TestMethod]
    public void HoverLiftsASegmentAndRollsTheCenter() => Run(chart =>
    {
        chart.ShowForTest();
        chart.AdvanceForTest(3);
        Assert.AreEqual("search", chart.HitForTest(new Point(190, 104)), "angle picks the segment");
        Assert.IsNull(chart.HitForTest(new Point(104, 104)), "the hole is not a segment");
        chart.PreviewForTest("search");
        Assert.AreEqual("Search", chart.CenterForTest);
        chart.AdvanceForTest(1);
        Assert.AreEqual(1, chart.LiftForTest("search"), .01);
        Assert.AreEqual(0, chart.LiftForTest("direct"), .01);
        chart.PreviewForTest(null);
        Assert.AreEqual("Visits", chart.CenterForTest);
    });

    [TestMethod]
    public void HidingRedistributesAndTheLastCannotHide() => Run(chart =>
    {
        chart.ShowForTest();
        chart.AdvanceForTest(3);
        chart.RowClickForTest("search");
        chart.AdvanceForTest(3);
        var arc = chart.ArcForTest("search");
        Assert.AreEqual(arc.Start, arc.End, .001, "a hidden segment closes where it sits");
        Assert.AreEqual("23,770", chart.CenterValueForTest);
        foreach (string key in chart.SlicesForTest.Skip(1)) { chart.RowClickForTest(key); }
        Assert.AreEqual(chart.SlicesForTest.Length - 1, chart.HiddenKeys!.Count, "one segment always stays");
        Assert.IsFalse(chart.HiddenKeys.Contains(chart.SlicesForTest[^1]));
    });

    [TestMethod]
    public void LeavingSegmentsCloseThenDrop() => Run(chart =>
    {
        chart.ShowForTest();
        chart.AdvanceForTest(3);
        chart.Data = [new("search", "Search", 4210), new("direct", "Direct", 2380)];
        CollectionAssert.AreEqual(new[] { "search", "direct" }, chart.SlicesForTest);
        chart.AdvanceForTest(3);
        Assert.AreEqual(2, chart.LegendForTest.Length);
        Assert.AreEqual(1, chart.ArcForTest("direct").End, .001);
        Assert.ThrowsExactly<KeyNotFoundException>(() => chart.ArcForTest("referrals"));
    });

    [TestMethod]
    public void TheRingWalksByKeyWithoutALegend() => Run(chart =>
    {
        chart.ShowLegend = false;
        chart.ShowForTest();
        chart.AdvanceForTest(3);
        chart.RingKeyForTest(Key.Right);
        Assert.AreEqual("search", chart.CurrentForTest);
        chart.RingKeyForTest(Key.Enter);
        Assert.AreEqual("search", chart.ActiveKey);
        chart.RingKeyForTest(Key.Escape);
        Assert.IsNull(chart.ActiveKey);
        Assert.IsNull(chart.CurrentForTest);
    });

    [TestMethod]
    public void LegendSitsBesideTheRingWhenThereIsRoom() => Sta.Run(() =>
    {
        var chart = new DonutChart { Label = "Visits", Data = Month };
        var window = new Window { Content = chart, Width = 700, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            Assert.IsTrue(chart.BesideForTest);
            window.Width = 400;
            Sta.Pump();
            Assert.IsFalse(chart.BesideForTest);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void TheRingWaitsToBeSeen() => ChartHost.BelowTheFold(() => new DonutChart { Label = "Visits", Data = Month }, chart =>
    {
        Assert.ThrowsExactly<KeyNotFoundException>(() => chart.ArcForTest("search"), "no segment until seen");
        chart.ShowForTest();
        chart.AdvanceForTest(.03);
        Assert.IsTrue(chart.ArcForTest("search").End < 16840d / 40610, "still sweeping");
    });

    [TestMethod]
    public void WithoutMotionItSettlesAsSoonAsItLoads() => Sta.Run(() =>
    {
        var chart = new DonutChart { Label = "Visits", Data = Month };
        ArcMotion.SetLevel(chart, ArcMotionLevel.Off);
        var window = new Window { Content = chart, Width = 700, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            Assert.AreEqual(1, chart.ArcForTest(DonutChart.OtherKey).End, 1e-6);
        }
        finally { window.Close(); }
    });
}
