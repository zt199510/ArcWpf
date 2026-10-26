using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcButton = ArcWpf.Controls.Button;
using WpfButton = System.Windows.Controls.Button;

namespace ArcWpf.Tests;

[TestClass]
public sealed class DrawerTests
{
    [TestMethod]
    public void OpenAndBindingCloseKeepContentAndHostContext() => Sta.Run(() =>
    {
        var model = new Model { Open = true };
        var input = new TextBox { Text = "original" };
        var drawer = new Drawer { Title = "Filters", Content = input };
        var binding = drawer.SetBinding(Drawer.IsOpenProperty, new Binding(nameof(Model.Open)) { Source = model });
        var host = new DrawerHost { Content = new Border(), Drawer = drawer, DataContext = model };
        Layout(host);
        Assert.IsTrue(drawer.IsOpen); Assert.IsTrue(drawer.IsPresented); Assert.AreSame(model, drawer.DataContext);
        input.Text = "edited"; drawer.Close(); Pump(260);
        Assert.IsFalse(model.Open); Assert.IsFalse(drawer.IsPresented); Assert.AreSame(binding, BindingOperations.GetBindingExpression(drawer, Drawer.IsOpenProperty));
        model.Open = true; Layout(host); Assert.IsTrue(drawer.IsPresented); Assert.AreSame(input, drawer.Content); Assert.AreEqual("edited", input.Text); drawer.Close();
    });

    [TestMethod]
    public void FourSidesUseExpectedPanelGeometry() => Sta.Run(() =>
    {
        var (host, drawer) = Open(DrawerSide.Right); var surface = Part<Border>(drawer, "PART_Surface");
        Assert.AreEqual(480d, surface.ActualWidth, .01); Assert.AreEqual(320d, surface.TranslatePoint(new Point(), host).X, .01); drawer.Close();
        foreach (var side in new[] { DrawerSide.Left, DrawerSide.Top, DrawerSide.Bottom })
        {
            drawer.Side = side; drawer.IsOpen = true; Layout(host);
            surface = Part<Border>(drawer, "PART_Surface");
            if (side is DrawerSide.Left) Assert.AreEqual(0d, surface.TranslatePoint(new Point(), host).X, .01);
            if (side is DrawerSide.Top) Assert.AreEqual(512d, surface.ActualHeight, .01);
            if (side is DrawerSide.Bottom) Assert.AreEqual(512d, surface.ActualHeight, .01);
            drawer.Close(); Pump(260);
        }
    });

    [TestMethod]
    public void CloseButtonUsesDialogCloseGeometry() => Sta.Run(() =>
    {
        var (_, drawer) = Open();
        var close = Part<ArcButton>(drawer, "PART_CloseButton");
        var path = FindVisual<Path>(close)!;
        Assert.AreEqual(32d, close.ActualWidth, .01);
        Assert.AreEqual(32d, close.ActualHeight, .01);
        Assert.AreEqual(0d, close.MinHeight, .01);
        Assert.AreEqual(new Rect(4, 4, 8, 8), path.Data.Bounds);
        drawer.Close();
    });

    [TestMethod]
    public void CancelledCloseKeepsPresentedAndOverlayButtonCloses() => Sta.Run(() =>
    {
        var (host, drawer) = Open(); bool cancel = true; DrawerCloseReason? reason = null;
        drawer.Closing += (_, e) => { reason = e.Reason; e.Cancel = cancel; };
        drawer.Close(); Assert.IsTrue(drawer.IsOpen); Assert.IsTrue(drawer.IsPresented); Assert.AreEqual(DrawerCloseReason.Programmatic, reason);
        cancel = false; ((ButtonBase)Part<ArcButton>(drawer, "PART_CloseButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Pump(260);
        Assert.IsFalse(drawer.IsPresented); Assert.IsTrue(Part<ContentPresenter>(host, "PART_Background").IsHitTestVisible);
    });

    [TestMethod]
    public void FocusAndAutomationUseTitleAndDescription() => Sta.Run(() =>
    {
        var first = new WpfButton { Content = "First" }; var second = new WpfButton { Content = "Second" };
        var drawer = new Drawer { Title = "Workspace", Description = "Review settings", InitialFocusElement = first, Content = new StackPanel { Children = { first, second } } };
        var host = new DrawerHost { Content = new Border(), Drawer = drawer }; Layout(host); drawer.IsOpen = true; Layout(host); drawer.FocusInitial();
        Assert.AreSame(first, drawer.InitialFocusElement);
        var peer = UIElementAutomationPeer.CreatePeerForElement(drawer)!; Assert.IsTrue(peer.IsDialog()); Assert.AreEqual("Workspace", peer.GetName()); Assert.AreEqual("Review settings", peer.GetHelpText()); drawer.Close();
    });

    [TestMethod]
    public void BackgroundIsDisabledAndConsumerEffectBindingRemains() => Sta.Run(() =>
    {
        var background = new Border(); var effect = new DropShadowEffect(); background.Effect = effect;
        var host = new DrawerHost { Content = background }; Layout(host); var drawer = new Drawer { Title = "Details" }; drawer.Show(background); Layout(host);
        var presenter = Part<ContentPresenter>(host, "PART_Background"); Assert.IsFalse(presenter.IsEnabled); Assert.IsInstanceOfType<BlurEffect>(presenter.Effect); Assert.AreSame(effect, background.Effect);
        drawer.Close(); Pump(260); Assert.IsTrue(presenter.IsEnabled); Assert.IsNull(presenter.Effect); Assert.AreSame(effect, background.Effect);
    });

    [TestMethod]
    public void FullReducedAndOffAnimationsReleaseSurface() => Sta.Run(() =>
    {
        var (host, drawer) = Open(); var surface = Part<FrameworkElement>(drawer, "PART_Surface");
        ArcMotion.SetLevel(drawer, ArcMotionLevel.Reduced); drawer.Close(); Pump(260); Assert.IsFalse(drawer.IsMotionRunning); Assert.IsFalse(drawer.IsPresented);
        drawer.IsOpen = true; Layout(host); ArcMotion.SetLevel(drawer, ArcMotionLevel.Off); Assert.AreEqual(1d, surface.Opacity, .001); drawer.Close(); Assert.IsFalse(drawer.IsPresented); Assert.IsFalse(drawer.IsMotionRunning);
    });

    [TestMethod]
    public void ReplacementUnloadAndTemplateChangeForceClose() => Sta.Run(() =>
    {
        var (host, drawer) = Open(); drawer.Closing += (_, e) => e.Cancel = true; var next = new Drawer { Title = "Next", IsOpen = true }; host.Drawer = next; Layout(host);
        Assert.IsFalse(drawer.IsOpen); Assert.IsFalse(drawer.IsPresented); Assert.IsTrue(next.IsPresented);
        next.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); Assert.IsFalse(next.IsOpen); Assert.IsFalse(next.IsPresented);
    });

    [TestMethod]
    public void GalleryConstructsWithoutWindow() => Sta.Run(() =>
    {
        var page = new ArcWpf.Gallery.Pages.DrawerPage(); var host = new DrawerHost { Content = page }; Layout(host); Assert.IsNotNull(page.Content); Assert.IsFalse(page.IsVisible);
    });

    private static (DrawerHost, Drawer) Open(DrawerSide side = DrawerSide.Right)
    {
        var drawer = new Drawer { Title = "Filters", Description = "Narrow the list", Side = side, Content = new TextBlock { Text = "Body" } };
        var host = new DrawerHost { Content = new Border(), Drawer = drawer }; Layout(host); drawer.IsOpen = true; Layout(host); return (host, drawer);
    }

    private static void Layout(FrameworkElement element, double width = 800, double height = 600)
    {
        if (!element.IsInitialized) { element.BeginInit(); element.EndInit(); }
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.ApplyTemplate(); element.UpdateLayout(); Sta.Pump();
    }

    private static T Part<T>(Control control, string name) where T : FrameworkElement => (T)control.Template.FindName(name, control);
    private static T? FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) return found;
            if (FindVisual<T>(child) is { } nested) return nested;
        }
        return null;
    }
    private static void Pump(int milliseconds) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) }; timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    private sealed class Model : INotifyPropertyChanged { private bool _open; public bool Open { get => _open; set { _open = value; PropertyChanged?.Invoke(this, new(nameof(Open))); } } public event PropertyChangedEventHandler? PropertyChanged; }
}
