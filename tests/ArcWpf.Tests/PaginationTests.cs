using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class PaginationTests
{
    [TestMethod]
    public void CoercionHandlesEmptyCountRestorationAndIntegerLimits() => Sta.Run(() =>
    {
        var pagination = new Pagination();
        Assert.AreEqual(0, pagination.Page);
        pagination.PageCount = 12; Assert.AreEqual(1, pagination.Page);
        pagination.Page = 99; Assert.AreEqual(12, pagination.Page);
        pagination.PageCount = 0; Assert.AreEqual(0, pagination.Page);
        pagination.PageCount = 120; Assert.AreEqual(99, pagination.Page);
        pagination.PageCount = -1; Assert.AreEqual(0, pagination.PageCount);
        pagination.PageCount = int.MaxValue; pagination.Page = int.MaxValue;
        Assert.AreEqual(int.MaxValue, pagination.Page);
    });

    [TestMethod]
    public void RequestsPreserveBindingAndHonorCommandsAndOrigins() => Sta.Run(() => InWindow(p =>
    {
        var model = new Pagination { PageCount = 12, Page = 1 };
        p.SetBinding(Pagination.PageProperty, new Binding("Page") { Source = model, Mode = BindingMode.TwoWay });
        var events = new List<PaginationPageChangedEventArgs>(); p.PageChanged += (_, e) => events.Add(e);
        var command = new PageCommand(); p.Command = command;
        p.Request(3, PaginationChangeOrigin.PageButton);
        Assert.AreEqual(3, model.Page); Assert.AreEqual(3, command.Executed);
        Assert.IsNotNull(p.GetBindingExpression(Pagination.PageProperty));
        Assert.AreEqual(PaginationChangeOrigin.PageButton, events.Single().Origin);
        p.Request(3, PaginationChangeOrigin.PageButton); Assert.HasCount(1, events);
        command.Allowed = false; command.Notify();
        p.Request(4, PaginationChangeOrigin.Next); Assert.AreEqual(3, p.Page);
        Assert.IsTrue(Host(p).Children.OfType<System.Windows.Controls.Button>().All(b => !b.IsEnabled));
        p.SetCurrentValue(Pagination.PageProperty, 10); Assert.AreEqual(3, command.Executed);
        p.PageCount = 2; Assert.AreEqual(2, p.Page);
        Assert.AreEqual(PaginationChangeOrigin.PageCount, events.Last().Origin);
    }));

    [TestMethod]
    public void WindowHasFivePagesWithNativeInvokeAndBoundaryStates() => Sta.Run(() => InWindow(p =>
    {
        Assert.AreEqual("1,2,3,4,5", Numbers(p));
        Assert.AreEqual(276d, Host(p).ActualWidth, .1);
        Assert.IsFalse(Buttons(p).First().IsEnabled);
        var button = Buttons(p).Single(b => AutomationProperties.GetName(b) == "Page 3");
        var peer = UIElementAutomationPeer.CreatePeerForElement(button)!;
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke(); Sta.Pump();
        Assert.AreEqual(3, p.Page);
        Assert.AreEqual("Current page", peer.GetItemStatus());
        p.Page = 12;
        Assert.AreEqual("8,9,10,11,12", Numbers(p));
        Assert.IsFalse(Buttons(p).Last().IsEnabled);
        p.PageCount = 0; Assert.HasCount(0, Host(p).Children);
        p.PageCount = int.MaxValue; p.Page = int.MaxValue;
        Assert.HasCount(7, Host(p).Children);
        Assert.IsFalse(Buttons(p).Last().IsEnabled);
    }));

    [TestMethod]
    public void SlidingRetargetsWithoutReflowAndSettlesMarkAndExits() => Sta.Run(() => InWindow(p =>
    {
        var originalSize = p.RenderSize;
        var mark = Mark(p); var transform = (TranslateTransform)mark.RenderTransform;
        p.Page = 3;
        Assert.IsTrue(p.IsAnimating);
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while ((transform.X <= 40 || transform.X >= 120) && DateTime.UtcNow < deadline) Pump(15);
        Assert.IsGreaterThan(40d, transform.X);
        Assert.IsLessThan(120d, transform.X);
        double from = transform.X;
        p.Page = 2;
        Pump(15);
        Assert.IsLessThan(40d, Math.Abs(transform.X - from), $"Retarget from {from} to {transform.X}");
        p.Page = 8;
        Assert.IsGreaterThan(0, Exits(p).Children.Count);
        p.Page = 4; p.Page = 12;
        UntilStopped(p);
        Assert.AreEqual(originalSize, p.RenderSize);
        Assert.HasCount(0, Exits(p).Children);
        AssertMarkAligned(p);
        Assert.IsFalse(mark.HasAnimatedProperties);
        Assert.IsFalse(transform.HasAnimatedProperties);
        foreach (var button in Buttons(p)) Assert.AreEqual(1d, button.Opacity, .001);
        var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
        theme.Mode = ArcThemeMode.Dark; Pump(200);
        AssertMarkAligned(p); Assert.IsFalse(p.IsAnimating);
    }));

    [TestMethod]
    public void NarrowLayoutWrapsAndResizeRealignsWithoutAnimation() => Sta.Run(() => InWindow(p =>
    {
        p.Width = 116; p.Page = 6; p.UpdateLayout(); UntilStopped(p);
        Assert.AreEqual(116d, p.ActualWidth, .1);
        Assert.AreEqual(116d, p.ActualHeight, .1);
        AssertMarkAligned(p);
        foreach (var b in Buttons(p))
        {
            Assert.AreEqual(36d, b.ActualWidth, .1); Assert.AreEqual(36d, b.ActualHeight, .1);
            var point = b.TranslatePoint(new Point(), p);
            Assert.IsTrue(point.X >= 0 && point.X + b.ActualWidth <= p.ActualWidth + .1);
        }
        p.Width = 276; p.UpdateLayout(); Sta.Pump();
        AssertMarkAligned(p); Assert.IsFalse(p.IsAnimating);
    }));

    [TestMethod]
    public void ReducedOffHideUnloadAndTemplateReplacementCleanUp() => Sta.Run(() => InWindow(p =>
    {
        p.Page = 8; Assert.IsTrue(p.IsAnimating);
        ArcMotion.SetLevel(p, ArcMotionLevel.Reduced);
        Assert.IsFalse(p.IsAnimating); Assert.HasCount(0, Exits(p).Children); AssertMarkAligned(p);
        p.Page = 1; Assert.IsFalse(p.IsAnimating); AssertMarkAligned(p);
        ArcMotion.SetLevel(p, ArcMotionLevel.Full); p.Page = 9;
        p.Visibility = Visibility.Hidden; Assert.IsFalse(p.IsAnimating);
        p.Visibility = Visibility.Visible; p.Page = 3;
        var template = p.Template; p.Template = new ControlTemplate(typeof(Pagination)); p.ApplyTemplate();
        Assert.IsFalse(p.IsAnimating);
        p.Template = template; p.ApplyTemplate(); p.UpdateLayout(); Sta.Pump(); AssertMarkAligned(p);
        ArcMotion.SetLevel(p, ArcMotionLevel.Off); p.Page = 12;
        Assert.IsFalse(p.IsAnimating); AssertMarkAligned(p);
        ArcMotion.SetLevel(p, ArcMotionLevel.Full); p.Page = 1;
        var window = Window.GetWindow(p); window.Content = null; Sta.Pump(); Assert.IsFalse(p.IsAnimating);
        window.Content = p; Sta.Pump(); AssertMarkAligned(p); Assert.IsFalse(p.IsAnimating);
    }));

    [TestMethod]
    public void NativeEnterFocusRecoveryAndSelectedColorFollowPage() => Sta.Run(() => InWindow(p =>
    {
        var next = Buttons(p).Last(); next.Focus();
        next.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(next)!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.AreEqual(2, p.Page); Assert.IsTrue(next.IsKeyboardFocused);
        var current = Buttons(p).Single(b => AutomationProperties.GetName(b) == "Page 2"); current.Focus();
        p.Page = 12; UntilStopped(p);
        var replacement = Buttons(p).Single(b => AutomationProperties.GetName(b) == "Page 12");
        Assert.IsTrue(replacement.IsKeyboardFocused);
        var presenter = (ContentPresenter)replacement.Template.FindName("Presenter", replacement);
        var brush = (SolidColorBrush)System.Windows.Documents.TextElement.GetForeground(presenter);
        Assert.AreEqual(((SolidColorBrush)p.FindResource("ArcWpf.Brush.Foreground")).Color, brush.Color);
        Assert.IsFalse(brush.HasAnimatedProperties);
    }));

    private static Panel Host(Pagination p) => (Panel)p.Template.FindName("PART_PagesHost", p);
    private static Canvas Exits(Pagination p) => (Canvas)p.Template.FindName("PART_ExitLayer", p);
    private static Border Mark(Pagination p) => (Border)p.Template.FindName("PART_Mark", p);
    private static System.Windows.Controls.Button[] Buttons(Pagination p) => Host(p).Children.OfType<System.Windows.Controls.Button>().ToArray();
    private static string Numbers(Pagination p) => string.Join(",", Buttons(p).Where(b => b.Content is string).Select(b => b.Content));
    private static void AssertMarkAligned(Pagination p)
    {
        var current = Buttons(p).Single(b => AutomationProperties.GetItemStatus(b) == "Current page");
        var expected = current.TranslatePoint(new Point(), p); var actual = Mark(p).TranslatePoint(new Point(), p);
        Assert.AreEqual(expected.X, actual.X, .1); Assert.AreEqual(expected.Y, actual.Y, .1);
    }
    private static void UntilStopped(Pagination p)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (p.IsAnimating && DateTime.UtcNow < deadline) Pump(20);
        Assert.IsFalse(p.IsAnimating, "Real WPF clocks should settle within three seconds.");
    }
    private static void InWindow(Action<Pagination> test)
    {
        var pagination = new Pagination { PageCount = 12, VerticalAlignment = VerticalAlignment.Top };
        var window = new Window { Content = pagination, Width = 420, Height = 260, ShowActivated = false };
        try { window.Show(); Sta.Pump(); test(pagination); }
        finally { window.Close(); Sta.Pump(); Assert.IsFalse(pagination.IsAnimating); }
    }
    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    private sealed class PageCommand : ICommand
    {
        public bool Allowed { get; set; } = true;
        public int Executed { get; private set; }
        public bool CanExecute(object? parameter) => Allowed;
        public void Execute(object? parameter) => Executed = (int)parameter!;
        public event EventHandler? CanExecuteChanged;
        public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
