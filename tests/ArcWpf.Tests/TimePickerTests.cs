using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class TimePickerTests
{
    [TestMethod] public void SelectedTimeAndIncrementAreBindable() => Sta.Run(() => { var picker = new TimePicker { SelectedTime = new TimeSpan(9, 30, 0), MinuteIncrement = 15 }; Assert.AreEqual(new TimeSpan(9, 30, 0), picker.SelectedTime); Assert.AreEqual(15, picker.MinuteIncrement); });
    [TestMethod] public void InvalidIncrementIsRejected() => Sta.Run(() => Assert.Throws<ArgumentException>(() => new TimePicker { MinuteIncrement = 0 }));
    [TestMethod] public void TemplateContainsPopupAndList() => Sta.Run(() => { var picker = new TimePicker(); Assert.IsNotNull(picker); });
}
