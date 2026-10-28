using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class CopyButtonTests
{
    [TestMethod]
    public void CopiesValueAndTransitionsToCopiedThenResets() => Sta.Run(() => InWindow(button =>
    {
        button.Value = "hello";
        button.ResetAfterMs = 100;
        button.Activate();
        Assert.IsTrue(button.IsCopied);
        Assert.AreEqual("hello", Clipboard.GetText());
        Assert.AreEqual("Copied", button.Content);
        Pump(180);
        Assert.IsFalse(button.IsCopied);
        Assert.AreEqual("Copy", button.Content);
        return Task.CompletedTask;
    }));

    [TestMethod]
    public void TemplateUsesCopyAndCheckPhasesAndKeepsAccessibleLabel() => Sta.Run(() => InWindow(button =>
    {
        button.ApplyTemplate();
        Assert.IsNotNull(button.Template.FindName("PART_CopyPhase", button));
        Assert.IsNotNull(button.Template.FindName("PART_CheckPhase", button));
        Assert.AreEqual("Copy", AutomationProperties.GetName(button));
        button.Value = "value";
        button.Copy();
        Assert.IsTrue(button.IsCopied);
        return Task.CompletedTask;
    }));

    [TestMethod]
    public void CopyCommandAndOffStateAreDeterministic() => Sta.Run(() => InWindow(button =>
    {
        button.Value = "command";
        ArcMotion.SetLevel(button, ArcMotionLevel.Off);
        CopyButton.CopyCommand.Execute(null, button);
        Assert.IsTrue(button.IsCopied);
        Assert.AreEqual("command", Clipboard.GetText());
        return Task.CompletedTask;
    }));

    private static void InWindow(Func<Probe, Task> action)
    {
        var button = new Probe { Width = 180, Height = 44 };
        var window = new Window { Content = button, Width = 260, Height = 120, ShowActivated = false };
        try { window.Show(); Sta.Pump(); action(button).GetAwaiter().GetResult(); }
        finally { window.Close(); Sta.Pump(); }
    }

    private sealed class Probe : CopyButton
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
