using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ColorPickerTests
{
    [TestMethod] public void ParsesEveryFormatBackToHex()
    {
        Assert.AreEqual("#2F6BFF", ColorMath.ToHex(ColorMath.Parse("#2f6bff")!.Value));
        Assert.AreEqual("#2F6BFF", ColorMath.ToHex(ColorMath.Parse("rgb(47 107 255)")!.Value));
        Assert.AreEqual("#FF000080", ColorMath.ToHex(ColorMath.Parse("rgba(255, 0, 0, 0.5)")!.Value));
        Assert.AreEqual("#FF0000", ColorMath.ToHex(ColorMath.Parse("hsl(0 100% 50%)")!.Value));
        Assert.IsNull(ColorMath.Parse("not a color"));
    }

    [TestMethod] public void FormatsRoundTrip()
    {
        var cobalt = ColorMath.Parse("#2F6BFF")!.Value;
        // HSL rounds to whole percents (as in the source), so it is checked as text instead of a round trip.
        foreach (var kind in new[] { ColorFormat.Hex, ColorFormat.Rgb, ColorFormat.Oklch })
            Assert.AreEqual("#2F6BFF", ColorMath.ToHex(ColorMath.Parse(ColorMath.Format(cobalt, kind), cobalt.H)!.Value), kind.ToString());
        Assert.AreEqual("rgb(47 107 255)", ColorMath.Format(cobalt, ColorFormat.Rgb));
        Assert.AreEqual("hsl(223 100% 59%)", ColorMath.Format(cobalt, ColorFormat.Hsl));
        Assert.AreEqual("oklch(58.1% 0.229 263.9)", ColorMath.Format(cobalt, ColorFormat.Oklch));
    }

    [TestMethod] public void ContrastAndGrade()
    {
        double black = ColorMath.Contrast(ColorMath.Parse("#000")!.Value, new Rgba(1, 1, 1, 1));
        Assert.AreEqual(21, black, .01);
        Assert.AreEqual("AAA", ColorMath.Grade(black));
        Assert.AreEqual("AA large", ColorMath.Grade(3.2));
        Assert.AreEqual("Fails", ColorMath.Grade(2));
    }

    [TestMethod] public void ValueIsNormalisedToHex() => Sta.Run(() =>
    {
        var picker = new ColorPicker { Label = "Accent", Value = "#2F6BFF" };
        Assert.AreEqual("#2F6BFF", picker.Value);
        Assert.IsFalse(picker.IsOpen);
    });
}
