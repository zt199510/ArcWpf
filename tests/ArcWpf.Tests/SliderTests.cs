using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;
using ArcSlider = ArcWpf.Controls.Slider;

namespace ArcWpf.Tests;
[TestClass]
public sealed class SliderTests
{
    [TestMethod] public void RangeStepAndNativeBindingStayIndependent() => Sta.Run(() => InWindow(s =>
    {
        var model = new SliderModel { Amount = 25 }; s.SetBinding(ArcSlider.ValueProperty, new Binding("Amount") { Source = model });
        Assert.AreEqual(25d, s.Value); Assert.AreEqual(100d, s.Maximum);
        s.Minimum = -1; s.Maximum = 1; s.Step = .3; Assert.AreEqual(1d, s.Value);
        Assert.AreEqual(.3, s.SmallChange); Assert.AreEqual(-.1, s.SnapValue(-.16), 1e-10); Assert.AreEqual(1d, s.SnapValue(1));
        model.Amount = .21; Assert.AreEqual(.21, s.Value, 1e-10, "External values are not forced to step grid.");
        System.Windows.Controls.Slider.IncreaseSmall.Execute(null, s); Assert.AreEqual(.5, model.Amount, 1e-10); Assert.IsNotNull(s.GetBindingExpression(ArcSlider.ValueProperty));
        s.Maximum = s.Minimum; Assert.AreEqual(s.Minimum, s.Value); Assert.AreEqual(s.Minimum, s.SnapValue(999));
        Assert.Throws<ArgumentException>(() => s.Step = 0); Assert.Throws<ArgumentException>(() => s.Step = double.NaN);
        Assert.Throws<ArgumentException>(() => s.Value = double.PositiveInfinity);
        s.Minimum = 0; s.Maximum = 1; s.Step = 1e-14; Assert.AreEqual(3e-14, s.SnapValue(3.1e-14), 1e-25);
    }));

    [TestMethod] public void GestureTracksContinuouslyButCommitsSteppedValueOnce() => Sta.Run(() => InWindow(s =>
    {
        var model = new SliderModel { Amount = 20 }; s.SetBinding(ArcSlider.ValueProperty, new Binding("Amount") { Source = model }); s.Step = 10; Pump(600);
        int commits = 0; s.ValueCommitted += (_, _) => commits++;
        s.BeginGesture(X(s, 20) + 4, true, 0); s.FollowGesture(X(s, 43) + 4, 20);
        Assert.AreEqual(43d, s.ShownPosition, .01); Assert.AreEqual(40d, model.Amount); Assert.AreEqual(0, commits);
        s.EndGesture(100, false); Assert.AreEqual(1, commits); Assert.AreEqual(40d, model.Amount); Pump(700);
        Assert.AreEqual(40d, s.ShownPosition, .01); Pump(200); Assert.IsFalse(s.IsMotionRunning); Assert.IsNotNull(s.GetBindingExpression(ArcSlider.ValueProperty));
    }));

    [TestMethod] public void RubberMomentumAndCancellationRespectLimits() => Sta.Run(() => InWindow(s =>
    {
        s.Step = 10; s.Value = 20; Pump(600); s.BeginGesture(X(s, 20), true, 0);
        s.FollowGesture(X(s, 42), 20); s.FollowGesture(X(s, 44), 40); s.EndGesture(45, false);
        Assert.AreEqual(50d, s.Value, "Coarse-grid momentum carries no more than one step."); Pump(650);
        s.BeginGesture(X(s, 50), true, 1000); s.FollowGesture(X(s, 140), 1020);
        Assert.AreEqual(100d, s.Value); Assert.IsGreaterThan(100d, s.ShownPosition);
        Assert.IsLessThan(9d, (s.ShownPosition - 100) / 100 * Travel(s));
        int commits = 0; s.ValueCommitted += (_, _) => commits++; s.EndGesture(1050, true);
        Assert.AreEqual(0, commits); Assert.AreEqual(100d, s.ShownPosition, .001); Assert.IsFalse(s.HasPendingLinger);
    }));

    [TestMethod] public void KeyboardCommandsReverseAndBoundaryStrainKeepValue() => Sta.Run(() => InWindow(s =>
    {
        Window.GetWindow(s).Activate(); s.Focus(); s.Step = 5; s.LargeChange = 20;
        Key(s, System.Windows.Input.Key.Right); Assert.AreEqual(5d, s.Value);
        Key(s, System.Windows.Input.Key.PageUp); Assert.AreEqual(25d, s.Value);
        s.IsDirectionReversed = true; Key(s, System.Windows.Input.Key.Right); Assert.AreEqual(20d, s.Value);
        Key(s, System.Windows.Input.Key.End); Pump(700); Assert.AreEqual(100d, s.Value);
        Key(s, System.Windows.Input.Key.Up); Pump(25); Assert.IsGreaterThan(100d, s.ShownPosition); Assert.AreEqual(100d, s.Value); Pump(750); Assert.AreEqual(100d, s.ShownPosition, .01);
        Assert.AreEqual(Visibility.Visible, Part<FrameworkElement>(s, "PART_FocusRing").Visibility);
        s.IsEnabled = false; Key(s, System.Windows.Input.Key.Home); Assert.AreEqual(100d, s.Value); Assert.IsFalse(s.IsMotionRunning);
    }));

    [TestMethod] public void TrackClickCatchesUpAndReversesWithoutJumping() => Sta.Run(() => InWindow(s =>
    {
        s.BeginGesture(X(s, 80), false, 0); Assert.AreEqual(80d, s.Value); Assert.AreEqual(0d, s.ShownPosition, .001);
        Pump(65); Assert.IsGreaterThan(0d, s.ShownPosition); Assert.IsLessThan(80d, s.ShownPosition);
        s.FollowGesture(X(s, 60), 70); s.EndGesture(150, false); Pump(700); Assert.AreEqual(60d, s.ShownPosition, .01);
        s.Value = 10; Pump(40); double middle = s.ShownPosition; s.Value = 90; Assert.AreEqual(middle, s.ShownPosition, .001); Pump(800); Assert.AreEqual(90d, s.ShownPosition, .01); Assert.IsFalse(s.IsMotionRunning);
    }));

    [TestMethod] public void BubbleStaysInsideEndpointsAndRetainsExitHost() => Sta.Run(() => InWindow(s =>
    {
        s.Width = 180; s.ValueFormat = "0' percent'"; s.UpdateLayout();
        foreach (double value in new[] { 0d, 100d })
        {
            s.BeginGesture(X(s, value), false, 0); Pump(400); s.UpdateLayout(); var bubble = Part<Border>(s, "PART_Bubble");
            Assert.AreEqual(Visibility.Visible, bubble.Visibility); Assert.IsGreaterThanOrEqualTo(0d, Canvas.GetLeft(bubble)); Assert.IsLessThanOrEqualTo(180.1, Canvas.GetLeft(bubble) + bubble.ActualWidth);
            s.EndGesture(1000, false); Pump(100); Assert.AreEqual(Visibility.Visible, bubble.Visibility); Pump(780); Assert.AreEqual(Visibility.Collapsed, bubble.Visibility);
        }
        s.BeginGesture(X(s, 100), false, 0); Pump(350); Save(Window.GetWindow(s), "bubble-endpoint.png"); s.EndGesture(1000, true);
    }));

    [TestMethod] public void MarksCollectionAndThemeRefreshPreserveGeometry() => Sta.Run(() => InWindow(s =>
    {
        var marks = new ObservableCollection<SliderMark> { new(0, "Minimum"), new(50, "Half"), new(100, "Maximum"), new(200, "ignored") }; s.Marks = marks; s.Value = 50; Pump(650); s.UpdateLayout();
        var canvas = Part<Canvas>(s, "PART_Marks"); Assert.HasCount(3, canvas.Children); marks.Add(new(25, "Quarter")); Assert.HasCount(4, canvas.Children);
        foreach (FrameworkElement label in canvas.Children) { Assert.IsGreaterThanOrEqualTo(0d, Canvas.GetLeft(label)); Assert.IsLessThanOrEqualTo(400.1, Canvas.GetLeft(label) + label.DesiredSize.Width); }
        var button = (System.Windows.Controls.Button)canvas.Children[2]; button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); Assert.AreEqual(100d, s.Value);
        s.IsDirectionReversed = true; s.UpdateLayout(); Assert.IsGreaterThan(Canvas.GetLeft((UIElement)canvas.Children[2]), Canvas.GetLeft((UIElement)canvas.Children[0]));
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark; Pump(300);
        var fill = (Border)Part<Grid>(s, "PART_FillClip").Children[0]; Assert.AreEqual(((SolidColorBrush)s.FindResource("ArcWpf.Brush.ControlFill")).Color, ((SolidColorBrush)fill.Background).Color);
        Assert.AreEqual(.35, ((DropShadowEffect)Part<Border>(s, "PART_ThumbVisual").Effect).Opacity, .001);
        Assert.AreEqual(((SolidColorBrush)s.FindResource("ArcWpf.Brush.TextSecondary")).Color, ((SolidColorBrush)button.Foreground).Color);
    }));

    [TestMethod] public void RollingNumbersAndReducedOffReachCleanStableValues() => Sta.Run(() => InWindow(s =>
    {
        s.Maximum = 1000; s.ValueFormat = "$#,0.0"; s.Value = 99; Pump(700);
        var readout = (SliderNumber)Part<Grid>(s, "PART_ReadoutHost").Children[0]; double before = readout.ActualWidth;
        s.Value = 100; Pump(45); Assert.IsTrue(readout.IsRunning); Assert.IsGreaterThan(before, readout.ActualWidth); Pump(750); Assert.IsFalse(readout.IsRunning); Assert.AreEqual("$100.0", s.ValueText);
        s.Value = 9; Pump(35); s.Value = 101; Pump(800); Assert.IsFalse(readout.IsRunning); Assert.AreEqual("$101.0", s.ValueText);
        ArcMotion.SetLevel(s, ArcMotionLevel.Reduced); s.Value = 99; Pump(150); Assert.IsFalse(s.IsMotionRunning); Assert.AreEqual(9.9, s.ShownPosition, .001);
        ArcMotion.SetLevel(s, ArcMotionLevel.Off); s.Value = 1000; Assert.IsFalse(s.IsMotionRunning); Assert.AreEqual(100d, s.ShownPosition); Assert.AreEqual("$1,000.0", s.ValueText);
        s.UpdateLayout(); double oldWidth = readout.ActualWidth; s.FontSize = 20; s.UpdateLayout(); Assert.IsGreaterThan(oldWidth, readout.ActualWidth);
    }));

    [TestMethod] public void DisabledUnloadAndTemplateSwapCancelGestureWork() => Sta.Run(() => InWindow(s =>
    {
        s.BeginGesture(X(s, 80), false, 0); Pump(30); s.IsEnabled = false;
        Assert.IsFalse(s.IsDragging); Assert.IsFalse(s.IsMotionRunning); Assert.IsFalse(s.HasPendingLinger); Assert.AreEqual(Visibility.Collapsed, Part<Border>(s, "PART_Bubble").Visibility);
        s.IsEnabled = true; var template = s.Template; s.BeginGesture(X(s, 20), false, 0); s.Template = new ControlTemplate(typeof(ArcSlider)); s.ApplyTemplate(); Assert.IsFalse(s.IsMotionRunning); Assert.IsFalse(s.IsDragging);
        s.Template = template; s.ApplyTemplate(); s.UpdateLayout(); s.Value = 60; Pump(30); s.Visibility = Visibility.Hidden; Assert.IsFalse(s.IsMotionRunning);
        s.Visibility = Visibility.Visible; s.Value = 10; Pump(800); Assert.AreEqual(10d, s.ShownPosition, .01);
        var surface = Part<FrameworkElement>(s, "PART_Surface"); Assert.IsTrue(surface.CaptureMouse()); s.BeginGesture(X(s, 20), false, 0); surface.ReleaseMouseCapture(); Assert.IsFalse(s.IsDragging); Assert.IsFalse(s.HasPendingLinger);
    }));

    [TestMethod] public void NativeAutomationAndHitSurfaceExposeSliderContract() => Sta.Run(() => InWindow(s =>
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(s)!; Assert.AreEqual(AutomationControlType.Slider, peer.GetAutomationControlType()); Assert.AreEqual("Volume", peer.GetName());
        var range = (IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue); range.SetValue(37); Assert.AreEqual(37d, s.Value); Assert.AreEqual(100d, range.Maximum);
        Assert.IsFalse(range.IsReadOnly); s.IsEnabled = false; Assert.Throws<System.Windows.Automation.ElementNotEnabledException>(() => range.SetValue(20)); s.IsEnabled = true;
        var surface = Part<FrameworkElement>(s, "PART_Surface"); var point = surface.TranslatePoint(new Point(X(s, 25), 22), s);
        Assert.AreSame(surface, s.InputHitTest(point), "Transparent row must receive pointer events, not an inert decorative thumb.");
    }));

    [TestMethod] public void LayoutAndGalleryRenderAtNormalAndNarrowWidths() => Sta.Run(() =>
    {
        InWindow(s =>
        {
            ArcMotion.SetLevel(s, ArcMotionLevel.Off); s.Label = "A long label that must leave space for the value";
            foreach (double width in new[] { 400d, 180d })
            {
                s.Width = width; s.UpdateLayout(); var surface = Part<FrameworkElement>(s, "PART_Surface"); var thumb = Part<Border>(s, "PART_ThumbVisual");
                Assert.AreEqual(44d, surface.ActualHeight, .01); Assert.AreEqual(22d, thumb.ActualWidth, .01); Assert.IsGreaterThan(44d, s.ActualHeight);
                var origin = surface.TranslatePoint(new Point(), Window.GetWindow(s));
                foreach (double value in new[] { 0d, 9d, 100d, 0d }) { s.Value = value; s.UpdateLayout(); Assert.AreEqual(origin, surface.TranslatePoint(new Point(), Window.GetWindow(s))); Assert.AreEqual(width, surface.ActualWidth, .01); Assert.IsGreaterThanOrEqualTo(0d, Canvas.GetLeft(thumb)); Assert.IsLessThanOrEqualTo(width + .01, Canvas.GetLeft(thumb) + thumb.ActualWidth); }
            }
            s.FlowDirection = FlowDirection.RightToLeft; s.Value = 25; s.UpdateLayout();
            var rtlThumb = Part<Border>(s, "PART_ThumbVisual"); var rtlSurface = Part<FrameworkElement>(s, "PART_Surface");
            double center = rtlThumb.TranslatePoint(new Point(11, 11), Window.GetWindow(s)).X;
            double left = rtlSurface.TranslatePoint(new Point(rtlSurface.ActualWidth, 0), Window.GetWindow(s)).X;
            Assert.AreEqual(left + 11 + Travel(s) * .75, center, .1);
        });
        var page = new SliderPage(); page.SetResourceReference(Control.BackgroundProperty, "ArcWpf.Brush.Background"); var window = new Window { Content = page, Width = 720, Height = 960, ShowActivated = false };
        try
        {
            window.Show(); Pump(180); Save(page, "gallery-light.png");
            Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark; Pump(220); Save(page, "gallery-dark.png");
        }
        finally { window.Close(); Sta.Pump(); }
    });

    private static T Part<T>(ArcSlider s, string name) where T : class => (T)s.Template.FindName(name, s);
    private static double Travel(ArcSlider s) => Part<FrameworkElement>(s, "PART_Surface").ActualWidth - 22;
    private static double X(ArcSlider s, double percent) => 11 + Travel(s) * percent / 100;
    private static void Key(UIElement target, Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    private static void InWindow(Action<ArcSlider> action)
    {
        var slider = new ArcSlider { Label = "Volume", Width = 400, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(30) };
        var window = new Window { Content = slider, Width = 600, Height = 300, ShowActivated = false };
        try { window.Show(); Sta.Pump(); action(slider); }
        finally { window.Close(); Sta.Pump(); Assert.IsFalse(slider.IsMotionRunning); Assert.IsFalse(slider.HasPendingLinger); }
    }
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static void Save(FrameworkElement element, string name)
    {
        element.UpdateLayout(); var image = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual();
        using (var drawing = backdrop.RenderOpen())
        {
            var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            drawing.DrawRectangle((Brush)element.FindResource("ArcWpf.Brush.Background"), null, bounds);
            drawing.DrawRectangle(new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds, Stretch = Stretch.Fill }, null, bounds);
        }
        image.Render(backdrop);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "ArcWpf.slnx"))) root = root.Parent;
        var folder = System.IO.Path.Combine(root!.FullName, "artifacts", "slider"); System.IO.Directory.CreateDirectory(folder);
        using var output = System.IO.File.Create(System.IO.Path.Combine(folder, name)); encoder.Save(output);
    }
}
