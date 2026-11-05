using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using ArcWpf.Controls;
using Pi.Agent;
using Pi.Agent.Types;
using Pi.Ai.Types;

namespace ArcWpf.AiAssistant.Backend;

/// <summary>A real agent on Pi.NET's agent loop. Pi's types stay inside this file and PiConfiguration, so the panel does not depend on them.</summary>
public sealed class PiAgentBackend(AssistantProfile profile, StreamFn stream, Func<string?, Model> model, IReadOnlyList<AssistantTool> tools, string systemPrompt, ThinkingLevel thinking = ThinkingLevel.Off) : IAssistantBackend
{
    private const int MaxFileCharacters = 200_000;

    // "Always allow in this workspace" outlives one chat.
    private readonly HashSet<string> _allowedEverywhere = [];

    public AssistantProfile Profile { get; } = profile;

    public IAssistantSession CreateSession() => new Session(this);

    private sealed class Session : IAssistantSession
    {
        private readonly PiAgentBackend _owner;
        private readonly Agent _agent;
        private readonly HashSet<string> _allowedHere = [];
        private readonly Dictionary<string, ToolCallData> _calls = [];
        private readonly HashSet<string> _denied = [];
        private readonly Dictionary<string, JsonElement> _edited = [];
        private ChannelWriter<AssistantEvent>? _out;
        private Task _previous = Task.CompletedTask;
        private DateTime _thinkingSince;
        private int _turnStart;

        public Session(PiAgentBackend owner)
        {
            _owner = owner;
            _agent = new Agent(new AgentOptions
            {
                StreamFn = owner._stream,
                Model = owner._model(null),
                ThinkingLevel = owner._thinking,
                SystemPrompt = owner._systemPrompt,
                Tools = [.. owner._tools.Select(Wrap)],
                BeforeToolCall = AskFirst,
                // Pi 1.4.5 drops BeforeToolCallResult.Terminate on a blocked call, so this is what keeps a stopped reply from going back to the model.
                FinishTurn = (_, cancellation) => Task.FromResult<AgentTurnDecision?>(cancellation.IsCancellationRequested ? new AgentTurnDecision.End() : null),
            });
        }

        public async IAsyncEnumerable<AssistantEvent> RunAsync(AssistantRequest request, [EnumeratorCancellation] CancellationToken cancellation)
        {
            // A stopped run may still be unwinding, and the agent takes one prompt at a time.
            await _previous.ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();

            if (request.Attempt > 0) { _agent.Messages = [.. _agent.Messages.Take(_turnStart)]; }
            _turnStart = _agent.Messages.Count;
            _calls.Clear();
            _denied.Clear();
            _edited.Clear();
            _agent.Model = _owner._model(request.Model);

            // Pi raises its events on pool threads. The channel hands them to whichever thread reads the stream.
            var channel = Channel.CreateUnbounded<AssistantEvent>(new UnboundedChannelOptions { SingleReader = true });
            _out = channel.Writer;
            using var subscription = _agent.Subscribe((item, _) =>
            {
                if (item is AgentEvent.AgentStart && cancellation.IsCancellationRequested) { _agent.Abort(); }
                Translate(item);
                return Task.CompletedTask;
            });
            using var abort = cancellation.Register(_agent.Abort);

            string prompt = Compose(request);
            var run = Task.Run(() => _agent.Prompt(prompt), CancellationToken.None);
            _previous = run.ContinueWith(_ => { }, TaskScheduler.Default);
            _ = run.ContinueWith(done => channel.Writer.TryComplete(done.Exception?.GetBaseException()), TaskScheduler.Default);

            await foreach (var item in channel.Reader.ReadAllAsync(CancellationToken.None)) { yield return item; }

            // Pi reports a failure as the last message's stop reason, not as an exception.
            var last = _agent.Messages.OfType<AssistantMessage>().LastOrDefault();
            if (last?.StopReason is StopReason.Error or StopReason.Aborted || cancellation.IsCancellationRequested) { Repair(); }
            if (last?.StopReason == StopReason.Aborted || cancellation.IsCancellationRequested) { throw new OperationCanceledException(cancellation); }
            if (last?.StopReason == StopReason.Error) { throw new AssistantException(last.ErrorMessage ?? "The model did not answer."); }
        }

        // A run cut off inside a tool round leaves a tool call with no result, which a provider may refuse on the next request.
        private void Repair()
        {
            var messages = _agent.Messages.ToList();
            int keep = messages.FindLastIndex(message => message is UserMessage or AssistantMessage { StopReason: StopReason.Stop });
            _agent.Messages = [.. messages.Take(keep + 1)];
        }

        private async Task<BeforeToolCallResult?> AskFirst(BeforeToolCallContext context, CancellationToken cancellation)
        {
            var tool = _owner._tools.FirstOrDefault(item => item.Name == context.ToolCall.Name);
            if (tool is not { RequiresApproval: true }) { return null; }
            if (_allowedHere.Contains(tool.Name) || _owner._allowedEverywhere.Contains(tool.Name)) { Publish(context.ToolCall.Id); return null; }

            var answer = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            _out?.TryWrite(new AssistantEvent.ApprovalNeeded(Question(tool, context.Args), answer));
            ApprovalDecision decision;
            try { decision = await answer.Task.WaitAsync(cancellation).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                _denied.Add(context.ToolCall.Id);
                return new BeforeToolCallResult(Block: true, Reason: "The person stopped the reply before answering.");
            }

            if (decision.Outcome == ApprovalOutcome.Denied)
            {
                _denied.Add(context.ToolCall.Id);
                return new BeforeToolCallResult(Block: true, Reason: "The person denied this tool call. Do not try it again unless they ask.");
            }
            if (decision.Outcome == ApprovalOutcome.AllowedForAgent) { _allowedHere.Add(tool.Name); }
            if (decision.Outcome == ApprovalOutcome.AllowedForWorkspace) { _owner._allowedEverywhere.Add(tool.Name); }
            if (decision.Edited) { _edited[context.ToolCall.Id] = Merge(context.Args, decision.Arguments); }
            Publish(context.ToolCall.Id);
            return null;
        }

        private ApprovalRequest Question(AssistantTool tool, object? args) => new()
        {
            AgentName = "Assistant",
            ModelName = _agent.Model.Name,
            ToolMark = tool.Mark,
            Title = tool.Label,
            Reason = tool.ApprovalReason ?? tool.Description,
            Risk = tool.Risk,
            Arguments = [.. Fields(args).Select(field => new ApprovalArgument(field.Name, field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString() ?? "" : field.Value.GetRawText())
            {
                IsEditable = field.Value.ValueKind == JsonValueKind.String,
                IsMono = false,
                IsMultiline = field.Value.ValueKind == JsonValueKind.String && (field.Value.GetString() ?? "").Length > 60,
            })],
        };

        private static IEnumerable<JsonProperty> Fields(object? args)
        {
            var json = JsonSerializer.SerializeToElement(args);
            return json.ValueKind == JsonValueKind.Object ? json.EnumerateObject() : [];
        }

        private static JsonElement Merge(object? args, IReadOnlyDictionary<string, string> edits)
        {
            var merged = JsonSerializer.SerializeToNode(args) as JsonObject ?? [];
            foreach (var field in Fields(args))
            {
                if (field.Value.ValueKind == JsonValueKind.String && edits.TryGetValue(field.Name, out var value)) { merged[field.Name] = value; }
            }
            return JsonSerializer.SerializeToElement(merged);
        }

        private AgentTool Wrap(AssistantTool tool) => new(tool.Name, tool.Description, new ToolSchema(tool.Parameters), tool.Label, async (id, args, cancellation, _) =>
        {
            var input = _edited.TryGetValue(id, out var edited) ? edited : JsonSerializer.SerializeToElement(args);
            var result = await tool.Execute(input, cancellation).ConfigureAwait(false);
            return new AgentToolResult([new TextContent(result.Text)], Details: result.Artifact);
        });

        // The loop announces a tool before it asks the hook, so a tool that needs approval stays off the thread until it is allowed.
        private void Translate(AgentEvent item)
        {
            switch (item)
            {
                case AgentEvent.MessageUpdate { AssistantMessageEvent: AssistantMessageEvent.TextDelta text }:
                    _out?.TryWrite(new AssistantEvent.Text(text.Delta));
                    break;
                case AgentEvent.MessageUpdate { AssistantMessageEvent: AssistantMessageEvent.ThinkingStart }:
                    _thinkingSince = DateTime.UtcNow;
                    break;
                case AgentEvent.MessageUpdate { AssistantMessageEvent: AssistantMessageEvent.ThinkingDelta thinking }:
                    _out?.TryWrite(new AssistantEvent.Reasoning(thinking.Delta));
                    break;
                case AgentEvent.MessageUpdate { AssistantMessageEvent: AssistantMessageEvent.ThinkingEnd }:
                    _out?.TryWrite(new AssistantEvent.ReasoningDone(Math.Max(1, Math.Round((DateTime.UtcNow - _thinkingSince).TotalSeconds))));
                    break;
                case AgentEvent.ToolExecutionStart start:
                    var tool = _owner._tools.FirstOrDefault(entry => entry.Name == start.ToolName);
                    _calls[start.ToolCallId] = new ToolCallData(start.ToolCallId, tool?.Label ?? start.ToolName, tool?.Label ?? start.ToolName, ToolCallStatus.Running)
                    {
                        Mark = tool?.Mark,
                        Input = [.. Fields(start.Args).Select(field => new KeyValuePair<string, string>(field.Name, field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString() ?? "" : field.Value.GetRawText()))],
                    };
                    if (tool is not { RequiresApproval: true }) { Publish(start.ToolCallId); }
                    break;
                case AgentEvent.ToolExecutionUpdate update when _calls.TryGetValue(update.ToolCallId, out var running):
                    if (Words(update.PartialResult) is { Length: > 0 } partial) { _out?.TryWrite(new AssistantEvent.Tool(running with { Output = new ToolOutput.Text(partial) })); }
                    break;
                case AgentEvent.ToolExecutionEnd end when _calls.TryGetValue(end.ToolCallId, out var call):
                    string words = Words(end.Result);
                    _out?.TryWrite(new AssistantEvent.Tool(_denied.Contains(end.ToolCallId) ? call with { Status = ToolCallStatus.Denied, Message = words }
                        : end.IsError ? call with { Status = ToolCallStatus.Error, Message = words }
                        : call with { Status = ToolCallStatus.Success, Output = new ToolOutput.Text(words), DurationMs = end.DurationMs }));
                    if (!end.IsError && end.Result is AgentToolResult { Details: ArtifactItem artifact }) { _out?.TryWrite(new AssistantEvent.Artifact(artifact)); }
                    break;
            }
        }

        private void Publish(string id)
        {
            if (_calls.TryGetValue(id, out var call)) { _out?.TryWrite(new AssistantEvent.Tool(call)); }
        }

        private static string Words(object? result) => result is AgentToolResult done ? string.Concat(done.Content.OfType<TextContent>().Select(block => block.Text)) : "";

        // Text goes along with the question. Anything else is named, so the model does not claim to have read it.
        private static string Compose(AssistantRequest request)
        {
            if (request.Files.Count == 0) { return request.Text; }
            var prompt = new StringBuilder(request.Text);
            foreach (var file in request.Files)
            {
                prompt.Append("\n\n");
                if (Read(file) is { } content) { prompt.Append($"<file name=\"{file.Name}\">\n{content}\n</file>"); }
                else { prompt.Append($"[The person attached {file.Name}, but its content could not be included.]"); }
            }
            return prompt.ToString();
        }

        private static string? Read(AssistantFile file)
        {
            if (file.PastedText is { } pasted) { return pasted; }
            if (file.FilePath is not { } path) { return null; }
            try
            {
                if (new FileInfo(path).Length > MaxFileCharacters) { return null; }
                string text = File.ReadAllText(path);
                return text.Contains('\0') ? null : text;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        }
    }

    private readonly StreamFn _stream = stream;
    private readonly Func<string?, Model> _model = model;
    private readonly IReadOnlyList<AssistantTool> _tools = tools;
    private readonly string _systemPrompt = systemPrompt;
    private readonly ThinkingLevel _thinking = thinking;
}
