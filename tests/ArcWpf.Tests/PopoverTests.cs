using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass]
public sealed class PopoverTests
{
    [TestMethod]
    public void FourSidesMeasureGapFromSurfaceNotShadow()
    {
        var anchor = new Rect(400, 400, 80, 40);
        var work = new Rect(0, 0, 1000, 1000);
        foreach (var side in Enum.GetValues<PopoverSide>())
        {
            var p = PopoverPositioner.Calculate(anchor, new Size(192, 100), work, side, PopoverAlignment.Start, 6, 10, false);
            Assert.AreEqual(side, p.Side);
            double gap = side switch { PopoverSide.Top => anchor.Top - p.Bounds.Bottom, PopoverSide.Bottom => p.Bounds.Top - anchor.Bottom,
                PopoverSide.Left => anchor.Left - p.Bounds.Right, _ => p.Bounds.Left - anchor.Right };
            Assert.AreEqual(6d, gap, .001);
        }
    }

    [TestMethod]
    public void BottomCollisionFlipsAndClampsToInset()
    {
        var p = PopoverPositioner.Calculate(new Rect(900, 950, 80, 30), new Size(250, 150), new Rect(0, 0, 1000, 1000), PopoverSide.Bottom, PopoverAlignment.Start, 6, 10, false);
        Assert.AreEqual(PopoverSide.Top, p.Side);
        Assert.AreEqual(new Rect(740, 794, 250, 150), p.Bounds);
    }

    [TestMethod]
    public void RtlReversesHorizontalAlignmentOnly()
    {
        var anchor = new Rect(400, 400, 80, 40);
        var work = new Rect(0, 0, 1000, 1000);
        var ltr = PopoverPositioner.Calculate(anchor, new Size(192, 100), work, PopoverSide.Bottom, PopoverAlignment.Start, 6, 10, false);
        var rtl = PopoverPositioner.Calculate(anchor, new Size(192, 100), work, PopoverSide.Bottom, PopoverAlignment.Start, 6, 10, true);
        Assert.AreEqual(400d, ltr.Bounds.Left);
        Assert.AreEqual(480d, rtl.Bounds.Right);
        var left = PopoverPositioner.Calculate(anchor, new Size(192, 100), work, PopoverSide.Left, PopoverAlignment.Start, 6, 10, true);
        Assert.AreEqual(400d, left.Bounds.Top);
    }

    [TestMethod]
    public void NegativeMonitorOriginAndTinyWorkAreaStayContained()
    {
        var work = new Rect(-200, -100, 150, 100);
        var p = PopoverPositioner.Calculate(new Rect(-150, -60, 50, 20), new Size(352, 500), work, PopoverSide.Bottom, PopoverAlignment.End, 6, 10, false);
        Assert.IsTrue(new Rect(-190, -90, 130, 80).Contains(p.Bounds));
        Assert.AreEqual(130d, p.Bounds.Width);
        Assert.IsLessThan(500d, p.Bounds.Height);
    }

    [TestMethod]
    public void NoRoomStillProducesFiniteReachableBounds()
    {
        var p = PopoverPositioner.Calculate(new Rect(0, 0, 100, 100), new Size(200, 200), new Rect(0, 0, 100, 100), PopoverSide.Bottom, PopoverAlignment.Center, 6, 10, false);
        Assert.AreEqual(new Rect(10, 10, 80, 80), p.Bounds);
        var zero = PopoverPositioner.Calculate(new Rect(0, 0, 5, 5), new Size(192, 100), new Rect(0, 0, 5, 5), PopoverSide.Bottom, PopoverAlignment.Start, 6, 10, false);
        Assert.AreEqual(0d, zero.Bounds.Width);
    }

    [TestMethod]
    public void ReopeningInvalidatesOldCompletionWithoutStartingNewHostCycle()
    {
        var state = new PopoverLifecycle();
        Assert.IsTrue(state.BeginOpen());
        int opening = state.Version;
        int closing = state.BeginClose();
        Assert.IsFalse(state.CompleteOpen(opening));
        Assert.IsFalse(state.BeginOpen());
        Assert.IsFalse(state.CompleteClose(closing));
        Assert.IsTrue(state.CompleteOpen(state.Version));
        int final = state.BeginClose();
        Assert.IsTrue(state.CompleteClose(final));
        Assert.IsFalse(state.CompleteClose(final));
        Assert.IsTrue(state.BeginOpen());
    }

    [TestMethod]
    public void DefaultsAndValidationMatchContract() => Sta.Run(() =>
    {
        var p = new Popover();
        Assert.IsFalse(p.IsOpen); Assert.IsFalse(p.IsPresented);
        Assert.AreEqual(PopoverSide.Bottom, p.Side);
        Assert.AreEqual(PopoverAlignment.Start, p.Alignment);
        Assert.AreEqual(6d, p.SideOffset); Assert.AreEqual(10d, p.CollisionPadding);
        Assert.IsTrue(p.CloseOnEscape && p.CloseOnOutsideClick);
        Assert.IsTrue(((FrameworkPropertyMetadata)Popover.IsOpenProperty.GetMetadata(typeof(Popover))).BindsTwoWayByDefault);
        foreach (double value in new[] { -1d, double.NaN, double.PositiveInfinity })
            Assert.ThrowsExactly<ArgumentException>(() => p.SideOffset = value);
        Assert.ThrowsExactly<ArgumentException>(() => p.Side = (PopoverSide)999);
    });

    [TestMethod]
    public void InvalidAnchorResetsRequestPreservesBindingAndEmitsNoEvents() => Sta.Run(() =>
    {
        var p = new Popover(); var model = new OpenModel(); int events = 0;
        p.Opened += (_, _) => events++; p.Closed += (_, _) => events++;
        var binding = p.SetBinding(Popover.IsOpenProperty, new Binding(nameof(OpenModel.Open)) { Source = model });
        p.SetCurrentValue(Popover.IsOpenProperty, true);
        Sta.Pump();
        Assert.IsFalse(p.IsOpen); Assert.IsFalse(p.IsPresented); Assert.IsFalse(model.Open);
        Assert.AreSame(binding, BindingOperations.GetBindingExpression(p, Popover.IsOpenProperty));
        p.Close(); Assert.AreEqual(0, events);
    });

    [TestMethod]
    public void ClosedReasonBubblesWithTypedHandler() => Sta.Run(() =>
    {
        var p = new Popover(); var parent = new Grid(); parent.Children.Add(p);
        PopoverCloseReason? observed = null;
        parent.AddHandler(Popover.ClosedEvent, new EventHandler<PopoverClosedEventArgs>((_, e) => observed = e.Reason));
        p.RaiseEvent(new PopoverClosedEventArgs(Popover.ClosedEvent, p, PopoverCloseReason.Escape));
        Assert.AreEqual(PopoverCloseReason.Escape, observed);
    });

    [TestMethod]
    public void DefaultTemplateReservesNoLayoutAndBridgesDataFlowAndMotion() => Sta.Run(() =>
    {
        var p = Create();
        Assert.AreEqual(new Size(), p.DesiredSize);
        var popup = (Popup)p.Template.FindName("PART_Popup", p);
        var surface = (Border)p.Template.FindName("PART_Surface", p);
        Assert.IsTrue(popup.StaysOpen && popup.AllowsTransparency);
        Assert.AreEqual(PopupAnimation.None, popup.PopupAnimation);
        Assert.AreEqual(new CornerRadius(26), surface.CornerRadius);
        Assert.AreEqual(new Thickness(16), surface.Padding);
        Assert.AreEqual(192d, surface.MinWidth); Assert.AreEqual(352d, surface.MaxWidth);
        Assert.IsInstanceOfType<DropShadowEffect>(surface.Effect);
        var first = new object(); p.DataContext = first; p.FlowDirection = FlowDirection.RightToLeft;
        ArcMotion.SetLevel(p, ArcMotionLevel.Reduced); Sta.Pump();
        var root = (FrameworkElement)popup.Child;
        Assert.AreSame(first, root.DataContext);
        Assert.AreEqual(FlowDirection.RightToLeft, root.FlowDirection);
        Assert.AreEqual(ArcMotionLevel.Reduced, ArcMotion.GetLevel(root));
        var second = new object(); p.DataContext = second; Sta.Pump(); Assert.AreSame(second, root.DataContext);
        Assert.IsFalse(popup.IsOpen);
    });

    [TestMethod]
    public void MissingHostAndTemplateReplacementDoNotCreatePhantomEvents() => Sta.Run(() =>
    {
        var p = Create(); int count = 0; p.Opened += (_, _) => count++; p.Closed += (_, _) => count++;
        p.Template = new ControlTemplate(typeof(Popover)); p.ApplyTemplate();
        p.IsOpen = true; p.Close();
        Assert.IsFalse(p.IsOpen); Assert.IsFalse(p.IsPresented); Assert.AreEqual(0, count);
    });

    [TestMethod]
    public void MotionOffAndTemplateReplacementRemoveAllOwnedClocks() => Sta.Run(() =>
    {
        var p = Create();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        void Call(string method, params object[] args) => typeof(Popover).GetMethod(method, flags)!.Invoke(p, args);
        var cycle = (PopoverLifecycle)typeof(Popover).GetField("_cycle", flags)!.GetValue(p)!;
        var surface = (Border)p.Template.FindName("PART_Surface", p);
        Call("SetOpen", true); cycle.BeginOpen(); Call("PrepareAnimation"); Call("RunAnimation", true, cycle.Version);
        Assert.IsTrue(surface.HasAnimatedProperties);
        ArcMotion.SetLevel(p, ArcMotionLevel.Off);
        Assert.IsFalse(surface.HasAnimatedProperties);
        Assert.AreEqual(1d, surface.Opacity);
        Assert.AreEqual(PopoverPhase.Open, cycle.Phase);
        ArcMotion.SetLevel(p, ArcMotionLevel.Full);
        Call("SetOpen", false); int closing = cycle.BeginClose(); Call("RunAnimation", false, closing);
        Assert.IsTrue(surface.HasAnimatedProperties);
        p.Template = new ControlTemplate(typeof(Popover)); p.ApplyTemplate();
        Assert.IsFalse(surface.HasAnimatedProperties);
        Assert.AreEqual(PopoverPhase.Closed, cycle.Phase);
        Assert.IsFalse(cycle.CompleteClose(closing));
    });

    [TestMethod]
    public void ClosedTemplateResourcesRespondToThemeChange() => Sta.Run(() =>
    {
        var p = Create();
        var theme = new ArcThemeResources(); p.Resources.MergedDictionaries.Add(theme);
        var surface = (Border)p.Template.FindName("PART_Surface", p);
        var light = ((SolidColorBrush)surface.Background).Color;
        theme.Mode = ArcThemeMode.Dark; Sta.Pump();
        Assert.AreNotEqual(light, ((SolidColorBrush)surface.Background).Color);
        Assert.AreEqual(.34, ((DropShadowEffect)surface.Effect).Opacity, .001);
    });

    private static Popover Create()
    {
        var p = new Popover { Content = new TextBlock { Text = "Anyone with the link can view." } };
        p.BeginInit(); p.EndInit(); p.Measure(new Size(600, 400)); p.Arrange(new Rect(0, 0, 600, 400)); p.ApplyTemplate();
        return p;
    }
    public sealed class OpenModel { public bool Open { get; set; } }
}
