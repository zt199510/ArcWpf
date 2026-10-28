using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
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

    [TestMethod]
    public void AnimatedUpdatesContinueFromCurrentFill() => Sta.Run(() =>
    {
        var progress = Create();
        var window = new Window { Content = progress, Width = 600, Height = 120, ShowInTaskbar = false };
        try
        {
            window.Show(); Layout(progress);
            progress.Value = 20;
            Pump(400);
            var fill = (FrameworkElement)progress.Template.FindName("PART_Fill", progress)!;
            var scale = (ScaleTransform)fill.RenderTransform;
            Assert.AreEqual(.2d, scale.ScaleX, .01);

            progress.Value = 40;
            Pump(50);
            Assert.IsGreaterThan(.2d, scale.ScaleX, "A new update should continue from the previous fill instead of restarting at zero.");
        }
        finally { window.Close(); Sta.Pump(); }
    });

    private static ArcProgress Create() { var progress = new ArcProgress(); progress.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/Progress/Themes/ProgressTheme.xaml", UriKind.Relative) }); return progress; }
    private static void Layout(FrameworkElement element) { element.Measure(new Size(600, 120)); element.Arrange(new Rect(0, 0, 600, 120)); element.UpdateLayout(); Sta.Pump(); }
    private static void Pump(int milliseconds)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}
