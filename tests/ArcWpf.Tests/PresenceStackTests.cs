using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;
namespace ArcWpf.Tests;

[TestClass]
public sealed class PresenceStackTests
{
    [TestMethod] public void IdentityBindingAndDuplicateIds() => Sta.Run(() => WithStack((s, people) =>
    {
        ArcMotion.SetLevel(s, ArcMotionLevel.Off); var model = new Model { Limit = 2 }; s.SetBinding(PresenceStack.MaxProperty, new Binding(nameof(Model.Limit)) { Source = model });
        Assert.AreEqual(3, s.OverflowCount); var first = s.Faces["0"]; people.Move(0, 1); Assert.AreSame(first, s.Faces["0"]);
        people[1].Name = "Updated"; Assert.AreSame(first, s.Faces["0"]);
        model.Limit = -3; Pump(30); Assert.AreEqual(0, s.Max); Assert.HasCount(1, s.Faces); Assert.IsNotNull(s.GetBindingExpression(PresenceStack.MaxProperty));
        model.Limit = 10; s.People = [people[0], new(people[0].Id) { Name = "Duplicate" }]; Pump(30); Assert.AreEqual(1, s.Count); Assert.HasCount(1, s.Faces);
        Assert.ThrowsExactly<ArgumentException>(() => s.Size = AvatarSize.ExtraLarge);
        Assert.AreNotSame(new PresenceStack().People, new PresenceStack().People);
    }));
    [TestMethod] public void GeometryReservesFullWidthAndEndAnchor() => Sta.Run(() => WithStack((s, people) =>
    {
        ArcMotion.SetLevel(s, ArcMotionLevel.Off);
        foreach (var size in new[] { AvatarSize.Small, AvatarSize.Medium, AvatarSize.Large })
        {
            s.Size = size; s.UpdateLayout(); double d = size == AvatarSize.Small ? 28 : size == AvatarSize.Large ? 44 : 36;
            Assert.AreEqual(d + 4 * Math.Floor(d * .68), s.ActualWidth, .1); Assert.AreEqual(d, s.ActualHeight, .1);
            Assert.AreEqual(Math.Floor(d * .68), s.Faces["1"].X, .1);
        }
        s.Size = AvatarSize.Medium; s.UpdateLayout(); Assert.AreEqual(132d, s.ActualWidth, .1);
        Assert.AreEqual(24d, s.Faces["1"].X); Assert.IsGreaterThan(Panel.GetZIndex(s.Faces["1"]), Panel.GetZIndex(s.Faces["0"]));
        double width = s.ActualWidth; s.Align = PresenceStackAlignment.End; people.RemoveAt(4); people.RemoveAt(3); s.UpdateLayout();
        Assert.AreEqual(width, s.ActualWidth, .1); Assert.AreEqual(width - s.Diameter, s.Faces["2"].X, .1);
        people.Clear(); Pump(30); s.UpdateLayout(); Assert.AreEqual(width, s.ActualWidth, .1); Assert.HasCount(0, s.Faces);
    }));
    [TestMethod] public void ReflowExitRejoinAndReducedSettle() => Sta.Run(() => WithStack((s, people) =>
    {
        Pump(400); double width = s.ActualWidth; var first = s.Faces["0"]; var person = people[0]; people.RemoveAt(0); Pump(30);
        Assert.IsTrue(first.Exiting); Assert.IsTrue(s.MotionRunning); people.Insert(0, person); Assert.AreSame(first, s.Faces["0"]); Pump(450);
        Assert.IsFalse(first.Exiting); Assert.AreEqual(1d, first.Opacity, .01); Assert.AreEqual(width, s.ActualWidth, .1);
        people[0].Status = PresenceStatus.Typing; Assert.IsTrue(s.MotionRunning);
        ArcMotion.SetLevel(s, ArcMotionLevel.Reduced); Assert.IsFalse(s.MotionRunning);
        people.RemoveAt(0); Pump(30); Assert.IsFalse(s.MotionRunning); Assert.IsFalse(s.Faces.ContainsKey("0"));
        ArcMotion.SetLevel(s, ArcMotionLevel.Full); people[0].Status = PresenceStatus.Typing; Assert.IsTrue(s.MotionRunning);
        s.Visibility = Visibility.Hidden; Assert.IsFalse(s.MotionRunning); s.Visibility = Visibility.Visible; Assert.IsTrue(s.MotionRunning);
    }));
    [TestMethod] public void MembershipAnnouncementsAndAutomation() => Sta.Run(() => WithStack((s, people) =>
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(s)!; s.Label = "Editors";
        Assert.AreEqual(AutomationControlType.Button, peer.GetAutomationControlType()); Assert.AreEqual("Editors, 5", peer.GetName()); Assert.IsNull(peer.GetChildren());
        Assert.IsNotNull(peer.GetPattern(PatternInterface.Invoke)); Assert.IsInstanceOfType<IExpandCollapseProvider>(peer.GetPattern(PatternInterface.ExpandCollapse));
        people.Add(new("new") { Name = "Guest" }); Assert.AreEqual("Guest joined", s.LastAnnouncement);
        people[^1].Status = PresenceStatus.Idle; Assert.AreEqual("Guest joined", s.LastAnnouncement);
        people.RemoveAt(people.Count - 1); Assert.AreEqual("Guest left", s.LastAnnouncement);
        s.Announce = false; people.Add(new("silent")); Assert.AreEqual("Guest left", s.LastAnnouncement);
    }));
    [TestMethod] public void RosterSelectionAndFiniteLayout() => Sta.Run(() => WithStack((s, people) =>
    {
        ArcMotion.SetLevel(s, ArcMotionLevel.Off); for (int i = 5; i < 15; i++) people.Add(new(i.ToString()) { Name = "Long member name " + new string('X', 70), Detail = "An extended detail " + new string('X', 100) });
        var popover = (Popover)s.Template.FindName("PART_Popover", s); var content = (StackPanel)popover.Content;
        content.Measure(new Size(274, double.PositiveInfinity)); content.Arrange(new Rect(new Point(), content.DesiredSize)); content.UpdateLayout();
        var scroll = (ScrollViewer)content.Children[1]; Assert.IsLessThanOrEqualTo(320d, scroll.ActualHeight); Assert.HasCount(15, s.Rows);
        var header = (Grid)content.Children[0]; var title = (TextBlock)header.Children[0]; var count = (PresenceRoll)header.Children[1];
        Assert.IsGreaterThan(title.ActualWidth / 2, count.TranslatePoint(new Point(), header).X);
        Assert.AreEqual(274d, content.ActualWidth, .1); Assert.AreEqual(new Thickness(6), popover.Padding);
        int calls = 0; s.PersonSelected += (_, e) => { calls++; Assert.AreSame(people[0], e.Person); };
        s.SelectPerson(people[0]); Assert.AreEqual(0, calls); s.IsPersonSelectionEnabled = true;
        var row = s.Rows["0"]; var button = (System.Windows.Controls.Button)row.Children[0]; button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(1, calls); Assert.IsFalse(s.IsOpen); s.IsPersonSelectionEnabled = false; Assert.IsFalse(row.Children[0] is System.Windows.Controls.Button);
    }));
    [TestMethod] public void TemplateUnloadAndReattachKeepLatestData() => Sta.Run(() => WithStack((s, people) =>
    {
        people[0].Status = PresenceStatus.Typing; var root = (Grid)VisualTreeHelper.GetParent(s); root.Children.Remove(s); Pump(40); Assert.IsFalse(s.MotionRunning);
        people.Clear(); root.Children.Add(s); Pump(50); Assert.AreEqual(0, s.Count);
        var template = s.Template; s.Template = new ControlTemplate(typeof(PresenceStack)); s.ApplyTemplate(); people.Add(new("return") { Name = "Return" });
        s.Template = template; s.ApplyTemplate(); Pump(50); Assert.HasCount(1, s.Faces); Assert.HasCount(1, s.Rows);
        ArcMotion.SetLevel(s, ArcMotionLevel.Off); Assert.IsFalse(s.MotionRunning);
    }));
    [TestMethod] public void KeyboardFanLabelAndRosterRendering() => Sta.Run(() => WithStack((s, people) =>
    {
        ArcMotion.SetLevel(s, ArcMotionLevel.Off); s.SelfId = "0"; s.Focus(); Pump(20);
        Assert.IsTrue(s.IsKeyboardFocused); double width = s.ActualWidth;
        Assert.AreEqual(s.Diameter + 4, s.Faces["1"].X, .1);
        var label = (Border)s.Template.FindName("PART_Label", s); Assert.AreEqual("Person 0 (you)", ((TextBlock)label.Child).Text); Assert.AreEqual(44d, Canvas.GetTop(label)); Assert.AreEqual(26d, label.Height);
        s.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(s), 0, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.AreEqual("Person 1", ((TextBlock)label.Child).Text); Assert.AreEqual(width, s.ActualWidth, .1);
        ArcMotion.SetLevel(s, ArcMotionLevel.Full); people[2].Name = "A much longer member name";
        double oldWidth = label.Width;
        s.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(s), 0, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.IsTrue(s.MotionRunning); Pump(40); Assert.IsGreaterThan(oldWidth, label.Width); Pump(400);
        Assert.IsFalse(s.MotionRunning); ArcMotion.SetLevel(s, ArcMotionLevel.Off);
        s.Align = PresenceStackAlignment.End; Assert.AreEqual(s.ActualWidth - s.Diameter, s.Faces["\0overflow"].X, .1);
        s.Align = PresenceStackAlignment.Start;
        Save((FrameworkElement)VisualTreeHelper.GetParent(s), "keyboard-fan.png");
        s.IsPersonSelectionEnabled = true; people[0].Detail = "Editing the canvas";
        var popover = (Popover)s.Template.FindName("PART_Popover", s); var content = (StackPanel)popover.Content; popover.Content = null;
        var surface = new Border { Width = 288, Padding = new Thickness(6), BorderThickness = new Thickness(1), Child = content };
        surface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised"); surface.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        using var host = Host(surface, 288, 360); Pump(30); Save(surface, "roster-light.png");
        Assert.IsNotNull(((System.Windows.Controls.Button)s.Rows["0"].Children[0]).Style);
        var peer = UIElementAutomationPeer.CreatePeerForElement(content)!; Assert.AreEqual("Here now", peer.GetName());
    }));
    [TestMethod] public void RollingDirectionAndInterruption() => Sta.Run(() =>
    {
        var roll = new PresenceRoll { Width = 100 }; using var host = Host(roll, 100, 30); roll.Set("2", false); roll.Set("3", true); Pump(25);
        var current = (TextBlock)roll.Children[1]; Assert.IsGreaterThan(0d, ((TranslateTransform)current.RenderTransform).Y);
        roll.Set("1", true); Pump(25); Assert.IsLessThan(0d, ((TranslateTransform)current.RenderTransform).Y); roll.Set("0", false); Assert.IsFalse(roll.IsRunning); Assert.AreEqual(0d, ((TranslateTransform)current.RenderTransform).Y);
    });
    [TestMethod] public void GalleryRendersBothThemes() => Sta.Run(() =>
    {
        var page = new PresenceStackPage { Width = 950, Height = 1000 }; page.SetResourceReference(Control.BackgroundProperty, "ArcWpf.Brush.Background"); ArcMotion.SetLevel(page, ArcMotionLevel.Off);
        using var host = Host(page, 950, 1000); Pump(80); Save(page, "gallery-light.png");
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark; Pump(80); Save(page, "gallery-dark.png");
        var reference = (PresenceStack)page.FindName("Reference");
        Assert.AreEqual(132d, reference.ActualWidth, .1); Assert.AreEqual(2, reference.OverflowCount);
        var status = reference.Faces["jasmine"].Children.OfType<Border>().Last();
        Assert.AreEqual(((SolidColorBrush)reference.FindResource("ArcWpf.Brush.AvatarOnline")).Color, ((SolidColorBrush)status.Background).Color);
    });
    private static void WithStack(Action<PresenceStack, ObservableCollection<PresencePerson>> action)
    {
        var people = new ObservableCollection<PresencePerson>(Enumerable.Range(0, 5).Select(i => new PresencePerson(i.ToString()) { Name = "Person " + i }));
        var stack = new PresenceStack { People = people, Margin = new Thickness(50), VerticalAlignment = VerticalAlignment.Top }; var root = new Grid { Width = 700, Height = 240 }; root.SetResourceReference(Panel.BackgroundProperty, "ArcWpf.Brush.Background"); root.Children.Add(stack);
        using (var host = Host(root, 700, 240)) { Pump(80); action(stack, people); } Pump(30); Assert.IsFalse(stack.MotionRunning);
    }
    private static HwndSource Host(FrameworkElement root, int width, int height)
    {
        var host = new HwndSource(new HwndSourceParameters("PresenceStack offscreen tests") { Width = width, Height = height, WindowStyle = unchecked((int)0x80000000) });
        root.Resources.MergedDictionaries.Add(Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single()); host.RootVisual = root; root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); return host;
    }
    private static void Pump(int ms) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) }; timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    private static void Save(FrameworkElement element, string name)
    {
        element.UpdateLayout(); var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen()) dc.DrawRectangle(new VisualBrush(element), null, new Rect(element.RenderSize)); var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "ArcWpf.slnx"))) root = root.Parent;
        var dir = System.IO.Path.Combine(root!.FullName, "artifacts", "presence-stack"); System.IO.Directory.CreateDirectory(dir); using var file = System.IO.File.Create(System.IO.Path.Combine(dir, name)); encoder.Save(file);
    }
    private sealed class Model : INotifyPropertyChanged { private int _limit; public int Limit { get => _limit; set { _limit = value; PropertyChanged?.Invoke(this, new(nameof(Limit))); } } public event PropertyChangedEventHandler? PropertyChanged; }
}
