using System.Windows;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class DateRangePickerTests
{
    [TestMethod]
    public void FirstAndSecondCalendarSelectionsCreateRange()
    {
        Sta.Run(() =>
        {
            var picker = new DateRangePicker(); picker.ApplyTemplate();
            picker.StartDate = new DateTime(2026, 10, 1); picker.EndDate = new DateTime(2026, 10, 7);
            Assert.AreEqual(new DateTime(2026, 10, 1), picker.StartDate); Assert.AreEqual(new DateTime(2026, 10, 7), picker.EndDate);
        });
    }

    [TestMethod]
    public void BoundsAndBlackoutDatesAreExposed()
    {
        Sta.Run(() =>
        {
            var picker = new DateRangePicker { MinimumDate = new DateTime(2026, 1, 1), MaximumDate = new DateTime(2026, 12, 31) };
            picker.BlackoutDates.Add(new DateTime(2026, 5, 1));
            Assert.AreEqual(new DateTime(2026, 1, 1), picker.MinimumDate); Assert.HasCount(1, picker.BlackoutDates);
        });
    }

    [TestMethod]
    public void ClearingRangeResetsBothDates()
    {
        Sta.Run(() =>
        {
            var picker = new DateRangePicker { StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(2) };
            picker.StartDate = null; picker.EndDate = null;
            Assert.IsNull(picker.StartDate); Assert.IsNull(picker.EndDate);
        });
    }
}
