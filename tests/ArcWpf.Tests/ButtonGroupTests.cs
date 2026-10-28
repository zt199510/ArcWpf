using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ButtonGroupTests
{
    [TestMethod]
    public void MetricsFollowSizeAndSourceGeometry() => Sta.Run(() => InWindow(group =>
    {
        var share = Segment(group, 0);
        Assert.AreEqual(44d, group.ActualHeight, .5);
        Assert.AreEqual(42d, share.ActualHeight, .5);
        var zoom = new ButtonGroupItem { Label = "Zoom in", IsIconOnly = true, Icon = Geometry.Parse("M 5 12 h 14 M 12 5 v 14") };
        group.Items.Add(zoom);
        group.Size = ArcWpf.ControlSize.Small;
        Pump(80);
        Assert.AreEqual(36d, group.ActualHeight, .5);
        Assert.AreEqual(34d, zoom.ActualWidth, .5, "Icon-only segments are square: control height minus the border.");
        Assert.AreEqual("Zoom in", AutomationProperties.GetName(zoom));
        Assert.AreEqual("Zoom in", zoom.ToolTip);
    }));

    [TestMethod]
    public void ReservedLabelsKeepTheSegmentWidth() => Sta.Run(() => InWindow(group =>
    {
        var share = Segment(group, 0);
        double width = share.ActualWidth;
        share.Label = "Copied";
        Pump(400);
        Assert.AreEqual(width, share.ActualWidth, .5, "Every reserved label sits in the same cell, so a confirmation never resizes the group.");
        Assert.AreEqual("Copied", AutomationProperties.GetName(share));
    }));

    [TestMethod]
    public void LabelChangeStartsCrossfadeWhileShown() => Sta.Run(() => InWindow(group =>
    {
        var share = Segment(group, 0);
        var phase = (FrameworkElement)share.Template.FindName("PART_Phase", share);
        var previous = (Image)share.Template.FindName("PART_PreviousPhase", share);
        share.Label = "Copied";
        Assert.IsTrue(phase.HasAnimatedProperties, "The new label must actually animate in, not snap.");
        Assert.IsNotNull(previous.Source, "The outgoing label is kept as a snapshot while it lifts away.");
        Pump(400);
        Assert.AreEqual(1d, phase.Opacity, .001);
        Assert.IsNull(previous.Source);
        Assert.IsNull(phase.Effect, "Blur is removed at rest so the label keeps ClearType.");
    }));

    [TestMethod]
    public void ContentValueUpdatesWithoutCrossfadeAndOffSnaps() => Sta.Run(() => InWindow(group =>
    {
        var value = new ButtonGroupItem { Label = "Zoom 100%", Content = "100%" };
        group.Items.Add(value);
        Pump(80);
        var phase = (FrameworkElement)value.Template.FindName("PART_Phase", value);
        value.Content = "110%";
        Assert.IsFalse(phase.HasAnimatedProperties);
        ArcMotion.SetLevel(group, ArcWpf.ArcMotionLevel.Off);
        var share = Segment(group, 0);
        var sharePhase = (FrameworkElement)share.Template.FindName("PART_Phase", share);
        share.Label = "Copied";
        Assert.IsFalse(sharePhase.HasAnimatedProperties);
    }));

    [TestMethod]
    public void ArrowKeysMoveAlongOrientationAndWrap() => Sta.Run(() => InWindow(group =>
    {
        var first = Segment(group, 0); var last = Segment(group, 2);
        first.Focus();
        Press(group, Key.Left);
        Assert.IsTrue(last.IsKeyboardFocused, "Moving back from the first segment wraps to the last.");
        Press(group, Key.Home);
        Assert.IsTrue(first.IsKeyboardFocused);
        Press(group, Key.Down);
        Assert.IsTrue(first.IsKeyboardFocused, "Perpendicular arrows do nothing in a horizontal group.");
        group.Orientation = Orientation.Vertical;
        Pump(50);
        Press(group, Key.Down);
        Assert.IsTrue(Segment(group, 1).IsKeyboardFocused);
    }));

    [TestMethod]
    public void LabelsCollapseWhenTheRowOverflowsAndReturn() => Sta.Run(() =>
    {
        var group = Create();
        var host = new Border { Width = 400, Child = group, HorizontalAlignment = HorizontalAlignment.Left };
        var window = new Window { Content = host, Width = 600, Height = 200, ShowActivated = false };
        try
        {
            window.Show(); Pump(100);
            Assert.IsFalse(group.IsCompact);
            host.Width = 120; Pump(100);
            Assert.IsTrue(group.IsCompact);
            Assert.AreEqual(42d, Segment(group, 0).ActualWidth, .5);
            Assert.AreEqual("Share", Segment(group, 0).ToolTip);
            Assert.IsLessThanOrEqualTo(120.5, group.ActualWidth);
            host.Width = 400; Pump(100);
            Assert.IsFalse(group.IsCompact);
        }
        finally { window.Close(); Sta.Pump(); }
    });

    [TestMethod]
    public void MenuAddsChevronSegmentAtTheEnd() => Sta.Run(() => InWindow(group =>
    {
        var menu = (ArcWpf.Controls.Menu)group.Template.FindName("PART_Menu", group);
        Assert.AreEqual(Visibility.Collapsed, menu.Visibility);
        group.MenuItems.Add(new ArcWpf.Controls.MenuItem { Label = "Delete", IsDestructive = true });
        Pump(80);
        Assert.AreEqual(Visibility.Visible, menu.Visibility);
        var trigger = (System.Windows.Controls.Button)menu.Template.FindName("PART_Trigger", menu);
        Assert.AreEqual(38d, trigger.ActualWidth, .5, "Chevron is the control height minus 6.");
        Assert.AreEqual("More actions", AutomationProperties.GetName(trigger));
        var last = Segment(group, 2);
        Assert.IsGreaterThanOrEqualTo(last.TranslatePoint(new Point(last.ActualWidth, 0), group).X - .5, trigger.TranslatePoint(new Point(0, 0), group).X);
        string? invoked = null;
        group.MenuItemInvoked += (_, e) => invoked = e.Item.Label;
        trigger.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, trigger)); Pump(150);
        Assert.IsTrue(menu.IsOpen, "Clicking the chevron opens the menu.");
        menu.RaiseEvent(new MenuItemInvokedEventArgs(ArcWpf.Controls.Menu.ItemInvokedEvent, menu, group.MenuItems[0]));
        Assert.AreEqual("Delete", invoked);
        menu.IsOpen = false; Pump(150);
    }));

    [TestMethod]
    public void DisabledSegmentDimsContentAndGroupIsNamed() => Sta.Run(() => InWindow(group =>
    {
        var middle = Segment(group, 1);
        middle.IsEnabled = false; Pump(30);
        Assert.AreEqual(.4, ((FrameworkElement)middle.Template.FindName("PART_Content", middle)).Opacity, .001);
        middle.IsEnabled = true; group.IsEnabled = false; Pump(30);
        Assert.AreEqual(1d, ((FrameworkElement)middle.Template.FindName("PART_Content", middle)).Opacity, .001, "A disabled group dims as a whole, not per segment.");
        var peer = UIElementAutomationPeer.CreatePeerForElement(group);
        Assert.AreEqual("Document actions", peer.GetName());
        Assert.AreEqual(AutomationControlType.Group, peer.GetAutomationControlType());
    }));

    [TestMethod]
    public void GalleryPageLoads() => Sta.Run(() =>
    {
        var window = new Window { Content = new ButtonGroupPage(), Width = 900, Height = 900, ShowActivated = false };
        try { window.Show(); Pump(200); }
        finally { window.Close(); Sta.Pump(); }
    });

    private static ButtonGroup Create()
    {
        var group = new ButtonGroup { Label = "Document actions" };
        var icon = Geometry.Parse("M 5 12 h 14");
        group.Items.Add(new ButtonGroupItem { Label = "Share", Reserve = ["Share", "Copied", "Failed"], Icon = icon });
        group.Items.Add(new ButtonGroupItem { Label = "Duplicate", Reserve = ["Duplicate", "Duplicated"], Icon = icon });
        group.Items.Add(new ButtonGroupItem { Label = "Archive", Reserve = ["Archive", "Restore"], Icon = icon });
        return group;
    }

    private static void InWindow(Action<ButtonGroup> action)
    {
        var group = Create();
        var window = new Window { Content = group, Width = 700, Height = 200, ShowActivated = false };
        try { window.Show(); Pump(100); action(group); }
        finally { window.Close(); Sta.Pump(); }
    }

    private static ButtonGroupItem Segment(ButtonGroup group, int index) => (ButtonGroupItem)group.ItemContainerGenerator.ContainerFromIndex(index);

    private static void Press(UIElement target, Key key)
    {
        var source = PresentationSource.FromVisual(target)!;
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        (Keyboard.FocusedElement as UIElement ?? target).RaiseEvent(args);
    }

    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
