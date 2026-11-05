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

    // Reads a stream to its end and answers each approval from the script.
    private static List<AssistantEvent> Collect(IAsyncEnumerable<AssistantEvent> stream, Func<ApprovalRequest, ApprovalDecision>? answer = null)
    {
        var events = new List<AssistantEvent>();
        Task.Run(async () =>
        {
            await foreach (var item in stream)
            {
                events.Add(item);
                if (item is AssistantEvent.ApprovalNeeded ask && answer is not null) { ask.Answer.TrySetResult(answer(ask.Request)); }
            }
        }).GetAwaiter().GetResult();
        return events;
    }

    private static string Words(IEnumerable<AssistantEvent> events) => string.Concat(events.OfType<AssistantEvent.Text>().Select(item => item.Delta));

    private static ApprovalDecision Decision(ApprovalRequest request, ApprovalOutcome outcome, params (string Key, string Value)[] edits)
    {
        var arguments = request.Arguments.ToDictionary(item => item.Label, item => item.Value);
        foreach (var (key, value) in edits) { arguments[key] = value; }
        return new ApprovalDecision(request, outcome, arguments, edits.Length > 0);
    }

    private static IAssistantSession Simulated() => new SimulatedBackend { Pace = 0 }.CreateSession();

    [TestMethod]
    public void WebhookWaitsForTheAnswerBeforeWriting()
    {
        var events = new List<AssistantEvent>();
        using var stop = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => Task.Run(async () =>
        {
            await foreach (var item in Simulated().RunAsync(Webhook, stop.Token))
            {
                events.Add(item);
                if (item is AssistantEvent.ApprovalNeeded) { stop.Cancel(); }
            }
        }).GetAwaiter().GetResult());
        Assert.AreEqual("", Words(events));
        Assert.IsFalse(events.OfType<AssistantEvent.Artifact>().Any());
        Assert.IsFalse(events.OfType<AssistantEvent.Tool>().Any(item => item.Call.Id == "apply"));
    }

    [TestMethod]
    public void ApprovingUsesTheEditedPathAndProducesAllFourResultKinds()
    {
        var events = Collect(Simulated().RunAsync(Webhook, CancellationToken.None), request => Decision(request, ApprovalOutcome.Approved, ("path", "a.ts")));
        var apply = events.OfType<AssistantEvent.Tool>().Last().Call;
        Assert.AreEqual(ToolCallStatus.Success, apply.Status);
        StringAssert.Contains(apply.Title, "a.ts");
        CollectionAssert.AreEquivalent(new[] { ArtifactKind.Code, ArtifactKind.Json, ArtifactKind.Markdown, ArtifactKind.File }, events.OfType<AssistantEvent.Artifact>().Select(item => item.Item.Kind).ToArray());
        Assert.AreNotEqual("", Words(events));
    }

    [TestMethod]
    public void DenyingRunsNoWriteAndProducesNoResults()
    {
        var events = Collect(Simulated().RunAsync(Webhook, CancellationToken.None), request => Decision(request, ApprovalOutcome.Denied));
        Assert.IsFalse(events.OfType<AssistantEvent.Tool>().Any(item => item.Call.Id == "apply"));
        Assert.IsFalse(events.OfType<AssistantEvent.Artifact>().Any());
        Assert.AreNotEqual("", Words(events));
    }

    [TestMethod]
    public void FailedToolRecoversOnTheNextAttempt()
    {
        var request = Webhook with { Text = "check migrations" };
        var first = Collect(Simulated().RunAsync(request, CancellationToken.None));
        Assert.AreEqual(ToolCallStatus.Error, first.OfType<AssistantEvent.Tool>().Last().Call.Status);
        var second = Collect(Simulated().RunAsync(request with { Attempt = 1 }, CancellationToken.None));
        Assert.AreEqual(ToolCallStatus.Success, second.OfType<AssistantEvent.Tool>().Last().Call.Status);
        Assert.AreEqual(1, second.OfType<AssistantEvent.Artifact>().Count());
    }

    [TestMethod]
    public void ConnectionDropFailsOnlyTheFirstAttempt()
    {
        var request = Webhook with { Text = "simulate a connection drop" };
        Assert.ThrowsExactly<AssistantException>(() => Collect(Simulated().RunAsync(request, CancellationToken.None)));
        Assert.AreNotEqual("", Words(Collect(Simulated().RunAsync(request with { Attempt = 1 }, CancellationToken.None))));
    }

    [TestMethod]
    public void CancellationEndsAScript()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => Collect(Simulated().RunAsync(Webhook, cancel.Token)));
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

    private static void Click(System.Windows.Controls.Primitives.ButtonBase button)
    {
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
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
        Until(() => Last<ApprovalCard>(ui) is not null);
        Assert.AreEqual(ApprovalStatus.Pending, Last<ApprovalCard>(ui)!.Status);
        Assert.IsTrue(ui.Composer.IsGenerating);
        Assert.AreEqual(0, ui.Results.Items.Count);
        Assert.AreEqual("fix the webhook", ui.Sidebar.Conversations[0].Title);
    });

    [TestMethod]
    public void ApprovingFillsTheResultsAndFreesTheComposer() => With(ui =>
    {
        Send(ui, "fix the webhook");
        Until(() => Last<ApprovalCard>(ui) is not null);
        Last<ApprovalCard>(ui)!.Decide(ApprovalOutcome.Approved);
        Until(() => !ui.Composer.IsGenerating);
        Assert.AreEqual(ApprovalStatus.Approved, Last<ApprovalCard>(ui)!.Status);
        Assert.AreEqual(4, ui.Results.Items.Count);
        Assert.AreEqual(StreamingStatus.Done, Last<StreamingMessage>(ui)!.Status);
        Assert.IsFalse(Last<MessageActions>(ui)!.IsGenerating);
    });

    [TestMethod]
    public void StoppingAtTheCardWithdrawsItAndRequestAgainRunsTheTurnAgain() => With(ui =>
    {
        Send(ui, "fix the webhook");
        Until(() => Last<ApprovalCard>(ui) is not null);
        var card = Last<ApprovalCard>(ui)!;
        Click(ui.Composer.SendForTest);
        Until(() => !ui.Composer.IsGenerating);
        Assert.AreEqual(ApprovalStatus.NotSent, card.Status);
        Click(card.AgainForTest!);
        Until(() => Last<ApprovalCard>(ui) is { } next && next != card);
        Assert.IsTrue(ui.Composer.IsGenerating);
        Last<ApprovalCard>(ui)!.Decide(ApprovalOutcome.Approved);
        Until(() => !ui.Composer.IsGenerating);
        Assert.AreEqual(4, ui.Results.Items.Count);
        Assert.AreEqual(1, Finder<ApprovalCard>(ui.Thread).Count());
    });

    [TestMethod]
    public void RegenerateClearsTheOldResults() => With(ui =>
    {
        Send(ui, "fix the webhook");
        Until(() => Last<ApprovalCard>(ui) is not null);
        Last<ApprovalCard>(ui)!.Decide(ApprovalOutcome.Approved);
        Until(() => !ui.Composer.IsGenerating);
        Click(Last<MessageActions>(ui)!.RegenerateForTest);
        Assert.AreEqual(0, ui.Results.Items.Count);
        Until(() => Last<ApprovalCard>(ui) is { Status: ApprovalStatus.Pending });
        Assert.AreEqual(1, Finder<ApprovalCard>(ui.Thread).Count());
    });

    [TestMethod]
    public void AToolThatFailedRunsTheTurnAgainFromItsCard() => With(ui =>
    {
        Send(ui, "check migrations");
        Until(() => !ui.Composer.IsGenerating);
        Assert.AreEqual(ToolCallStatus.Error, Last<ToolCallGroup>(ui)!.Calls![0].Status);
        var retry = Finder<System.Windows.Controls.Primitives.ButtonBase>(Last<ToolCallGroup>(ui)!).First(button => System.Windows.Automation.AutomationProperties.GetName(button).StartsWith("Retry", StringComparison.Ordinal));
        Click(retry);
        Until(() => !ui.Composer.IsGenerating && Last<ToolCallGroup>(ui)?.Calls![0].Status == ToolCallStatus.Success);
        Assert.AreEqual(1, ui.Results.Items.Count);
    });

    [TestMethod]
    public void EachChatKeepsItsOwnThreadDraftResultsAndFiles() => With(ui =>
    {
        Send(ui, "fix the webhook");
        Until(() => Last<ApprovalCard>(ui) is not null);
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
