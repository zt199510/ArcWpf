using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class DateRangePickerTests
{
    [TestMethod]
    public void FormatsLikeIntlFormatRangeInEnglish() => Sta.Run(() =>
    {
        var picker = new DateRangePicker { Language = XmlLanguage.GetLanguage("en-US") };
        Assert.AreEqual("Select dates", picker.Format(null));
        Assert.AreEqual("Oct 24, 2026", picker.Format(new DateRange(new(2026, 10, 24), new(2026, 10, 24))));
        Assert.AreEqual("Oct 24 – 30, 2026", picker.Format(new DateRange(new(2026, 10, 24), new(2026, 10, 30))));
        Assert.AreEqual("Oct 24 – Nov 3, 2026", picker.Format(new DateRange(new(2026, 10, 24), new(2026, 11, 3))));
        Assert.AreEqual("Dec 28, 2025 – Jan 3, 2026", picker.Format(new DateRange(new(2025, 12, 28), new(2026, 1, 3))));
    });

    [TestMethod]
    public void DefaultPresetsMatchTheSource()
    {
        var today = new DateTime(2026, 10, 30);
        var ranges = DateRangePicker.DefaultPresets.ToDictionary(p => p.Label, p => p.Range(today));
        Assert.AreEqual(new DateRange(today.AddDays(-6), today), ranges["Last 7 days"]);
        Assert.AreEqual(new DateRange(new(2026, 9, 1), new(2026, 9, 30)), ranges["Last month"]);
        Assert.AreEqual(new DateRange(new(2026, 10, 1), today), ranges["This quarter"]);
        Assert.AreEqual(new DateRange(new(2026, 1, 1), today), ranges["Year to date"]);
        Assert.HasCount(8, DateRangePicker.DefaultPresets);
    }

    [TestMethod]
    public void TwoPicksMakeAnOrderedDraftAndApplyCommitsIt() => Sta.Run(() =>
    {
        var picker = new DateRangePicker { Months = DateRangePickerMonths.Two };
        DateRange? applied = null; picker.RangeChanged += (_, e) => applied = e.Range;
        var window = new Window { Content = picker, Width = 900, Height = 600, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            ArcMotion.SetLevel(picker, ArcMotionLevel.Off);
            picker.IsOpen = true; Sta.Pump();
            picker.Pick(new DateTime(2026, 10, 20));
            picker.Pick(new DateTime(2026, 10, 12)); // earlier second pick is ordered
            Assert.IsNull(picker.StartDate); // nothing commits before Apply
            var apply = typeof(DateRangePicker).GetMethod("Apply", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            apply.Invoke(picker, null); Sta.Pump();
            Assert.AreEqual(new DateTime(2026, 10, 12), picker.StartDate);
            Assert.AreEqual(new DateTime(2026, 10, 20), picker.EndDate);
            Assert.AreEqual(new DateRange(new(2026, 10, 12), new(2026, 10, 20)), applied);
            Assert.IsFalse(picker.IsOpen);
        }
        finally { window.Close(); Sta.Pump(); }
    });

    [TestMethod]
    public void BoundsDisableDaysOutsideTheRange() => Sta.Run(() =>
    {
        var picker = new DateRangePicker { MinimumDate = new DateTime(2026, 10, 5), MaximumDate = new DateTime(2026, 10, 25) };
        Assert.IsTrue(picker.IsDisabled(new DateTime(2026, 10, 4)));
        Assert.IsFalse(picker.IsDisabled(new DateTime(2026, 10, 5)));
        Assert.IsTrue(picker.IsDisabled(new DateTime(2026, 10, 26)));
    });
}
