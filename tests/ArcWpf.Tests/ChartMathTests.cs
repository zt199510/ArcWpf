using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

// Hosts a chart past a scroller's fold, so its in-view reveal has not fired yet.
internal static class ChartHost
{
    public static void BelowTheFold<T>(Func<T> create, Action<T> action) where T : FrameworkElement => Sta.Run(() =>
    {
        var chart = create();
        var stack = new StackPanel();
        stack.Children.Add(new Border { Height = 3000 });
        stack.Children.Add(chart);
        var window = new Window { Content = new ScrollViewer { Content = stack }, Width = 520, Height = 400, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(chart); }
        finally { window.Close(); }
    });
}

[TestClass]
public sealed class ChartMathTests
{
    [TestMethod]
    public void BarScaleTakesTheSmallestCleanStepInFourRows()
    {
        var scale = ChartMath.ScaleFor(86);
        Assert.AreEqual(90, scale.Max);
        CollectionAssert.AreEqual(new double[] { 0, 30, 60, 90 }, scale.Ticks);
        CollectionAssert.AreEqual(new double[] { 0, 30, 60, 90, 120 }, ChartMath.ScaleFor(118).Ticks);
    }

    [TestMethod]
    public void LineScaleStartsAtZeroUnlessTheDataDipsBelow()
    {
        var scale = ChartMath.NiceScale(121, 437);
        Assert.AreEqual(0, scale.Min);
        Assert.AreEqual(600, scale.Max);
        CollectionAssert.AreEqual(new double[] { 0, 200, 400, 600 }, scale.Ticks);
        Assert.IsTrue(ChartMath.NiceScale(-40, 90).Min < 0);
    }

    [TestMethod]
    public void CurveKeepsEveryPointAndNeverSwingsPastTheData()
    {
        double[] values = [128, 198, 192, 437, 134];
        var shape = ChartMath.Curve(values, true);
        for (int index = 0; index < values.Length; index++) { Assert.AreEqual(values[index], ChartMath.ValueAt(shape, index / 4d), 1e-6); }
        Assert.IsTrue(shape.All(point => point.Y is >= 128 and <= 437));
        Assert.AreEqual(values.Length, ChartMath.Curve(values, false).Length);
    }

    [TestMethod]
    public void CompactMatchesIntl()
    {
        Assert.AreEqual("100K", ChartMath.Compact(100000));
        Assert.AreEqual("61.2K", ChartMath.Compact(61170));
        Assert.AreEqual("600", ChartMath.Compact(600));
        Assert.AreEqual("1.5M", ChartMath.Compact(1500000));
    }

    [TestMethod]
    public void TimedValueWaitsOutItsDelayThenLands()
    {
        var value = new Timed(0);
        value.Go(1, .2, null, .1);
        value.Step(.05);
        Assert.AreEqual(0, value.Value);
        value.Step(.15);
        Assert.AreEqual(.5, value.Value, 1e-9);
        value.Step(.2);
        Assert.AreEqual(1, value.Value);
        Assert.IsFalse(value.Active);
    }
}
