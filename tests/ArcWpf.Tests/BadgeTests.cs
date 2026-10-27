using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;

namespace ArcWpf.Tests;

[TestClass]
public sealed class BadgeTests
{
    [TestMethod] public void BindingAndAutomationExposeOnlyCurrentLabel() => Sta.Run(() => WithBadge(b =>
    {
        var model = new Model(); b.DataContext = model; b.SetBinding(ContentControl.ContentProperty, new Binding(nameof(Model.Label)));
        b.Icon = new TextBlock { Text = "icon" }; Layout(b); model.Label = "Live"; Pump(350);
        Assert.AreEqual("Live", b.Content); Assert.AreSame(model, b.DataContext); Assert.IsNotNull(b.GetBindingExpression(ContentControl.ContentProperty));
        var peer = UIElementAutomationPeer.CreatePeerForElement(b)!;
        Assert.AreEqual(AutomationControlType.Text, peer.GetAutomationControlType()); Assert.AreEqual("Live", peer.GetName());
        Assert.IsTrue(peer.GetChildren() is null or { Count: 0 }); Assert.IsFalse(b.Focusable); Assert.IsFalse(b.IsTabStop); Assert.IsNull(b.FocusVisualStyle);
        AutomationProperties.SetName(b, "Deployment status"); Assert.AreEqual("Deployment status", peer.GetName());
        Assert.ThrowsExactly<ArgumentException>(() => b.Size = (BadgeSize)10);
    }));

    [TestMethod] public void GeometryTracksNaturalContentIconGapAndBothSizes() => Sta.Run(() => WithBadge(b =>
    {
        ArcMotion.SetLevel(b, ArcMotionLevel.Off); b.Content = "Status"; Layout(b);
        var label = Part<ContentPresenter>(b, "PART_Label"); var frame = Part<Border>(b, "PART_Frame");
        Assert.AreEqual(26d, b.ActualHeight, .1); Assert.AreEqual(11d, label.TranslatePoint(new Point(), b).X, .1);
        double plain = b.ActualWidth; b.Icon = new Border { Width = 12, Height = 12 }; Layout(b);
        Assert.AreEqual(plain + 17, b.ActualWidth, .1); Assert.AreEqual(28d, label.TranslatePoint(new Point(), b).X, .1);
        Assert.AreEqual(13d, frame.CornerRadius.TopLeft, .1);
        b.Icon = null; Layout(b); Assert.AreEqual(plain, b.ActualWidth, .1);
        b.Size = BadgeSize.Small; Layout(b); Assert.AreEqual(22d, b.ActualHeight, .1); Assert.AreEqual(9d, label.TranslatePoint(new Point(), b).X, .1);
        b.Content = ""; Layout(b); Assert.AreEqual(18d, b.ActualWidth, .1);
        b.Content = "A very long label which must never wrap"; b.MaxWidth = 100; Layout(b); Assert.AreEqual(100d, b.ActualWidth, .1); Assert.AreEqual(22d, b.ActualHeight, .1);
        b.MaxWidth = double.PositiveInfinity; b.Content = new Border { Width = 30, Height = 40 }; Layout(b); Assert.AreEqual(42d, b.ActualHeight, .1); Assert.AreEqual(21d, frame.CornerRadius.TopLeft, .1);
    }));

    [TestMethod] public void TextAndWidthAnimateReverseSettleAndPassiveReflowSnaps() => Sta.Run(() => WithBadge(b =>
    {
        b.Content = "A"; Pump(1800); Layout(b); double start = b.ActualWidth;
        b.Content = "Deployment completed"; Layout(b);
        var label = Part<ContentPresenter>(b, "PART_Label"); var ghosts = Part<Canvas>(b, "PART_LabelGhost");
        Assert.HasCount(1, ghosts.Children); Assert.IsTrue(b.IsMotionRunning);
        var oldPixels = (BitmapSource)((Image)ghosts.Children[0]).Source; byte[] rgba = new byte[oldPixels.PixelWidth * oldPixels.PixelHeight * 4]; oldPixels.CopyPixels(rgba, oldPixels.PixelWidth * 4, 0);
        Assert.IsTrue(Enumerable.Range(0, rgba.Length / 4).Any(i => rgba[i * 4 + 3] > 0), "Outgoing content must have visible pixels at local coordinates.");
        Pump(60); Layout(b); Assert.IsGreaterThan(start, b.ActualWidth); Assert.IsLessThan(1d, label.Opacity);
        b.Content = "Ready"; Pump(1900); Layout(b);
        Assert.IsFalse(b.IsMotionRunning); Assert.HasCount(0, ghosts.Children); Assert.AreEqual(1d, label.Opacity, .001);
        Assert.AreEqual(0d, ((TranslateTransform)label.RenderTransform).Y, .001); Assert.AreEqual(0d, ((BlurEffect)label.Effect).Radius, .001);
        double settled = b.ActualWidth; b.InvalidateVisual(); Pump(80); Assert.AreEqual(settled, b.ActualWidth, .1);
        b.FontSize = 20; Layout(b); Assert.IsGreaterThan(settled, b.ActualWidth); Assert.IsFalse(b.IsMotionRunning);
        Assert.IsTrue(double.IsNaN(b.Width));
    }));

    [TestMethod] public void IconIdentitySwapAndRemovalHaveNoResidualSlot() => Sta.Run(() => WithBadge(b =>
    {
        b.Icon = new Border { Width = 12, Height = 12, Background = Brushes.Red }; Pump(1800); Layout(b); double withIcon = b.ActualWidth;
        b.Icon = new TextBlock { Text = "!" }; Layout(b); Assert.HasCount(1, Part<Canvas>(b, "PART_IconGhost").Children);
        Pump(400); Assert.AreEqual(1d, Part<ContentPresenter>(b, "PART_Icon").Opacity, .001);
        b.Icon = new TextBlock { Text = "?" }; Layout(b); Assert.HasCount(0, Part<Canvas>(b, "PART_IconGhost").Children);
        b.IconKey = "new identity"; Layout(b); Assert.HasCount(1, Part<Canvas>(b, "PART_IconGhost").Children);
        b.Icon = null; Pump(1800); Layout(b); Assert.IsFalse(b.IsMotionRunning);
        Assert.AreEqual(Visibility.Collapsed, Part<FrameworkElement>(b, "PART_IconSlot").Visibility);
        Assert.HasCount(0, Part<Canvas>(b, "PART_IconGhost").Children); Assert.AreEqual(withIcon - 17, b.ActualWidth, .1);
    }));

    [TestMethod] public void ReducedOffVisibilityAndTemplateChangesCleanUp() => Sta.Run(() => WithBadge(b =>
    {
        ArcMotion.SetLevel(b, ArcMotionLevel.Reduced); b.Content = "A longer status"; Layout(b);
        var label = Part<ContentPresenter>(b, "PART_Label"); Assert.IsNull(label.Effect); Assert.AreEqual(Transform.Identity.Value, label.RenderTransform.Value);
        Assert.IsFalse(Part<BadgeBody>(b, "PART_Body").IsRunning); Pump(220); Assert.IsFalse(b.IsMotionRunning);
        ArcMotion.SetLevel(b, ArcMotionLevel.Off); b.Content = "Off"; Layout(b); Assert.IsFalse(b.IsMotionRunning);
        ArcMotion.SetLevel(b, ArcMotionLevel.Full); b.Content = "A status in motion"; Pump(40); b.Visibility = Visibility.Hidden; Assert.IsFalse(b.IsMotionRunning);
        b.Visibility = Visibility.Visible; var template = b.Template; b.Template = new ControlTemplate(typeof(Badge)); b.ApplyTemplate(); b.Content = "Safe"; Assert.IsFalse(b.IsMotionRunning);
        b.Template = template; b.ApplyTemplate(); Layout(b); Assert.AreEqual("Safe", Part<ContentPresenter>(b, "PART_Label").Content);
        b.Content = "Again"; Pump(40); b.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); Assert.IsFalse(b.IsMotionRunning);
    }));

    [TestMethod] public void ToneColorClocksFinishAndThemeOverridesRemainDynamic() => Sta.Run(() => WithBadge(b =>
    {
        var frame = Part<Border>(b, "PART_Frame"); var before = ((SolidColorBrush)frame.Background).Color;
        b.Tone = BadgeTone.Success; Pump(40); var during = ((SolidColorBrush)frame.Background).Color;
        Assert.AreNotEqual(before, during); Assert.AreNotEqual(((SolidColorBrush)b.Background).Color, during);
        Pump(350); Assert.AreEqual(((SolidColorBrush)b.Background).Color, ((SolidColorBrush)frame.Background).Color); Assert.IsFalse(b.IsMotionRunning);
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark; Pump(350);
        Assert.AreEqual(((SolidColorBrush)b.Background).Color, ((SolidColorBrush)frame.Background).Color); Assert.AreNotEqual(before, ((SolidColorBrush)frame.Background).Color);
        b.Resources["ArcWpf.Brush.BadgeSuccessBackground"] = Brushes.Magenta; Pump(350); Assert.AreEqual(Colors.Magenta, ((SolidColorBrush)frame.Background).Color);
        b.Background = Brushes.Cyan; Pump(350); Assert.AreEqual(Colors.Cyan, ((SolidColorBrush)frame.Background).Color); b.InvalidateVisual(); Pump(50); Assert.AreEqual(Colors.Cyan, ((SolidColorBrush)frame.Background).Color);
        b.IsEnabled = false; Assert.AreEqual(1d, b.Opacity);
    }));

    [TestMethod] public void CustomContentAndWidthBindingsStayUnderConsumerControl() => Sta.Run(() => WithBadge(b =>
    {
        b.SetBinding(FrameworkElement.WidthProperty, new Binding { Source = 140d });
        b.ContentTemplate = (DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding}' FontWeight='Bold'/></DataTemplate>");
        b.Content = "Custom template"; Layout(b); Assert.IsFalse(b.IsMotionRunning); Assert.AreEqual(140d, b.ActualWidth, .1);
        b.Content = "Updated"; Layout(b); Assert.IsFalse(b.IsMotionRunning); Assert.IsNotNull(b.GetBindingExpression(FrameworkElement.WidthProperty));
        Assert.AreSame(b.ContentTemplate, Part<ContentPresenter>(b, "PART_Label").ContentTemplate);
        b.ContentTemplate = null; b.ContentStringFormat = "{0:000}"; b.Content = 9; Pump(350); Layout(b);
        Assert.AreEqual("{0:000}", Part<ContentPresenter>(b, "PART_Label").ContentStringFormat);
        Assert.AreEqual(140d, b.ActualWidth, .1);
    }));

    [TestMethod] public void OfflineGalleryAndCapsuleRenderLightAndDark() => Sta.Run(() =>
    {
        var page = new BadgePage { Width = 760, Height = 1050 }; page.SetResourceReference(Control.BackgroundProperty, "ArcWpf.Brush.Background"); ArcMotion.SetLevel(page, ArcMotionLevel.Off);
        using var host = Host(page, 760, 1050); Pump(100); Save(page, "gallery-light.png");
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark; Pump(100); Save(page, "gallery-dark.png");
        var badge = (Badge)page.FindName("Reference"); Assert.AreEqual("Draft", badge.Content); Assert.AreEqual(26d, badge.ActualHeight, .1);
        Save(badge, "badge-only.png"); var bitmap = Render(badge); byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        byte Alpha(int x, int y) => pixels[(y * bitmap.PixelWidth + x) * 4 + 3];
        Assert.IsLessThan((byte)40, Alpha(0, 0)); Assert.IsGreaterThan((byte)200, Alpha(bitmap.PixelWidth / 2, 1));
        ((ArcWpf.Controls.Button)page.FindName("LiveToggle")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Pump(80); Assert.AreEqual("Live", badge.Content); Assert.IsNotNull(badge.Icon); Save(page, "gallery-live-dark.png");
    });

    private static void WithBadge(Action<Badge> test)
    {
        var b = new Badge { Content = "Draft", VerticalAlignment = VerticalAlignment.Top };
        var root = new Grid { Width = 600, Height = 200 }; root.Children.Add(b);
        using (var host = Host(root, 600, 200)) { Pump(80); Layout(b); test(b); }
        Pump(20); Assert.IsFalse(b.IsMotionRunning);
    }
    // Hidden presentation source supplies a real Dispatcher/compositor without showing or operating a desktop window.
    private static HwndSource Host(FrameworkElement root, int width, int height)
    {
        var source = new HwndSource(new HwndSourceParameters("Badge offscreen tests") { Width = width, Height = height, WindowStyle = unchecked((int)0x80000000) });
        root.Resources.MergedDictionaries.Add(Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single()); source.RootVisual = root; root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); return source;
    }
    private static void Layout(Badge b) { b.UpdateLayout(); }
    private static T Part<T>(Badge b, string name) where T : class => (T)b.Template.FindName(name, b);
    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static RenderTargetBitmap Render(FrameworkElement element)
    { element.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32); var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen()) dc.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.Fill }, null, new Rect(element.RenderSize)); bitmap.Render(drawing); return bitmap; }
    private static void Save(FrameworkElement element, string name)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Render(element)));
        var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "ArcWpf.slnx"))) root = root.Parent;
        var dir = System.IO.Path.Combine(root!.FullName, "artifacts", "badge"); System.IO.Directory.CreateDirectory(dir); using var file = System.IO.File.Create(System.IO.Path.Combine(dir, name)); encoder.Save(file);
    }
    private sealed class Model : INotifyPropertyChanged
    {
        private string _label = "Draft";
        public string Label { get => _label; set { _label = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}




