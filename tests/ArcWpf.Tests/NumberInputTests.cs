using System.Windows;
using System.Windows.Controls;
using ArcNumberInput = ArcWpf.Controls.NumberInput;

namespace ArcWpf.Tests;

[TestClass]
public sealed class NumberInputTests
{
    [TestMethod]
    public void ValueIsClampedToBounds() => Sta.Run(() =>
    {
        var input = Create(); input.Minimum = 1; input.Maximum = 10; input.Value = 20; Assert.AreEqual(10d, input.Value); input.Value = -2; Assert.AreEqual(1d, input.Value);
    });

    [TestMethod]
    public void StepAndBoundsAreExposed() => Sta.Run(() =>
    {
        var input = Create(); input.Minimum = 1; input.Maximum = 10; input.Step = 2; input.Value = 5; input.Value += input.Step; Assert.AreEqual(7d, input.Value);
    });

    [TestMethod]
    public void TemplateLoadsInputAndStepperParts() => Sta.Run(() =>
    {
        var input = Create(); Layout(input); Assert.IsNotNull(input.Template.FindName("PART_Input", input)); Assert.IsNotNull(input.Template.FindName("PART_Increase", input)); Assert.IsNotNull(input.Template.FindName("PART_Decrease", input));
    });

    private static ArcNumberInput Create() { var input = new ArcNumberInput(); input.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/NumberInput/Themes/NumberInputTheme.xaml", UriKind.Relative) }); return input; }
    private static void Layout(FrameworkElement element) { element.Measure(new Size(400, 180)); element.Arrange(new Rect(0, 0, 400, 180)); element.UpdateLayout(); Sta.Pump(); }
}
