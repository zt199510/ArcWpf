using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum StreamingStatus { Thinking, Streaming, Done, Stopped, Error }

public enum StreamingBlockKind { Paragraph, Heading, Ordered, Bullet, Code }
public enum StreamingSpanKind { Text, Bold, Code, Citation }

public sealed record StreamingSpan(StreamingSpanKind Kind, string Text, int Number = 0);

public sealed record StreamingBlock(StreamingBlockKind Kind, IReadOnlyList<StreamingSpan> Spans, string? Marker = null, string? Language = null)
{
    public string PlainText => string.Concat(Spans.Select(span => span.Text));
}

/// <summary>
/// The deliberately small Markdown a streamed reply uses: paragraphs, ordered and bulleted lists, short headings, **bold**, `inline code`,
/// fenced code and [n] citations. Half-streamed markers never show raw.
/// </summary>
public static partial class StreamingMarkdown
{
    [GeneratedRegex(@"^(\d+)[.)]\s+(.*)$")]
    private static partial Regex Ordered();
    [GeneratedRegex(@"^[-*]\s+(.*)$")]
    private static partial Regex Bullet();
    [GeneratedRegex(@"^(#{1,3})\s+(.*)$")]
    private static partial Regex Heading();

    public static IReadOnlyList<StreamingBlock> Parse(string? text)
    {
        var blocks = new List<StreamingBlock>();
        if (string.IsNullOrEmpty(text)) { return blocks; }
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var paragraph = new StringBuilder();
        void Flush()
        {
            if (paragraph.Length == 0) { return; }
            blocks.Add(new StreamingBlock(StreamingBlockKind.Paragraph, Inline(paragraph.ToString())));
            paragraph.Clear();
        }
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            string trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                Flush();
                string language = trimmed[3..].Trim();
                var code = new StringBuilder();
                index++;
                // An unclosed fence runs to the end of what has arrived.
                for (; index < lines.Length && !lines[index].Trim().StartsWith("```", StringComparison.Ordinal); index++)
                {
                    if (code.Length > 0) { code.Append('\n'); }
                    code.Append(lines[index]);
                }
                blocks.Add(new StreamingBlock(StreamingBlockKind.Code, [new StreamingSpan(StreamingSpanKind.Text, code.ToString())], null, language));
                continue;
            }
            if (trimmed.Length == 0) { Flush(); continue; }
            if (Ordered().Match(trimmed) is { Success: true } ordered) { Flush(); blocks.Add(new StreamingBlock(StreamingBlockKind.Ordered, Inline(ordered.Groups[2].Value), ordered.Groups[1].Value + ".")); continue; }
            if (Bullet().Match(trimmed) is { Success: true } bullet) { Flush(); blocks.Add(new StreamingBlock(StreamingBlockKind.Bullet, Inline(bullet.Groups[1].Value), "•")); continue; }
            if (Heading().Match(trimmed) is { Success: true } heading) { Flush(); blocks.Add(new StreamingBlock(StreamingBlockKind.Heading, Inline(heading.Groups[2].Value))); continue; }
            // A list line still arriving: its number or dash waits for the space that proves it is a marker.
            if (index == lines.Length - 1 && paragraph.Length == 0 && (trimmed is "-" or "*" or "#" or "##" or "###" || (trimmed.TrimEnd('.', ')').All(char.IsDigit) && trimmed.Length <= 4))) { continue; }
            if (paragraph.Length > 0) { paragraph.Append(' '); }
            paragraph.Append(trimmed);
        }
        Flush();
        return blocks;
    }

    private static List<StreamingSpan> Inline(string text)
    {
        var spans = new List<StreamingSpan>();
        var plain = new StringBuilder();
        void Flush(StreamingSpanKind kind = StreamingSpanKind.Text)
        {
            if (plain.Length == 0) { return; }
            spans.Add(new StreamingSpan(kind, plain.ToString()));
            plain.Clear();
        }
        int at = 0;
        while (at < text.Length)
        {
            char c = text[at];
            if (c == '*' && at + 1 < text.Length && text[at + 1] == '*')
            {
                Flush();
                int end = text.IndexOf("**", at + 2, StringComparison.Ordinal);
                string inner = end >= 0 ? text[(at + 2)..end] : text[(at + 2)..].TrimEnd('*');
                foreach (var span in Inline(inner)) { spans.Add(span.Kind == StreamingSpanKind.Text ? span with { Kind = StreamingSpanKind.Bold } : span); }
                at = end >= 0 ? end + 2 : text.Length;
                continue;
            }
            if (c == '*' && at == text.Length - 1) { break; }
            if (c == '`')
            {
                Flush();
                int end = text.IndexOf('`', at + 1);
                string inner = end >= 0 ? text[(at + 1)..end] : text[(at + 1)..];
                if (inner.Length > 0) { spans.Add(new StreamingSpan(StreamingSpanKind.Code, inner)); }
                at = end >= 0 ? end + 1 : text.Length;
                continue;
            }
            if (c == '[')
            {
                int end = text.IndexOf(']', at + 1);
                if (end < 0 && text.AsSpan(at + 1).IndexOfAnyExcept("0123456789") < 0) { break; } // "[1" is still arriving.
                if (end > at + 1 && int.TryParse(text.AsSpan(at + 1, end - at - 1), out int number))
                {
                    Flush();
                    spans.Add(new StreamingSpan(StreamingSpanKind.Citation, number.ToString(System.Globalization.CultureInfo.InvariantCulture), number));
                    at = end + 1;
                    if (plain.Length == 0 && at < text.Length && text[at] == ' ' && at + 1 < text.Length && text[at + 1] == '[') { at++; }
                    continue;
                }
            }
            plain.Append(c);
            at++;
        }
        Flush();
        return spans;
    }
}

/// <summary>
/// An assistant reply that streams in. Text arrives in any chunk size; a reveal clock paces it in whole words and each new chunk only fades in,
/// so nothing already on screen moves. Stop cuts the reply where it stands; Retry and Copy sit under the finished message.
/// </summary>
public class StreamingMessage : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(StreamingMessage), new FrameworkPropertyMetadata("", (d, _) => ((StreamingMessage)d).OnText()));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(StreamingStatus), typeof(StreamingMessage), new FrameworkPropertyMetadata(StreamingStatus.Done, (d, e) => ((StreamingMessage)d).OnStatus((StreamingStatus)e.OldValue)));
    public static readonly DependencyProperty ErrorProperty = Prop(nameof(Error), typeof(string), null);
    public static readonly DependencyProperty ModelNameProperty = Prop(nameof(ModelName), typeof(string), null);
    public static readonly DependencyProperty ModelMarkProperty = Prop(nameof(ModelMark), typeof(AiMark), null);
    public static readonly DependencyProperty SourcesProperty = DependencyProperty.Register(nameof(Sources), typeof(IReadOnlyList<CitationSource>), typeof(StreamingMessage), new FrameworkPropertyMetadata(null, (d, _) => ((StreamingMessage)d).Rebuild()));
    public static readonly DependencyProperty CanStopProperty = Prop(nameof(CanStop), typeof(bool), false);
    public static readonly DependencyProperty CanRetryProperty = Prop(nameof(CanRetry), typeof(bool), true);
    public static readonly DependencyProperty ShowFeedbackProperty = Prop(nameof(ShowFeedback), typeof(bool), false);
    public static readonly DependencyProperty ThinkingLabelProperty = Prop(nameof(ThinkingLabel), typeof(string), "Thinking");

    private static DependencyProperty Prop(string name, Type type, object? fallback) => DependencyProperty.Register(name, type, typeof(StreamingMessage), new FrameworkPropertyMetadata(fallback, (d, _) => ((StreamingMessage)d).Chrome()));

    /// <summary>The reply so far. Set the growing text or call Append; either way the reveal clock paces it.</summary>
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public StreamingStatus Status { get => (StreamingStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    /// <summary>The reason shown under "Reply failed".</summary>
    public string? Error { get => (string?)GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }
    public string? ModelName { get => (string?)GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }
    public AiMark? ModelMark { get => (AiMark?)GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }
    /// <summary>What [n] citations index into, 1-based. A citation with no matching source renders as a plain number.</summary>
    public IReadOnlyList<CitationSource>? Sources { get => (IReadOnlyList<CitationSource>?)GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }
    /// <summary>Shows a Stop action beside the model name while the reply is running.</summary>
    public bool CanStop { get => (bool)GetValue(CanStopProperty); set => SetValue(CanStopProperty, value); }
    public bool CanRetry { get => (bool)GetValue(CanRetryProperty); set => SetValue(CanRetryProperty, value); }
    /// <summary>Thumbs up and down under a finished reply.</summary>
    public bool ShowFeedback { get => (bool)GetValue(ShowFeedbackProperty); set => SetValue(ShowFeedbackProperty, value); }
    public string ThinkingLabel { get => (string)GetValue(ThinkingLabelProperty); set => SetValue(ThinkingLabelProperty, value); }

    public event EventHandler? StopRequested;
    public event EventHandler? RetryRequested;
    /// <summary>The reader rated the reply: true for up, false for down, null when they took it back.</summary>
    public event EventHandler<bool?>? FeedbackGiven;

    private const double Tick = .034, ChunkFadeSeconds = .15;
    private const int ChunkWords = 3;

    private readonly FrameLoop _loop;
    private double _clock;
    private int _revealed;
    private string _target = "";
    private bool _built, _cut;
    private readonly Grid _head = new() { Height = 19.6, Margin = new Thickness(0, 0, 0, 10) };
    private readonly Decorator _mark = new() { Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = AiUi.Line("", 14, 19.6, "TextSecondary");
    private readonly Decorator _stopSlot = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -6, 0, -6) };
    private readonly SpringHeightBox _clip = new(.2);
    private readonly StackPanel _blocks = new();
    private readonly FadeSwap _thinking = new() { Height = 25.6, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Grid _foot = new() { Margin = new Thickness(-6, 6, 0, 0) };
    private readonly StackPanel _footRow = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _errorRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _errorText = AiUi.Wrapped("", 14, 19.6, "TextSecondary");
    private readonly TextBlock _stopped = AiUi.Line("Stopped", 12, 16.8, "TextMuted", false, false);
    private readonly CopyAction _copy;
    private readonly ToolbarButton _retry;
    private readonly List<BlockView> _views = [];
    private readonly ToolbarButton _up, _down;
    private bool? _rating;
    private AiMark? _shownMark;
    private bool _hasMark;
    private StreamingStatus? _thinkingShownFor;

    private sealed class BlockView(StreamingBlockKind kind, FrameworkElement root)
    {
        public StreamingBlockKind Kind { get; } = kind;
        public FrameworkElement Root { get; } = root;
        public TextBlock? Text { get; set; }
        public TextBlock? Code { get; set; }
        public TextBlock? Language { get; set; }
        public Func<string>? CodeText { get; set; }
        public List<StreamingSpan> Spans { get; set; } = [];
        public string? Marker { get; set; }
    }

    static StreamingMessage() => DefaultStyleKeyProperty.OverrideMetadata(typeof(StreamingMessage), new FrameworkPropertyMetadata(typeof(StreamingMessage)));

    public StreamingMessage()
    {
        _loop = new FrameLoop(Frame);
        _copy = new CopyAction(() => Text ?? "", "Copy", 32);
        _retry = new ToolbarButton(16) { Height = 32, Padding = new Thickness(8, 0, 10, 0), Roles = self => (self.IsMouseOver ? "SurfaceMuted" : null, null, self.IsMouseOver ? "Foreground" : "TextSecondary") };
        var retryRow = new StackPanel { Orientation = Orientation.Horizontal };
        var retryIcon = Lucide.Glyph(Lucide.RotateCcw, 14);
        retryIcon.Margin = new Thickness(0, 0, 6, 0);
        retryRow.Children.Add(retryIcon);
        var retryText = new TextBlock { Text = "Retry", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        retryRow.Children.Add(retryText);
        _retry.Content = retryRow;
        _retry.Click += (_, _) => RetryRequested?.Invoke(this, EventArgs.Empty);
        AutomationProperties.SetName(_retry, "Retry");
        _up = Thumb(Lucide.ThumbsUp, true, "Good reply");
        _down = Thumb(Lucide.ThumbsDown, false, "Bad reply");
        Unloaded += (_, _) => _loop.Stop();
        Loaded += (_, _) => { Kick(); Chrome(); };
    }

    private ToolbarButton Thumb(string glyph, bool value, string name)
    {
        var button = AiUi.Icon(glyph, 32, 16, name);
        button.Roles = self => (self.IsMouseOver ? "SurfaceMuted" : null, null, _rating == value || self.IsMouseOver ? "Foreground" : "TextSecondary");
        button.Click += (_, _) =>
        {
            _rating = _rating == value ? null : value;
            _up.Refresh(true);
            _down.Refresh(true);
            FeedbackGiven?.Invoke(this, _rating);
        };
        return button;
    }

    /// <summary>Adds text to the reply. Chunks may be any size.</summary>
    public void Append(string delta) => SetCurrentValue(TextProperty, (Text ?? "") + delta);

    /// <summary>The revealed slice has caught up with the text received.</summary>
    public bool IsRevealing => _revealed < _target.Length;

    private bool Running => Status is StreamingStatus.Thinking or StreamingStatus.Streaming;

    // ArtifactPreview shows a finished document: it has its own copy action, so the reply foot stays away.
    internal bool HidesFoot { get; set; }

    internal string RevealedForTest => _target[.._revealed];
    internal int ViewCountForTest => _views.Count;
    internal StreamingBlockKind KindForTest(int index) => _views[index].Kind;
    internal string BlockTextForTest(int index) => _views[index].Code?.Text ?? new TextRange(_views[index].Text!.ContentStart, _views[index].Text!.ContentEnd).Text;
    internal int RunCountForTest(int index) => _views[index].Text?.Inlines.Count ?? 0;
    internal Inline InlineForTest(int block, int index) => _views[block].Text!.Inlines.ElementAt(index);
    internal string? MarkerForTest(int index) => _views[index].Marker;
    internal bool ThinkingShownForTest => _thinking.Visibility == Visibility.Visible;
    internal bool FootShownForTest => _foot.Visibility == Visibility.Visible;
    internal bool RetryShownForTest => _retry.Visibility == Visibility.Visible;
    internal bool StoppedShownForTest => _stopped.Visibility == Visibility.Visible;
    internal bool ErrorShownForTest => _errorRow.Visibility == Visibility.Visible;
    internal string ErrorTextForTest => _errorText.Text;
    internal System.Windows.Controls.Button RetryForTest => _retry;
    internal CopyAction CopyForTest => _copy;
    internal System.Windows.Controls.Button ThumbUpForTest => _up;
    internal System.Windows.Controls.Button? StopForTest => _stopSlot.Child as System.Windows.Controls.Button;
    internal bool LoopRunningForTest => _loop.IsRunning;
    internal void AdvanceForTest(double seconds) { for (double left = seconds; left > 1e-9; left -= Math.Min(left, Tick)) { Frame(Math.Min(left, Tick)); } }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host) { return; }
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _head.ColumnDefinitions.Add(new ColumnDefinition());
        _head.Children.Add(_mark);
        Grid.SetColumn(_name, 1);
        _head.Children.Add(_name);
        Grid.SetColumn(_stopSlot, 1);
        _head.Children.Add(_stopSlot);
        var body = new StackPanel();
        body.Children.Add(_thinking);
        body.Children.Add(_blocks);
        _clip.Child = body;
        _footRow.Children.Add(_copy);
        _footRow.Children.Add(_retry);
        _footRow.Children.Add(_up);
        _footRow.Children.Add(_down);
        _stopped.Margin = new Thickness(8, 0, 0, 0);
        _footRow.Children.Add(_stopped);
        _foot.Children.Add(_footRow);
        var alert = AiUi.Glyph(Lucide.CircleAlert, 16, "ArcDanger");
        alert.Margin = new Thickness(0, 0, 6, 0);
        alert.VerticalAlignment = VerticalAlignment.Top;
        _errorRow.Children.Add(alert);
        _errorRow.Children.Add(_errorText);
        var column = new StackPanel();
        column.Children.Add(_head);
        column.Children.Add(_clip);
        column.Children.Add(_errorRow);
        column.Children.Add(_foot);
        host.Child = column;
        _built = true;
        Chrome();
        OnText();
    }

    private void OnText()
    {
        string text = Text ?? "";
        // A rewrite from the start, a retry say, begins again rather than running on from the old reply.
        if (!text.StartsWith(_target, StringComparison.Ordinal)) { _target = ""; _revealed = 0; _cut = false; Clear(); }
        if (_cut) { return; }
        _target = text;
        // History arrives whole: only a running reply paces itself.
        if (!Running || !AiUi.Moving(this)) { _revealed = _target.Length; Render(false); }
        Kick();
        Chrome();
    }

    private void Clear()
    {
        _blocks.Children.Clear();
        _views.Clear();
    }

    private void OnStatus(StreamingStatus old)
    {
        if (old is StreamingStatus.Done or StreamingStatus.Stopped or StreamingStatus.Error && Running) { _cut = false; }
        if (Status == StreamingStatus.Stopped)
        {
            // Stop cuts the reply where it stands.
            _cut = true;
            _target = _target[.._revealed];
            Render(false);
            _loop.Stop();
        }
        Announcer.Say(this, Status switch
        {
            StreamingStatus.Thinking => "Reply started",
            StreamingStatus.Done => "Reply finished",
            StreamingStatus.Stopped => "Reply stopped",
            StreamingStatus.Error => $"Reply failed. {Error}",
            _ => "",
        });
        Chrome();
        Kick();
    }

    private void Kick()
    {
        if (!_built) { return; }
        if (IsRevealing && AiUi.Moving(this)) { _loop.Start(); }
        else if (IsRevealing) { _revealed = _target.Length; Render(false); Chrome(); }
    }

    private bool Frame(double seconds)
    {
        _clock += seconds;
        if (_clock < Tick) { return IsRevealing; }
        _clock %= Tick;
        if (_revealed >= _target.Length) { Chrome(); return false; }
        _revealed = NextStop();
        Render(true);
        if (!IsRevealing) { Chrome(); }
        return IsRevealing;
    }

    // Whole words: three at a time, more when a live stream runs ahead.
    private int NextStop()
    {
        int backlog = _target.Length - _revealed;
        int words = ChunkWords + backlog / 400;
        int at = _revealed;
        for (int count = 0; count < words && at < _target.Length; count++)
        {
            while (at < _target.Length && char.IsWhiteSpace(_target[at])) { at++; }
            while (at < _target.Length && !char.IsWhiteSpace(_target[at])) { at++; }
        }
        return at;
    }

    private void Rebuild()
    {
        if (!_built) { return; }
        Clear();
        Render(false);
    }

    private void Render(bool fade)
    {
        if (!_built) { return; }
        var blocks = StreamingMarkdown.Parse(_target[.._revealed]);
        bool grew = false;
        for (int index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];
            if (index >= _views.Count) { var made = Make(block, index); _views.Add(made); _blocks.Children.Add(made.Root); grew = true; }
            else if (_views[index].Kind != block.Kind || _views[index].Marker != block.Marker)
            {
                _blocks.Children.RemoveAt(index);
                _views[index] = Make(block, index);
                _blocks.Children.Insert(index, _views[index].Root);
            }
            Update(_views[index], block, fade);
        }
        while (_views.Count > blocks.Count) { _views.RemoveAt(_views.Count - 1); _blocks.Children.RemoveAt(_blocks.Children.Count - 1); }
        if (fade && (grew || blocks.Count > 0)) { _clip.MorphNext(); }
        if (_thinking.Visibility == Visibility.Visible != (Status == StreamingStatus.Thinking && blocks.Count == 0)) { Chrome(); }
    }

    private BlockView Make(StreamingBlock block, int index)
    {
        Thickness gap = new(0, index == 0 ? 0 : block.Kind is StreamingBlockKind.Ordered or StreamingBlockKind.Bullet ? 6 : 12, 0, 0);
        if (block.Kind == StreamingBlockKind.Code) { return MakeCode(block, gap); }
        var text = AiUi.Wrapped("", 16, block.Kind == StreamingBlockKind.Heading ? 24 : 25.6, "Foreground", block.Kind == StreamingBlockKind.Heading);
        if (block.Kind is StreamingBlockKind.Ordered or StreamingBlockKind.Bullet)
        {
            var row = new Grid { Margin = gap };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var marker = AiUi.Line(block.Marker ?? "", 16, 25.6, "TextMuted", false, false);
            marker.VerticalAlignment = VerticalAlignment.Top;
            marker.HorizontalAlignment = HorizontalAlignment.Right;
            marker.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(marker);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            return new BlockView(block.Kind, row) { Text = text, Marker = block.Marker };
        }
        text.Margin = gap;
        return new BlockView(block.Kind, text) { Text = text, Marker = block.Marker };
    }

    private BlockView MakeCode(StreamingBlock block, Thickness gap)
    {
        var language = AiUi.Line(block.Language ?? "", 12, 16.8, "TextMuted");
        language.SetResourceReference(TextBlock.FontFamilyProperty, "ArcWpf.Font.Mono");
        var code = new TextBlock { FontSize = 13, LineHeight = 20.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        code.SetResourceReference(TextBlock.FontFamilyProperty, "ArcWpf.Font.Mono");
        code.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        BlockView view = null!;
        var copy = new CopyAction(() => view.Code?.Text ?? "", "Copy code");
        var head = new Grid { Height = 36, Margin = new Thickness(14, 0, 6, 0) };
        head.Children.Add(language);
        copy.HorizontalAlignment = HorizontalAlignment.Right;
        head.Children.Add(copy);
        var scroller = new ScrollViewer { Content = code, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(14, 10, 14, 12), Focusable = false };
        var rule = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = scroller };
        rule.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        var column = new StackPanel();
        column.Children.Add(head);
        column.Children.Add(rule);
        var frame = new Border { CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Margin = gap, Child = column };
        frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        view = new BlockView(StreamingBlockKind.Code, frame) { Code = code, Language = language };
        return view;
    }

    // Parts of a partial reply: what stands stays put, and only the tail is new. A span that changed shape rebuilds without a fade.
    private void Update(BlockView view, StreamingBlock block, bool fade)
    {
        if (view.Code is { } code)
        {
            view.Language!.Text = block.Language ?? "";
            code.Text = block.PlainText;
            return;
        }
        var text = view.Text!;
        var was = view.Spans;
        var now = block.Spans;
        bool prefix = was.Count <= now.Count;
        for (int index = 0; prefix && index < was.Count; index++)
        {
            bool last = index == was.Count - 1;
            prefix = was[index].Kind == now[index].Kind && (last ? now[index].Text.StartsWith(was[index].Text, StringComparison.Ordinal) : was[index].Text == now[index].Text);
        }
        if (!prefix) { text.Inlines.Clear(); was = []; fade = false; }
        for (int index = Math.Max(0, was.Count - 1); index < now.Count; index++)
        {
            bool continuing = index < was.Count;
            string delta = continuing ? now[index].Text[was[index].Text.Length..] : now[index].Text;
            if (delta.Length == 0 && continuing) { continue; }
            AddSpan(text, now[index], delta, continuing, fade);
        }
        view.Spans = [.. now];
    }

    private void AddSpan(TextBlock text, StreamingSpan span, string delta, bool continuing, bool fade)
    {
        double seconds = fade ? ChunkFadeSeconds : 0;
        switch (span.Kind)
        {
            case StreamingSpanKind.Text: ChunkFade.Add(text.Inlines, delta, "Foreground", this, seconds); break;
            case StreamingSpanKind.Bold: ChunkFade.Add(text.Inlines, delta, "Foreground", this, seconds, FontWeights.Medium); break;
            case StreamingSpanKind.Code:
                if (continuing && text.Inlines.LastInline is InlineUIContainer { Child: Border { Child: TextBlock grown } }) { grown.Text = span.Text; break; }
                text.Inlines.Add(new InlineUIContainer(CodeChip(span.Text)) { BaselineAlignment = BaselineAlignment.Center });
                break;
            default:
                var source = Sources is { } sources && span.Number >= 1 && span.Number <= sources.Count ? sources[span.Number - 1] : null;
                if (source is null) { ChunkFade.Add(text.Inlines, $" {span.Number} ", "TextSecondary", this, seconds); break; }
                text.Inlines.Add(new InlineUIContainer(new SourceCitation { Number = span.Number, Source = source }) { BaselineAlignment = BaselineAlignment.Center });
                break;
        }
    }

    private static Border CodeChip(string code)
    {
        var view = new TextBlock { Text = code, FontSize = 13.76, LineHeight = 18.2, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        view.SetResourceReference(TextBlock.FontFamilyProperty, "ArcWpf.Font.Mono");
        view.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        var chip = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(1, 0, 1, 0), Child = view };
        chip.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        return chip;
    }

    private void Chrome()
    {
        if (!_built) { return; }
        bool hasHead = !string.IsNullOrEmpty(ModelName) || ModelMark is not null;
        _head.Visibility = hasHead ? Visibility.Visible : Visibility.Collapsed;
        _name.Text = ModelName ?? "";
        if (!_hasMark || _shownMark != ModelMark) { _hasMark = true; _shownMark = ModelMark; _mark.Child = ModelMark is null ? null : AiUi.Mark(ModelMark, 16, ModelName ?? ""); _mark.Visibility = ModelMark is null ? Visibility.Collapsed : Visibility.Visible; }

        bool thinking = Status == StreamingStatus.Thinking && _revealed == 0;
        _thinking.Visibility = thinking ? Visibility.Visible : Visibility.Collapsed;
        if (thinking && _thinkingShownFor != Status) { _thinking.Set(new TextShimmer { Text = ThinkingLabel, IsActive = true, FontSize = 14, VerticalAlignment = VerticalAlignment.Center }); }
        _thinkingShownFor = thinking ? Status : null;

        bool stop = Running && CanStop;
        if (stop != _stopSlot.Child is not null)
        {
            if (stop)
            {
                var button = AiUi.Outline("Stop");
                button.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
                _stopSlot.Child = button;
            }
            else { _stopSlot.Child = null; }
        }

        bool failed = Status == StreamingStatus.Error;
        _errorRow.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
        _errorText.Text = string.IsNullOrEmpty(Error) ? "Reply failed" : "Reply failed. " + Error;
        // The foot appears once the words have landed, not when the model stopped sending.
        bool settled = !Running && !IsRevealing;
        _foot.Visibility = settled && !HidesFoot ? Visibility.Visible : Visibility.Collapsed;
        _copy.Visibility = (Text ?? "").Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _retry.Visibility = CanRetry ? Visibility.Visible : Visibility.Collapsed;
        _stopped.Visibility = Status == StreamingStatus.Stopped ? Visibility.Visible : Visibility.Collapsed;
        // Thumbs rate a whole reply, so a cut or failed one does not offer them.
        _up.Visibility = _down.Visibility = ShowFeedback && Status == StreamingStatus.Done ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>A scroller for a live thread. While stuck to the bottom it follows new lines on a soft lerp; scrolling up releases it and shows a jump to latest button.</summary>
public class StreamingScrollHost : ContentControl
{
    private const double StickWithin = 24, Follow = 12;

    private readonly OverlayScrollViewer _viewer = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly ToolbarButton _jump;
    private readonly FrameLoop _loop;
    private bool _stuck = true, _shown;

    static StreamingScrollHost() => DefaultStyleKeyProperty.OverrideMetadata(typeof(StreamingScrollHost), new FrameworkPropertyMetadata(typeof(StreamingScrollHost)));

    public StreamingScrollHost()
    {
        _loop = new FrameLoop(Frame);
        _jump = new ToolbarButton(18, 1) { Width = 36, Height = 36, Padding = new Thickness(0), Content = Lucide.Glyph(Lucide.ArrowDown, 16), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed, Roles = self => (self.IsMouseOver ? "SurfaceMuted" : "SurfaceRaised", "Border", "Foreground") };
        _jump.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Direction = 270, Opacity = .1, Color = Colors.Black };
        AutomationProperties.SetName(_jump, "Jump to latest");
        _jump.Click += (_, _) => Jump();
        _viewer.ScrollChanged += OnScroll;
        // The scroller handles the wheel itself, so this listens to handled events too.
        _viewer.AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler((_, e) => { if (e.Delta > 0 && _viewer.ScrollableHeight > 0) { Release(); } }), true);
        _viewer.PreviewKeyDown += (_, e) => { if (e.Key is Key.Up or Key.PageUp or Key.Home) { Release(); } };
        Unloaded += (_, _) => _loop.Stop();
    }

    public bool IsStuckToBottom => _stuck;

    internal double OffsetForTest => _viewer.VerticalOffset;
    internal double ScrollableForTest => _viewer.ScrollableHeight;
    internal bool JumpShownForTest => _jump.Visibility == Visibility.Visible;
    internal System.Windows.Controls.Button JumpForTest => _jump;
    internal ScrollViewer ViewerForTest => _viewer;
    internal void ReleaseForTest() => Release();
    internal void AdvanceForTest(double seconds) => Frame(seconds);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host) { return; }
        var grid = new Grid();
        _viewer.Content = Content;
        grid.Children.Add(_viewer);
        grid.Children.Add(_jump);
        host.Child = grid;
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        _viewer.Content = newContent;
    }

    private void Release() { _stuck = false; _loop.Stop(); UpdateJump(); }

    private void Jump()
    {
        _stuck = true;
        UpdateJump();
        if (AiUi.Moving(this)) { _loop.Start(); } else { _viewer.ScrollToVerticalOffset(_viewer.ScrollableHeight); }
    }

    private void OnScroll(object sender, ScrollChangedEventArgs e)
    {
        double distance = _viewer.ScrollableHeight - _viewer.VerticalOffset;
        // Dragging the bar or scrolling back down re-sticks once the reader is at the bottom again.
        if (!_stuck && distance <= StickWithin && e.VerticalChange > 0 && e.ExtentHeightChange == 0) { _stuck = true; }
        else if (_stuck && e.VerticalChange < 0 && e.ExtentHeightChange == 0 && distance > StickWithin) { _stuck = false; }
        UpdateJump();
        if (_stuck && distance > .5)
        {
            if (AiUi.Moving(this)) { _loop.Start(); } else { _viewer.ScrollToVerticalOffset(_viewer.ScrollableHeight); }
        }
    }

    private bool Frame(double seconds)
    {
        if (!_stuck) { return false; }
        double distance = _viewer.ScrollableHeight - _viewer.VerticalOffset;
        if (distance <= .5) { _viewer.ScrollToVerticalOffset(_viewer.ScrollableHeight); return false; }
        _viewer.ScrollToVerticalOffset(_viewer.VerticalOffset + distance * (1 - Math.Exp(-Follow * Math.Min(seconds, .05))));
        return true;
    }

    private void UpdateJump()
    {
        bool show = !_stuck && _viewer.ScrollableHeight - _viewer.VerticalOffset > StickWithin;
        if (show == _shown) { return; }
        _shown = show;
        if (show)
        {
            _jump.Visibility = Visibility.Visible;
            if (AiUi.Moving(this)) { _jump.Opacity = 0; Tween.To(_jump, OpacityProperty, 1, .16, ArcEasing.Enter); }
            else { Tween.Set(_jump, OpacityProperty, 1); }
        }
        else if (AiUi.Moving(this)) { Tween.To(_jump, OpacityProperty, 0, .1, ArcEasing.Standard, 0, () => { if (!_shown) { _jump.Visibility = Visibility.Collapsed; } }); }
        else { _jump.Visibility = Visibility.Collapsed; }
    }
}
