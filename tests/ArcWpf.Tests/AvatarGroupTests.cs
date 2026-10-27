using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;

namespace ArcWpf.Tests;

[TestClass]
public sealed class AvatarGroupTests
{
    [TestMethod] public void BindingMaxAndMemberIdentityRemainStable() => Sta.Run(() => WithGroup((g, members) =>
    {
        ArcMotion.SetLevel(g, ArcMotionLevel.Off); var model = new Model { Limit = 3 }; g.SetBinding(AvatarGroup.MaxProperty, new Binding(nameof(Model.Limit)) { Source = model });
        Assert.AreEqual(2, g.OverflowCount); Assert.HasCount(4, g.Slots); var first = g.Slots[0]; members[0].DisplayName = "Changed Person"; members[0].Status = AvatarStatus.Offline; Pump(20);
        Assert.AreSame(first, g.Slots[0]); var avatar = Part<Avatar>(first, "PART_Avatar"); Assert.AreEqual("Changed Person", avatar.DisplayName); Assert.AreEqual(AvatarStatus.Offline, avatar.Status);
        members.Move(0, 2); Assert.AreSame(first, g.Slots[2]); model.Limit = -1; Assert.AreEqual(0, g.Max); Assert.AreEqual(-1, model.Limit); Assert.HasCount(1, g.Slots); Assert.AreEqual(5, g.OverflowCount);
        Assert.IsNotNull(g.GetBindingExpression(AvatarGroup.MaxProperty)); model.Limit = 100; Assert.HasCount(5, g.Slots); Assert.AreEqual(0, g.OverflowCount);
        g.Members = new[] { members[0], members[0], new AvatarGroupMember { DisplayName = members[0].DisplayName } }; Assert.HasCount(3, g.Slots);
        Assert.AreNotSame(g.Slots[0], g.Slots[1]); g.Members = null; Assert.HasCount(0, g.Slots);
    }));

    [TestMethod] public void ActualOverlapAndSizesMatchSourceGeometry() => Sta.Run(() => WithGroup((g, _) =>
    {
        ArcMotion.SetLevel(g, ArcMotionLevel.Off); g.Max = 3;
        foreach (var size in new[] { AvatarSize.Small, AvatarSize.Medium, AvatarSize.Large })
        {
            g.Size = size; g.UpdateLayout(); double d = size == AvatarSize.Small ? 28 : size == AvatarSize.Large ? 48 : 36;
            Assert.AreEqual(d, g.ActualHeight, .1); Assert.AreEqual(6 + 4 * (d - 6), g.ActualWidth, .1);
            for (int i = 0; i < 4; i++) { var lift = Part<FrameworkElement>(g.Slots[i], "PART_Lift"); Assert.AreEqual(i * (d - 6), lift.TranslatePoint(new Point(), g).X, .1); Assert.AreEqual(d, lift.ActualWidth, .1); }
            var ring = Part<Border>(g.Slots[0], "PART_Ring"); Assert.AreEqual(d + 2, ring.ActualWidth, .1); Assert.AreEqual(-1d, ring.TranslatePoint(new Point(), g).X, .1);
        }
        g.FlowDirection = FlowDirection.RightToLeft; g.UpdateLayout(); var physicalRoot = (UIElement)VisualTreeHelper.GetParent(g); Assert.IsGreaterThan(Part<FrameworkElement>(g.Slots[1], "PART_Lift").TranslatePoint(new Point(), physicalRoot).X, Part<FrameworkElement>(g.Slots[0], "PART_Lift").TranslatePoint(new Point(), physicalRoot).X);
        g.Members = null; g.Size = AvatarSize.Small; g.UpdateLayout(); Assert.AreEqual(28d, g.ActualHeight, .1); Assert.AreEqual(6d, g.ActualWidth, .1);
        Assert.ThrowsExactly<ArgumentException>(() => g.Size = AvatarSize.ExtraLarge);
    }));

    [TestMethod] public void GroupAutomationExcludesOverflowMembersAndExitingCopies() => Sta.Run(() => WithGroup((g, members) =>
    {
        g.Max = 3; Pump(500); g.Label = "Editors";
        var peer = UIElementAutomationPeer.CreatePeerForElement(g)!; Assert.AreEqual(AutomationControlType.Group, peer.GetAutomationControlType()); Assert.AreEqual("Editors", peer.GetName());
        var children = peer.GetChildren(); Assert.HasCount(4, children); Assert.AreEqual("Maya Chen, online", children[0].GetName()); Assert.AreEqual("2 more editors", children[3].GetName());
        Assert.IsTrue(children.All(c => c.GetAutomationControlType() == AutomationControlType.Image)); Assert.IsFalse(g.Focusable); Assert.IsFalse(g.IsTabStop); Assert.IsNull(g.FocusVisualStyle);
        members.RemoveAt(0); var live = g.Slots.Where(s => !s.IsExiting).ToArray(); Assert.HasCount(4, live); Assert.IsTrue(g.Slots.Any(s => s.IsExiting));
        Assert.IsFalse(UIElementAutomationPeer.CreatePeerForElement(g.Slots.First(s => s.IsExiting))!.IsControlElement());
        g.Max = 0; Pump(50); Assert.HasCount(1, peer.GetChildren()); Assert.AreEqual("4 more editors", peer.GetChildren()[0].GetName());
    }));

    [TestMethod] public void JoinLeaveAndReverseMoveActualStackWidthThenCleanUp() => Sta.Run(() => WithGroup((g, members) =>
    {
        g.Max = 10; Pump(550); double start = g.ActualWidth;
        var added = new AvatarGroupMember { DisplayName = "New Person" }; members.Add(added); Pump(60); g.UpdateLayout();
        Assert.IsGreaterThan(start, g.ActualWidth); Assert.IsLessThan(start + 30, g.ActualWidth); Assert.IsTrue(g.IsMotionRunning);
        members.Remove(added); Pump(30); members.Add(added); Pump(650); g.UpdateLayout();
        Assert.AreEqual(start + 30, g.ActualWidth, .1); Assert.IsFalse(g.IsMotionRunning); Assert.HasCount(6, g.Slots);
        members.RemoveAt(0); Pump(650); g.UpdateLayout(); Assert.AreEqual(start, g.ActualWidth, .1); Assert.IsFalse(g.Slots.Any(s => s.IsExiting));
        members.Add(added); members.RemoveAt(members.Count - 1); Pump(650); Assert.HasCount(5, g.Slots);
        g.InvalidateVisual(); Pump(50); Assert.IsFalse(g.IsMotionRunning);
    }));

    [TestMethod] public void OverflowRollsInBothDirectionsAndReducedOnlyFades() => Sta.Run(() => WithGroup((g, members) =>
    {
        g.Max = 3; Pump(500); var overflow = g.Slots.Last(); var current = Part<TextBlock>(overflow, "PART_Count"); var old = Part<TextBlock>(overflow, "PART_OldCount");
        members.Add(new AvatarGroupMember { DisplayName = "Extra" }); Assert.AreEqual("+3", current.Text); Pump(25); Assert.IsGreaterThan(0d, ((TranslateTransform)current.RenderTransform).Y);
        Pump(60); Assert.IsLessThan(1d, current.Opacity); Assert.AreEqual("+2", old.Text);
        members.RemoveAt(members.Count - 1); Pump(25); Assert.IsLessThan(0d, ((TranslateTransform)current.RenderTransform).Y); Pump(350);
        Assert.AreEqual(1d, current.Opacity, .001); Assert.AreEqual(0d, old.Opacity, .001); Assert.AreEqual(0d, ((BlurEffect)current.Effect).Radius, .001); Assert.IsFalse(g.IsMotionRunning);
        ArcMotion.SetLevel(g, ArcMotionLevel.Reduced); members.Add(new AvatarGroupMember { DisplayName = "Reduced" }); Assert.IsNull(current.Effect); Assert.AreEqual(0d, ((TranslateTransform)current.RenderTransform).Y); Pump(220); Assert.IsFalse(g.IsMotionRunning);
    }));

    [TestMethod] public void PointerFanLiftNeighborRingAndTipDoNotChangeLayout() => Sta.Run(() => WithGroup((g, _) =>
    {
        g.Max = 3; Pump(500); g.UpdateLayout(); double width = g.ActualWidth; var selected = g.Slots[1]; Enter(selected); Pump(350); g.UpdateLayout();
        double[] positions = [-9, -2, 5, 9];
        for (int i = 0; i < 4; i++) { var transform = (TranslateTransform)Part<FrameworkElement>(g.Slots[i], "PART_Lift").RenderTransform; Assert.AreEqual(positions[i], transform.X, .05); Assert.AreEqual(i == 1 ? -2d : 0d, transform.Y, .05); }
        Assert.AreEqual(width, g.ActualWidth, .1); var tip = Part<Border>(selected, "PART_Tip"); Assert.AreEqual(1d, tip.Opacity, .001); Assert.AreEqual(-tip.ActualHeight - 8, Canvas.GetTop(tip), .1);
        Assert.AreEqual((36 - tip.ActualWidth) / 2, Canvas.GetLeft(tip), .1); Assert.AreEqual(((SolidColorBrush)g.FindResource("ArcWpf.Brush.BorderStrong")).Color, ((SolidColorBrush)Part<Border>(selected, "PART_Ring").BorderBrush).Color);
        var lift = Part<FrameworkElement>(selected, "PART_Lift"); var p = lift.TranslatePoint(new Point(18, 18), g); Assert.IsNotNull(g.InputHitTest(p));
        Leave(g); Pump(350); Assert.AreEqual(0d, tip.Opacity, .001); Assert.AreEqual(width, g.ActualWidth, .1); Assert.IsFalse(g.IsMotionRunning);
        ArcMotion.SetLevel(g, ArcMotionLevel.Reduced); Enter(selected); Pump(220); Assert.AreEqual(0d, ((TranslateTransform)lift.RenderTransform).X); Assert.AreEqual(0d, ((TranslateTransform)lift.RenderTransform).Y); Assert.AreEqual(1d, tip.Opacity); Assert.IsFalse(g.IsMotionRunning);
    }));

    [TestMethod] public void UnloadTemplateModeAndThemeChangesLeaveNoOwnedClocks() => Sta.Run(() => WithGroup((g, members) =>
    {
        Enter(g.Slots[0]); members.RemoveAt(0); Pump(40); g.Visibility = Visibility.Hidden; Assert.IsFalse(g.IsMotionRunning); Assert.IsFalse(g.Slots.Any(s => s.IsExiting));
        g.Visibility = Visibility.Visible; var template = g.Template; g.Template = new ControlTemplate(typeof(AvatarGroup)); g.ApplyTemplate(); members.Add(new AvatarGroupMember { DisplayName = "During missing template" }); g.Template = template; g.ApplyTemplate(); Pump(50);
        Assert.AreEqual(1, g.OverflowCount); Assert.HasCount(5, g.Slots); var root = (Grid)VisualTreeHelper.GetParent(g); root.Children.Remove(g); Pump(40); members.Clear(); Assert.IsFalse(g.IsLoaded); root.Children.Add(g); Pump(80); Assert.HasCount(0, g.Slots);
        members.Add(new AvatarGroupMember { DisplayName = "Return" }); Pump(30); ArcMotion.SetLevel(g, ArcMotionLevel.Off); Assert.IsFalse(g.IsMotionRunning);
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark; Pump(30);
        var ring = Part<Border>(g.Slots[0], "PART_Ring"); Assert.AreEqual(((SolidColorBrush)g.FindResource("ArcWpf.Brush.Border")).Color, ((SolidColorBrush)ring.BorderBrush).Color);
    }));

    [TestMethod] public void OnlyVisibleMembersMaterializeAndDefaultsAreIndependent() => Sta.Run(() => WithGroup((g, _) =>
    {
        ArcMotion.SetLevel(g, ArcMotionLevel.Off); g.Members = Enumerable.Range(0, 1000).Select(i => new AvatarGroupMember { DisplayName = $"Member {i}" }).ToArray(); g.Max = 4; g.UpdateLayout();
        Assert.HasCount(5, g.Slots); Assert.AreEqual(996, g.OverflowCount); var first = new AvatarGroup(); var second = new AvatarGroup(); Assert.AreNotSame(first.Members, second.Members);
        ((ObservableCollection<AvatarGroupMember>)first.Members!).Add(new AvatarGroupMember()); Assert.HasCount(0, second.Members!);
    }));

    [TestMethod] public void GalleryRendersLightDarkAndHover() => Sta.Run(() =>
    {
        var page = new AvatarGroupPage { Width = 850, Height = 1120 }; page.SetResourceReference(Control.BackgroundProperty, "ArcWpf.Brush.Background"); ArcMotion.SetLevel(page, ArcMotionLevel.Off);
        using var source = Host(page, 850, 1120); Pump(100); Save(page, "gallery-light.png"); var g = (AvatarGroup)page.FindName("Reference"); Assert.AreEqual(2, g.OverflowCount); Assert.HasCount(4, g.Slots);
        ArcMotion.SetLevel(page, ArcMotionLevel.Full); Enter(g.Slots[1]); Pump(350); Save(page, "gallery-hover-light.png");
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark; Pump(250); Save(page, "gallery-hover-dark.png");
        ((ArcWpf.Controls.Button)page.FindName("AddButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Assert.AreEqual(2, ((AvatarGroup)page.FindName("Editable")).OverflowCount);
    });

    private static T Part<T>(AvatarGroupItem item, string name) where T : class => (T)item.Template.FindName(name, item);
    private static void Enter(UIElement item) => item.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
    private static void Leave(UIElement item) => item.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
    private static void WithGroup(Action<AvatarGroup, ObservableCollection<AvatarGroupMember>> action)
    {
        var members = new ObservableCollection<AvatarGroupMember> { new() { DisplayName = "Maya Chen", Status = AvatarStatus.Online }, new() { DisplayName = "Leo Park" }, new() { DisplayName = "Sara Ruiz" }, new() { DisplayName = "Tom Hale" }, new() { DisplayName = "Ines Ma" } };
        var group = new AvatarGroup { Members = members, Margin = new Thickness(40), VerticalAlignment = VerticalAlignment.Top }; var root = new Grid { Width = 700, Height = 240 }; root.Children.Add(group);
        using (var source = Host(root, 700, 240)) { Pump(100); action(group, members); } Pump(30); Assert.IsFalse(group.IsMotionRunning);
    }
    private static HwndSource Host(FrameworkElement root, int width, int height)
    {
        var source = new HwndSource(new HwndSourceParameters("AvatarGroup offscreen tests") { Width = width, Height = height, WindowStyle = unchecked((int)0x80000000) });
        root.Resources.MergedDictionaries.Add(Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single()); source.RootVisual = root; root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); return source;
    }
    private static void Pump(int ms) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) }; timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    private static void Save(FrameworkElement element, string name)
    {
        element.UpdateLayout(); var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen()) dc.DrawRectangle(new VisualBrush(element), null, new Rect(element.RenderSize)); var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "ArcWpf.slnx"))) root = root.Parent;
        var dir = System.IO.Path.Combine(root!.FullName, "artifacts", "avatar-group"); System.IO.Directory.CreateDirectory(dir); using var file = System.IO.File.Create(System.IO.Path.Combine(dir, name)); encoder.Save(file);
    }
    private sealed class Model : INotifyPropertyChanged { private int _limit; public int Limit { get => _limit; set { _limit = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Limit))); } } public event PropertyChangedEventHandler? PropertyChanged; }
}
