using ArcWpf.Controls;

namespace ArcWpf.AiAssistant.Backend;

/// <summary>What the person sent. Attempt counts the regenerates and retries of this turn.</summary>
public sealed record AssistantRequest(string Text, IReadOnlyList<string> Files, string? Model, string? ModelName, AiMark? ModelMark, IReadOnlyList<string> Tools, int Attempt = 0);

/// <summary>One step of a reply. A stream that ends with <see cref="ApprovalNeeded"/> waits for <see cref="IAssistantBackend.ContinueAsync"/>.</summary>
public abstract record AssistantEvent
{
    public sealed record Reasoning(string Delta) : AssistantEvent;
    public sealed record ReasoningDone(double Seconds) : AssistantEvent;
    public sealed record Text(string Delta) : AssistantEvent;
    /// <summary>A tool call started or changed. Calls are matched by Id.</summary>
    public sealed record Tool(ToolCallData Call) : AssistantEvent;
    public sealed record ApprovalNeeded(ApprovalRequest Request) : AssistantEvent;
    public sealed record Sources(IReadOnlyList<CitationSource> Items) : AssistantEvent;
    public sealed record Artifact(ArtifactItem Item) : AssistantEvent;
    public sealed record Progress(double Value) : AssistantEvent;
}

/// <summary>What resumes a paused or failed part of a reply.</summary>
public abstract record AssistantContinuation
{
    public sealed record Approval(ApprovalDecision Decision) : AssistantContinuation;
    public sealed record ToolRetry(ToolCallData Call) : AssistantContinuation;
}

/// <summary>A failed reply. Message is the short reason the thread shows.</summary>
public sealed class AssistantException(string message) : Exception(message);

/// <summary>
/// The model behind the panel. It holds no state between calls, as a model API does: tool approval ends one stream and the answer starts the next.
/// Implement this over a real API to replace <see cref="SimulatedBackend"/>.
/// </summary>
public interface IAssistantBackend
{
    IAsyncEnumerable<AssistantEvent> StreamAsync(AssistantRequest request, CancellationToken cancellation);
    IAsyncEnumerable<AssistantEvent> ContinueAsync(AssistantRequest request, AssistantContinuation continuation, CancellationToken cancellation);
}
