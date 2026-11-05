using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ArcWpf.AiAssistant;
using ArcWpf.AiAssistant.Backend;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class AiAssistantTests
{
    private static readonly AssistantRequest Webhook = new("Fix the webhook", [], "opus-5.5", "Claude Opus 5.5", null, []);

    private static List<AssistantEvent> Collect(IAsyncEnumerable<AssistantEvent> stream)
    {
        var events = new List<AssistantEvent>();
        var task = Task.Run(async () => { await foreach (var item in stream) { events.Add(item); } });
        task.GetAwaiter().GetResult();
        return events;
    }

    private static string Words(IEnumerable<AssistantEvent> events) => string.Concat(events.OfType<AssistantEvent.Text>().Select(item => item.Delta));

    private static ApprovalDecision Decision(ApprovalOutcome outcome) => new(new ApprovalRequest(), outcome, new Dictionary<string, string> { ["path"] = "a.ts" }, false);

    [TestMethod]
    public void WebhookStreamEndsAtTheApprovalWithoutWriting()
    {
        var backend = new SimulatedBackend { Pace = 0 };
        var events = Collect(backend.StreamAsync(Webhook, CancellationToken.None));
        Assert.IsInstanceOfType<AssistantEvent.ApprovalNeeded>(events[^1]);
        Assert.AreEqual("", Words(events));
        Assert.IsFalse(events.OfType<AssistantEvent.Artifact>().Any());
        Assert.IsFalse(events.OfType<AssistantEvent.Tool>().Any(item => item.Call.Id == "apply"));
    }

    [TestMethod]
    public void ApprovingUsesTheEditedPathAndProducesAllFourResultKinds()
    {
        var backend = new SimulatedBackend { Pace = 0 };
        var events = Collect(backend.ContinueAsync(Webhook, new AssistantContinuation.Approval(Decision(ApprovalOutcome.Approved)), CancellationToken.None));
        var apply = events.OfType<AssistantEvent.Tool>().Last().Call;
        Assert.AreEqual(ToolCallStatus.Success, apply.Status);
        StringAssert.Contains(apply.Title, "a.ts");
        CollectionAssert.AreEquivalent(new[] { ArtifactKind.Code, ArtifactKind.Json, ArtifactKind.Markdown, ArtifactKind.File }, events.OfType<AssistantEvent.Artifact>().Select(item => item.Item.Kind).ToArray());
        Assert.AreNotEqual("", Words(events));
    }

    [TestMethod]
    public void DenyingRunsNoToolAndProducesNoResults()
    {
        var backend = new SimulatedBackend { Pace = 0 };
        var events = Collect(backend.ContinueAsync(Webhook, new AssistantContinuation.Approval(Decision(ApprovalOutcome.Denied)), CancellationToken.None));
        Assert.IsFalse(events.OfType<AssistantEvent.Tool>().Any());
        Assert.IsFalse(events.OfType<AssistantEvent.Artifact>().Any());
        Assert.AreNotEqual("", Words(events));
    }

    [TestMethod]
    public void FailedToolRecoversThroughItsRetry()
    {
        var backend = new SimulatedBackend { Pace = 0 };
        var request = Webhook with { Text = "check migrations" };
        var first = Collect(backend.StreamAsync(request, CancellationToken.None));
        var failed = first.OfType<AssistantEvent.Tool>().Last().Call;
        Assert.AreEqual(ToolCallStatus.Error, failed.Status);
        var second = Collect(backend.ContinueAsync(request, new AssistantContinuation.ToolRetry(failed), CancellationToken.None));
        Assert.AreEqual(ToolCallStatus.Success, second.OfType<AssistantEvent.Tool>().Last().Call.Status);
        Assert.AreEqual(1, second.OfType<AssistantEvent.Artifact>().Count());
    }

    [TestMethod]
    public void ConnectionDropFailsOnlyTheFirstAttempt()
    {
        var backend = new SimulatedBackend { Pace = 0 };
        var request = Webhook with { Text = "simulate a connection drop" };
        Assert.ThrowsExactly<AssistantException>(() => Collect(backend.StreamAsync(request, CancellationToken.None)));
        Assert.AreNotEqual("", Words(Collect(backend.StreamAsync(request with { Attempt = 1 }, CancellationToken.None))));
    }

    [TestMethod]
    public void CancellationEndsAScript()
    {
        var backend = new SimulatedBackend { Pace = 0 };
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => Collect(backend.StreamAsync(Webhook, cancel.Token)));
    }

    private sealed record Ui(AssistantPanel Panel, PromptInput Composer, ConversationSidebar Sidebar, StreamingScrollHost Thread, ArtifactPreview Results, AttachmentTray Files);

    private static void With(Action<Ui> action) => Sta.Run(() =>
    {
        var panel = new AssistantPanel { Backend = new SimulatedBackend { Pace = 0 } };
        ArcMotion.SetLevel(panel, ArcMotionLevel.Off);
        var window = new Window { Content = panel, Width = 1240, Height = 800, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            action(new Ui(panel, (PromptInput)panel.FindName("Composer"), (ConversationSidebar)panel.FindName("Sidebar"), (StreamingScrollHost)panel.FindName("Scroller"), (ArtifactPreview)panel.FindName("Results"), (AttachmentTray)panel.FindName("Files")));
        }
        finally { window.Close(); }
    });

    private static void Send(Ui ui, string text)
    {
        ui.Composer.TextBoxForTest.Text = text;
        ui.Composer.KeyForTest(Key.Enter);
        Sta.Pump();
    }

    private static void Attach(Ui ui, string row)
    {
        ui.Composer.PlusForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        ui.Composer.ClickPlusRowForTest(row);
    }

    private static void Until(Func<bool> done)
    {
        var limit = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < limit)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(20) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
        Assert.IsTrue(done(), "Timed out");
    }

    private static T? Last<T>(Ui ui) where T : DependencyObject => Finder<T>(ui.Thread).LastOrDefault();

    private static IEnumerable<T> Finder<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) { yield return match; }
            foreach (var nested in Finder<T>(child)) { yield return nested; }
        }
    }

    [TestMethod]
    public void AChatWaitsAtTheCardAndKeepsTheComposerBusy() => With(ui =>
    {
        Send(ui, "fix the webhook");
        Assert.IsNotNull(Last<ApprovalCard>(ui));
        Assert.AreEqual(ApprovalStatus.Pending, Last<ApprovalCard>(ui)!.Status);
        Assert.IsTrue(ui.Composer.IsGenerating);
        Assert.AreEqual(0, ui.Results.Items.Count);
        Assert.AreEqual("fix the webhook", ui.Sidebar.Conversations[0].Title);
    });

    [TestMethod]
    public void ApprovingFillsTheResultsAndFreesTheComposer() => With(ui =>
    {
        Send(ui, "fix the webhook");
        Last<ApprovalCard>(ui)!.Decide(ApprovalOutcome.Approved);
        Until(() => !ui.Composer.IsGenerating);
        Assert.AreEqual(ApprovalStatus.Approved, Last<ApprovalCard>(ui)!.Status);
        Assert.AreEqual(4, ui.Results.Items.Count);
        Assert.AreEqual(StreamingStatus.Done, Last<StreamingMessage>(ui)!.Status);
        Assert.IsFalse(Last<MessageActions>(ui)!.IsGenerating);
    });

    [TestMethod]
    public void StoppingAtTheCardWithdrawsItAndRequestAgainCanStillFinish() => With(ui =>
    {
        Send(ui, "fix the webhook");
        ui.Composer.SendForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        var card = Last<ApprovalCard>(ui)!;
        Assert.AreEqual(ApprovalStatus.NotSent, card.Status);
        Assert.IsFalse(ui.Composer.IsGenerating);
        card.AgainForTest!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.IsTrue(ui.Composer.IsGenerating);
        Last<ApprovalCard>(ui)!.Decide(ApprovalOutcome.Approved);
        Until(() => !ui.Composer.IsGenerating);
        Assert.AreEqual(4, ui.Results.Items.Count);
    });

    [TestMethod]
    public void StoppingWhileAnAnswerIsSendingRunsNothing() => With(ui =>
    {
        Send(ui, "fix the webhook");
        var card = Last<ApprovalCard>(ui)!;
        card.Decide(ApprovalOutcome.Approved);
        Assert.AreEqual(ApprovalStatus.Sending, card.Status);
        ui.Composer.SendForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Until(() => card.Status == ApprovalStatus.NotSent);
        Assert.AreEqual(0, ui.Results.Items.Count);
        Assert.IsFalse(ui.Composer.IsGenerating);
    });

    [TestMethod]
    public void EachChatKeepsItsOwnThreadDraftResultsAndFiles() => With(ui =>
    {
        Send(ui, "fix the webhook");
        Last<ApprovalCard>(ui)!.Decide(ApprovalOutcome.Approved);
        Until(() => !ui.Composer.IsGenerating);
        string first = ui.Sidebar.SelectedId!;
        ui.Composer.TextBoxForTest.Text = "my draft";
        ui.Sidebar.NewForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.AreNotEqual(first, ui.Sidebar.SelectedId);
        Assert.AreEqual("", ui.Composer.Text);
        Assert.AreEqual(0, ui.Results.Items.Count);
        Assert.IsNull(Last<ApprovalCard>(ui));
        Attach(ui, "Add a sample log");
        Sta.Pump();
        Assert.AreEqual(1, ui.Composer.Attachments.Count);
        Assert.AreEqual(1, ui.Files.Items.Count);

        ui.Sidebar.RowForTest(first).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.AreEqual("my draft", ui.Composer.Text);
        Assert.AreEqual(4, ui.Results.Items.Count);
        Assert.IsNotNull(Last<ApprovalCard>(ui));
        Assert.AreEqual(0, ui.Composer.Attachments.Count);
        Assert.AreEqual(0, ui.Files.Items.Count);
    });

    [TestMethod]
    public void AFileThatFailedDoesNotStayListedAfterSending() => With(ui =>
    {
        Attach(ui, "Add a file that fails once");
        Until(() => ui.Files.Items.Count == 1 && ui.Files.Items[0].Status == PromptAttachmentStatus.Error);
        Send(ui, "hello");
        Assert.AreEqual(0, ui.Files.Items.Count);
    });

    [TestMethod]
    public void ArchivingTheOpenChatMovesToAnother() => With(ui =>
    {
        Send(ui, "hello");
        Until(() => !ui.Composer.IsGenerating);
        var open = ui.Sidebar.Conversations.First(item => item.Id == ui.Sidebar.SelectedId);
        ui.Sidebar.ArchiveForTest(open.Id).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.AreNotEqual(open.Id, ui.Sidebar.SelectedId);
        Assert.IsNotNull(ui.Thread.Content);
    });

    [TestMethod]
    public void ChromeButtonsReadTheWholeThemeOnASwitch() => Sta.Run(() =>
    {
        var theme = new ArcThemeResources();
        var button = new ToolbarButton(8) { Roles = _ => ("Border", "ControlTrack", "Foreground") };
        var window = new Window { Content = button, Width = 200, Height = 100, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(theme);
        try
        {
            window.Show();
            Sta.Pump();
            theme.Mode = ArcThemeMode.Dark;
            Sta.Pump();
            var color = ((System.Windows.Media.SolidColorBrush)theme["ArcWpf.Brush.Border"]).Color;
            Assert.AreEqual(color, ((System.Windows.Media.SolidColorBrush)button.Background).Color);
            Assert.AreEqual(((System.Windows.Media.SolidColorBrush)theme["ArcWpf.Brush.ControlTrack"]).Color, ((System.Windows.Media.SolidColorBrush)button.BorderBrush).Color);
        }
        finally { window.Close(); }
    });
}
