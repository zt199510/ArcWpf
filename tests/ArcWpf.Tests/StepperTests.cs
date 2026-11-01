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
    private static System.Windows.Controls.Button? FindButton(DependencyObject root)
    {
        if (root is System.Windows.Controls.Button button) return button;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindButton(System.Windows.Media.VisualTreeHelper.GetChild(root, i)) is { } child) return child;
        return null;
    }
}
