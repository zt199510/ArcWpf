using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ArcButton = ArcWpf.Controls.Button;

namespace ArcWpf.Tests;

// Directly exercise the transition engine without displaying a window.
// These tests do not claim to verify actual pointer input, rendering or loaded-event timing.
[TestClass]
public sealed class ButtonTransitionTests
{
    private static void Invoke(ArcButton button, string name, params object?[] arguments) =>
        typeof(ArcButton).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, arguments);

    private static ArcButton Create()
    {
        var button = new ArcButton { Content = "A longer replacement label", HorizontalAlignment = HorizontalAlignment.Left };
        button.Measure(new Size(500, 100));
        button.Arrange(new Rect(button.DesiredSize));
        button.ApplyTemplate();
        button.UpdateLayout();
        return button;
    }

    [TestMethod]
    public void OffCancelsWidthAndEffectsWithoutDestroyingContentBinding() => Sta.Run(() =>
    {
        var button = Create();
        button.SetBinding(ContentControl.ContentProperty, new Binding { Source = "Bound label" });
        var expression = BindingOperations.GetBindingExpression(button, ContentControl.ContentProperty);
        Invoke(button, "BeginContentTransition", 25d, null, false);
        var slot = (Border)button.Template.FindName("PART_LabelSlot", button);
        Assert.IsTrue(slot.HasAnimatedProperties);
        Assert.IsNotNull(slot.Effect);
        ArcMotion.SetLevel(button, ArcMotionLevel.Off);
        Assert.IsFalse(slot.HasAnimatedProperties);
        Assert.IsTrue(double.IsNaN(slot.Width));
        Assert.AreEqual(1d, slot.Opacity);
        Assert.IsNull(slot.Effect);
        Assert.IsTrue(slot.RenderTransform.Value.IsIdentity);
        Assert.AreSame(expression, BindingOperations.GetBindingExpression(button, ContentControl.ContentProperty));
    });

    [TestMethod]
    public void ReducedFadesWithoutWidthBlurOrSpatialMovement() => Sta.Run(() =>
    {
        var button = Create();
        ArcMotion.SetLevel(button, ArcMotionLevel.Reduced);
        Invoke(button, "BeginContentTransition", 20d, null, false);
        var slot = (Border)button.Template.FindName("PART_LabelSlot", button);
        Assert.IsTrue(double.IsNaN(slot.Width));
        Assert.IsNull(slot.Effect);
        Assert.IsTrue(slot.RenderTransform.Value.IsIdentity);
        Assert.IsTrue(slot.HasAnimatedProperties);
        button.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.IsFalse(slot.HasAnimatedProperties);
    });

    [TestMethod]
    public void RepeatedTransitionsAndTemplateReplacementClearOldParts() => Sta.Run(() =>
    {
        var button = Create();
        var slot = (Border)button.Template.FindName("PART_LabelSlot", button);
        for (int i = 0; i < 10; i++)
        {
            Invoke(button, "StopContentTransition");
            Invoke(button, "BeginContentTransition", 20d + i, null, i % 2 == 0);
        }
        var transform = (TransformGroup)slot.RenderTransform;
        var scale = transform.Children[0];
        Assert.IsTrue(scale.HasAnimatedProperties);
        button.Template = new ControlTemplate(typeof(ArcButton));
        button.ApplyTemplate();
        Assert.IsFalse(slot.HasAnimatedProperties);
        Assert.IsFalse(scale.HasAnimatedProperties);
        Assert.IsNull(slot.Effect);
        Assert.IsTrue(double.IsNaN(slot.Width));
    });

    [TestMethod]
    public void FixedWidthBindingSurvivesContentTransition() => Sta.Run(() =>
    {
        var button = Create();
        button.SetBinding(FrameworkElement.WidthProperty, new Binding { Source = 240d });
        var expression = BindingOperations.GetBindingExpression(button, FrameworkElement.WidthProperty);
        Invoke(button, "BeginContentTransition", 25d, null, false);
        var slot = (Border)button.Template.FindName("PART_LabelSlot", button);
        Assert.IsTrue(double.IsNaN(slot.Width));
        Assert.AreEqual(240d, button.Width);
        Invoke(button, "StopTransitions");
        Assert.AreSame(expression, BindingOperations.GetBindingExpression(button, FrameworkElement.WidthProperty));
    });

    [TestMethod]
    public void BrushTransitionsCloneResourcesAndOffRestoresCurrentPalette() => Sta.Run(() =>
    {
        var button = Create();
        var border = (Border)button.Template.FindName("PART_PressTarget", button);
        border.SetCurrentValue(Border.BackgroundProperty, Brushes.Red);
        button.Background = Brushes.Blue;
        // Unloaded controls update immediately. Seed the previous rendered colour for engine verification.
        border.SetCurrentValue(Border.BackgroundProperty, Brushes.Red);
        Invoke(button, "ApplyPalette", true);
        var animated = (SolidColorBrush)border.Background;
        Assert.AreNotSame(Brushes.Blue, animated);
        Assert.IsTrue(animated.HasAnimatedProperties);
        Assert.IsFalse(Brushes.Blue.HasAnimatedProperties);
        ArcMotion.SetLevel(button, ArcMotionLevel.Off);
        Assert.IsFalse(animated.HasAnimatedProperties);
        Assert.AreSame(Brushes.Blue, border.Background);
    });
}
