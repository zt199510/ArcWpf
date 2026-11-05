using ArcWpf.Controls;

namespace ArcWpf.AiAssistant.Backend;

/// <summary>A file on a question. PastedText is set for a long paste, FilePath for a file on disk, and neither for a sample.</summary>
public sealed record AssistantFile(string Name, string? FilePath = null, string? PastedText = null);

/// <summary>What the person sent. An Attempt above zero runs the turn again and replaces the session's previous turn.</summary>
public sealed record AssistantRequest(string Text, IReadOnlyList<AssistantFile> Files, string? Model, string? ModelName, AiMark? ModelMark, IReadOnlyList<string> Tools, int Attempt = 0);

/// <summary>One step of a reply.</summary>
public abstract record AssistantEvent
{
    public sealed record Reasoning(string Delta) : AssistantEvent;
    public sealed record ReasoningDone(double Seconds) : AssistantEvent;
    public sealed record Text(string Delta) : AssistantEvent;
    /// <summary>A tool call started or changed. Calls are matched by Id.</summary>
    public sealed record Tool(ToolCallData Call) : AssistantEvent;
    /// <summary>The stream pauses until Answer is set. Stopping the run withdraws the question.</summary>
    public sealed record ApprovalNeeded(ApprovalRequest Request, TaskCompletionSource<ApprovalDecision> Answer) : AssistantEvent;
    public sealed record Sources(IReadOnlyList<CitationSource> Items) : AssistantEvent;
    public sealed record Artifact(ArtifactItem Item) : AssistantEvent;
    public sealed record Progress(double Value) : AssistantEvent;
}

/// <summary>A failed reply. Message is the short reason the thread shows.</summary>
public sealed class AssistantException(string message) : Exception(message);

/// <summary>One chat with the model. It remembers the conversation, so the panel holds one per chat.</summary>
public interface IAssistantSession
{
    /// <summary>Runs one turn. Cancelling ends the stream with an OperationCanceledException; a failed reply ends it with an AssistantException.</summary>
    IAsyncEnumerable<AssistantEvent> RunAsync(AssistantRequest request, CancellationToken cancellation);
}

/// <summary>What the panel offers around a backend.</summary>
public sealed record AssistantProfile
{
    public bool IsSimulated { get; init; }
    public string Badge { get; init; } = "";
    public string BadgeHint { get; init; } = "";
    public string Welcome { get; init; } = "";
    public IReadOnlyList<PickerProvider> Providers { get; init; } = [];
    public IReadOnlyList<PickerModel> Models { get; init; } = [];
    public IReadOnlyList<string> FeaturedModels { get; init; } = [];
    public IReadOnlyList<PromptCommand> Commands { get; init; } = [];
    public IReadOnlyList<PromptMentionGroup> Mentions { get; init; } = [];
    public IReadOnlyList<PromptAttachAction> AttachActions { get; init; } = [];
    public IReadOnlyList<PromptTool> Tools { get; init; } = [];
    public IReadOnlyList<(string Label, string Prompt)> Starters { get; init; } = [];
    /// <summary>Earlier chats to show. They are for looking at and do not reach the model.</summary>
    public IReadOnlyList<(string Id, string Title, string Group, string Question, string Answer, bool Archived)> History { get; init; } = [];
}

/// <summary>The model behind the panel. Implement this over a real API to replace <see cref="SimulatedBackend"/>.</summary>
public interface IAssistantBackend
{
    AssistantProfile Profile { get; }
    IAssistantSession CreateSession();
}
