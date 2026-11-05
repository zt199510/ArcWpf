using System.Windows;
using System.Windows.Controls;
using ArcWpf.AiAssistant.Backend;
using ArcWpf.Controls;

namespace ArcWpf.AiAssistant;

/// <summary>One question and its reply. It turns the backend's events into controls and owns the reply's run, so a chat keeps working while another one is on screen.</summary>
internal sealed class TurnView : StackPanel
{
    private readonly IAssistantBackend? _backend;
    private readonly StackPanel _body = new();
    private readonly Dictionary<string, ToolCallGroup> _groups = [];
    private readonly HashSet<string> _stoppedCalls = [];
    private readonly List<ArtifactItem> _artifacts = [];
    private AssistantRequest _request = new("", [], null, null, null, []);
    private CancellationTokenSource? _run;
    private StreamingMessage? _message;
    private MessageActions? _actions;
    private ReasoningBlock? _reasoning;
    private ToolCallGroup? _group;
    private ApprovalCard? _waiting;
    private CancellationTokenSource? _sending;
    private bool _canRegenerate = true;

    private IAssistantBackend Backend => _backend ?? throw new InvalidOperationException("A settled turn does not run.");

    public bool IsBusy { get; private set; }
    public double? Progress { get; private set; }

    /// <summary>IsBusy or Progress changed.</summary>
    public event EventHandler? StateChanged;
    public event EventHandler<ArtifactItem>? ArtifactAdded;
    public event EventHandler<ArtifactItem>? ArtifactRemoved;
    public event EventHandler<string>? QuoteRequested;

    public TurnView(IAssistantBackend backend, AssistantRequest request)
    {
        _backend = backend;
        _request = request;
        Margin = new Thickness(0, 0, 0, 28);
        Children.Add(Question(request.Text, request.Files));
        Children.Add(_body);
    }

    private TurnView(string question, string answer)
    {
        Margin = new Thickness(0, 0, 0, 28);
        Children.Add(Question(question, []));
        Children.Add(_body);
        _message = new StreamingMessage { Text = answer, Status = StreamingStatus.Done, HidesFoot = true, ShowFeedback = false, CanRetry = false, Margin = new Thickness(0, 0, 0, 4) };
        _body.Children.Add(_message);
        _body.Children.Add(new MessageActions { Text = answer, CanRegenerate = false, CanQuote = false, HorizontalAlignment = HorizontalAlignment.Left });
    }

    /// <summary>A reply from an earlier session: nothing runs and it cannot be generated again.</summary>
    public static TurnView Settled(string question, string answer) => new(question, answer) { _canRegenerate = false };

    public bool CanRegenerate
    {
        get => _canRegenerate;
        set { _canRegenerate = value; if (_actions is not null) { _actions.CanRegenerate = value; } }
    }

    public void Start() => Run(token => Backend.StreamAsync(_request, token));

    public void Regenerate()
    {
        _request = _request with { Attempt = _request.Attempt + 1 };
        Reset();
        Start();
    }

    public void Stop()
    {
        if (_waiting is { } card)
        {
            // Nothing is running while the card waits, so stopping withdraws the question. An answer on its way ends as NotSent through its own cancellation.
            _waiting = null;
            if (card.Status == ApprovalStatus.Sending) { _sending?.Cancel(); } else { card.Status = ApprovalStatus.NotSent; }
            SetBusy(false);
            return;
        }
        _run?.Cancel();
    }

    private async void Run(Func<CancellationToken, IAsyncEnumerable<AssistantEvent>> stream)
    {
        _run?.Cancel();
        var run = _run = new CancellationTokenSource();
        SetBusy(true);
        BeginMessage();
        try
        {
            await foreach (var item in stream(run.Token).WithCancellation(run.Token))
            {
                if (run != _run) { return; }
                Apply(item);
            }
            if (run == _run && _waiting is null) { Finish(); }
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
                    _reasoning = new ReasoningBlock { Status = ReasoningStatus.Thinking, ModelName = _request.ModelName, ModelMark = _request.ModelMark, CanStop = false, CanRetry = false };
                    Place(_reasoning);
                }
                _reasoning.Reasoning += step.Delta;
                break;
            case AssistantEvent.ReasoningDone done when _reasoning is not null:
                _reasoning.Seconds = done.Seconds;
                _reasoning.Status = ReasoningStatus.Done;
                break;
            case AssistantEvent.Tool tool:
                Update(tool.Call);
                break;
            case AssistantEvent.ApprovalNeeded approval:
                Ask(approval.Request);
                break;
            case AssistantEvent.Sources sources when _message is not null:
                _message.Sources = sources.Items;
                break;
            case AssistantEvent.Artifact artifact:
                _artifacts.Add(artifact.Item);
                ArtifactAdded?.Invoke(this, artifact.Item);
                break;
            case AssistantEvent.Progress progress:
                Progress = progress.Value;
                StateChanged?.Invoke(this, EventArgs.Empty);
                break;
            case AssistantEvent.Text text when _message is not null:
                Words(text.Delta);
                break;
        }
    }

    private void Words(string delta)
    {
        _message!.Visibility = Visibility.Visible;
        _message.Append(delta);
        _message.Status = StreamingStatus.Streaming;
        if (_actions is null)
        {
            _actions = new MessageActions { IsGenerating = true, CanRegenerate = _canRegenerate, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
            _actions.StopRequested += (_, _) => Stop();
            _actions.RegenerateRequested += (_, _) => Regenerate();
            _actions.QuoteRequested += (_, quote) => QuoteRequested?.Invoke(this, quote);
            _actions.SubmitFeedback = _ => Task.Delay(400);
            _body.Children.Add(_actions);
        }
    }

    private void Update(ToolCallData call)
    {
        if (_message is not null && _message.Text.Length == 0) { _message.Visibility = Visibility.Collapsed; }
        if (!_groups.TryGetValue(call.Id, out var group))
        {
            group = _group ??= NewGroup();
            _groups[call.Id] = group;
        }
        var calls = (group.Calls ?? []).ToList();
        int at = calls.FindIndex(existing => existing.Id == call.Id);
        if (at >= 0) { calls[at] = call; } else { calls.Add(call); }
        group.Calls = calls;
    }

    private ToolCallGroup NewGroup()
    {
        var group = new ToolCallGroup { AutoCollapse = true };
        group.RetryRequested += (_, e) => RetryTool(e.Call);
        Place(group);
        return group;
    }

    private void RetryTool(ToolCallData call)
    {
        if (IsBusy) { return; }
        // A call the person stopped has no paused step to return to, so the whole reply runs again.
        if (_stoppedCalls.Contains(call.Id)) { Regenerate(); return; }
        Clear(_message, _actions);
        Run(token => Backend.ContinueAsync(_request, new AssistantContinuation.ToolRetry(call), token));
    }

    private void Ask(ApprovalRequest request)
    {
        _group = null;
        if (_message is not null) { _message.Visibility = Visibility.Collapsed; }
        var card = new ApprovalCard { Request = request, Workspace = "arc-ui/web", SendDecision = _ => { _sending = new CancellationTokenSource(); return Task.Delay(400, _sending.Token); } };
        card.Decided += (_, decision) =>
        {
            if (_waiting != card) { return; }
            _waiting = null;
            Clear(_message, _actions);
            Run(token => Backend.ContinueAsync(_request, new AssistantContinuation.Approval(decision), token));
        };
        card.RequestedAgain += (_, _) =>
        {
            // Asking again replaces whatever followed the first answer.
            _run?.Cancel();
            _run = null;
            int at = _body.Children.IndexOf(card);
            while (_body.Children.Count > at + 1)
            {
                if (_body.Children[at + 1] is ToolCallGroup group) { foreach (var key in _groups.Where(pair => pair.Value == group).Select(pair => pair.Key).ToList()) { _groups.Remove(key); } }
                _body.Children.RemoveAt(at + 1);
            }
            foreach (var artifact in _artifacts) { ArtifactRemoved?.Invoke(this, artifact); }
            _artifacts.Clear();
            _message = null;
            _actions = null;
            _group = null;
            _waiting = card;
            SetBusy(true);
        };
        Place(card);
        _waiting = card;
        _group = null;
    }

    private void BeginMessage()
    {
        _message = new StreamingMessage { Status = StreamingStatus.Thinking, ModelName = _request.ModelName, ModelMark = _request.ModelMark, HidesFoot = true, ShowFeedback = false, CanStop = false };
        _message.RetryRequested += (_, _) => Regenerate();
        _body.Children.Add(_message);
    }

    private void Place(UIElement element)
    {
        if (element is FrameworkElement framed) { framed.Margin = new Thickness(0, 0, 0, 12); }
        if (_message is null) { _body.Children.Add(element); }
        else { _body.Children.Insert(_body.Children.IndexOf(_message), element); }
    }

    private void Finish()
    {
        _message!.Status = StreamingStatus.Done;
        if (_actions is { } actions)
        {
            actions.Text = _message.Text;
            actions.IsGenerating = false;
        }
        SetBusy(false);
    }

    private void Stopped()
    {
        EndReasoning(ReasoningStatus.Stopped);
        foreach (var group in _groups.Values.Distinct())
        {
            var calls = (group.Calls ?? []).Select(call =>
            {
                if (call.Status != ToolCallStatus.Running) { return call; }
                _stoppedCalls.Add(call.Id);
                return call with { Status = ToolCallStatus.Error, Message = "Stopped before it finished" };
            }).ToList();
            group.Calls = calls;
        }
        EndMessage(StreamingStatus.Stopped, null);
    }

    private void Failed(string reason)
    {
        EndReasoning(ReasoningStatus.Error);
        EndMessage(StreamingStatus.Error, reason);
    }

    private void EndReasoning(ReasoningStatus status)
    {
        if (_reasoning is { Status: ReasoningStatus.Thinking }) { _reasoning.Status = status; }
    }

    private void EndMessage(StreamingStatus status, string? error)
    {
        Clear(_actions);
        _actions = null;
        if (_message is { } message)
        {
            // A cut or failed reply has no MessageActions, so its own foot offers Copy and Retry.
            message.HidesFoot = false;
            message.Visibility = Visibility.Visible;
            message.Error = error;
            message.Status = status;
        }
        SetBusy(false);
    }

    private void Reset()
    {
        _run?.Cancel();
        _run = null;
        _waiting = null;
        _reasoning = null;
        _group = null;
        _message = null;
        _actions = null;
        _groups.Clear();
        _stoppedCalls.Clear();
        _body.Children.Clear();
        foreach (var artifact in _artifacts) { ArtifactRemoved?.Invoke(this, artifact); }
        _artifacts.Clear();
    }

    private void Clear(params UIElement?[] elements)
    {
        foreach (var element in elements) { if (element is not null) { _body.Children.Remove(element); } }
        if (elements.Contains(_message)) { _message = null; }
        if (elements.Contains(_actions)) { _actions = null; }
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        if (!busy) { Progress = null; }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static FrameworkElement Question(string text, IReadOnlyList<string> files)
    {
        var stack = new StackPanel();
        var words = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 21 };
        words.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        if (text.Length > 0) { stack.Children.Add(words); }
        if (files.Count > 0)
        {
            var names = new TextBlock { Text = string.Join(", ", files), FontSize = 12, LineHeight = 17, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, text.Length > 0 ? 4 : 0, 0, 0) };
            names.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            stack.Children.Add(names);
        }
        var bubble = new Border { Child = stack, CornerRadius = new CornerRadius(20), Padding = new Thickness(16, 10, 16, 10), MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(48, 0, 0, 16) };
        bubble.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        return bubble;
    }
}
