using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class StepperTests
{
    [TestMethod] public void NextAndPreviousMoveCurrentIndex() => Sta.Run(() => { var stepper = new Stepper(); stepper.Steps.Add(new StepperStep { Title = "A" }); stepper.Steps.Add(new StepperStep { Title = "B" }); stepper.Next(); Assert.AreEqual(1, stepper.CurrentIndex); stepper.Previous(); Assert.AreEqual(0, stepper.CurrentIndex); });
    [TestMethod] public void NextStopsAtLastStep() => Sta.Run(() => { var stepper = new Stepper(); stepper.Steps.Add(new StepperStep { Title = "A" }); stepper.Next(); Assert.AreEqual(0, stepper.CurrentIndex); });
    [TestMethod] public void StepsAreMutable() => Sta.Run(() => { var stepper = new Stepper(); stepper.Steps.Add(new StepperStep { Title = "A" }); Assert.HasCount(1, stepper.Steps); });
}
