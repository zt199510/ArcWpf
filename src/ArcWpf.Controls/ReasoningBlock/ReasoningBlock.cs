using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum ReasoningStatus { Thinking, Done, Stopped, Error }
public enum ReasoningVariant { Card, Plain }

public sealed record ReasoningStep(string? Title, string Text);

/// <summary>Splits streamed reasoning into steps: blank lines split paragraphs; **Heading** or ## Heading starts a step. A half-streamed heading never shows raw.</summary>
public static partial class ReasoningParser
{
    [GeneratedRegex(@"^\s*(?:\*\*(?<t>.+?)\*\*|##+\s+(?<t>.+?))\s*$")]
    private static partial Regex Heading();
    [GeneratedRegex(@"^\s*(\*\*?[^*]*\*?|##*)\s*$")]
    private static partial Regex HalfHeading();

    public static IReadOnlyList<ReasoningStep> Parse(string? reasoning)
    {
        var steps = new List<ReasoningStep>();
        if (string.IsNullOrWhiteSpace(reasoning)) { return steps; }
        string? title = null;
        var text = new StringBuilder();
        void Flush()
        {
            if (title is null && text.Length == 0) { return; }
            steps.Add(new ReasoningStep(title, text.ToString().Trim()));
            title = null;
            text.Clear();
        }
        string[] lines = reasoning.Replace("\r\n", "\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (string.IsNullOrWhiteSpace(line)) { if (text.Length > 0) { Flush(); } continue; }
            if (Heading().Match(line) is { Success: true } heading) { Flush(); title = heading.Groups["t"].Value.Trim(); continue; }
            // The stream's tail may be a heading still arriving: hold it back rather than print its markers.
            if (index == lines.Length - 1 && HalfHeading().IsMatch(line)) { continue; }
            if (text.Length > 0) { text.Append(' '); }
            text.Append(line.Trim());
        }
        Flush();
        return steps;
    }
}

/// <summary>
/// The collapsible thinking block of an AI reply. While the model thinks a shimmering label counts the seconds and the latest
/// reasoning streams beneath it; when it finishes it folds to "Thought for 7s" and opens to the full reasoning as calm steps.
/// </summary>
public class ReasoningBlock : Control
{
    public static readonly DependencyProperty ReasoningProperty = Prop(nameof(Reasoning), typeof(string), null);
    public static readonly DependencyProperty StepsProperty = Prop(nameof(Steps), typeof(IReadOnlyList<ReasoningStep>), null);
    public static readonly DependencyProperty StatusProperty = Prop(nameof(Status), typeof(ReasoningStatus), ReasoningStatus.Done);
    public static readonly DependencyProperty SecondsProperty = Prop(nameof(Seconds), typeof(double?), null);
    public static readonly DependencyProperty ModelNameProperty = Prop(nameof(ModelName), typeof(string), "Assistant");
    public static readonly DependencyProperty ModelMarkProperty = Prop(nameof(ModelMark), typeof(AiMark), null);
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ReasoningBlock), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ReasoningBlock)d).Sync()));
    public static readonly DependencyProperty CanStopProperty = Prop(nameof(CanStop), typeof(bool), false);
    public static readonly DependencyProperty CanRetryProperty = Prop(nameof(CanRetry), typeof(bool), false);
    public static readonly DependencyProperty ErrorProperty = Prop(nameof(Error), typeof(string), null);
    public static readonly DependencyProperty VariantProperty = Prop(nameof(Variant), typeof(ReasoningVariant), ReasoningVariant.Card);

    private static DependencyProperty Prop(string name, Type type, object? fallback) => DependencyProperty.Register(name, type, typeof(ReasoningBlock), new FrameworkPropertyMetadata(fallback, (d, _) => ((ReasoningBlock)d).Sync()));

    /// <summary>The reasoning so far. Pass the growing string while the model streams.</summary>
    public string? Reasoning { get => (string?)GetValue(ReasoningProperty); set => SetValue(ReasoningProperty, value); }
    /// <summary>Ready-made steps; they win over Reasoning.</summary>
    public IReadOnlyList<ReasoningStep>? Steps { get => (IReadOnlyList<ReasoningStep>?)GetValue(StepsProperty); set => SetValue(StepsProperty, value); }
    public ReasoningStatus Status { get => (ReasoningStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    /// <summary>Seconds spent thinking. Left out, the block measures from the moment Status turns Thinking and freezes when it leaves.</summary>
    public double? Seconds { get => (double?)GetValue(SecondsProperty); set => SetValue(SecondsProperty, value); }
    public string ModelName { get => (string)GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }
    public AiMark? ModelMark { get => (AiMark?)GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    /// <summary>Shows a compact Stop action while thinking.</summary>
    public bool CanStop { get => (bool)GetValue(CanStopProperty); set => SetValue(CanStopProperty, value); }
    /// <summary>Shows Retry in the header when Status is Error.</summary>
    public bool CanRetry { get => (bool)GetValue(CanRetryProperty); set => SetValue(CanRetryProperty, value); }
    /// <summary>The reason shown at the end of the reasoning when Status is Error.</summary>
    public string? Error { get => (string?)GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }
    public ReasoningVariant Variant { get => (ReasoningVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    public event EventHandler? StopRequested;
    public event EventHandler? RetryRequested;

    private const double ChunkFadeSeconds = .14;

    private Border? _frame;
    private ArcChromeButton? _toggle;
    private Decorator? _mark;
    private AiMark? _shownMark;
    private readonly FadeSwap _label = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Decorator _actions = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 10, 0) };
    private readonly TurningChevron _chevron = new();
    private SpringHeightBox? _liveClip, _panelClip;
    private readonly TextBlock _live = AiUi.Wrapped("", 14, 20, "TextMuted");
    private readonly TranslateTransform _liveGlide = new();
    private readonly StackPanel _steps = new() { Margin = new Thickness(16, 4, 16, 0) };
    private readonly Grid _foot = new() { Margin = new Thickness(16, 12, 16, 16), Height = 28 };
    private readonly Decorator _footIcon = new() { Width = 20, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _footText = AiUi.Wrapped("", 12, 20, "TextSecondary");
    private readonly CopyAction _copy;
    private readonly List<StepView> _stepViews = [];
    private string _liveText = "";
    private readonly DispatcherTimer _clock = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(250) };
    private DateTime _since;
    private double _measured;
    private bool _hasMeasure, _built;
    private ReasoningStatus? _lastStatus;
    private TextBlock? _counter;
    private string _labelKey = "";

    private sealed record StepView(Grid Root, TextBlock Title, TextBlock Body, Rectangle Rail) { public string Shown { get; set; } = ""; }

    static ReasoningBlock() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ReasoningBlock), new FrameworkPropertyMetadata(typeof(ReasoningBlock)));

    public ReasoningBlock()
    {
        _copy = new CopyAction(FullText, "Copy reasoning") { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, -6, 0) };
        _clock.Tick += (_, _) => UpdateCounter();
        Unloaded += (_, _) => _clock.Stop();
        Loaded += (_, _) => Sync();
        _live.RenderTransform = _liveGlide;
        _live.VerticalAlignment = VerticalAlignment.Bottom;
    }

    private IReadOnlyList<ReasoningStep> Parsed => Steps ?? ReasoningParser.Parse(Reasoning);
    private string FullText() => string.Join("\n\n", Parsed.Select(step => step.Title is null ? step.Text : step.Title + "\n" + step.Text));

    private double Elapsed => Seconds ?? (Status == ReasoningStatus.Thinking && _hasMeasure ? (ClockNow - _since).TotalSeconds : _measured);
    internal Func<DateTime>? ClockForTest { get; set; }
    private DateTime ClockNow => ClockForTest?.Invoke() ?? DateTime.UtcNow;

    internal string LabelForTest => _labelKey;
    internal string CounterForTest => _counter?.Text ?? "";
    internal string LiveForTest => _liveText;
    internal bool LiveShownForTest => _liveClip?.Visibility == Visibility.Visible;
    internal bool PanelShownForTest => _panelClip?.Visibility == Visibility.Visible;
    internal int StepCountForTest => _stepViews.Count(view => view.Root.Visibility == Visibility.Visible);
    internal string StepTextForTest(int index) => new System.Windows.Documents.TextRange(_stepViews[index].Body.ContentStart, _stepViews[index].Body.ContentEnd).Text;
    internal string? StepTitleForTest(int index) => _stepViews[index].Title.Visibility == Visibility.Visible ? _stepViews[index].Title.Text : null;
    internal string FootForTest => _footText.Text;
    internal bool FootShownForTest => _foot.Visibility == Visibility.Visible;
    internal double FrameHeightForTest => _frame?.ActualHeight ?? 0;
    internal FrameworkElement? ToggleForTest => _toggle;
    internal System.Windows.Controls.Button? ActionForTest => _actions.Child as System.Windows.Controls.Button;
    internal void TickForTest() => UpdateCounter();
    internal CopyAction CopyForTest => _copy;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host) { return; }
        var head = new Grid { Height = 44 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        _mark = new Decorator { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        head.Children.Add(_mark);
        Grid.SetColumn(_label, 1);
        head.Children.Add(_label);
        Grid.SetColumn(_actions, 2);
        head.Children.Add(_actions);
        _chevron.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(_chevron, 3);
        head.Children.Add(_chevron);

        _toggle = new ToolbarButton(8) { Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Content = head, Roles = _ => (null, null, "Foreground") };
        _toggle.Click += (_, _) => SetCurrentValue(IsOpenProperty, !IsOpen);

        _live.Margin = new Thickness(0);
        var liveInner = new Grid { Height = 44, Margin = new Thickness(46, 0, 16, 0), ClipToBounds = true };
        liveInner.Children.Add(_live);
        _liveClip = new SpringHeightBox(Tween.Snappy) { Child = liveInner, Visibility = Visibility.Collapsed, Margin = new Thickness(0, -12, 0, 0), IsHitTestVisible = false };

        _foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _foot.ColumnDefinitions.Add(new ColumnDefinition());
        _foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _foot.Children.Add(_footIcon);
        _footText.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_footText, 1);
        _foot.Children.Add(_footText);
        Grid.SetColumn(_copy, 2);
        _foot.Children.Add(_copy);
        var panel = new StackPanel();
        panel.Children.Add(_steps);
        panel.Children.Add(_foot);
        _panelClip = new SpringHeightBox(Tween.Snappy) { Child = panel, Visibility = Visibility.Collapsed };

        var column = new StackPanel();
        column.Children.Add(_toggle);
        column.Children.Add(_liveClip);
        column.Children.Add(_panelClip);
        _frame = new Border { Child = column };
        host.Child = _frame;
        _built = true;
        Sync();
    }

    private void Sync()
    {
        if (!_built || _frame is null || _toggle is null || _mark is null) { return; }
        bool card = Variant == ReasoningVariant.Card;
        _frame.BorderThickness = new Thickness(card ? 1 : 0);
        _frame.CornerRadius = new CornerRadius(18);
        if (card) { _frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface"); _frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border"); }
        else { _frame.Background = null; _frame.BorderBrush = null; }
        if (_shownMark != ModelMark || _mark.Child is null)
        {
            _shownMark = ModelMark;
            _mark.Child = ModelMark is null ? AiUi.Glyph(Lucide.Sparkles, 18, "TextSecondary") : AiUi.Mark(ModelMark, 18, ModelName);
        }

        var status = Status;
        if (status != _lastStatus)
        {
            bool wasThinking = _lastStatus == ReasoningStatus.Thinking;
            if (status == ReasoningStatus.Thinking) { _since = ClockNow; _hasMeasure = true; if (IsLoaded) { _clock.Start(); } }
            else if (wasThinking) { _measured = (ClockNow - _since).TotalSeconds; _hasMeasure = true; _clock.Stop(); }
            if (_lastStatus is not null) { Announce(status); }
            _lastStatus = status;
        }
        if (status == ReasoningStatus.Thinking && IsLoaded && !_clock.IsEnabled) { _clock.Start(); }

        Label();
        Actions();
        Foot();
        Fill();
        Live();
        Panel();
        _chevron.Point(IsOpen);
        AutomationProperties.SetName(_toggle, _labelKey);
    }

    private void Announce(ReasoningStatus status) => Announcer.Say(this, status switch
    {
        ReasoningStatus.Thinking => "Thinking",
        ReasoningStatus.Done => Elapsed > 0 || Seconds is not null ? $"Thought for {Math.Round(Elapsed):0} seconds" : "Thought process",
        ReasoningStatus.Stopped => "Stopped",
        _ => "Thinking interrupted",
    });

    private void Label()
    {
        string key = Status switch
        {
            ReasoningStatus.Thinking => "Thinking",
            ReasoningStatus.Stopped => HasTime ? $"Stopped after {AiUi.Seconds(Elapsed)}" : "Stopped",
            ReasoningStatus.Error => "Thinking interrupted",
            _ => HasTime ? $"Thought for {AiUi.Seconds(Elapsed)}" : "Thought process",
        };
        if (key == _labelKey) { return; }
        _labelKey = key;
        _counter = null;
        FrameworkElement view;
        if (Status == ReasoningStatus.Thinking)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var shimmer = new TextShimmer { Text = key, IsActive = true, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            _counter = AiUi.Line(AiUi.Seconds(Elapsed), 14, 20, "TextMuted", false, false);
            _counter.Margin = new Thickness(6, 0, 0, 0);
            row.Children.Add(shimmer);
            row.Children.Add(_counter);
            view = row;
        }
        else if (Status == ReasoningStatus.Error)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var alert = AiUi.Glyph(Lucide.CircleAlert, 16, "ArcDanger");
            alert.Margin = new Thickness(0, 0, 6, 0);
            row.Children.Add(alert);
            row.Children.Add(AiUi.Line(key, 14, 20));
            view = row;
        }
        else { view = AiUi.Line(key, 14, 20, "TextSecondary"); }
        _label.Set(view);
    }

    private bool HasTime => Seconds is not null || _hasMeasure;

    private void UpdateCounter() { if (_counter is not null) { _counter.Text = AiUi.Seconds(Elapsed); } }

    private void Actions()
    {
        bool stop = Status == ReasoningStatus.Thinking && CanStop, retry = Status == ReasoningStatus.Error && CanRetry;
        string want = stop ? "stop" : retry ? "retry" : "";
        if ((_actions.Tag as string ?? "") == want) { return; }
        _actions.Tag = want;
        if (want.Length == 0) { _actions.Child = null; return; }
        ToolbarButton action;
        if (stop)
        {
            action = new ToolbarButton(14) { Height = 28, Padding = new Thickness(10, 0, 11, 0), Roles = self => (self.IsMouseOver ? "SurfaceMuted" : null, null, self.IsMouseOver ? "Foreground" : "TextSecondary") };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var square = new Rectangle { Width = 8, Height = 8, RadiusX = 1.5, RadiusY = 1.5, Margin = new Thickness(0, 0, 6, 0) };
            square.SetBinding(Shape.FillProperty, new System.Windows.Data.Binding(nameof(Foreground)) { Source = action });
            row.Children.Add(square);
            var text = new TextBlock { Text = "Stop", FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(text);
            action.Content = row;
            action.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            action = AiUi.Outline("Retry");
            action.Click += (_, _) => RetryRequested?.Invoke(this, EventArgs.Empty);
        }
        action.Margin = new Thickness(0, 0, 0, 0);
        _actions.Child = action;
    }

    private void Foot()
    {
        bool shown = Status != ReasoningStatus.Thinking;
        _foot.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        (string glyph, string role, string text) = Status switch
        {
            ReasoningStatus.Stopped => (Lucide.Square, "TextSecondary", "Stopped"),
            ReasoningStatus.Error => (Lucide.CircleAlert, "ArcDanger", string.IsNullOrEmpty(Error) ? "Thinking interrupted" : Error!),
            _ => (Lucide.Check, "Success", "Done"),
        };
        _footText.Text = text;
        _footIcon.Child = AiUi.Glyph(glyph, 14, role);
        _copy.Visibility = Parsed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // Only what streams in while open fades on its own: steps keep their views, and a body grows by appended runs.
    private void Fill()
    {
        var steps = Parsed;
        if (!IsOpen && _stepViews.Count == 0) { return; }
        bool live = Status == ReasoningStatus.Thinking && IsOpen;
        for (int index = 0; index < steps.Count; index++)
        {
            if (index >= _stepViews.Count) { _stepViews.Add(NewStep()); _steps.Children.Add(_stepViews[index].Root); }
            var view = _stepViews[index];
            var step = steps[index];
            view.Root.Visibility = Visibility.Visible;
            view.Title.Text = step.Title ?? "";
            view.Title.Visibility = step.Title is null ? Visibility.Collapsed : Visibility.Visible;
            view.Root.Margin = new Thickness(0, index == 0 ? 0 : 16, 0, 0);
            view.Rail.Visibility = index == steps.Count - 1 ? Visibility.Collapsed : Visibility.Visible;
            if (step.Text == view.Shown) { continue; }
            if (live && step.Text.StartsWith(view.Shown, StringComparison.Ordinal) && view.Shown.Length > 0)
            {
                ChunkFade.Add(view.Body.Inlines, step.Text[view.Shown.Length..], "TextSecondary", this, ChunkFadeSeconds);
            }
            else
            {
                view.Body.Inlines.Clear();
                view.Body.Text = step.Text;
            }
            view.Shown = step.Text;
        }
        for (int index = steps.Count; index < _stepViews.Count; index++) { _stepViews[index].Root.Visibility = Visibility.Collapsed; }
    }

    private StepView NewStep()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        var rail = new Rectangle { Width = 1, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(10.5, 18, 0, -20) };
        rail.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Border");
        root.Children.Add(rail);
        var dot = new Ellipse { Width = 7, Height = 7, StrokeThickness = 1.5, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(7, 7, 0, 0) };
        dot.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Surface");
        dot.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.TextMuted");
        root.Children.Add(dot);
        var column = new StackPanel();
        var title = ArcText.Block("", 14, 15.4, "Foreground", true);
        title.Margin = new Thickness(0, 0, 0, 2);
        var body = AiUi.Wrapped("", 14, 22, "TextSecondary");
        column.Children.Add(title);
        column.Children.Add(body);
        Grid.SetColumn(column, 1);
        root.Children.Add(column);
        return new StepView(root, title, body, rail);
    }

    // The newest paragraph in two lines, anchored to its last line; a wrap glides it up.
    private void Live()
    {
        if (_liveClip is null) { return; }
        bool shown = Status == ReasoningStatus.Thinking && !IsOpen;
        if (shown != (_liveClip.Visibility == Visibility.Visible))
        {
            if (shown) { _liveClip.Visibility = Visibility.Visible; if (AiUi.Moving(this)) { _liveClip.Reveal(); } }
            else if (AiUi.Moving(this)) { _liveClip.Collapse(() => _liveClip.Visibility = Visibility.Collapsed, true); }
            else { _liveClip.Visibility = Visibility.Collapsed; }
        }
        if (!shown) { return; }
        var steps = Parsed;
        string text = steps.Count > 0 ? steps[^1].Text : "";
        if (text == _liveText) { return; }
        double before = _live.ActualHeight;
        if (_liveText.Length > 0 && text.StartsWith(_liveText, StringComparison.Ordinal))
        {
            ChunkFade.Add(_live.Inlines, text[_liveText.Length..], "TextMuted", this, ChunkFadeSeconds);
        }
        else { _live.Inlines.Clear(); _live.Text = text; }
        _liveText = text;
        if (!AiUi.Moving(this)) { return; }
        _live.Dispatcher.BeginInvoke(() =>
        {
            double grew = _live.ActualHeight - before;
            if (before <= 0 || grew < 1) { return; }
            _liveGlide.Y = grew;
            Tween.Spring(_liveGlide, TranslateTransform.YProperty, 0, .24);
        }, DispatcherPriority.Loaded);
    }

    private void Panel()
    {
        if (_panelClip is null) { return; }
        if (IsOpen == (_panelClip.Visibility == Visibility.Visible && !_panelClip.IsCollapsing)) { return; }
        if (IsOpen)
        {
            if (_panelClip.IsCollapsing) { _panelClip.Cancel(); return; }
            _panelClip.Visibility = Visibility.Visible;
            if (AiUi.Moving(this))
            {
                _panelClip.Reveal();
                _panelClip.Opacity = 0;
                Tween.To(_panelClip, OpacityProperty, 1, .16, ArcEasing.Standard);
            }
            return;
        }
        if (!AiUi.Moving(this)) { _panelClip.Visibility = Visibility.Collapsed; return; }
        Tween.To(_panelClip, OpacityProperty, 0, .1, ArcEasing.Standard);
        _panelClip.Collapse(() => { _panelClip.Visibility = Visibility.Collapsed; Tween.Set(_panelClip, OpacityProperty, 1); }, true);
    }
}
