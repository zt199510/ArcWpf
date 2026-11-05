using System.Runtime.CompilerServices;
using ArcWpf.Controls;

namespace ArcWpf.AiAssistant.Backend;

/// <summary>Scripted replies with no model and no network. It only pauses and emits canned text, so nothing it reports has really happened.</summary>
public sealed class SimulatedBackend : IAssistantBackend
{
    /// <summary>Scales every pause. Zero runs a script without waiting.</summary>
    public double Pace { get; init; } = 1;

    /// <summary>Why the real backend did not start, shown instead of the usual hint.</summary>
    public string? Note { get; init; }

    public AssistantProfile Profile => _profile with { BadgeHint = Note ?? _profile.BadgeHint, Welcome = Note is null ? _profile.Welcome : $"This assistant is simulated. {Note}" };

    private readonly AssistantProfile _profile = new()
    {
        IsSimulated = true,
        Badge = "Simulated",
        BadgeHint = "Replies are scripted: no model and no network",
        Welcome = "This assistant is simulated. Replies are scripted, with no model and no network.",
        Providers = SampleData.Providers,
        Models = SampleData.Models,
        FeaturedModels = SampleData.Featured,
        Commands = SampleData.Commands,
        Mentions = SampleData.Mentions,
        AttachActions = SampleData.AttachActions,
        Tools = SampleData.Tools(),
        Starters = SampleData.Starters,
        History = SampleData.History,
    };

    public IAssistantSession CreateSession() => new Session(this);

    private sealed class Session(SimulatedBackend owner) : IAssistantSession
    {
        public IAsyncEnumerable<AssistantEvent> RunAsync(AssistantRequest request, CancellationToken cancellation)
        {
            string text = request.Text.ToLowerInvariant();
            if (text.Contains("webhook")) { return owner.Webhook(request, cancellation); }
            if (text.Contains("migration")) { return owner.Migrations(request, cancellation); }
            if (text.Contains("connection drop")) { return owner.Dropped(request, cancellation); }
            return owner.Plain(request, cancellation);
        }
    }

    private async IAsyncEnumerable<AssistantEvent> Webhook(AssistantRequest request, [EnumeratorCancellation] CancellationToken cancellation)
    {
        await Wait(350, cancellation);
        yield return new AssistantEvent.Progress(0.1);
        await foreach (var delta in Words(SampleData.WebhookReasoning, cancellation, 16)) { yield return new AssistantEvent.Reasoning(delta); }
        yield return new AssistantEvent.ReasoningDone(4);

        var search = SampleData.SearchCall;
        yield return new AssistantEvent.Tool(search with { Status = ToolCallStatus.Running, Output = null, DurationMs = null });
        await Wait(900, cancellation);
        yield return new AssistantEvent.Tool(search);
        yield return new AssistantEvent.Progress(0.3);

        var read = SampleData.ReadCall;
        yield return new AssistantEvent.Tool(read with { Status = ToolCallStatus.Running, Output = null, DurationMs = null });
        await Wait(700, cancellation);
        yield return new AssistantEvent.Tool(read);
        yield return new AssistantEvent.Progress(0.45);

        await Wait(300, cancellation);
        var answer = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        yield return new AssistantEvent.ApprovalNeeded(SampleData.PatchRequest(request.ModelName, request.ModelMark), answer);
        var decision = await answer.Task.WaitAsync(cancellation);

        if (decision.Outcome == ApprovalOutcome.Denied)
        {
            await foreach (var delta in Words(SampleData.DeniedAnswer, cancellation)) { yield return new AssistantEvent.Text(delta); }
            yield break;
        }

        yield return new AssistantEvent.Progress(0.55);
        var apply = SampleData.ApplyCall(decision.Arguments["path"]);
        yield return new AssistantEvent.Tool(apply with { Status = ToolCallStatus.Running, Output = null, DurationMs = null });
        await Wait(1100, cancellation);
        yield return new AssistantEvent.Tool(apply);
        yield return new AssistantEvent.Progress(0.7);
        foreach (var item in SampleData.PatchArtifacts) { yield return new AssistantEvent.Artifact(item); }
        yield return new AssistantEvent.Sources(SampleData.WebhookSources);
        await foreach (var delta in Words(SampleData.WebhookAnswer, cancellation)) { yield return new AssistantEvent.Text(delta); }
    }

    private async IAsyncEnumerable<AssistantEvent> Migrations(AssistantRequest request, [EnumeratorCancellation] CancellationToken cancellation)
    {
        await Wait(300, cancellation);
        var call = SampleData.MigrationCall;
        yield return new AssistantEvent.Tool(call with { Status = ToolCallStatus.Running });
        await Wait(1000, cancellation);
        // The first attempt times out so that Retry has something to recover from.
        if (request.Attempt == 0)
        {
            yield return new AssistantEvent.Tool(call with { Status = ToolCallStatus.Error, Message = "Connection to app_production timed out after 5s" });
            await foreach (var delta in Words(SampleData.MigrationFailed, cancellation)) { yield return new AssistantEvent.Text(delta); }
            yield break;
        }
        yield return new AssistantEvent.Tool(call with { Status = ToolCallStatus.Success, Output = SampleData.MigrationOutput, DurationMs = 880 });
        yield return new AssistantEvent.Artifact(SampleData.MigrationArtifact);
        await foreach (var delta in Words(SampleData.MigrationAnswer, cancellation)) { yield return new AssistantEvent.Text(delta); }
    }

    private async IAsyncEnumerable<AssistantEvent> Dropped(AssistantRequest request, [EnumeratorCancellation] CancellationToken cancellation)
    {
        await Wait(500, cancellation);
        // The first attempt breaks off so that Retry has something to recover from.
        if (request.Attempt == 0)
        {
            await foreach (var delta in Words(SampleData.DroppedStart, cancellation)) { yield return new AssistantEvent.Text(delta); }
            throw new AssistantException("The connection dropped. This is a simulated failure.");
        }
        await foreach (var delta in Words(SampleData.DroppedAnswer, cancellation)) { yield return new AssistantEvent.Text(delta); }
    }

    private async IAsyncEnumerable<AssistantEvent> Plain(AssistantRequest request, [EnumeratorCancellation] CancellationToken cancellation)
    {
        await Wait(650, cancellation);
        if (request.Tools.Contains("web")) { yield return new AssistantEvent.Sources(SampleData.WebhookSources); }
        await foreach (var delta in Words(SampleData.PlainAnswer(request), cancellation)) { yield return new AssistantEvent.Text(delta); }
    }

    private async IAsyncEnumerable<string> Words(string text, [EnumeratorCancellation] CancellationToken cancellation, double pause = 24)
    {
        int at = 0;
        while (at < text.Length)
        {
            int end = text.IndexOf(' ', at + 1);
            end = end < 0 ? text.Length : end;
            await Wait(pause, cancellation);
            yield return text[at..end];
            at = end;
        }
    }

    private async Task Wait(double milliseconds, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (Pace > 0) { await Task.Delay(TimeSpan.FromMilliseconds(milliseconds * Pace), cancellation); }
    }
}
