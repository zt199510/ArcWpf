using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ArcWpf;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass]
public sealed class ActionSwapTests
{
    private static ActionSwap Swap(ControlSize size = ControlSize.Small)
    {
        var c = new ActionSwap { Size = size };
        ArcMotion.SetLevel(c, ArcMotionLevel.Off);
        c.States.Add(new ActionSwapState { Id = "idle", Label = "Send", Icon = "M5,12 H19" });
        c.States.Add(new ActionSwapState { Id = "done", Label = "Sent", Icon = "M5,12 H19", Tone = ActionSwapTone.Success });
        return c;
    }
    private static void Shown(FrameworkElement content, Action body)
    {
        var window = new Window { Width = 400, Height = 300, Content = content, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        try { window.Show(); window.UpdateLayout(); Sta.Pump(); body(); }
        finally { window.Close(); }
    }
    private static void Wait(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    [TestMethod]
    public void ActionSwapCommitsAsyncState() => Sta.Run(() =>
    {
        var c = new ActionSwap();
        c.States.Add(new ActionSwapState { Id = "idle", Label = "Send" });
        c.States.Add(new ActionSwapState { Id = "done", Label = "Sent" });
        c.Value = "idle";
        c.OnAction = _ => Task.CompletedTask;
        c.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, c));
        Assert.AreEqual("done", c.Value);
        Assert.IsFalse(c.IsWorking);
    });

    [TestMethod]
    public void ActionSwapFailureShowsFailureLabel() => Sta.Run(() =>
    {
        var c = new ActionSwap { FailureLabel = "Failed" };
        c.States.Add(new ActionSwapState { Id = "idle", Label = "Send" });
        c.States.Add(new ActionSwapState { Id = "done", Label = "Sent" });
        c.Value = "idle";
        c.OnAction = _ => Task.FromException(new InvalidOperationException());
        c.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, c));
        Assert.IsFalse(c.IsWorking);
        Assert.AreEqual("Failed", c.Content);
        Assert.AreEqual(ActionSwapTone.Danger, c.Tone);
        Assert.AreEqual("idle", c.Value, "a failure returns to the current state");
    });

    [TestMethod]
    public void HeightAndPaddingFollowSize() => Sta.Run(() =>
    {
        foreach (var (size, height, inset) in new[] { (ControlSize.Small, 36d, 17d), (ControlSize.Medium, 44d, 21d), (ControlSize.Large, 50d, 25d) })
        {
            var c = Swap(size);
            Shown(c, () =>
            {
                Assert.AreEqual(height, c.ActualHeight);
                var icon = (FrameworkElement)c.Template.FindName("PART_Icon", c);
                Assert.AreEqual(inset, icon.TransformToAncestor(c).Transform(new Point()).X, 0.51, "border 1 + inline padding");
            });
        }
    });

    [TestMethod]
    public void WorkingWithoutPendingLabelFoldsIntoACircle() => Sta.Run(() =>
    {
        var c = Swap();
        var finish = new TaskCompletionSource();
        c.OnAction = _ => finish.Task;
        Shown(c, () =>
        {
            c.Advance();
            Assert.IsTrue(c.IsWorking);
            c.UpdateLayout();
            Assert.AreEqual(36, c.ActualWidth, 0.51, "folded pill is a circle");
            finish.SetResult();
            Wait(600);
            Assert.AreEqual("done", c.Value);
            Assert.IsFalse(c.IsWorking);
        });
    });

    [TestMethod]
    public void WorkingKeepsItsShapeForTheMinimumTime() => Sta.Run(() =>
    {
        var c = Swap();
        var finish = new TaskCompletionSource();
        c.OnAction = _ => finish.Task;
        Shown(c, () =>
        {
            c.Advance();
            finish.SetResult();
            Wait(120);
            Assert.IsTrue(c.IsWorking, "a fast response must not flash");
            Wait(600);
            Assert.IsFalse(c.IsWorking);
        });
    });

    [TestMethod]
    public void HoldMovesOnToTheNextState() => Sta.Run(() =>
    {
        var c = new ActionSwap();
        ArcMotion.SetLevel(c, ArcMotionLevel.Off);
        c.States.Add(new ActionSwapState { Id = "a", Label = "A" });
        c.States.Add(new ActionSwapState { Id = "b", Label = "B", HoldFor = 60 });
        c.States.Add(new ActionSwapState { Id = "c", Label = "C" });
        Shown(c, () =>
        {
            c.Advance();
            Assert.AreEqual("b", c.Value);
            Wait(300);
            Assert.AreEqual("c", c.Value, "the hold advances to the next state, not the first");
        });
    });

    [TestMethod]
    public void FewerThanTwoStatesIsInactive() => Sta.Run(() =>
    {
        var c = new ActionSwap();
        c.States.Add(new ActionSwapState { Id = "only", Label = "Only" });
        Assert.IsFalse(c.IsEnabled);
        c.States.Add(new ActionSwapState { Id = "more", Label = "More" });
        Assert.IsTrue(c.IsEnabled);
    });

    [TestMethod]
    public void NameFollowsTheVisibleLabel() => Sta.Run(() =>
    {
        var c = Swap();
        c.Advance();
        Assert.AreEqual("Sent", AutomationProperties.GetName(c));
    });
}
