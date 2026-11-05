using System.Windows;
using System.Windows.Controls;
using ArcWpf.AiAssistant.Backend;
using ArcWpf.Controls;

namespace ArcWpf.AiAssistant;

/// <summary>One question and its reply. It turns the backend's events into controls and owns the reply's run, so a chat keeps working while another one is on screen.</summary>
internal sealed class TurnView : StackPanel
{
    private readonly IAssistantSession? _session;
    private readonly StackPanel _body = new();
    private readonly Dictionary<string, ToolCallGroup> _groups = [];
    private readonly List<StreamingMessage> _segments = [];
    private readonly List<ArtifactItem> _artifacts = [];
    private AssistantRequest _request = new("", [], null, null, null, []);
    private IReadOnlyList<CitationSource>? _sources;
    private CancellationTokenSource? _run;
    private StreamingMessage? _message;
    private ReasoningBlock? _reasoning;
    private ToolCallGroup? _group;
    private ApprovalCard? _waiting;
    private TaskCompletionSource<ApprovalDecision>? _answer;
    private bool _canRegenerate = true;

    public bool IsBusy { get; private set; }
    public double? Progress { get; private set; }

    /// <summary>IsBusy or Progress changed.</summary>
    public event EventHandler? StateChanged;
    public event EventHandler<ArtifactItem>? ArtifactAdded;
    public event EventHandler<ArtifactItem>? ArtifactRemoved;
    public event EventHandler<string>? QuoteRequested;

    public TurnView(IAssistantSession session, AssistantRequest request)
    {
        _session = session;
        _request = request;
        Margin = new Thickness(0, 0, 0, 28);
        Children.Add(Question(request.Text, request.Files));
        Children.Add(_body);
    }

    private TurnView(string question, string answer)
    {
        _canRegenerate = false;
        Margin = new Thickness(0, 0, 0, 28);
        Children.Add(Question(question, []));
        Children.Add(_body);
        _body.Children.Add(new StreamingMessage { Text = answer, Status = StreamingStatus.Done, HidesFoot = true, ShowFeedback = false, CanRetry = false, Margin = new Thickness(0, 0, 0, 4) });
        _body.Children.Add(new MessageActions { Text = answer, CanRegenerate = false, CanQuote = false, HorizontalAlignment = HorizontalAlignment.Left });
    }

    /// <summary>A reply from an earlier session: nothing runs and it cannot be generated again.</summary>
    public static TurnView Settled(string question, string answer) => new(question, answer);

    /// <summary>Only the newest turn of a chat can run again, since the ones after it were built on this answer.</summary>
    public bool CanRegenerate
    {
        get => _canRegenerate;
        set
        {
            _canRegenerate = value;
            foreach (var child in _body.Children)
            {
                switch (child)
                {
                    case MessageActions actions: actions.CanRegenerate = value; break;
                    case StreamingMessage message: message.CanRetry = value; break;
                    case ApprovalCard card when card.Status is ApprovalStatus.Denied or ApprovalStatus.NotSent: card.IsEnabled = value; break;
                }
            }
        }
    }

    public void Start() => Run();

    public void Regenerate()
    {
        if (!_canRegenerate) { return; }
        _request = _request with { Attempt = _request.Attempt + 1 };
        Reset();
        Run();
    }

    public void Stop() => _run?.Cancel();

    private async void Run()
    {
        var run = _run = new CancellationTokenSource();
        SetBusy(true);
        Begin();
        try
        {
            await foreach (var item in _session!.RunAsync(_request, run.Token).WithCancellation(run.Token))
            {
                if (run != _run) { return; }
                Apply(item);
            }
            if (run == _run) { Finish(); }
        }
        catch (OperationCanceledException) { if (run == _run) { Stopped(); } }
        catch (AssistantException ex) { if (run == _run) { Failed(ex.Message); } }
    }

    private void Apply(AssistantEvent item)
    {
        switch (item)
        {
            case AssistantEvent.Reasoning step:
                if (_reasoning is null)
                {
                    EndSegment();
                    _reasoning = new ReasoningBlock { Status = ReasoningStatus.Thinking, ModelName = _request.ModelName, ModelMark = _request.ModelMark, CanStop = false, CanRetry = false };
                    Place(_reasoning);
                }
                _reasoning.Reasoning += step.Delta;
                break;
            case AssistantEvent.ReasoningDone done when _reasoning is not null:
                _reasoning.Seconds = done.Seconds;
                _reasoning.Status = ReasoningStatus.Done;
                _reasoning = null;
                Begin();
                break;
            case AssistantEvent.Tool tool:
                Update(tool.Call);
                break;
            case AssistantEvent.ApprovalNeeded approval:
                Ask(approval.Request, approval.Answer);
                break;
            case AssistantEvent.Sources sources:
                _sources = sources.Items;
                if (_message is not null) { _message.Sources = _sources; }
                break;
            case AssistantEvent.Artifact artifact:
                _artifacts.Add(artifact.Item);
                ArtifactAdded?.Invoke(this, artifact.Item);
                break;
            case AssistantEvent.Progress progress:
                Progress = progress.Value;
                StateChanged?.Invoke(this, EventArgs.Empty);
                break;
            case AssistantEvent.Text text:
                _group = null;
                if (_message is null) { Begin(); }
                _message!.Append(text.Delta);
                _message.Status = StreamingStatus.Streaming;
                break;
        }
    }

    private void Update(ToolCallData call)
    {
        EndSegment();
        if (!_groups.TryGetValue(call.Id, out var group))
        {
            group = _group ??= NewGroup();
            _groups[call.Id] = group;
        }
        var calls = (group.Calls ?? []).ToList();
        int at = calls.FindIndex(existing => existing.Id == call.Id);
        if (at >= 0) { calls[at] = call; } else { calls.Add(call); }
        group.Calls = calls;
        // Once every tool is back the model is working again, and the thread should say so.
        if (!_groups.Values.Distinct().Any(entry => entry.Calls!.Any(item => item.Status == ToolCallStatus.Running))) { Begin(); }
    }

    private ToolCallGroup NewGroup()
    {
        var group = new ToolCallGroup { AutoCollapse = true };
        // A retry runs the whole turn: the model decides again with what it now knows.
        group.RetryRequested += (_, _) => { if (!IsBusy) { Regenerate(); } };
        Place(group);
        return group;
    }

    private void Ask(ApprovalRequest request, TaskCompletionSource<ApprovalDecision> answer)
    {
        EndSegment();
        _group = null;
        var card = new ApprovalCard { Request = request, Workspace = "this computer" };
        card.Decided += (_, decision) =>
        {
            if (_waiting != card) { return; }
            _waiting = null;
            Begin();
            answer.TrySetResult(decision);
        };
        card.RequestedAgain += (_, _) => Regenerate();
        Place(card);
        _waiting = card;
        _answer = answer;
    }

    // The next words of the reply, shown as thinking until they arrive.
    private void Begin()
    {
        EndSegment();
        _message = new StreamingMessage { Status = StreamingStatus.Thinking, Sources = _sources, ModelName = _segments.Count == 0 ? _request.ModelName : null, ModelMark = _segments.Count == 0 ? _request.ModelMark : null, HidesFoot = true, ShowFeedback = false, CanStop = false, CanRetry = _canRegenerate };
        _message.RetryRequested += (_, _) => Regenerate();
        Place(_message);
    }

    // A reply can speak before and after its tools. A placeholder that never got words goes away.
    private void EndSegment()
    {
        if (_message is not { } message) { return; }
        _message = null;
        if (message.Text.Length == 0) { _body.Children.Remove(message); return; }
        message.Status = StreamingStatus.Done;
        _segments.Add(message);
    }

    private void Place(FrameworkElement element)
    {
        element.Margin = new Thickness(0, 0, 0, 12);
        _body.Children.Add(element);
    }

    private void Finish()
    {
        EndSegment();
        string text = string.Join("\n\n", _segments.Select(segment => segment.Text));
        if (text.Length > 0)
        {
            var actions = new MessageActions { Text = text, CanRegenerate = _canRegenerate, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, -8, 0, 0), SubmitFeedback = _ => Task.Delay(400) };
            actions.RegenerateRequested += (_, _) => Regenerate();
            actions.QuoteRequested += (_, quote) => QuoteRequested?.Invoke(this, quote);
            _body.Children.Add(actions);
        }
        SetBusy(false);
    }

    private void Stopped()
    {
        if (_reasoning is not null) { _reasoning.Status = ReasoningStatus.Stopped; }
        if (_waiting is { } card) { card.Status = ApprovalStatus.NotSent; _waiting = null; }
        foreach (var group in _groups.Values.Distinct())
        {
            group.Calls = [.. group.Calls!.Select(call => call.Status == ToolCallStatus.Running ? call with { Status = ToolCallStatus.Error, Message = "Stopped before it finished" } : call)];
        }
        EndMessage(StreamingStatus.Stopped, null);
    }

    private void Failed(string reason)
    {
        if (_reasoning is not null) { _reasoning.Status = ReasoningStatus.Error; }
        EndMessage(StreamingStatus.Error, reason);
    }

    private void EndMessage(StreamingStatus status, string? error)
    {
        // A cut or failed reply has no MessageActions, so its own foot offers Copy and Retry.
        if (_message is null) { Begin(); }
        var message = _message!;
        message.HidesFoot = false;
        message.Error = error;
        message.Status = status;
        SetBusy(false);
    }

    private void Reset()
    {
        _run?.Cancel();
        _run = null;
        _answer?.TrySetCanceled();
        _answer = null;
        _waiting = null;
        _reasoning = null;
        _group = null;
        _message = null;
        _sources = null;
        _groups.Clear();
        _segments.Clear();
        _body.Children.Clear();
        foreach (var artifact in _artifacts) { ArtifactRemoved?.Invoke(this, artifact); }
        _artifacts.Clear();
        Progress = null;
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        if (!busy) { Progress = null; }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static FrameworkElement Question(string text, IReadOnlyList<AssistantFile> files)
    {
        var stack = new StackPanel();
        var words = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 21 };
        words.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        if (text.Length > 0) { stack.Children.Add(words); }
        if (files.Count > 0)
        {
            var names = new TextBlock { Text = string.Join(", ", files.Select(file => file.Name)), FontSize = 12, LineHeight = 17, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, text.Length > 0 ? 4 : 0, 0, 0) };
            names.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            stack.Children.Add(names);
        }
        var bubble = new Border { Child = stack, CornerRadius = new CornerRadius(20), Padding = new Thickness(16, 10, 16, 10), MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(48, 0, 0, 16) };
        bubble.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        return bubble;
    }
}
