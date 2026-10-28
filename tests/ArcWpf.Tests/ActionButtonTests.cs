using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ActionButtonTests
{
    [TestMethod]
    public void AsyncActionTransitionsToSuccessAndResets() => Sta.Run(() => InWindow(async button =>
    {
        var calls = 0;
        button.ResetAfterMs = 150; button.Action = async () => { calls++; await Task.Delay(20); };
        button.Activate();
        Assert.IsTrue(button.IsPending);
        Assert.AreEqual("Saving", button.Content);
        button.Activate();
        Assert.AreEqual(1, calls);
        Pump(80);
        // Source goes straight from pending to success when the task settles.
        Assert.IsFalse(button.IsPending);
        Assert.IsTrue(button.IsSuccess);
        Assert.AreEqual("Saved", button.Content);
        Pump(250);
        Assert.IsFalse(button.IsSuccess);
        Assert.AreEqual("Save", button.Content);
    }));

    [TestMethod]
    public void SuccessStateCanRunAgain() => Sta.Run(() => InWindow(async button =>
    {
        var calls = 0;
        button.ResetAfterMs = 0; button.Action = async () => { calls++; await Task.Delay(10); };
        button.Activate(); Pump(60);
        Assert.IsTrue(button.IsSuccess);
        button.Activate();
        Assert.IsTrue(button.IsPending);
        Assert.IsFalse(button.IsSuccess);
        Pump(60);
        Assert.AreEqual(2, calls);
        await Task.CompletedTask;
    }));

    [TestMethod]
    public void FailureReturnsIdleAndRaisesTypedEvent() => Sta.Run(() => InWindow(async button =>
    {
        ActionButtonFailedEventArgs? failure = null;
        button.ActionFailed += (_, e) => failure = e;
        button.Action = async () => { await Task.Delay(10); throw new InvalidOperationException("no"); };
        button.Activate(); Pump(80);
        Assert.IsFalse(button.IsPending); Assert.IsFalse(button.IsSuccess);
        Assert.AreEqual("Save", button.Content);
        Assert.IsNotNull(failure); Assert.AreEqual("no", failure!.Exception.Message);
    }));

    [TestMethod]
    public void BindingAndResetCoercionArePreserved() => Sta.Run(() =>
    {
        var button = new ActionButton { ResetAfterMs = -1, Label = "Publish" };
        Assert.AreEqual(0, button.ResetAfterMs);
        Assert.AreEqual("Publish", button.Label);
        Assert.AreEqual("Publish", AutomationProperties.GetName(button));
        Assert.IsNotNull(ActionButton.ActionFailedEvent);
    });

    [TestMethod]
    public void IconSlotSwapsArrowSpinnerCheck() => Sta.Run(() => InWindow(button =>
    {
        ArcMotion.SetLevel(button, ArcMotionLevel.Off);
        var completion = new TaskCompletionSource();
        button.Action = () => completion.Task;
        button.ApplyTemplate();
        var arrow = (FrameworkElement)button.Template.FindName("PART_ArrowPhase", button)!;
        var spinner = (FrameworkElement)button.Template.FindName("PART_SpinnerPhase", button)!;
        var check = (FrameworkElement)button.Template.FindName("PART_CheckPhase", button)!;
        Assert.AreEqual(1d, arrow.Opacity); Assert.AreEqual(0d, spinner.Opacity); Assert.AreEqual(0d, check.Opacity);
        button.Activate();
        Assert.AreEqual(0d, arrow.Opacity); Assert.AreEqual(1d, spinner.Opacity); Assert.AreEqual(0d, check.Opacity);
        completion.SetResult();
        Pump(40);
        Assert.AreEqual(0d, arrow.Opacity); Assert.AreEqual(0d, spinner.Opacity); Assert.AreEqual(1d, check.Opacity);
        return Task.CompletedTask;
    }));

    private static void InWindow(Func<Probe, Task> action)
    {
        var button = new Probe { Label = "Save", Width = 180, Height = 44 };
        var window = new Window { Content = button, Width = 260, Height = 120, ShowActivated = false };
        try { window.Show(); Sta.Pump(); action(button).GetAwaiter().GetResult(); }
        finally { window.Close(); Sta.Pump(); }
    }

    private sealed class Probe : ActionButton
    {
        public void Activate() => OnClick();
    }

    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}





