using ArcWpf.Controls;
using Pi.Agent;
using Pi.Ai.Api;
using Pi.Ai.Types;

namespace ArcWpf.AiAssistant.Backend;

/// <summary>
/// Picks the backend from the environment, so that no key is ever kept in the source or in a project file:
/// ANTHROPIC_API_KEY or OPENAI_API_KEY (with an optional OPENAI_BASE_URL for a compatible endpoint), ARCWPF_AI_MODELS as a comma list, the optional
/// ARCWPF_AI_THINKING as a Pi ThinkingLevel name, and ARCWPF_AI_BACKEND=simulated to force the script. With no key the script runs.
/// </summary>
public static class PiConfiguration
{
    private const int MaxTokens = 4096;

    private const string SystemPrompt = "You are the assistant inside a desktop app. Answer briefly. Use your tools when they help, and say plainly when one was denied or failed.";

    public static IAssistantBackend Create(Func<string, string?> read)
    {
        if (string.Equals(read("ARCWPF_AI_BACKEND"), "simulated", StringComparison.OrdinalIgnoreCase)) { return new SimulatedBackend(); }
        return FromKey(read, "anthropic", "ANTHROPIC_API_KEY", "claude-opus-5-5")
            ?? FromKey(read, "openai", "OPENAI_API_KEY", null)
            ?? new SimulatedBackend();
    }

    private static IAssistantBackend? FromKey(Func<string, string?> read, string provider, string keyName, string? defaultModel)
    {
        if (read(keyName) is not { Length: > 0 } key) { return null; }
        bool anthropic = provider == "anthropic";
        var ids = (read("ARCWPF_AI_MODELS") ?? defaultModel ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // A compatible endpoint has no default model worth guessing, so it asks instead of failing on the first message.
        if (ids.Length == 0) { return Misconfigured($"{keyName} is set, but ARCWPF_AI_MODELS is empty. Name at least one model."); }

        string? baseUrl = anthropic ? null : read("OPENAI_BASE_URL");
        string api = anthropic ? "anthropic-messages" : "openai-completions";
        var mark = anthropic ? SampleData.Claude : SampleData.OpenAI;
        StreamFn stream = (model, context, options, cancellation) =>
        {
            var merged = (options ?? new SimpleStreamOptions()) with { ApiKey = key, MaxTokens = options?.MaxTokens ?? MaxTokens, BaseUrl = baseUrl ?? options?.BaseUrl };
            return anthropic ? AnthropicMessages.StreamSimple(model, context, merged, cancellation) : OpenAiCompletions.StreamSimple(model, context, merged, cancellation);
        };
        var thinking = Enum.TryParse<ThinkingLevel>(read("ARCWPF_AI_THINKING"), true, out var level) ? level : ThinkingLevel.Off;

        var profile = new AssistantProfile
        {
            Badge = provider == "anthropic" ? "Anthropic" : baseUrl is null ? "OpenAI" : "OpenAI compatible",
            BadgeHint = "Connected through Pi.NET. Your messages and their files go to this provider.",
            Welcome = "Your messages and the files you attach go to the model provider.",
            Providers = [new PickerProvider(provider, anthropic ? "Anthropic" : "OpenAI", mark)],
            Models = [.. ids.Select(id => new PickerModel(id, id, provider) { Mark = mark })],
            FeaturedModels = ids,
            AttachActions = [new PromptAttachAction("upload", "Upload file", AiGlyphs.Paperclip)],
            Starters = [("Ask what time it is", "What time is it right now?"), ("Save a note, with a tool approval", "Save a note titled Ideas that says: try the approval card.")],
        };
        return new PiAgentBackend(profile, stream, id => new Model(id ?? ids[0], id ?? ids[0], api, provider), DemoTools.Create(), SystemPrompt, thinking);
    }

    // Stays on the script and says why in the badge, rather than opening an app that cannot answer.
    private static SimulatedBackend Misconfigured(string reason) => new() { Note = reason };
}
