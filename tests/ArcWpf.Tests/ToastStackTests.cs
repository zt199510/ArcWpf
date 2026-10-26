using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ArcStack = ArcWpf.Controls.ToastStack;
using ArcItem = ArcWpf.Controls.ToastStackItem;
using ArcWpf.Controls;
using WpfButton = System.Windows.Controls.Button;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ToastStackTests
{
    [TestMethod]
    public void ShowAddsNewestFirstAndHonorsLimit() => Sta.Run(() =>
    {
        var stack = new ArcStack { Limit = 2 };
        var first = stack.Show("First", duration: 0);
        var second = stack.Show("Second", duration: 0);
        var third = stack.Show("Third", duration: 0);
        Assert.AreEqual(2, stack.Count);
        Assert.AreEqual("Third", ((ArcItem)stack.Items[0]).Title);
        Assert.AreEqual(second, ((ArcItem)stack.Items[1]).Id);
        Assert.AreNotEqual(first, ((ArcItem)stack.Items[0]).Id);
    });

    [TestMethod]
    public void ReusingIdUpdatesInPlaceAndRestartsTypeDuration() => Sta.Run(() =>
    {
        var stack = new ArcStack { Duration = 1000 };
        var id = stack.Show(new ToastStackOptions { Id = "build", Title = "Building", Type = ToastStackType.Loading });
        var same = stack.Show(new ToastStackOptions { Id = id, Title = "Ready", Type = ToastStackType.Success, Duration = 0 });
        var item = (ArcItem)stack.Items[0];
        Assert.AreEqual(id, same);
        Assert.AreEqual(1, stack.Count);
        Assert.AreEqual("Ready", item.Title);
        Assert.AreEqual(ToastStackType.Success, item.Type);
        Assert.AreEqual(0, item.Duration);
    });

    [TestMethod]
    public void TypeChangesIconAndActionButtonVisibility() => Sta.Run(() =>
    {
        var stack = new ArcStack();
        stack.Show(new ToastStackOptions { Title = "Warning", Type = ToastStackType.Warning, ActionText = "Undo", Duration = 0 });
        Layout(stack);
        var item = (ArcItem)stack.Items[0]; Layout(item);
        Assert.AreEqual(Visibility.Visible, ((System.Windows.Shapes.Path)item.Template.FindName("WarningIcon", item)).Visibility);
        Assert.AreEqual(Visibility.Visible, ((WpfButton)item.Template.FindName("PART_Action", item)).Visibility);
        Assert.AreEqual(Visibility.Collapsed, ((System.Windows.Shapes.Path)item.Template.FindName("InfoIcon", item)).Visibility);
    });

    [TestMethod]
    public void ActionCommandRunsAndDismissesItem() => Sta.Run(() =>
    {
        bool ran = false;
        var stack = new ArcStack();
        stack.Show(new ToastStackOptions { Id = "undo", Title = "Saved", ActionText = "Undo", ActionCommand = new DelegateCommand(_ => ran = true), Duration = 0 });
        Layout(stack); var item = (ArcItem)stack.Items[0]; Layout(item);
        ((WpfButton)item.Template.FindName("PART_Action", item)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.IsTrue(ran);
        Assert.AreEqual(0, stack.Count);
    });

    [TestMethod]
    public void DismissOneAndAllRemoveItems() => Sta.Run(() =>
    {
        var stack = new ArcStack();
        var first = stack.Show("First", duration: 0); var second = stack.Show("Second", duration: 0);
        stack.Dismiss(first); Assert.AreEqual(1, stack.Count);
        stack.Dismiss(); Assert.AreEqual(0, stack.Count);
        stack.Dismiss(second);
    });

    [TestMethod]
    public void ExpandedPanelShowsGapAndCollapsedPanelPeeksOlderItems() => Sta.Run(() =>
    {
        var stack = new ArcStack { VisibleToasts = 3 };
        stack.Show("One", duration: 0); stack.Show("Two", duration: 0); stack.Show("Three", duration: 0);
        Layout(stack); var panel = FindPanel(stack); Assert.IsFalse(panel.Expanded);
        var collapsedHeight = panel.ActualHeight;
        stack.IsExpanded = true; Layout(stack);
        Assert.IsTrue(panel.Expanded);
        Assert.IsGreaterThan(collapsedHeight, panel.ActualHeight);
    });

    [TestMethod]
    public void WarningAndErrorUseLongerDefaultLifetimeAndLoadingStaysOpen() => Sta.Run(() =>
    {
        var stack = new ArcStack { Duration = 1000 };
        stack.Show(new ToastStackOptions { Title = "Warning", Type = ToastStackType.Warning });
        stack.Show(new ToastStackOptions { Title = "Loading", Type = ToastStackType.Loading });
        Assert.AreEqual(1600, ((ArcItem)stack.Items[1]).Duration);
        Assert.AreEqual(0, ((ArcItem)stack.Items[0]).Duration);
    });

    [TestMethod]
    public void AutomationNameUsesLabelAndThemeBuilds() => Sta.Run(() =>
    {
        var stack = new ArcStack { Label = "Activity notifications" };
        var peer = UIElementAutomationPeer.CreatePeerForElement(stack)!;
        Assert.AreEqual("Activity notifications", peer.GetName());
        Assert.AreEqual(AutomationControlType.List, peer.GetAutomationControlType());
        Layout(stack);
    });

    private static ToastStackPanel FindPanel(ArcStack stack) => FindVisual<ToastStackPanel>(stack)!;
    private static T? FindVisual<T>(DependencyObject root) where T : DependencyObject { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T found) return found; var nested = FindVisual<T>(child); if (nested is not null) return nested; } return null; }

    private static void Layout(FrameworkElement element)
    {
        if (!element.IsInitialized) { element.BeginInit(); element.EndInit(); }
        element.Measure(new Size(520, 600)); element.Arrange(new Rect(new Point(), element.DesiredSize)); element.ApplyTemplate(); element.UpdateLayout(); Sta.Pump();
    }

    private sealed class DelegateCommand(Action<object?> execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
    }
}


