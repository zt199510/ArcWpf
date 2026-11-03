using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class ConfirmPopoverTests
{
    private static void With(Action<ConfirmPopover> action, string? phrase = null) => Sta.Run(() =>
    {
        var popover = new ConfirmPopover { Label = "Delete", Title = "Delete Atlas?", Description = "Gone for good.", Icon = "trash-2", ConfirmText = phrase, ConfirmLabel = "Delete project", PendingLabel = "Deleting", SuccessLabel = "Deleted", Align = ConfirmPopoverAlign.End, ResultDuration = TimeSpan.FromMilliseconds(30), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 40, 0, 0) };
        var window = new Window { Content = new AdornerDecorator { Child = popover }, Width = 700, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(popover); }
        finally { window.Close(); }
    });

    private static void Wait(int milliseconds)
    {
        var end = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < end) { Sta.Pump(); }
    }

    [TestMethod] public void PhraseMatchIgnoresCaseAndOuterSpaces()
    {
        Assert.IsTrue(ConfirmPhrase.Matches("  ATLAS ", "atlas"));
        Assert.IsFalse(ConfirmPhrase.Matches("atla", "atlas"));
    }

    [TestMethod] public void ShareCountsTheMatchedStartOnly()
    {
        Assert.AreEqual(.4, ConfirmPhrase.Share("at", "atlas"), 1e-9);
        Assert.AreEqual(0d, ConfirmPhrase.Share("xt", "atlas"));
        Assert.AreEqual(1d, ConfirmPhrase.Share("ATLAS", "atlas"));
    }

    [TestMethod] public void OpeningGrowsTheSurfaceAndKeepsTheEndEdge() => With(popover =>
    {
        popover.IsOpen = true;
        Sta.Pump();
        Assert.IsTrue(popover.HasCardForTest);
        Assert.AreEqual(ConfirmPopoverPhase.Confirming, popover.Phase);
        Assert.AreEqual(320d, popover.CardTargetWidthForTest);
        popover.FinishMotionForTest();
        Sta.Pump();
        double right = popover.LeftForTest + popover.SurfaceForTest!.Width;
        Assert.AreEqual(popover.TriggerForTest!.ActualWidth, right, 1);
    });

    [TestMethod] public void ConfirmIsFullWithoutAPhrase() => With(popover =>
    {
        popover.IsOpen = true;
        Sta.Pump();
        Assert.AreEqual(1d, popover.FillTargetForTest, 1e-9);
    });

    [TestMethod] public void WrongPhraseDoesNotRunTheAction() => With(popover =>
    {
        int runs = 0;
        popover.ConfirmAction = _ => { runs++; return Task.CompletedTask; };
        popover.IsOpen = true;
        Sta.Pump();
        popover.FieldForTest!.Text = "atl";
        Sta.Pump();
        popover.SubmitForTest();
        Sta.Pump();
        Assert.AreEqual(0, runs);
        Assert.AreEqual(ConfirmPopoverPhase.Confirming, popover.Phase);
    }, "atlas");

    [TestMethod] public void CancelFoldsBackAndRaisesCancelled() => With(popover =>
    {
        int cancelled = 0;
        popover.Cancelled += (_, _) => cancelled++;
        popover.IsOpen = true;
        Sta.Pump();
        popover.PressOutsideForTest();
        popover.FinishMotionForTest();
        Sta.Pump();
        Assert.AreEqual(1, cancelled);
        Assert.AreEqual(ConfirmPopoverPhase.Idle, popover.Phase);
        Assert.IsFalse(popover.IsOpen);
        Assert.IsFalse(popover.HasCardForTest);
    });

    [TestMethod] public void SuccessEndsIdleWithDone() => With(popover =>
    {
        int done = 0;
        var phases = new List<ConfirmPopoverPhase>();
        popover.PhaseChanged += (_, e) => phases.Add(e.Phase);
        popover.Done += (_, _) => done++;
        popover.ConfirmAction = context => { context.SetProgress(.5); return Task.CompletedTask; };
        ArcMotion.SetLevel(popover, ArcMotionLevel.Off);
        popover.IsOpen = true;
        Sta.Pump();
        popover.SubmitForTest();
        Wait(200);
        CollectionAssert.AreEqual(new[] { ConfirmPopoverPhase.Confirming, ConfirmPopoverPhase.Working, ConfirmPopoverPhase.Success, ConfirmPopoverPhase.Idle }, phases);
        Assert.AreEqual(1, done);
        Assert.IsFalse(popover.HasCardForTest);
    });

    [TestMethod] public void FailureStaysOpenWithTheMessageAndRetryLabel() => With(popover =>
    {
        ConfirmPopoverErrorEventArgs? failed = null;
        popover.Failed += (_, e) => failed = e;
        popover.ConfirmAction = _ => throw new InvalidOperationException("No response");
        ArcMotion.SetLevel(popover, ArcMotionLevel.Off);
        popover.IsOpen = true;
        Sta.Pump();
        popover.SubmitForTest();
        Wait(60);
        Assert.AreEqual(ConfirmPopoverPhase.Error, popover.Phase);
        Assert.IsTrue(popover.ErrorVisibleForTest);
        Assert.AreEqual("Try again", popover.ConfirmTextForTest);
        Assert.AreEqual("No response", failed?.Error.Message);
    });

    [TestMethod] public void ProgressShowsAPercent() => With(popover =>
    {
        var gate = new TaskCompletionSource();
        popover.ConfirmAction = async context => { context.SetProgress(.33); await gate.Task; };
        ArcMotion.SetLevel(popover, ArcMotionLevel.Off);
        popover.IsOpen = true;
        Sta.Pump();
        popover.SubmitForTest();
        Sta.Pump();
        Assert.AreEqual(ConfirmPopoverPhase.Working, popover.Phase);
        Assert.AreEqual("33%", popover.PercentForTest);
        gate.SetResult();
        Wait(100);
    });
}
