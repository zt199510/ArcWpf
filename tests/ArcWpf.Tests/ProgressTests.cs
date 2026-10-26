using System.Windows;
using System.Windows.Automation;
using ArcProgress = ArcWpf.Controls.Progress;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ProgressTests
{
    [TestMethod]
    public void ClampsValueAndMaxAndComputesPercentage() => Sta.Run(() =>
    {
        var progress = Create(); progress.Max = 200; progress.Value = 250; Layout(progress);
        Assert.AreEqual(200d, progress.Value); Assert.AreEqual(1d, progress.Percentage); progress.Max = 0; Assert.AreEqual(0d, progress.Percentage);
    });

    [TestMethod]
    public void ExposesAccessibleLabelAndPercentageText() => Sta.Run(() =>
    {
        var progress = Create(); progress.Label = "Uploading"; progress.Value = 25; progress.ShowValue = true; Layout(progress);
        Assert.AreEqual("Uploading", AutomationProperties.GetName(progress)); Assert.AreEqual("25%", AutomationProperties.GetHelpText(progress));
    });

    [TestMethod]
    public void TemplateLoadsAndSupportsCompletion() => Sta.Run(() =>
    {
        var progress = Create(); progress.Value = 100; Layout(progress); Assert.AreEqual(1d, progress.Percentage); Assert.IsNotNull(progress.Template.FindName("PART_Fill", progress));
    });

    private static ArcProgress Create() { var progress = new ArcProgress(); progress.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/Progress/Themes/ProgressTheme.xaml", UriKind.Relative) }); return progress; }
    private static void Layout(FrameworkElement element) { element.Measure(new Size(600, 120)); element.Arrange(new Rect(0, 0, 600, 120)); element.UpdateLayout(); Sta.Pump(); }
}
