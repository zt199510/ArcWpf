using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shell;
using ArcWpf.Controls;
using WpfButton = System.Windows.Controls.Button;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ArcWindowTests
{
    [TestMethod]
    public void CaptionButtonsRunTheWindowCommandsAndMaximizedContentStaysOnScreen() => Sta.Run(() =>
    {
        var window = Create(); bool closed = false; window.Closed += (_, _) => closed = true;
        Shown(window, () =>
        {
            var root = (FrameworkElement)window.Template.FindName("PART_Root", window);
            Assert.AreEqual("Minimize", AutomationProperties.GetName(window.MinimizeButton!)); Assert.AreEqual("Maximize", AutomationProperties.GetName(window.MaximizeButton!)); Assert.AreEqual("Close", AutomationProperties.GetName(window.CloseButton!));
            Assert.IsFalse(window.CloseButton!.Focusable, "Caption buttons stay out of the tab order, as native ones do.");
            Click(window.MaximizeButton!);
            Assert.AreEqual(WindowState.Maximized, window.WindowState); Assert.AreEqual("Restore", AutomationProperties.GetName(window.MaximizeButton!));
            Assert.AreEqual(SystemParameters.WindowResizeBorderThickness, root.Margin, "A maximized chrome window spills past the monitor by its resize border.");
            Click(window.MaximizeButton!);
            Assert.AreEqual(WindowState.Normal, window.WindowState); Assert.AreEqual(new Thickness(0), root.Margin); Assert.AreEqual("Maximize", AutomationProperties.GetName(window.MaximizeButton!));
            Click(window.MinimizeButton!); Assert.AreEqual(WindowState.Minimized, window.WindowState);
            window.WindowState = WindowState.Normal; Sta.Pump();
            Click(window.CloseButton!); Assert.IsTrue(closed);
        });
    });

    [TestMethod]
    public void ResizeModeDecidesTheCaptionButtonsAndTheResizeBorder() => Sta.Run(() =>
    {
        var window = Create(); window.ResizeMode = ResizeMode.CanMinimize;
        Shown(window, () =>
        {
            Assert.IsNotNull(window.MinimizeButton); Assert.IsFalse(window.MaximizeButton!.IsEnabled, "CanMinimize keeps a disabled maximize button.");
            Assert.AreEqual(new Thickness(0), WindowChrome.GetWindowChrome(window).ResizeBorderThickness);
            window.ResizeMode = ResizeMode.NoResize; Sta.Pump();
            var captions = (Panel)window.Template.FindName("PART_CaptionButtons", window);
            Assert.AreEqual(1, captions.Children.Count); Assert.AreSame(window.CloseButton, captions.Children[0]); Assert.IsNull(window.MaximizeButton);
            window.ResizeMode = ResizeMode.CanResize; Sta.Pump();
            Assert.AreEqual(3, captions.Children.Count); Assert.IsTrue(window.MaximizeButton!.IsEnabled);
            Assert.AreEqual(SystemParameters.WindowResizeBorderThickness, WindowChrome.GetWindowChrome(window).ResizeBorderThickness);
        });
    });

    [TestMethod]
    public void TitleBarHeightIsTheCaptionAndItsSlotsTakeInput() => Sta.Run(() =>
    {
        var action = new WpfButton { Content = "Share" }; var tab = new WpfButton { Content = "Chat" };
        var window = Create(); window.TitleBarContent = tab; window.TitleBarActions = action;
        Shown(window, () =>
        {
            var bar = (FrameworkElement)window.Template.FindName("PART_TitleBar", window);
            Assert.AreEqual(40, bar.ActualHeight, .01); Assert.AreEqual(40, WindowChrome.GetWindowChrome(window).CaptionHeight);
            Assert.AreEqual(40, window.CloseButton!.ActualHeight, .01, "Caption buttons fill the bar's height."); Assert.AreEqual(46, window.CloseButton.ActualWidth, .01);
            Assert.IsTrue(WindowChrome.GetIsHitTestVisibleInChrome(tab) && WindowChrome.GetIsHitTestVisibleInChrome(action), "Slot content must take clicks inside the caption.");
            Assert.IsTrue(WindowChrome.GetIsHitTestVisibleInChrome(window.CloseButton));
            window.TitleBarHeight = 48; Sta.Pump();
            Assert.AreEqual(48, WindowChrome.GetWindowChrome(window).CaptionHeight); Assert.AreEqual(48, bar.ActualHeight, .01);
            var title = Descendants(bar).OfType<TextBlock>().First(text => text.Text == "Project");
            window.ShowTitle = false; Sta.Pump(); Assert.AreEqual(Visibility.Collapsed, title.Visibility);
        });
    });

    [TestMethod]
    public void MaximizeAnswersTheSnapLayoutHitTestAndClicksThroughNonClientMessages() => Sta.Run(() =>
    {
        var window = Create();
        Shown(window, () =>
        {
            var maximize = (ArcWindow.CaptionButton)window.MaximizeButton!;
            var over = maximize.PointToScreen(new Point(maximize.ActualWidth / 2, maximize.ActualHeight / 2));
            bool handled = false;
            Assert.AreEqual(ArcWindow.HTMAXBUTTON, window.NonClient(0x84, IntPtr.Zero, Pack(over), ref handled).ToInt32()); Assert.IsTrue(handled);
            Assert.IsTrue(maximize.IsHot, "The system owns the pointer over the button now; the button still shows hover.");
            var title = window.CloseButton!.PointToScreen(new Point(-200, 20)); handled = false;
            Assert.AreEqual(0, window.NonClient(0x84, IntPtr.Zero, Pack(title), ref handled).ToInt32()); Assert.IsFalse(handled, "Elsewhere the chrome's own hit test decides.");
            Assert.IsFalse(maximize.IsHot);
            handled = false; window.NonClient(0xA1, (IntPtr)ArcWindow.HTMAXBUTTON, Pack(over), ref handled); Assert.IsTrue(handled, "The press is kept from the system's classic button tracking.");
            Assert.AreEqual(WindowState.Normal, window.WindowState, "Pressing alone does not maximize.");
            handled = false; window.NonClient(0xA2, (IntPtr)ArcWindow.HTMAXBUTTON, Pack(over), ref handled); Sta.Pump();
            Assert.AreEqual(WindowState.Maximized, window.WindowState, "Release over the button maximizes.");
            handled = false; window.NonClient(0xA2, (IntPtr)ArcWindow.HTMAXBUTTON, Pack(over), ref handled); Sta.Pump();
            Assert.AreEqual(WindowState.Maximized, window.WindowState, "A release without a press does nothing.");
        });
    });

    [TestMethod]
    public void CloseHoverTurnsRedWithWhiteInkInBothThemes() => Sta.Run(() =>
    {
        var window = Create();
        Shown(window, () =>
        {
            var close = (ArcWindow.CaptionButton)window.CloseButton!;
            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            Assert.AreEqual(0, ((SolidColorBrush)close.Background).Color.A, "At rest the button has no fill.");
            close.SetHot(true);
            var fill = ((SolidColorBrush)close.Background).Color; var ink = ((SolidColorBrush)close.Foreground).Color;
            Assert.AreEqual(((SolidColorBrush)Application.Current.Resources["ArcWpf.Brush.WindowCloseHover"]).Color, fill); Assert.AreEqual(Colors.White, ink);
            Assert.IsTrue(fill.R > 150 && fill.G < 90, $"Close hover is a red, got {fill}.");
            var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single(); theme.Mode = ArcThemeMode.Dark; Sta.Pump();
            Assert.AreEqual(fill, ((SolidColorBrush)close.Background).Color, "The dark theme keeps the same red so white ink stays readable.");
            close.SetHot(false); var maximize = (ArcWindow.CaptionButton)window.MaximizeButton!; maximize.SetHot(true);
            Assert.AreEqual(((SolidColorBrush)Application.Current.Resources["ArcWpf.Brush.SurfaceMuted"]).Color, ((SolidColorBrush)maximize.Background).Color);
        });
    });

    [TestMethod]
    public void GalleryOpensTheAgentWindowWithItsTitleBarSlots() => Sta.Run(() =>
    {
        var window = ArcWpf.Gallery.Pages.ArcWindowPage.CreateAgentWindow(); window.Resources.MergedDictionaries.Add(Theme()); window.ShowInTaskbar = false;
        Shown(window, () =>
        {
            Assert.IsInstanceOfType(window.TitleBarContent, typeof(SegmentedControl)); Assert.IsInstanceOfType(window.TitleBarActions, typeof(ThemeSwitch));
            var views = (FrameworkElement)window.TitleBarContent!; var bar = (FrameworkElement)window.Template.FindName("PART_TitleBar", window);
            Assert.IsTrue(views.ActualHeight > 0 && views.ActualHeight <= bar.ActualHeight, $"The view switch fits the bar, {views.ActualHeight}.");
        });
    });

    private static ResourceDictionary Theme() => new() { Source = new Uri("/ArcWpf.Controls;component/ArcWindow/Themes/ArcWindowTheme.xaml", UriKind.Relative) };
    private static ArcWindow Create() { var window = new ArcWindow { Title = "Project", Width = 640, Height = 420, ShowInTaskbar = false, Left = 0, Top = 0 }; window.Resources.MergedDictionaries.Add(Theme()); return window; }
    private static void Shown(ArcWindow window, Action body)
    {
        window.Show();
        try { window.Activate(); Sta.Pump(); body(); }
        finally { if (window.IsLoaded) window.Close(); }
    }
    // Through the automation invoke, which runs the button's own click and so its command.
    private static void Click(ButtonBase button) { ((System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer((WpfButton)button).GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)).Invoke(); Sta.Pump(); }
    private static IntPtr Pack(Point screen) => (IntPtr)(((int)screen.Y << 16) | ((int)screen.X & 0xFFFF));
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root) { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var next in Descendants(child)) yield return next; } }
}
