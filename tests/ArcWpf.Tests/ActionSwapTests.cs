using ArcWpf.Controls;
using System.Threading.Tasks;
namespace ArcWpf.Tests;
[TestClass] public sealed class ActionSwapTests
{
    [TestMethod] public void ActionSwapCommitsAsyncState() => Sta.Run(() => { var c = new ActionSwap(); c.States.Add(new ActionSwapState { Id = "idle", Label = "Send" }); c.States.Add(new ActionSwapState { Id = "done", Label = "Sent" }); c.Value = "idle"; c.OnAction = _ => Task.CompletedTask; c.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, c)); Assert.AreEqual("done", c.Value); Assert.IsFalse(c.IsWorking); });
    [TestMethod] public void ActionSwapFailureShowsFailureLabel() => Sta.Run(() => { var c = new ActionSwap { FailureLabel = "Failed" }; c.States.Add(new ActionSwapState { Id = "idle", Label = "Send" }); c.States.Add(new ActionSwapState { Id = "done", Label = "Sent" }); c.Value = "idle"; c.OnAction = _ => Task.FromException(new InvalidOperationException()); c.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, c)); Assert.IsFalse(c.IsWorking); Assert.AreEqual("Failed", c.Content); });
}
