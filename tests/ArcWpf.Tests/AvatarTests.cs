using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
public sealed class AvatarTests
{
    [TestMethod] public void InitialsBindingAndSingleImageAutomation() => Sta.Run(() => WithAvatar(a =>
    {
        var model = new Model(); a.DataContext = model; a.SetBinding(Avatar.DisplayNameProperty, new Binding(nameof(Model.Name)));
        Assert.AreEqual("MC", a.Initials); model.Name = "  noah\tvan Williams "; Assert.AreEqual("NV", a.Initials);
        model.Name = "😀 Developer"; Assert.AreEqual("😀D", a.Initials); model.Name = "林晓"; Assert.AreEqual("林", a.Initials);
        model.Name = " "; Assert.AreEqual("", a.Initials); model.Name = "Maya Chen"; a.Status = AvatarStatus.Online;
        var peer = UIElementAutomationPeer.CreatePeerForElement(a)!; Assert.AreEqual(AutomationControlType.Image, peer.GetAutomationControlType()); Assert.AreEqual("Maya Chen, online", peer.GetName());
        Assert.IsTrue(peer.GetChildren() is null or { Count: 0 }); AutomationProperties.SetName(a, "Owner"); Assert.AreEqual("Owner", peer.GetName());
        Assert.IsFalse(a.Focusable); Assert.IsFalse(a.IsTabStop); Assert.IsNull(a.FocusVisualStyle); Assert.AreSame(model, a.DataContext); Assert.IsNotNull(a.GetBindingExpression(Avatar.DisplayNameProperty));
    }));

    [TestMethod] public void FourSizesPlaceDotAndOutlineWithoutClipping() => Sta.Run(() => WithAvatar(a =>
    {
        ArcMotion.SetLevel(a, ArcMotionLevel.Off); a.Status = AvatarStatus.Online;
        double[] sizes = [28, 36, 48, 88], dots = [8, 10, 12, 14];
        foreach (var size in Enum.GetValues<AvatarSize>())
        {
            a.Size = size; a.UpdateLayout(); int i = (int)size; var dot = Part<Border>(a, "PART_Status"); var face = Part<FrameworkElement>(a, "PART_Face");
            Assert.AreEqual(sizes[i], a.ActualWidth, .1); Assert.AreEqual(sizes[i], a.ActualHeight, .1); Assert.AreEqual(dots[i], dot.ActualWidth, .1);
            double offset = size == AvatarSize.ExtraLarge ? 4 : 0; var point = dot.TranslatePoint(new Point(), a);
            Assert.AreEqual(sizes[i] - dots[i] - offset, point.X, .1); Assert.AreEqual(point.X, point.Y, .1);
            Assert.AreEqual(sizes[i] / 2, ((EllipseGeometry)face.Clip).RadiusX, .1);
            var ring = (Border)dot.Child; Assert.AreEqual(dots[i] + 2, ring.ActualWidth, .1); Assert.AreEqual(-1d, ring.TranslatePoint(new Point(), dot).X, .1);
        }
        a.Width = 120; a.Height = 60; a.UpdateLayout(); Assert.AreEqual(60d, Part<FrameworkElement>(a, "PART_Face").ActualWidth, .1); Assert.AreEqual(30d, Part<FrameworkElement>(a, "PART_Face").TranslatePoint(new Point(), a).X, .1);
        Assert.ThrowsExactly<ArgumentException>(() => a.Size = (AvatarSize)99);
    }));

    [TestMethod] public void DecodedImagesAreImmediateAndSourceBindingSurvives() => Sta.Run(() => WithAvatar(a =>
    {
        var source = Bitmap(); a.SetBinding(Avatar.SourceProperty, new Binding { Source = source }); a.UpdateLayout();
        Assert.AreSame(source, Part<Image>(a, "PART_Image").Source); Assert.AreEqual(1d, Part<Image>(a, "PART_Image").Opacity); Assert.IsFalse(a.IsMotionRunning);
        Assert.AreEqual(Visibility.Collapsed, Part<TextBlock>(a, "PART_Initials").Visibility); Assert.IsNotNull(a.GetBindingExpression(Avatar.SourceProperty));
        a.ClearValue(Avatar.SourceProperty); Assert.AreEqual(Visibility.Visible, Part<TextBlock>(a, "PART_Initials").Visibility); Assert.IsFalse(a.HasImageError);
    }));

    [TestMethod] public void DownloadCompletesWithRealImageBlurFadeAndSettles() => Sta.Run(() => WithAvatar(a =>
    {
        using var server = new ImageServer(); var source = Load(server.Uri); a.Source = source; Pump(60);
        Assert.IsTrue(a.IsImageLoading); Assert.AreEqual(0d, Part<Image>(a, "PART_Image").Opacity);
        server.Send(Png()); Until(() => !a.IsImageLoading); Pump(35);
        var image = Part<Image>(a, "PART_Image"); Assert.IsFalse(a.HasImageError); Assert.IsTrue(a.IsMotionRunning); Assert.IsLessThan(1d, image.Opacity); Assert.IsNotNull(image.Effect);
        Pump(350); Assert.AreEqual(1d, image.Opacity, .001); Assert.AreEqual(0d, ((BlurEffect)image.Effect).Radius, .001); Assert.IsFalse(a.IsMotionRunning);
        a.InvalidateVisual(); Pump(50); Assert.AreEqual(1d, image.Opacity, .001);
    }));

    [TestMethod] public void FailedDownloadFallsBackAndOldRequestsCannotReplaceNewSource() => Sta.Run(() => WithAvatar(a =>
    {
        using (var failure = new ImageServer())
        {
            a.Source = Load(failure.Uri); failure.Send(Encoding.UTF8.GetBytes("not an image")); Until(() => a.HasImageError);
            Assert.AreEqual(Visibility.Visible, Part<TextBlock>(a, "PART_Initials").Visibility); Assert.AreEqual(Visibility.Collapsed, Part<Image>(a, "PART_Image").Visibility);
            Pump(350); Assert.AreEqual(1d, Part<TextBlock>(a, "PART_Initials").Opacity); Assert.IsFalse(a.IsMotionRunning);
        }
        using var old = new ImageServer(); a.Source = Load(old.Uri); Pump(30); var newest = Bitmap(); a.Source = newest;
        old.Send(Encoding.UTF8.GetBytes("bad old request")); Pump(300);
        Assert.AreSame(newest, a.Source); Assert.AreSame(newest, Part<Image>(a, "PART_Image").Source); Assert.IsFalse(a.HasImageError); Assert.IsFalse(a.IsImageLoading);
    }));

    [TestMethod] public void PresenceTransitionsExitReverseAndModesCleanClocks() => Sta.Run(() => WithAvatar(a =>
    {
        a.Status = AvatarStatus.Online; Pump(35); var dot = Part<Border>(a, "PART_Status"); Assert.IsLessThan(1d, dot.Opacity);
        a.Status = AvatarStatus.Offline; Assert.AreEqual(Visibility.Visible, Part<Border>(a, "PART_StatusExit").Visibility);
        Pump(350); Assert.IsFalse(a.IsMotionRunning); Assert.AreEqual(1d, dot.Opacity, .001); Assert.AreEqual(1d, ((ScaleTransform)dot.RenderTransform).ScaleX, .001);
        a.Status = AvatarStatus.None; Assert.AreEqual(Visibility.Visible, Part<Border>(a, "PART_StatusExit").Visibility); Pump(250); Assert.AreEqual(Visibility.Collapsed, Part<Border>(a, "PART_StatusExit").Visibility);
        foreach (var mode in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off }) { ArcMotion.SetLevel(a, mode); a.Status = AvatarStatus.Online; Assert.IsFalse(a.IsMotionRunning); Assert.AreEqual(1d, dot.Opacity); a.Status = AvatarStatus.None; }
        ArcMotion.SetLevel(a, ArcMotionLevel.Full); a.Status = AvatarStatus.Online; Pump(25); a.Visibility = Visibility.Hidden; Assert.IsFalse(a.IsMotionRunning); a.Visibility = Visibility.Visible;
        var template = a.Template; a.Template = new ControlTemplate(typeof(Avatar)); a.ApplyTemplate(); a.Status = AvatarStatus.Offline; a.Template = template; a.ApplyTemplate(); a.UpdateLayout(); Assert.IsFalse(a.IsMotionRunning);
        Assert.AreEqual(Visibility.Visible, Part<Border>(a, "PART_Status").Visibility);
    }));

    [TestMethod] public void ReducedLoadingAndUnloadReloadUseCurrentSource() => Sta.Run(() => WithAvatar(a =>
    {
        ArcMotion.SetLevel(a, ArcMotionLevel.Reduced); using var server = new ImageServer(); a.Source = Load(server.Uri);
        server.Send(Png()); Until(() => !a.IsImageLoading); Assert.IsFalse(a.IsMotionRunning); Assert.IsNull(Part<Image>(a, "PART_Image").Effect);
        a.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); a.Source = Bitmap(); a.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.AreSame(a.Source, Part<Image>(a, "PART_Image").Source); Assert.IsFalse(a.IsMotionRunning);
    }));

    [TestMethod] public void ActualDetachDuringDownloadReloadsWithoutOldClocks() => Sta.Run(() => WithAvatar(a =>
    {
        using var server = new ImageServer(); a.Source = Load(server.Uri); Pump(30);
        var root = (Grid)VisualTreeHelper.GetParent(a); root.Children.Remove(a); Pump(30); Assert.IsFalse(a.IsLoaded); Assert.IsFalse(a.IsMotionRunning);
        server.Send(Png()); Until(() => a.Source is BitmapSource { IsDownloading: false });
        root.Children.Add(a); Pump(80); Assert.IsTrue(a.IsLoaded); Assert.IsFalse(a.IsImageLoading); Assert.IsFalse(a.HasImageError); Assert.IsFalse(a.IsMotionRunning); Assert.AreEqual(1d, Part<Image>(a, "PART_Image").Opacity);
    }));

    [TestMethod] public void GalleryRendersThemesAndPortraitCrop() => Sta.Run(() =>
    {
        var page = new AvatarPage { Width = 780, Height = 1160 }; page.SetResourceReference(Control.BackgroundProperty, "ArcWpf.Brush.Background"); ArcMotion.SetLevel(page, ArcMotionLevel.Off);
        using var source = Host(page, 780, 1160); Pump(100); Save(page, "gallery-light.png");
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark; Pump(100); Save(page, "gallery-dark.png");
        var a = (Avatar)page.FindName("Reference"); Assert.AreEqual("Maya Chen", a.DisplayName); Assert.AreEqual(48d, a.ActualWidth, .1);
        ((ArcWpf.Controls.Button)page.FindName("StatusButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Assert.AreEqual(AvatarStatus.Offline, ((Avatar)page.FindName("Editable")).Status);
        var face = Part<FrameworkElement>(a, "PART_Face"); var bitmap = Render(face); byte[] data = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(data, bitmap.PixelWidth * 4, 0);
        Assert.AreEqual(0, data[3]); Assert.IsGreaterThan((byte)240, data[(24 * bitmap.PixelWidth + 24) * 4 + 3]); Save(a, "avatar-portrait.png");
    });

    private static T Part<T>(Avatar a, string name) where T : class => (T)a.Template.FindName(name, a);
    private static void WithAvatar(Action<Avatar> test)
    {
        var a = new Avatar { DisplayName = "Maya Chen", VerticalAlignment = VerticalAlignment.Top }; var root = new Grid { Width = 500, Height = 200 }; root.Children.Add(a);
        using (var host = Host(root, 500, 200)) { Pump(60); test(a); } Pump(30); Assert.IsFalse(a.IsMotionRunning);
    }
    private static HwndSource Host(FrameworkElement root, int width, int height)
    {
        var host = new HwndSource(new HwndSourceParameters("Avatar offscreen tests") { Width = width, Height = height, WindowStyle = unchecked((int)0x80000000) });
        root.Resources.MergedDictionaries.Add(Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single()); host.RootVisual = root;
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); return host;
    }
    private static void Pump(int ms) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) }; timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    private static void Until(Func<bool> condition) { var timer = System.Diagnostics.Stopwatch.StartNew(); while (!condition() && timer.ElapsedMilliseconds < 4000) Pump(10); Assert.IsTrue(condition(), "Timed out waiting for native image pipeline."); }
    private static BitmapImage Load(Uri uri) { var image = new BitmapImage(); image.BeginInit(); image.UriSource = uri; image.EndInit(); return image; }
    private static BitmapSource Bitmap() { byte[] bytes = Enumerable.Repeat((byte)150, 20 * 10 * 4).ToArray(); var bitmap = BitmapSource.Create(20, 10, 96, 96, PixelFormats.Bgra32, null, bytes, 80); bitmap.Freeze(); return bitmap; }
    private static byte[] Png() { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Bitmap())); using var stream = new System.IO.MemoryStream(); encoder.Save(stream); return stream.ToArray(); }
    private static RenderTargetBitmap Render(FrameworkElement element)
    { element.UpdateLayout(); var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen()) dc.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.Fill }, null, new Rect(element.RenderSize)); var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing); return bitmap; }
    private static void Save(FrameworkElement element, string name)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Render(element))); var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "ArcWpf.slnx"))) root = root.Parent;
        var dir = System.IO.Path.Combine(root!.FullName, "artifacts", "avatar"); System.IO.Directory.CreateDirectory(dir); using var file = System.IO.File.Create(System.IO.Path.Combine(dir, name)); encoder.Save(file);
    }
    private sealed class Model : INotifyPropertyChanged { private string _name = "Maya Chen"; public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } } public event PropertyChangedEventHandler? PropertyChanged; }
    private sealed class ImageServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly TaskCompletionSource<byte[]> _body = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _cancel = new();
        public Uri Uri { get; }
        public ImageServer() { _listener.Start(); Uri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/image.png"); _ = Serve(); }
        public void Send(byte[] bytes) => _body.TrySetResult(bytes);
        private async Task Serve()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_cancel.Token); using var stream = client.GetStream(); var buffer = new byte[4096]; var request = new StringBuilder();
                while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    int count = await stream.ReadAsync(buffer, _cancel.Token); if (count == 0) return;
                    request.Append(Encoding.ASCII.GetString(buffer, 0, count)); if (request.Length > 16384) return;
                }
                var bytes = await _body.Task.WaitAsync(_cancel.Token); var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, _cancel.Token); await stream.WriteAsync(bytes, _cancel.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException or System.IO.IOException or ObjectDisposedException) { }
        }
        public void Dispose() { _cancel.Cancel(); _listener.Stop(); _cancel.Dispose(); }
    }
}
