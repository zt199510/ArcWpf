using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class AccordionTests
{
    [TestMethod]
    public void FirstRowOpensByDefaultAndOnlyOneRowRemainsExpanded() => Sta.Run(() =>
    {
        var accordion = Make();
        Layout(accordion);
        var first = Row(accordion, 0); var second = Row(accordion, 1);
        Assert.AreEqual(0, accordion.ExpandedIndex);
        Assert.IsTrue(first.IsExpanded); Assert.IsFalse(second.IsExpanded);
        second.IsExpanded = true; Layout(accordion); Pump(260);
        Assert.AreEqual(1, accordion.ExpandedIndex); Assert.IsFalse(first.IsExpanded); Assert.IsTrue(second.IsExpanded);
        second.IsExpanded = false; Assert.AreEqual(-1, accordion.ExpandedIndex);
    });

    [TestMethod]
    public void ExpandedIndexBindingAndContentIdentitySurviveRoundTrip() => Sta.Run(() =>
    {
        var model = new Model { Index = -1 }; var input = new TextBox { Text = "original" }; var accordion = Make(input);
        var binding = accordion.SetBinding(Accordion.ExpandedIndexProperty, new Binding(nameof(Model.Index)) { Source = model });
        Layout(accordion); Assert.AreEqual(-1, accordion.ExpandedIndex); Assert.IsFalse(Row(accordion, 0).IsExpanded);
        input.Text = "edited"; model.Index = 1; Layout(accordion); Assert.IsTrue(Row(accordion, 1).IsExpanded);
        model.Index = 0; Layout(accordion); Assert.AreSame(input, Row(accordion, 0).Content); Assert.AreEqual("edited", ((TextBox)Row(accordion, 0).Content).Text);
        Assert.AreSame(binding, BindingOperations.GetBindingExpression(accordion, Accordion.ExpandedIndexProperty));
    });

    [TestMethod]
    public void HeaderGeometryMatchesMediumAndLargeContracts() => Sta.Run(() =>
    {
        var accordion = Make(); Layout(accordion); var first = Row(accordion, 0); var header = Part<ToggleButton>(first, "PART_HeaderButton");
        Assert.AreEqual(44d, header.ActualHeight, .01); Assert.AreEqual(17d, Part<Path>(first, "PART_Chevron").ActualWidth, .01);
        accordion.Size = AccordionSize.Large; Layout(accordion); Assert.AreEqual(76d, header.ActualHeight, .01); Assert.AreEqual(18d, header.FontSize, .01);
        Assert.AreEqual(16d, TextElement.GetFontSize(Part<Border>(first, "PART_PanelInner")), .01);
    });

    [TestMethod]
    public void HeaderUsesKeyboardOnlyFocusVisualInsteadOfPersistentFocusRing() => Sta.Run(() =>
    {
        var accordion = Make(); Layout(accordion); var row = Row(accordion, 0); var header = Part<ToggleButton>(row, "PART_HeaderButton");
        Assert.IsNotNull(header.FocusVisualStyle);
        Assert.IsNull(row.Template.FindName("FocusRing", row));
    });

    [TestMethod]
    public void KeyboardNavigationWrapsAndSkipsDisabledRows() => Sta.Run(() =>
    {
        var accordion = Make(); accordion.Items.Add(new AccordionItem { Header = "Third", Content = new TextBlock { Text = "Third content" } });
        Layout(accordion); var first = Row(accordion, 0); var third = Row(accordion, 2); first.Focus();
        Assert.AreSame(third, accordion.FindNext(first, Key.Up)); Assert.AreSame(third, accordion.FindNext(first, Key.End));
        Row(accordion, 1).IsEnabled = false; Assert.AreSame(third, accordion.FindNext(first, Key.Down));
    });

    [TestMethod]
    public void ChevronUsesSourceGeometryAndExpansionAutomation() => Sta.Run(() =>
    {
        var accordion = Make(); Layout(accordion); var row = Row(accordion, 0); var chevron = Part<Path>(row, "PART_Chevron");
        Assert.AreEqual(new Rect(4, 6, 9, 4.5), chevron.Data.Bounds);
        var peer = UIElementAutomationPeer.CreatePeerForElement(row)!; var pattern = (IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)!;
        Assert.AreEqual(ExpandCollapseState.Expanded, pattern.ExpandCollapseState); pattern.Collapse(); Assert.IsFalse(row.IsExpanded); pattern.Expand(); Assert.IsTrue(row.IsExpanded);
    });

    [TestMethod]
    public void PanelSpringRetargetsAndReducedOrOffSettleWithoutClocks() => Sta.Run(() =>
    {
        var accordion = Make(); Layout(accordion); var row = Row(accordion, 0); var panel = Part<Border>(row, "PART_Panel");
        row.IsExpanded = false; Assert.IsTrue(row.IsMotionRunning); row.IsExpanded = true; Pump(20); Assert.IsTrue(row.IsMotionRunning);
        Pump(500); Assert.IsFalse(row.IsMotionRunning); Assert.IsTrue(row.IsExpanded); Assert.IsTrue(double.IsNaN(panel.Height));
        ArcMotion.SetLevel(accordion, ArcMotionLevel.Reduced); row.IsExpanded = false; Assert.IsFalse(row.IsMotionRunning); Assert.AreEqual(Visibility.Hidden, panel.Visibility);
        ArcMotion.SetLevel(accordion, ArcMotionLevel.Off); row.IsExpanded = true; Assert.IsFalse(row.IsMotionRunning); Assert.AreEqual(Visibility.Visible, panel.Visibility);
    });

    [TestMethod]
    public void ItemsSourceAndCustomHeaderContentRemainNative() => Sta.Run(() =>
    {
        var accordion = new Accordion { ItemsSource = new[] { "One", "Two" } }; Layout(accordion);
        Assert.IsInstanceOfType<AccordionItem>(accordion.ItemContainerGenerator.ContainerFromIndex(0));
        Assert.AreEqual("One", Row(accordion, 0).Header); Assert.AreEqual("One", Row(accordion, 0).Content);
        var custom = new AccordionItem { Header = new TextBlock { Text = "Custom" }, Content = new Border { Width = 80 } }; accordion.ItemsSource = null; accordion.Items.Clear(); accordion.Items.Add(custom); Layout(accordion);
        Assert.AreSame(custom, Row(accordion, 0)); Assert.IsInstanceOfType<Border>(custom.Content);
    });

    [TestMethod]
    public void GalleryPageAndConsumerConstructWithoutOpeningWindows() => Sta.Run(() =>
    {
        var page = new ArcWpf.Gallery.Pages.AccordionPage(); Layout(page, 800); Assert.IsNotNull(page.Content);
        var consumer = new ArcWpf.Consumer.MainWindow(); Assert.IsNotNull(consumer.Content); consumer.Close();
    });

    private static Accordion Make(TextBox? input = null)
    {
        var accordion = new Accordion();
        accordion.Items.Add(new AccordionItem { Header = "Can I cancel anytime?", Content = (object?)input ?? new TextBlock { Text = "Yes. Your plan stays active until the period ends.", TextWrapping = TextWrapping.Wrap } });
        accordion.Items.Add(new AccordionItem { Header = "Do you offer refunds?", Content = new TextBlock { Text = "Within 14 days of purchase, no questions asked.", TextWrapping = TextWrapping.Wrap } });
        return accordion;
    }

    private static AccordionItem Row(Accordion accordion, int index) => (AccordionItem)accordion.ItemContainerGenerator.ContainerFromIndex(index);
    private static T Part<T>(Control control, string name) where T : FrameworkElement => (T)control.Template.FindName(name, control);
    private static void Layout(FrameworkElement element, double width = 560)
    {
        if (!element.IsInitialized) { element.BeginInit(); element.EndInit(); }
        element.Measure(new Size(width, 1000)); element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height)); element.UpdateLayout(); Sta.Pump();
    }
    private static void Pump(int milliseconds) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) }; timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    private sealed class Model : INotifyPropertyChanged
    {
        private int _index; public int Index { get => _index; set { _index = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Index))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
