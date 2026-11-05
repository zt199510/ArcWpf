using System.Text.Json;
using ArcWpf.Controls;

namespace ArcWpf.AiAssistant.Backend;

/// <summary>What a tool hands back: the text the model reads and, optionally, a result for the pane.</summary>
public sealed record AssistantToolResult(string Text, ArtifactItem? Artifact = null);

/// <summary>Something the agent can do. A tool that changes anything sets RequiresApproval, and the person sees a card before it runs.</summary>
public sealed record AssistantTool(string Name, string Label, string Description, IReadOnlyDictionary<string, object?> Parameters, Func<JsonElement, CancellationToken, Task<AssistantToolResult>> Execute)
{
    public bool RequiresApproval { get; init; }
    public ApprovalRisk Risk { get; init; } = ApprovalRisk.Medium;
    public string? ApprovalReason { get; init; }
    public AiMark? Mark { get; init; }
}

/// <summary>Two small tools to prove the path end to end. They touch nothing outside the app: replace them with your own.</summary>
public static class DemoTools
{
    public static IReadOnlyList<AssistantTool> Create() =>
    [
        new("get_current_time", "Read the clock", "Returns the current local date and time of this computer.", Schema(), (_, _) => Task.FromResult(new AssistantToolResult(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz")))),
        new("save_note", "Save a note", "Saves a short note for the person and shows it in the results pane. The person is asked first.", Schema(("title", "A few words naming the note"), ("text", "The note, as Markdown")), (input, _) =>
        {
            string title = input.TryGetProperty("title", out var name) ? name.GetString() ?? "Note" : "Note";
            string text = input.TryGetProperty("text", out var body) ? body.GetString() ?? "" : "";
            return Task.FromResult(new AssistantToolResult($"Saved the note \"{title}\".", new ArtifactItem { Title = title, Kind = ArtifactKind.Markdown, Content = text }));
        })
        {
            RequiresApproval = true,
            Risk = ApprovalRisk.Low,
            ApprovalReason = "Adds a note to the results pane of this chat. Nothing is written to disk.",
        },
    ];

    private static Dictionary<string, object?> Schema(params (string Name, string Description)[] strings) => new()
    {
        ["type"] = "object",
        ["properties"] = strings.ToDictionary(item => item.Name, item => (object?)new Dictionary<string, object?> { ["type"] = "string", ["description"] = item.Description }),
        ["required"] = strings.Select(item => item.Name).ToArray(),
    };
}
