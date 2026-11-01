using ArcWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace ArcWpf.Tests;
[TestClass] public sealed class StepperTests
{
    [TestMethod] public void NextAndPreviousMoveCurrentIndex() => Sta.Run(() => { var stepper = new Stepper(); stepper.Steps.Add(new StepperStep { Title = "A" }); stepper.Steps.Add(new StepperStep { Title = "B" }); stepper.Next(); Assert.AreEqual(1, stepper.CurrentIndex); stepper.Previous(); Assert.AreEqual(0, stepper.CurrentIndex); });
    [TestMethod] public void NextStopsAtLastStep() => Sta.Run(() => { var stepper = new Stepper(); stepper.Steps.Add(new StepperStep { Title = "A" }); stepper.Next(); Assert.AreEqual(0, stepper.CurrentIndex); });
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
}
