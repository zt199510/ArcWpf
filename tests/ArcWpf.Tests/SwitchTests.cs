using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ArcSwitch = ArcWpf.Controls.Switch;

namespace ArcWpf.Tests;

[TestClass]
public sealed class SwitchTests
{
    [TestMethod]
    public void TwoStateCoercionKeepsNullableSourceAndBinding() => Sta.Run(() =>
    {
        var model = new Model();
        var control = new Probe { DataContext = model, IsThreeState = true };
        control.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(Model.Value)));
        var expression = BindingOperations.GetBindingExpression(control, ToggleButton.IsCheckedProperty);
        Assert.IsFalse(control.IsChecked);
        Assert.IsNull(model.Value);
        Assert.IsFalse(control.IsThreeState);
        control.Activate();
        Assert.IsTrue(model.Value);
        Assert.AreSame(expression, BindingOperations.GetBindingExpression(control, ToggleButton.IsCheckedProperty));
        Assert.AreSame(model, control.DataContext);
    });

    [TestMethod]
    public void NativeActivationOrdersStateClickAndCommandOnce() => Sta.Run(() =>
    {
        var sequence = new List<string>();
        var control = new Probe { Command = new Command(() => sequence.Add("command")) };
        control.Checked += (_, _) => { Assert.IsTrue(control.IsChecked); sequence.Add("checked"); };
        control.Click += (_, _) => sequence.Add("click");
        control.Activate();
        CollectionAssert.AreEqual(new[] { "checked", "click", "command" }, sequence);
        sequence.Clear();
        control.IsChecked = false;
        Assert.IsEmpty(sequence);
    });

    [TestMethod]
    public void DisabledBlocksActivationButAllowsProgrammaticState() => Sta.Run(() =>
    {
        int calls = 0;
        var control = new Probe { Command = new Command(() => calls++), IsEnabled = false };
        control.Activate();
        Assert.IsFalse(control.IsChecked);
        control.IsChecked = true;
        Assert.IsTrue(control.IsChecked);
        Assert.AreEqual(0, calls);
    });

    [TestMethod]
    public void CommandCanExecuteDisablesNativeAndAutomationActivation() => Sta.Run(() =>
    {
        int calls = 0;
        var command = new Command(() => calls++) { Enabled = false };
        var control = new Probe { Command = command };
        Assert.IsFalse(control.IsEnabled);
        control.Activate();
        Assert.IsFalse(control.IsChecked);
        var peer = UIElementAutomationPeer.CreatePeerForElement(control)!;
        var toggle = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);
        Assert.ThrowsExactly<ElementNotEnabledException>(() => toggle.Toggle());
        Assert.AreEqual(0, calls);
    });

    [TestMethod]
    public void AutomationToggleUsesCommandAndReportsNameAndState() => Sta.Run(() =>
    {
        int calls = 0;
        var control = new Probe { Content = "Notifications", Command = new Command(() => calls++) };
        AutomationProperties.SetName(control, "通知");
        var peer = UIElementAutomationPeer.CreatePeerForElement(control)!;
        var toggle = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);
        Assert.AreEqual("通知", peer.GetName());
        Assert.AreEqual("开关", peer.GetLocalizedControlType());
        toggle.Toggle();
        Assert.AreEqual(ToggleState.On, toggle.ToggleState);
        Assert.AreEqual(1, calls);
        control.IsEnabled = false;
        Assert.ThrowsExactly<ElementNotEnabledException>(() => toggle.Toggle());
    });

    [TestMethod]
    public void DefaultTemplateUsesSpecifiedGeometryAndInitialCheckedPosition() => Sta.Run(() =>
    {
        var control = new ArcSwitch { IsChecked = true };
        Layout(control);
        Assert.IsNotNull(control.Template);
        var track = (FrameworkElement)control.Template.FindName("PART_Track", control);
        var thumb = Thumb(control);
        Assert.AreEqual(42d, track.Width);
        Assert.AreEqual(24d, track.Height);
        Assert.AreEqual(18d, thumb.Width);
        Assert.AreEqual(18d, Position(thumb));
        Assert.IsFalse(control.IsMotionRunning);
    });

    [TestMethod]
    public void TrackRendersCapsuleWithFlatTopRatherThanEllipse() => Sta.Run(() =>
    {
        var control = new ArcSwitch { IsChecked = false };
        Layout(control);
        var track = (Border)control.Template.FindName("PART_Track", control);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(42, 24, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(track);
        var pixels = new byte[42 * 24 * 4];
        bitmap.CopyPixels(pixels, 42 * 4, 0);
        Assert.IsGreaterThan((byte)240, pixels[14 * 4 + 3], "Capsule top edge must be flat away from its semicircular ends.");
        Assert.AreEqual((byte)0, pixels[3], "Outer corner must remain transparent.");
    });

    [TestMethod]
    public void PressStretchOffCancellationAndTemplateCleanup() => Sta.Run(() =>
    {
        var control = new Probe { IsChecked = true };
        Layout(control);
        control.Press(true);
        control.SetMotionTargets(true); // Drive engine without opening a window.
        for (int i = 0; i < 200; i++) { control.AdvanceMotion(.016); }
        var thumb = Thumb(control);
        Assert.AreEqual(23d, thumb.Width);
        Assert.AreEqual(new CornerRadius(9), ((Border)thumb).CornerRadius);
        Assert.AreEqual(13d, Position(thumb));
        Assert.IsFalse(control.IsMotionRunning);
        ArcMotion.SetLevel(control, ArcMotionLevel.Off);
        Assert.AreEqual(18d, thumb.Width);
        Assert.AreEqual(18d, Position(thumb));
        ArcMotion.SetLevel(control, ArcMotionLevel.Full);
        control.SetMotionTargets(true);
        Assert.IsTrue(control.IsMotionRunning);
        control.Template = new ControlTemplate(typeof(ArcSwitch));
        Assert.IsFalse(control.IsMotionRunning);
        Assert.IsTrue(thumb.RenderTransform.Value.IsIdentity);
        control.ApplyTemplate();
        control.Activate();
        Assert.IsFalse(control.IsChecked);
    });

    [TestMethod]
    public void ReentrantCheckedHandlerUsesFinalLogicalState() => Sta.Run(() =>
    {
        var control = new Probe();
        Layout(control);
        control.Checked += (_, _) => control.IsChecked = false;
        control.Activate();
        Assert.IsFalse(control.IsChecked);
        Assert.AreEqual(0d, Position(Thumb(control)));
    });

    [TestMethod]
    public void UnloadCancelsSubscriptionAndRetainsConsumerTransform() => Sta.Run(() =>
    {
        var transform = new TranslateTransform(2, 3);
        var control = new Probe { RenderTransform = transform };
        Layout(control);
        control.Press(true);
        control.SetMotionTargets(true);
        Assert.IsTrue(control.IsMotionRunning);
        control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.IsFalse(control.IsMotionRunning);
        Assert.AreEqual(18d, Thumb(control).Width);
        Assert.AreSame(transform, control.RenderTransform);
    });

    [TestMethod]
    public void SpringReversalPreservesVelocityAndEventuallySettles()
    {
        var spring = new ArcWpf.Controls.ControlSpring(0, .3) { Target = 18 };
        for (int i = 0; i < 8; i++) { spring.Step(.016); }
        double position = spring.Position, velocity = spring.Velocity;
        spring.Target = 0;
        Assert.AreEqual(position, spring.Position);
        Assert.AreEqual(velocity, spring.Velocity);
        for (int i = 0; i < 300; i++) { spring.Step(.016); }
        Assert.AreEqual(0d, spring.Position);
        Assert.AreEqual(0d, spring.Velocity);
    }

    [TestMethod]
    public void SpringCapsLongFramesAndRejectsInvalidElapsedTime()
    {
        var shortFrame = new ArcWpf.Controls.ControlSpring(0, .3) { Target = 18 };
        var longFrame = new ArcWpf.Controls.ControlSpring(0, .3) { Target = 18 };
        shortFrame.Step(.05);
        longFrame.Step(100);
        Assert.AreEqual(shortFrame.Position, longFrame.Position);
        double current = longFrame.Position;
        longFrame.Step(double.NaN);
        longFrame.Step(-1);
        Assert.AreEqual(current, longFrame.Position);
    }

    private static void Layout(FrameworkElement control)
    {
        if (!control.IsInitialized) { control.BeginInit(); control.EndInit(); }
        control.Measure(new Size(500, 100));
        control.Arrange(new Rect(control.DesiredSize));
        control.ApplyTemplate();
        control.UpdateLayout();
    }
    private static FrameworkElement Thumb(ArcSwitch control) => (FrameworkElement)control.Template.FindName("PART_Thumb", control);
    private static double Position(FrameworkElement thumb) => ((TransformGroup)thumb.RenderTransform).Children.OfType<TranslateTransform>().Single().X;
    private sealed class Probe : ArcSwitch
    {
        public void Activate() => OnClick();
        public void Press(bool value) => IsPressed = value;
    }
    private sealed class Model : INotifyPropertyChanged
    {
        private bool? _value;
        public bool? Value { get => _value; set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Command(Action execute) : ICommand
    {
        public bool Enabled { get; set; } = true;
        public bool CanExecute(object? parameter) => Enabled;
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}




