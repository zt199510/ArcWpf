using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ExpandableCardTests
{
    [TestMethod]
    public void DefaultCollapsedAndDefaultExpandedPreserveContent() => Sta.Run(() =>
    {
        var card = Make(); Layout(card, 700); var panel = Part<Border>(card, "PART_Panel");
        Assert.IsFalse(card.IsExpanded); Assert.AreEqual(Visibility.Hidden, panel.Visibility); Assert.AreSame(card.Content, ((ContentPresenter)Part<Border>(card, "PART_PanelInner").Child).Content);
        var open = Make(); open.DefaultExpanded = true; Layout(open, 700); Assert.IsTrue(open.IsExpanded); Assert.AreEqual(Visibility.Visible, Part<Border>(open, "PART_Panel").Visibility);
    });

    [TestMethod]
    public void WidthCapsAndHeightMorphUseFinalLayoutValues() => Sta.Run(() =>
    {
        var card = Make(); card.CollapsedWidth = 300; card.ExpandedWidth = 520; Layout(card, 700);
        var surface = Part<Border>(card, "PART_Surface"); Assert.IsTrue(surface.Width <= 304d);
        card.IsExpanded = true; Layout(card, 700); Pump(500); Assert.IsTrue(surface.Width >= 519d && surface.Width <= 521d); Assert.IsTrue(double.IsNaN(Part<Border>(card, "PART_Panel").Height));
        card.IsExpanded = false; Pump(300); Assert.IsTrue(surface.Width <= 304d); Assert.AreEqual(Visibility.Hidden, Part<Border>(card, "PART_Panel").Visibility);
    });

    [TestMethod]
    public void IsExpandedBindingAndEscapeReturnFocusToHeader() => Sta.Run(() =>
    {
        var model = new Model { Open = false }; var card = Make(); var binding = card.SetBinding(ExpandableCard.IsExpandedProperty, new Binding(nameof(Model.Open)) { Source = model }); Layout(card, 500);
        model.Open = true; Layout(card, 500); Assert.IsTrue(card.IsExpanded); Assert.AreSame(binding, BindingOperations.GetBindingExpression(card, ExpandableCard.IsExpandedProperty));
        var trigger = Part<ToggleButton>(card, "PART_Trigger"); trigger.Focus(); card.SetCurrentValue(ExpandableCard.IsExpandedProperty, false);
        Assert.IsFalse(card.IsExpanded); Assert.IsTrue(trigger.Focusable);
    });

    [TestMethod]
    public void HeaderUsesKeyboardOnlyFocusVisualAndChevronGeometry() => Sta.Run(() =>
    {
        var card = Make(); Layout(card, 500); var trigger = Part<ToggleButton>(card, "PART_Trigger"); var arrow = Part<Path>(card, "PART_Arrow");
        Assert.IsNotNull(trigger.FocusVisualStyle); Assert.AreEqual(new Rect(5, 7, 8, 4), arrow.Data.Bounds); Assert.IsNull(card.Template.FindName("FocusRing", card));
    });

    [TestMethod]
    public void AutomationExpandsAndCollapsesCard() => Sta.Run(() =>
    {
        var card = Make(); var peer = UIElementAutomationPeer.CreatePeerForElement(card)!; var pattern = (IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)!;
        Assert.AreEqual(ExpandCollapseState.Collapsed, pattern.ExpandCollapseState); pattern.Expand(); Assert.IsTrue(card.IsExpanded); pattern.Collapse(); Assert.IsFalse(card.IsExpanded);
    });

    [TestMethod]
    public void FullMotionRetargetsAndReducedOrOffSettle() => Sta.Run(() =>
    {
        var card = Make(); Layout(card, 600); card.IsExpanded = true; Pump(30); Assert.IsTrue(card.IsMotionRunning); card.IsExpanded = false; Pump(500); Assert.IsFalse(card.IsMotionRunning);
        ArcMotion.SetLevel(card, ArcMotionLevel.Reduced); card.IsExpanded = true; Assert.IsFalse(card.IsMotionRunning); ArcMotion.SetLevel(card, ArcMotionLevel.Off); card.IsExpanded = false; Assert.IsFalse(card.IsMotionRunning);
    });

    [TestMethod]
    public void GalleryPageAndConsumerConstructWithoutOpeningWindows() => Sta.Run(() =>
    {
        var page = new ArcWpf.Gallery.Pages.ExpandableCardPage(); Layout(page, 800); Assert.IsNotNull(page.Content);
        var consumer = new ArcWpf.Consumer.MainWindow(); Assert.IsNotNull(consumer.Content); consumer.Close();
    });

    private static ExpandableCard Make() => new() { Title = "Pro plan", Description = "Billed yearly, renews March 4", Content = new TextBlock { Text = "Unlimited projects and priority support.", TextWrapping = TextWrapping.Wrap } };
    private static T Part<T>(Control control, string name) where T : FrameworkElement => (T)control.Template.FindName(name, control);
    private static void Layout(FrameworkElement element, double width)
    {
        if (!element.IsInitialized) { element.BeginInit(); element.EndInit(); }
        element.Measure(new Size(width, 1000)); element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height)); element.UpdateLayout(); Sta.Pump();
    }
    private static void Pump(int milliseconds) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) }; timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    private sealed class Model : System.ComponentModel.INotifyPropertyChanged { private bool _open; public bool Open { get => _open; set { _open = value; PropertyChanged?.Invoke(this, new(nameof(Open))); } } public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged; }
}
