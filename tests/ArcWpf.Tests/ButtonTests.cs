using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ArcButton = ArcWpf.Controls.Button;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ButtonTests
{
    [TestMethod]
    public void DefaultsAndInvalidEnums() => Sta.Run(() =>
    {
        var button = new ProbeButton();
        Assert.AreEqual(Controls.ButtonVariant.Primary, button.Variant);
        Assert.AreEqual(ControlSize.Medium, button.Size);
        Assert.IsFalse(button.IsLoading);
        Assert.IsTrue(button.IsPressAnimationEnabled);
        Assert.ThrowsExactly<ArgumentException>(() => button.Size = (ControlSize)99);
        Assert.ThrowsExactly<ArgumentException>(() => button.Variant = (Controls.ButtonVariant)99);
        Assert.ThrowsExactly<ArgumentException>(() => ArcMotion.SetLevel(button, (ArcMotionLevel)99));
    });

    [TestMethod]
    public void ClickAndCommandExecuteOnceWithOriginalParameter() => Sta.Run(() =>
    {
        var events = new List<string>();
        object parameter = new();
        var command = new TestCommand(p => { Assert.AreSame(parameter, p); events.Add("command"); });
        var button = new ProbeButton { Command = command, CommandParameter = parameter };
        button.Click += (_, _) => events.Add("click");
        button.Activate();
        CollectionAssert.AreEqual(new[] { "click", "command" }, events);
    });

    [TestMethod]
    public void LoadingBlocksActivationWhenMarkedAsDefault() => Sta.Run(() =>
    {
        int calls = 0, clicks = 0;
        var button = new ProbeButton { IsDefault = true, Command = new TestCommand(_ => calls++), IsLoading = true };
        button.Click += (_, _) => clicks++;
        button.Activate();
        Assert.AreEqual(0, calls);
        Assert.AreEqual(0, clicks);
        Assert.IsTrue(button.IsEnabled);
        Assert.IsTrue(button.Focusable);
        button.IsLoading = false;
        button.Activate();
        Assert.AreEqual(1, calls);
        Assert.AreEqual(1, clicks);
    });

    [TestMethod]
    public void DisabledWinsAndCommandCanExecuteIsRespected() => Sta.Run(() =>
    {
        int calls = 0;
        var command = new TestCommand(_ => calls++);
        var button = new ProbeButton { Command = command, IsEnabled = false, IsLoading = true };
        button.Activate();
        Assert.AreEqual(0, calls);
        button.IsEnabled = true;
        Assert.IsTrue(button.IsLoading);
        button.Activate();
        Assert.AreEqual(0, calls);
        button.IsLoading = false;
        command.Enabled = false;
        command.Notify();
        Assert.IsFalse(button.IsEnabled);
        button.Activate();
        Assert.AreEqual(0, calls);
        command.Enabled = true;
        command.Notify();
        button.Activate();
        Assert.AreEqual(1, calls);
    });

    [TestMethod]
    public void NativeAutomationInvokePassesThroughLoadingGate() => Sta.Run(() =>
    {
        int calls = 0;
        var button = new ProbeButton { Content = "Save", Command = new TestCommand(_ => calls++), IsLoading = true };
        var peer = new ButtonAutomationPeer(button);
        var invoke = (IInvokeProvider)peer.GetPattern(PatternInterface.Invoke);
        invoke.Invoke();
        Sta.Pump();
        Assert.AreEqual(0, calls);
        button.IsLoading = false;
        invoke.Invoke();
        Sta.Pump();
        Assert.AreEqual(1, calls);
        button.IsEnabled = false;
        Assert.ThrowsExactly<ElementNotEnabledException>(() => invoke.Invoke());
    });

    [TestMethod]
    public void LoadingBindingAndDataContextSurviveStateChanges() => Sta.Run(() =>
    {
        var model = new LoadingModel();
        var button = new ProbeButton { DataContext = model };
        button.SetBinding(ArcButton.IsLoadingProperty, new Binding(nameof(LoadingModel.Loading)));
        var expression = BindingOperations.GetBindingExpression(button, ArcButton.IsLoadingProperty);
        model.Loading = true;
        Assert.IsTrue(button.IsLoading);
        button.IsEnabled = false;
        button.IsEnabled = true;
        Apply(button);
        button.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        model.Loading = false;
        Assert.IsFalse(button.IsLoading);
        Assert.AreSame(expression, BindingOperations.GetBindingExpression(button, ArcButton.IsLoadingProperty));
        Assert.AreSame(model, button.DataContext);
    });

    [TestMethod]
    public void DefaultStyleIsDiscoveredWithOnlyPublicThemeResources() => Sta.Run(() =>
    {
        var button = new ArcButton { Content = "Standalone" };
        Apply(button);
        Assert.IsNotNull(button.Template);
        Assert.IsNotNull(button.Template.FindName("PART_PressTarget", button));
        Assert.AreEqual(44d, button.MinHeight);
        Assert.IsInstanceOfType<SolidColorBrush>(button.Background);
    });

    [TestMethod]
    public void SizesAndConsumerLocalValuesRespectWpfPrecedence() => Sta.Run(() =>
    {
        var button = new ArcButton { Content = "Size" };
        Apply(button);
        button.Size = ControlSize.Small;
        Assert.AreEqual(36d, button.MinHeight);
        Assert.AreEqual(new Thickness(12, 0, 12, 0), button.Padding);
        button.Size = ControlSize.Large;
        Assert.AreEqual(50d, button.MinHeight);
        button.Padding = new Thickness(30);
        button.Size = ControlSize.Small;
        Assert.AreEqual(new Thickness(30), button.Padding);
    });

    [TestMethod]
    public void LoadingPreservesLabelMeasurementAndDisabledRestoresLabel() => Sta.Run(() =>
    {
        var button = new ArcButton { Content = "A stable width for this label" };
        Apply(button);
        var size = button.DesiredSize;
        button.IsLoading = true;
        Apply(button);
        var label = (ContentPresenter)button.Template.FindName("Label", button);
        var spinner = (FrameworkElement)button.Template.FindName("PART_Spinner", button);
        Assert.AreEqual(size, button.DesiredSize);
        Assert.AreEqual(0d, label.Opacity);
        Assert.AreEqual(Visibility.Visible, spinner.Visibility);
        button.IsEnabled = false;
        Assert.AreEqual(1d, label.Opacity);
        Assert.AreEqual(Visibility.Hidden, spinner.Visibility);
        Assert.IsTrue(button.IsLoading);
    });

    [TestMethod]
    public void DisabledOutlinedButtonsKeepBorderAndMutedSurface() => Sta.Run(() =>
    {
        var button = new ArcButton { Content = "Delete", Variant = Controls.ButtonVariant.Danger, IsEnabled = false };
        Apply(button);
        Assert.AreEqual(1d, ((FrameworkElement)button.Template.FindName("PART_PressTarget", button)).Opacity);
        Assert.AreEqual(((SolidColorBrush)button.FindResource("ArcWpf.Brush.SurfaceMuted")).Color, ((SolidColorBrush)button.Background).Color);
        Assert.AreEqual(((SolidColorBrush)button.FindResource("ArcWpf.Brush.Border")).Color, ((SolidColorBrush)button.BorderBrush).Color);
    });

    [TestMethod]
    public void PrimaryButtonDoesNotPaintAHeavyBlackBorder() => Sta.Run(() =>
    {
        var button = new ArcButton { Content = "Save" };
        Apply(button);
        Assert.AreEqual(Colors.Transparent, ((SolidColorBrush)button.BorderBrush).Color);
        var surface = (Border)button.Template.FindName("PART_PressTarget", button);
        Assert.AreEqual(Colors.Transparent, ((SolidColorBrush)surface.BorderBrush).Color);
    });

    [TestMethod]
    public void ThemeSwitchUpdatesDynamicResourcesAndKeepsApplicationOverrides() => Sta.Run(() =>
    {
        var theme = new ArcThemeResources();
        Application.Current.Resources.MergedDictionaries.Add(theme);
        var button = new ArcButton { Content = "Theme" };
        Apply(button);
        var host = new Window { Content = button };
        var light = ((SolidColorBrush)button.Background).Color;
        theme.Mode = ArcThemeMode.Dark;
        Sta.Pump();
        Assert.AreNotEqual(light, ((SolidColorBrush)button.Background).Color);
        Application.Current.Resources["ArcWpf.Brush.Foreground"] = Brushes.Magenta;
        theme.Mode = ArcThemeMode.Light;
        Sta.Pump();
        Assert.AreSame(Brushes.Magenta, button.Background);
        Assert.AreSame(Brushes.Magenta, Application.Current.Resources["ArcWpf.Brush.Foreground"]);
        host.Close();
    });

    [TestMethod]
    public void NativeContentAndAccessibilityNameArePreserved() => Sta.Run(() =>
    {
        var button = new ArcButton { Content = "_Save" };
        AutomationProperties.SetName(button, "Save changes");
        Apply(button);
        Assert.AreEqual("Save changes", new ButtonAutomationPeer(button).GetName());
        var presenter = (ContentPresenter)button.Template.FindName("Label", button);
        Assert.IsTrue(presenter.RecognizesAccessKey);
        Assert.AreEqual(button.Content, presenter.Content);
    });

    [TestMethod]
    public void ReplacingTemplateReleasesOldTransformsAndAllowsMissingParts() => Sta.Run(() =>
    {
        int calls = 0;
        var button = new ProbeButton { Content = "Template", Command = new TestCommand(_ => calls++) };
        Apply(button);
        var old = (FrameworkElement)button.Template.FindName("PART_PressTarget", button);
        var scale = ((TransformGroup)old.RenderTransform).Children.OfType<ScaleTransform>().Single();
        button.Template = new ControlTemplate(typeof(ArcButton));
        button.ApplyTemplate();
        Assert.IsFalse(scale.HasAnimatedProperties);
        Assert.IsTrue(old.RenderTransform.Value.IsIdentity);
        button.Activate();
        Assert.AreEqual(1, calls);
    });

    [TestMethod]
    public void MotionLevelInheritsAndOffDoesNotReplaceConsumerTransform() => Sta.Run(() =>
    {
        var button = new ProbeButton { Content = "Motion", RenderTransform = new TranslateTransform(3, 4) };
        var consumerTransform = button.RenderTransform;
        var parent = new StackPanel();
        parent.Children.Add(button);
        ArcMotion.SetLevel(parent, ArcMotionLevel.Off);
        Apply(button);
        Assert.AreEqual(ArcMotionLevel.Off, ArcMotion.GetLevel(button));
        button.Press(true);
        var part = (FrameworkElement)button.Template.FindName("PART_PressTarget", button);
        var scale = ((TransformGroup)part.RenderTransform).Children.OfType<ScaleTransform>().Single();
        Assert.AreEqual(1d, scale.ScaleX);
        Assert.IsFalse(scale.HasAnimatedProperties);
        Assert.AreSame(consumerTransform, button.RenderTransform);
    });

    [TestMethod]
    public void UnloadedButtonsDoNotStartSpinnerClocks() => Sta.Run(() =>
    {
        var button = new ArcButton { Content = "Loading", IsLoading = true };
        Apply(button);
        var spinner = (FrameworkElement)button.Template.FindName("PART_Spinner", button);
        var rotation = ((TransformGroup)spinner.RenderTransform).Children.OfType<RotateTransform>().Single();
        Assert.IsFalse(rotation.HasAnimatedProperties);
        button.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.IsFalse(rotation.HasAnimatedProperties);
    });

    [TestMethod]
    public void SampleWindowsLoadXamlWithoutShowingWindows() => Sta.Run(() =>
    {
        var gallery = new Gallery.MainWindow();
        var consumer = new Consumer.MainWindow();
        Assert.IsNotNull(gallery.Content);
        Assert.IsNotNull(consumer.Content);
        Assert.IsFalse(gallery.IsVisible);
        Assert.IsFalse(consumer.IsVisible);
        gallery.Close();
        consumer.Close();
    });

    private static void Apply(FrameworkElement element)
    {
        element.Measure(new Size(600, 200));
        element.Arrange(new Rect(new Point(), element.DesiredSize));
        element.ApplyTemplate();
        element.UpdateLayout();
    }

    private sealed class ProbeButton : ArcButton
    {
        public void Activate() => OnClick();
        public void Press(bool value) => IsPressed = value;
    }

    private sealed class TestCommand(Action<object?> execute) : ICommand
    {
        public bool Enabled { get; set; } = true;
        public bool CanExecute(object? parameter) => Enabled;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged;
        public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class LoadingModel : INotifyPropertyChanged
    {
        private bool _loading;
        public bool Loading { get => _loading; set { _loading = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Loading))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

internal static class Sta
{
    private static readonly Lazy<Dispatcher> Ui = new(() =>
    {
        var ready = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ready.SetResult(app.Dispatcher);
            Dispatcher.Run();
        }) { IsBackground = true, Name = "ArcWpf STA tests" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    });

    public static void Run(Action action) => Ui.Value.Invoke(() =>
    {
        try { Application.Current.Resources.MergedDictionaries.Add(new ArcThemeResources()); action(); }
        finally { Application.Current.Resources.Clear(); Application.Current.Resources.MergedDictionaries.Clear(); }
    });

    public static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}


