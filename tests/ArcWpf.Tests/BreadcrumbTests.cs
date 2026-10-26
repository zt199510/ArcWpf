using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ArcWpf.Controls;
using WpfButton = System.Windows.Controls.Button;

namespace ArcWpf.Tests;

[TestClass]
public sealed class BreadcrumbTests
{
    [TestMethod]
    public void DefaultsAndCurrentItemFollowPath() => Sta.Run(() =>
    {
        var breadcrumb = Create();
        var workspace = new BreadcrumbItem { Label = "Workspace", Command = new DelegateCommand() };
        var settings = new BreadcrumbItem { Label = "Settings", Command = new DelegateCommand() };
        var billing = new BreadcrumbItem { Label = "Billing" };
        breadcrumb.Items.Add(workspace); breadcrumb.Items.Add(settings); breadcrumb.Items.Add(billing);
        Assert.AreEqual("Breadcrumb", breadcrumb.Label);
        Assert.IsFalse(workspace.IsCurrent); Assert.IsFalse(settings.IsCurrent); Assert.IsTrue(billing.IsCurrent);
        Assert.IsFalse(workspace.ShowSeparator); Assert.IsTrue(settings.ShowSeparator); Assert.IsTrue(billing.ShowSeparator);
    });

    [TestMethod]
    public void TemplateUsesArcSpacingAndCurrentLabel() => Sta.Run(() =>
    {
        var breadcrumb = Create(); breadcrumb.Items.Add(new BreadcrumbItem { Label = "Workspace", Command = new DelegateCommand() }); breadcrumb.Items.Add(new BreadcrumbItem { Label = "Billing" });
        Layout(breadcrumb);
        var host = (ItemsControl)breadcrumb.Template.FindName("PART_ItemsHost", breadcrumb);
        Assert.IsNotNull(host); Assert.AreEqual(2, host.Items.Count);
        var current = FindText(host, "Billing");
        Assert.IsNotNull(current); Assert.AreEqual(FontWeights.Medium, current.FontWeight);
        Assert.IsNull(FindButton(host, "Billing"));
        Assert.IsNotNull(FindButton(host, "Workspace"));
    });

    [TestMethod]
    public void ClickingInteractiveCrumbRaisesEventAndExecutesCommand() => Sta.Run(() =>
    {
        var breadcrumb = Create(); var command = new DelegateCommand(); var item = new BreadcrumbItem { Label = "Workspace", Command = command }; breadcrumb.Items.Add(item); breadcrumb.Items.Add(new BreadcrumbItem { Label = "Billing" }); Layout(breadcrumb);
        BreadcrumbItem? invoked = null; breadcrumb.ItemInvoked += (_, e) => invoked = e.Item;
        FindButton((ItemsControl)breadcrumb.Template.FindName("PART_ItemsHost", breadcrumb), "Workspace")!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.AreSame(item, invoked); Assert.AreEqual(1, command.Calls);
    });

    [TestMethod]
    public void CurrentAndDisabledCrumbsCannotInvoke() => Sta.Run(() =>
    {
        var breadcrumb = Create(); var command = new DelegateCommand(); var disabled = new BreadcrumbItem { Label = "Disabled", Command = command, IsEnabled = false }; breadcrumb.Items.Add(disabled); breadcrumb.Items.Add(new BreadcrumbItem { Label = "Current" }); Layout(breadcrumb);
        var host = (ItemsControl)breadcrumb.Template.FindName("PART_ItemsHost", breadcrumb);
        FindButton(host, "Disabled")!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.AreEqual(0, command.Calls); Assert.IsNull(FindButton(host, "Current"));
    });

    private static Breadcrumb Create()
    {
        var breadcrumb = new Breadcrumb();
        breadcrumb.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/Breadcrumb/Themes/BreadcrumbTheme.xaml", UriKind.Relative) });
        return breadcrumb;
    }

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(600, 200)); element.Arrange(new Rect(0, 0, 600, element.DesiredSize.Height)); element.UpdateLayout(); Sta.Pump();
    }

    private static WpfButton? FindButton(DependencyObject root, string label) => FindVisual(root, element => element is WpfButton button && button.Visibility == Visibility.Visible && Equals(button.Content, label)) as WpfButton;
    private static TextBlock? FindText(DependencyObject root, string label) => FindVisual(root, element => element is TextBlock text && text.Text == label) as TextBlock;
    private static DependencyObject? FindVisual(DependencyObject root, Func<DependencyObject, bool> match)
    {
        if (match(root)) return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) if (FindVisual(VisualTreeHelper.GetChild(root, i), match) is { } result) return result;
        return null;
    }

    private sealed class DelegateCommand : ICommand
    {
        public int Calls { get; private set; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Calls++;
    }
}
