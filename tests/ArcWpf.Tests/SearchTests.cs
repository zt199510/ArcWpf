using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ArcSearch = ArcWpf.Controls.Search;

namespace ArcWpf.Tests;

[TestClass]
public sealed class SearchTests
{
    [TestMethod]
    public void ClearPreservesBindingFocusAndUndo() => Sta.Run(() => InWindow(search =>
    {
        var source = new ArcSearch { Text = "Alex" };
        search.SetBinding(TextBox.TextProperty, new Binding("Text") { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        search.Focus();
        ArcSearch.ClearCommand.Execute(null, search);
        Assert.AreEqual(string.Empty, source.Text);
        Assert.IsNotNull(search.GetBindingExpression(TextBox.TextProperty));
        Assert.IsTrue(search.IsKeyboardFocused);
        Assert.IsTrue(search.CanUndo);
        search.Undo();
        Assert.AreEqual("Alex", source.Text);
        search.IsReadOnly = true;
        Assert.IsFalse(ArcSearch.ClearCommand.CanExecute(null, search));
        search.IsReadOnly = false; search.IsEnabled = false;
        Assert.IsFalse(ArcSearch.ClearCommand.CanExecute(null, search));
    }));

    [TestMethod]
    public void NativeTextAndPlaceholderAlignAndClearSlotNeverReflows() => Sta.Run(() => InWindow(search =>
    {
        search.Placeholder = "Name or email";
        search.UpdateLayout();
        var host = (ScrollViewer)search.Template.FindName("PART_ContentHost", search);
        var watermark = (TextBlock)search.Template.FindName("Watermark", search);
        double width = host.ActualWidth;
        var placeholder = watermark.TranslatePoint(new Point(), search);
        search.Text = "Alex"; search.UpdateLayout(); Sta.Pump();
        var first = search.GetRectFromCharacterIndex(0);
        Assert.AreEqual(placeholder.X, first.X, .5);
        Assert.AreEqual(width, host.ActualWidth, .5);
        ArcSearch.ClearCommand.Execute(null, search); search.UpdateLayout();
        Assert.AreEqual(width, host.ActualWidth, .5);
        search.Width = 180; search.UpdateLayout();
        Assert.IsGreaterThan(50d, host.ActualWidth);
        Assert.AreEqual(24d, ((FrameworkElement)search.Template.FindName("PART_ClearVisual", search)).ActualWidth, .1);
    }));

    [TestMethod]
    public void EscapeDoesNotClearDuringComposition() => Sta.Run(() => InWindow(search =>
    {
        search.Text = "existing";
        var composition = new TextComposition(InputManager.Current, search, "中");
        search.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.PreviewTextInputStartEvent });
        SendEscape(search);
        Assert.AreEqual("existing", search.Text);
        search.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent });
        SendEscape(search);
        Assert.AreEqual(string.Empty, search.Text);
    }));

    [TestMethod]
    public void ClearMotionRetargetsAndLeavesNoClockAfterExit() => Sta.Run(() => InWindow(search =>
    {
        var visual = (FrameworkElement)search.Template.FindName("PART_ClearVisual", search);
        var button = (Button)search.Template.FindName("PART_Clear", search);
        search.Text = "a";
        Assert.IsTrue(search.IsClearAnimating);
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (visual.Opacity <= 0 && DateTime.UtcNow < deadline) Pump(15);
        Assert.IsGreaterThan(0d, visual.Opacity, "The real WPF animation clock must advance within the bounded wait.");
        search.Clear(); Pump(40); search.Text = "again"; Pump(400);
        Assert.AreEqual(1d, visual.Opacity, .001);
        Assert.IsFalse(visual.HasAnimatedProperties);
        Assert.IsFalse(search.IsClearAnimating);
        search.Clear(); Pump(220);
        Assert.AreEqual(0d, visual.Opacity, .001);
        Assert.AreEqual(Visibility.Hidden, button.Visibility);
        Assert.IsFalse(button.Focusable);
        Assert.IsFalse(search.IsClearAnimating);
        var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
        theme.Mode = ArcThemeMode.Dark; Pump(50);
        Assert.AreEqual(0d, visual.Opacity, .001);
        ArcMotion.SetLevel(search, ArcMotionLevel.Reduced); search.Text = "reduced"; Pump(180);
        Assert.AreEqual(1d, ((ScaleTransform)visual.RenderTransform).ScaleX, .001);
        ArcMotion.SetLevel(search, ArcMotionLevel.Off); search.Clear();
        Assert.IsFalse(search.IsClearAnimating);
        Assert.AreEqual(Visibility.Hidden, button.Visibility);
    }));

    [TestMethod]
    public void AutomationAndTemplateReplacementKeepNativeEditing() => Sta.Run(() => InWindow(search =>
    {
        search.Text = "query"; ArcMotion.SetLevel(search, ArcMotionLevel.Off);
        var peer = UIElementAutomationPeer.CreatePeerForElement(search)!;
        Assert.AreEqual("Search", peer.GetClassName());
        Assert.AreEqual("Search members", peer.GetName());
        Assert.IsNotNull(peer.GetPattern(PatternInterface.Value));
        Assert.IsTrue(peer.GetChildren().Any(child => child.GetName() == "Clear search"));
        var old = search.Template;
        search.Template = new ControlTemplate(typeof(ArcSearch)); search.ApplyTemplate();
        Assert.IsFalse(search.IsClearAnimating);
        search.Template = old; search.ApplyTemplate(); search.UpdateLayout();
        Assert.AreEqual("query", search.Text);
        Assert.IsNotNull(search.Template.FindName("PART_ContentHost", search));
    }));

    [TestMethod]
    public void ColorsRetargetPreserveBindingAndStopOnUnload() => Sta.Run(() => InWindow(search =>
    {
        var icon = (System.Windows.Shapes.Path)search.Template.FindName("SearchIcon", search);
        var source = new Border { BorderBrush = Brushes.Red };
        search.SetBinding(Control.BorderBrushProperty, new Binding("BorderBrush") { Source = source });
        Pump(220);
        var surface = (Border)search.Template.FindName("Surface", search);
        Assert.AreEqual(Colors.Red, ((SolidColorBrush)surface.BorderBrush).Color);
        source.BorderBrush = Brushes.Blue;
        Assert.IsTrue(search.IsColorAnimating);
        source.BorderBrush = Brushes.Green;
        Pump(240);
        Assert.AreEqual(Colors.Green, ((SolidColorBrush)surface.BorderBrush).Color);
        Assert.IsNotNull(search.GetBindingExpression(Control.BorderBrushProperty));
        search.Text = "query";
        Pump(220);
        Assert.AreEqual(((SolidColorBrush)search.Foreground).Color, ((SolidColorBrush)icon.Stroke).Color);
        ArcMotion.SetLevel(search, ArcMotionLevel.Off);
        source.BorderBrush = Brushes.Red;
        Assert.IsFalse(search.IsColorAnimating);
        ArcMotion.SetLevel(search, ArcMotionLevel.Full);
        source.BorderBrush = Brushes.Blue;
        Assert.IsTrue(search.IsColorAnimating);
        search.Visibility = Visibility.Hidden;
        Assert.IsFalse(search.IsColorAnimating);
        search.Visibility = Visibility.Visible;
    }));

    [TestMethod]
    public void GalleryFilteringAndClearAnimationKeepFieldBoundsStable() => Sta.Run(() =>
    {
        var page = new ArcWpf.Gallery.Pages.SearchPage();
        var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var window = new Window { Content = scroll, Width = 680, Height = 600, ShowActivated = false };
        try
        {
            window.Show(); Sta.Pump();
            var search = (ArcSearch)page.FindName("MemberSearch");
            var surface = (Border)search.Template.FindName("Surface", search);
            var host = (ScrollViewer)search.Template.FindName("PART_ContentHost", search);
            var origin = surface.TranslatePoint(new Point(), window);
            var size = surface.RenderSize;
            var hostWidth = host.ActualWidth;
            foreach (string query in new[] { "Alex", "", "no matches", "", new string('x', 200), "" })
            {
                search.Text = query;
                for (int i = 0; i < 5; i++)
                {
                    Pump(60); window.UpdateLayout();
                    Assert.AreEqual(size.Width, surface.ActualWidth, .1);
                    Assert.AreEqual(size.Height, surface.ActualHeight, .1);
                    Assert.AreEqual(origin.X, surface.TranslatePoint(new Point(), window).X, .1);
                    Assert.AreEqual(hostWidth, host.ActualWidth, .1);
                }
            }
        }
        finally { window.Close(); Sta.Pump(); }
    });

    private static void InWindow(Action<ArcSearch> action)
    {
        var search = new ArcSearch { Width = 328, Label = "Search members", VerticalAlignment = VerticalAlignment.Top };
        var window = new Window { Content = search, Width = 420, Height = 220, ShowActivated = false };
        try { window.Show(); Sta.Pump(); action(search); }
        finally { window.Close(); Sta.Pump(); Assert.IsFalse(search.IsClearAnimating); Assert.IsFalse(search.IsColorAnimating); }
    }
    private static void SendEscape(ArcSearch search) => search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(search)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}
