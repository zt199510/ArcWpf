using ArcWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace ArcWpf.Tests;
[TestClass] public sealed class StepperTests
{
    [TestMethod] public void NextAndPreviousMoveCurrentIndex() => Sta.Run(() => { var stepper = new Stepper(); stepper.Steps.Add(new StepperStep { Title = "A" }); stepper.Steps.Add(new StepperStep { Title = "B" }); stepper.Next(); Assert.AreEqual(1, stepper.CurrentIndex); stepper.Previous(); Assert.AreEqual(0, stepper.CurrentIndex); });
    [TestMethod] public void NextReachesCompleteStateAndStops() => Sta.Run(() => { var stepper = new Stepper(); stepper.Steps.Add(new StepperStep { Title = "A" }); stepper.Next(); Assert.AreEqual(1, stepper.CurrentIndex); stepper.Next(); Assert.AreEqual(1, stepper.CurrentIndex); stepper.Previous(); Assert.AreEqual(0, stepper.CurrentIndex); });
    [TestMethod] public void StepsAreMutable() => Sta.Run(() => { var stepper = new Stepper(); stepper.Steps.Add(new StepperStep { Title = "A" }); Assert.HasCount(1, stepper.Steps); });
    [TestMethod] public void CurrentAndCompletedMarksUpdateInWindow() => Sta.Run(() =>
    {
        var stepper = new Stepper { CurrentIndex = 1 };
        stepper.Steps.Add(new StepperStep { Title = "A" });
        stepper.Steps.Add(new StepperStep { Title = "B" });
        stepper.Steps.Add(new StepperStep { Title = "C" });
        var window = new Window { Content = stepper, Width = 520, Height = 140, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); stepper.UpdateLayout();
            var items = (ItemsControl)stepper.Template.FindName("PART_Items", stepper)!;
            Assert.AreEqual("✓", items.Items[0]!.GetType().GetProperty("Mark")!.GetValue(items.Items[0]));
            Assert.AreEqual("2", items.Items[1]!.GetType().GetProperty("Mark")!.GetValue(items.Items[1]));
            Assert.IsTrue((bool)items.Items[1]!.GetType().GetProperty("IsCurrent")!.GetValue(items.Items[1])!);
            stepper.Next(); stepper.UpdateLayout();
            Assert.AreEqual("✓", items.Items[1]!.GetType().GetProperty("Mark")!.GetValue(items.Items[1]));
        }
        finally { window.Close(); }
    });
    [TestMethod] public void TemplateButtonHonorsClickPolicy() => Sta.Run(() =>
    {
        var stepper = new Stepper { CurrentIndex = 1 };
        stepper.Steps.Add(new StepperStep { Title = "A" });
        stepper.Steps.Add(new StepperStep { Title = "B" });
        stepper.Steps.Add(new StepperStep { Title = "C" });
        var window = new Window { Content = stepper, Width = 380, Height = 130, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var items = (ItemsControl)stepper.Template.FindName("PART_Items", stepper)!;
            var container = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(0)!;
            var button = FindButton(container)!;
            Assert.IsNotNull(button);
            stepper.AllowStepClick = false;
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            Assert.AreEqual(1, stepper.CurrentIndex);
            stepper.AllowStepClick = true;
            stepper.UpdateLayout();
            container = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(2)!;
            var future = FindButton(container)!;
            future.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, future));
            Assert.AreEqual(1, stepper.CurrentIndex);
            container = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(0)!;
            button = FindButton(container)!;
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            Assert.AreEqual(0, stepper.CurrentIndex);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void ErrorChangesRefreshVisibleStepAndCompletionMark() => Sta.Run(() =>
    {
        var stepper = new Stepper();
        var first = new StepperStep { Title = "Account", Description = "Details" };
        stepper.Steps.Add(first);
        var window = new Window { Content = stepper, Width = 380, Height = 130, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var items = (ItemsControl)stepper.Template.FindName("PART_Items", stepper)!;
            first.Error = "Review required";
            Assert.AreEqual("!", items.Items[0]!.GetType().GetProperty("Mark")!.GetValue(items.Items[0]));
            Assert.AreEqual("Review required", items.Items[0]!.GetType().GetProperty("Description")!.GetValue(items.Items[0]));
            first.Error = "";
            stepper.Next();
            Assert.AreEqual("✓", items.Items[0]!.GetType().GetProperty("Mark")!.GetValue(items.Items[0]));
        }
        finally { window.Close(); }
    });
    [TestMethod] public void WindowLayoutSwitchesBetweenHorizontalVerticalAndCompact() => Sta.Run(() =>
    {
        var stepper = new Stepper { Width = 600, CurrentIndex = 1 };
        stepper.Steps.Add(new StepperStep { Title = "Cart" });
        stepper.Steps.Add(new StepperStep { Title = "Shipping", Description = "Address and delivery" });
        stepper.Steps.Add(new StepperStep { Title = "Payment" });
        var window = new Window { Content = stepper, Width = 700, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); stepper.UpdateLayout();
            var items = (ItemsControl)stepper.Template.FindName("PART_Items", stepper)!;
            var first = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(0)!;
            var second = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(1)!;
            double firstX = first.TranslatePoint(new Point(), items).X;
            double secondX = second.TranslatePoint(new Point(), items).X;
            Assert.IsTrue(secondX > firstX + 100, $"Horizontal cells: first={firstX}, second={secondX}, stepper={stepper.ActualWidth}, items={items.ActualWidth}");
            var fill = (Border)items.ItemTemplate.FindName("ConnectorFill", first)!;
            Assert.AreEqual(Visibility.Visible, fill.Visibility);

            stepper.Width = 300; Sta.Pump(); stepper.UpdateLayout();
            Assert.IsTrue(stepper.IsCompact);
            Assert.AreEqual(Visibility.Visible, ((StackPanel)stepper.Template.FindName("Caption", stepper)!).Visibility);
            Assert.AreEqual("Shipping", stepper.CurrentLabel);
            Assert.AreEqual(Visibility.Collapsed, ((StackPanel)items.ItemTemplate.FindName("Copy", first)!).Visibility);

            stepper.Orientation = StepperOrientation.Vertical; Sta.Pump(); stepper.UpdateLayout();
            Assert.IsFalse(stepper.IsCompact);
            first = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(0)!;
            second = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(1)!;
            double firstY = first.TranslatePoint(new Point(), items).Y;
            double secondY = second.TranslatePoint(new Point(), items).Y;
            Assert.IsTrue(secondY > firstY + 40, $"Vertical cells: first={firstY}, second={secondY}");
        }
        finally { window.Close(); }
    });
    [TestMethod] public void KeyboardMovesFocusAcrossCompletedStepsIncludingRtl() => Sta.Run(() =>
    {
        var stepper = new Stepper { CurrentIndex = 3 };
        for (int index = 0; index < 4; index++) stepper.Steps.Add(new StepperStep { Title = $"Step {index + 1}" });
        var window = new Window { Content = stepper, Width = 620, Height = 170, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); stepper.UpdateLayout();
            var items = (ItemsControl)stepper.Template.FindName("PART_Items", stepper)!;
            var first = FindButton((ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(0)!)!;
            var second = FindButton((ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(1)!)!;
            first.Focus();
            var right = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, System.Windows.PresentationSource.FromVisual(first), 0, System.Windows.Input.Key.Right) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            first.RaiseEvent(right);
            Assert.IsTrue(second.IsKeyboardFocused);
            stepper.FlowDirection = FlowDirection.RightToLeft;
            var rtlRight = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, System.Windows.PresentationSource.FromVisual(second), 0, System.Windows.Input.Key.Right) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            second.RaiseEvent(rtlRight);
            Assert.IsTrue(first.IsKeyboardFocused);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void ConnectorMotionReversesAndClearsClock() => Sta.Run(() =>
    {
        var stepper = new Stepper();
        for (int index = 0; index < 3; index++) stepper.Steps.Add(new StepperStep { Title = $"Step {index + 1}" });
        ArcMotion.SetLevel(stepper, ArcMotionLevel.Full);
        var window = new Window { Content = stepper, Width = 620, Height = 180, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); stepper.UpdateLayout();
            var items = (ItemsControl)stepper.Template.FindName("PART_Items", stepper)!;
            var first = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(0)!;
            var second = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(1)!;
            var firstFill = (Border)items.ItemTemplate.FindName("ConnectorFill", first)!;
            var secondFill = (Border)items.ItemTemplate.FindName("ConnectorFill", second)!;
            var firstScale = (System.Windows.Media.ScaleTransform)firstFill.RenderTransform;
            var secondScale = (System.Windows.Media.ScaleTransform)secondFill.RenderTransform;
            Assert.AreEqual(0d, firstScale.ScaleX, .001);
            stepper.CurrentIndex = 2; Sta.Pump();
            Assert.IsTrue(firstScale.HasAnimatedProperties);
            stepper.CurrentIndex = 1; Sta.Pump();
            Assert.AreSame(firstFill, items.ItemTemplate.FindName("ConnectorFill", (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(0)!)!);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual(1d, firstScale.ScaleX, .01);
            Assert.AreEqual(0d, secondScale.ScaleX, .01);
            Assert.IsFalse(firstScale.HasAnimatedProperties);
            Assert.IsFalse(secondScale.HasAnimatedProperties);
            ArcMotion.SetLevel(stepper, ArcMotionLevel.Off);
            stepper.CurrentIndex = 2; Sta.Pump();
            Assert.AreEqual(1d, secondScale.ScaleX, .001);
            Assert.IsFalse(secondScale.HasAnimatedProperties);
            ArcMotion.SetLevel(stepper, ArcMotionLevel.Full);
            stepper.CurrentIndex = 0; Sta.Pump();
            Assert.IsTrue(secondScale.HasAnimatedProperties);
            ArcMotion.SetLevel(stepper, ArcMotionLevel.Off);
            Assert.AreEqual(0d, secondScale.ScaleX, .001);
            Assert.IsFalse(secondScale.HasAnimatedProperties);
            stepper.Orientation = StepperOrientation.Vertical;
            stepper.CurrentIndex = 2; Sta.Pump(); stepper.UpdateLayout();
            second = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(1)!;
            secondFill = (Border)items.ItemTemplate.FindName("ConnectorFill", second)!;
            secondScale = (System.Windows.Media.ScaleTransform)secondFill.RenderTransform;
            Assert.AreEqual(1d, secondScale.ScaleX, .001);
            Assert.AreEqual(1d, secondScale.ScaleY, .001);
        }
        finally { window.Close(); }
    });
    private static System.Windows.Controls.Button? FindButton(DependencyObject root)
    {
        if (root is System.Windows.Controls.Button button) return button;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindButton(System.Windows.Media.VisualTreeHelper.GetChild(root, i)) is { } child) return child;
        return null;
    }
}
