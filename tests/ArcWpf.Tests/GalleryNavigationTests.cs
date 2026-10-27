using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcWpf.Gallery;
using ArcWpf.Gallery.Navigation;

namespace ArcWpf.Tests;

[TestClass]
public sealed class GalleryNavigationTests
{
    [TestMethod]
    public void SearchRestoresGroupsAndPreservesCurrentPage() => Sta.Run(() => InWindow((w, nav) =>
    {
        nav.SelectPage("segmented-control"); Pump();
        var host = (ContentControl)w.FindName("PageHost"); var page = host.Content;
        nav.Groups[1].IsExpanded = false;
        nav.Query = "PASSWORD"; Pump();
        Assert.AreEqual(1, nav.Groups.Sum(g => g.Count));
        Assert.AreEqual(Visibility.Visible, nav.Groups[0].Children.Single(n => n.Id == "password-input").Visibility);
        Assert.AreSame(page, host.Content);
        nav.Query = "no-such-component"; Pump();
        Assert.AreEqual(0, nav.Groups.Sum(g => g.Count)); Assert.AreSame(page, host.Content);
        nav.Query = ""; Pump();
        Assert.IsFalse(nav.Groups[1].IsExpanded);
        Assert.AreEqual("segmented-control", nav.SelectedPageId);
        Assert.AreEqual(34, nav.Groups.Sum(g => g.Count));
        nav.Query = "布局"; Pump(); Assert.AreEqual(8, nav.Groups.Sum(g => g.Count));
    }));

    [TestMethod]
    public void SearchKeyboardOpensOnePageAndEscapeClears() => Sta.Run(() => InWindow((w, nav) =>
    {
        nav.Query = "slider"; Pump(); int changes = 0; nav.PageSelected += (_, _) => changes++;
        var search = (TextBox)nav.FindName("SearchBox"); search.Focus(); Press(search, Key.Enter); Pump();
        Assert.AreEqual("slider", nav.SelectedPageId); Assert.AreEqual(1, changes);
        Assert.IsInstanceOfType<ArcWpf.Gallery.Pages.SliderPage>(((ContentControl)w.FindName("PageHost")).Content);
        search.Focus(); Press(search, Key.Escape); Pump(); Assert.AreEqual("", nav.Query);
    }));

    [TestMethod]
    public void GeometrySelectionRevealAndStickyHeading() => Sta.Run(() => InWindow((w, nav) =>
    {
        ArcMotion.SetLevel(nav, ArcMotionLevel.Off); Pump();
        var tree = (NavigationTree)nav.FindName("Tree");
        var root = (NavigationItem)tree.ItemContainerGenerator.ContainerFromIndex(0);
        var leaf = (NavigationItem)root.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.AreEqual(28d, ((FrameworkElement)root.Template.FindName("PART_Row", root)).ActualHeight, .1);
        Assert.AreEqual(30d, leaf.ActualHeight, .1); Assert.IsNull(leaf.FocusVisualStyle);
        Assert.AreEqual(32d, ((FrameworkElement)nav.FindName("SearchShell")).ActualHeight, .1);
        var scroll = (ScrollViewer)nav.FindName("Scroller");
        scroll.ScrollToVerticalOffset(50); Pump();
        Assert.AreEqual(Visibility.Visible, ((FrameworkElement)nav.FindName("StickyGroup")).Visibility);
        Assert.IsNotNull(scroll.OpacityMask);
        nav.SelectPage("slider"); Pump(); Assert.IsGreaterThan(0d, scroll.VerticalOffset);
        var last = (NavigationItem)tree.ItemContainerGenerator.ContainerFromIndex(3);
        var slider = (NavigationItem)last.ItemContainerGenerator.ContainerFromIndex(7);
        Assert.IsTrue(slider.IsSelected);
        var point = slider.TranslatePoint(new Point(), scroll);
        Assert.IsTrue(point.Y >= 0 && point.Y + slider.ActualHeight <= scroll.ViewportHeight + 1);
        root.IsExpanded = false; Pump(); Assert.AreEqual(28d, root.ActualHeight, .1);
        root.IsExpanded = true; Pump(); Assert.IsGreaterThan(28d, root.ActualHeight);
    }));

    [TestMethod]
    public void GalleryRendersLightDarkAndMotionSettles() => Sta.Run(() => InWindow((w, nav) =>
    {
        nav.SelectPage("segmented-control"); Pump(); Save(w, "gallery-light.png");
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark;
        Pump(); Save(w, "gallery-dark.png");
        var tree = (NavigationTree)nav.FindName("Tree");
        var root = (NavigationItem)tree.ItemContainerGenerator.ContainerFromIndex(0);
        root.IsExpanded = false; Pump(50); root.IsExpanded = true; Pump(500);
        Assert.IsFalse(root.IsAnimating);
        ArcMotion.SetLevel(nav, ArcMotionLevel.Reduced); root.IsExpanded = false; Pump(); Assert.IsFalse(root.IsAnimating);
    }));

    private static void InWindow(Action<MainWindow, GalleryNavigation> action)
    {
        var window = new MainWindow { ShowActivated = false };
        try { window.Show(); Pump(400); action(window, (GalleryNavigation)window.FindName("NavigationPanel")); }
        finally { window.Close(); Sta.Pump(); }
    }
    private static void Press(UIElement target, Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    private static void Pump(int ms = 300)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static void Save(FrameworkElement element, string name)
    {
        element.UpdateLayout(); var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
        var visual = new DrawingVisual();
        using (var draw = visual.RenderOpen())
        {
            draw.DrawRectangle((Brush)element.FindResource("ArcWpf.Brush.Background"), null, bounds);
            draw.DrawRectangle(new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds, Stretch = Stretch.Fill }, null, bounds);
        }
        var bitmap = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "ArcWpf.slnx"))) root = root.Parent;
        var dir = System.IO.Path.Combine(root!.FullName, "artifacts", "gallery-navigation"); System.IO.Directory.CreateDirectory(dir);
        using var file = System.IO.File.Create(System.IO.Path.Combine(dir, name)); encoder.Save(file);
    }
}
