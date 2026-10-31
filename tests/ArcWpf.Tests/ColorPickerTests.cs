using System.Windows.Media;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ColorPickerTests
{
    [TestMethod] public void ValueAndLabelAreBindable() => Sta.Run(() => { var picker = new ColorPicker { Value = Colors.CornflowerBlue, Label = "Accent" }; Assert.AreEqual(Colors.CornflowerBlue, picker.Value); Assert.AreEqual("Accent", picker.Label); });
    [TestMethod] public void IsOpenDefaultsFalse() => Sta.Run(() => Assert.IsFalse(new ColorPicker().IsOpen));
    [TestMethod] public void TransparentValueIsSupported() => Sta.Run(() => { var picker = new ColorPicker { Value = Colors.Transparent }; Assert.AreEqual(Colors.Transparent, picker.Value); });
}
