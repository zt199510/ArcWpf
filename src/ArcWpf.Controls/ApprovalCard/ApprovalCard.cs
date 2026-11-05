using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum ApprovalRisk { Low, Medium, High }

public enum ApprovalStatus { Pending, Sending, Failed, Approved, Denied, AllowedByRule, NotSent }

public enum ApprovalOutcome { Approved, Denied, AllowedForAgent, AllowedForWorkspace }

public sealed class ApprovalArgument(string label, string value)
{
    public string Label { get; } = label;
    public string Value { get; set; } = value;
    public bool IsEditable { get; init; } = true;
    public bool IsMono { get; init; } = true;
    public bool IsMultiline { get; init; }
}

public sealed class ApprovalRequest
{
    public string AgentName { get; init; } = "";
    public string? ModelName { get; init; }
    public AiMark? ModelMark { get; init; }
    /// <summary>How long ago the agent asked: "2 min ago".</summary>
    public string? Asked { get; init; }
    public AiMark? ToolMark { get; init; }
    /// <summary>The call in plain words. Backticks set code in the mono face: "Run `vercel deploy --prod`".</summary>
    public string Title { get; init; } = "";
    public string? Reason { get; init; }
    public ApprovalRisk Risk { get; init; } = ApprovalRisk.Medium;
    public IReadOnlyList<ApprovalArgument> Arguments { get; init; } = [];
}

/// <param name="Arguments">The values with the person's edits applied.</param>
public sealed record ApprovalDecision(ApprovalRequest Request, ApprovalOutcome Outcome, IReadOnlyDictionary<string, string> Arguments, bool Edited);

/// <summary>
/// A permission prompt before a tool call runs: who asks and why, the arguments with click-to-edit values, the risk, and Deny, Always allow or Approve.
/// Arc's single request, without the block's queue, rules popover and log. A failed send offers Retry; a denied or unsent call can be requested again.
/// </summary>
[TemplatePart(Name = "PART_Root", Type = typeof(Decorator))]
public partial class ApprovalCard : Control
{
    public static readonly DependencyProperty RequestProperty = DependencyProperty.Register(nameof(Request), typeof(ApprovalRequest), typeof(ApprovalCard), new FrameworkPropertyMetadata(null, (d, _) => ((ApprovalCard)d).OnRequest()));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(ApprovalStatus), typeof(ApprovalCard), new FrameworkPropertyMetadata(ApprovalStatus.Pending, (d, _) => ((ApprovalCard)d).Decisions(true)));
    public static readonly DependencyProperty WorkspaceProperty = DependencyProperty.Register(nameof(Workspace), typeof(string), typeof(ApprovalCard), new FrameworkPropertyMetadata("this workspace"));

    public ApprovalRequest? Request { get => (ApprovalRequest?)GetValue(RequestProperty); set => SetValue(RequestProperty, value); }
    public ApprovalStatus Status { get => (ApprovalStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    /// <summary>Names the workspace-wide Always allow option.</summary>
    public string Workspace { get => (string)GetValue(WorkspaceProperty); set => SetValue(WorkspaceProperty, value); }
    /// <summary>Reaches the agent. Throw to show the failure with Retry. Without it a decision settles at once.</summary>
    public Func<ApprovalDecision, Task>? SendDecision { get; set; }

    /// <summary>A decision has settled.</summary>
    public event EventHandler<ApprovalDecision>? Decided;
    /// <summary>The person asked for a denied or unsent call to be raised again. The card is pending once more.</summary>
    public event EventHandler<ApprovalRequest>? RequestedAgain;

    private readonly Border _card = new() { CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1), Padding = new Thickness(20), Focusable = true, FocusVisualStyle = null };
    private readonly SpringHeightBox _height = new(Tween.Smooth);
    private readonly FadeSwap _face = new();
    private readonly FadeSwap _decision = new() { Margin = new Thickness(0, 14, 0, 0) };
    private readonly Popup _popup = new() { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Custom, PopupAnimation = PopupAnimation.None };
    private readonly StackPanel _menuRows = new();
    private readonly List<ArgumentRow> _rows = [];
    private readonly Dictionary<string, string> _original = [];
    private ToolbarButton? _deny, _always, _approve, _retry, _again;
    private ApprovalOutcome _pending;
    private Decorator? _root;
    private int _generation;
    private string? _error;

    internal ToolbarButton? DenyForTest => _deny;
    internal ToolbarButton? AlwaysForTest => _always;
    internal ToolbarButton? ApproveForTest => _approve;
    internal ToolbarButton? RetryForTest => _retry;
    internal ToolbarButton? AgainForTest => _again;
    internal bool MenuOpenForTest => _popup.IsOpen;
    internal FrameworkElement MenuForTest => (FrameworkElement)_popup.Child;
    internal IReadOnlyList<ToolbarButton> MenuRowsForTest => [.. _menuRows.Children.OfType<ToolbarButton>()];
    internal string DecisionTextForTest { get; private set; } = "";
    internal System.Windows.Controls.Button? ValueForTest(string label) => _rows.First(row => row.Argument.Label == label).ValueButton;
    internal TextBox? EditorForTest(string label) => _rows.First(row => row.Argument.Label == label).Editor;
    internal System.Windows.Controls.Button? RestoreForTest(string label) => _rows.First(row => row.Argument.Label == label).Restore;
    internal Border CardForTest => _card;
    internal FrameworkElement DecisionForTest => _decision;

    static ApprovalCard() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ApprovalCard), new FrameworkPropertyMetadata(typeof(ApprovalCard)));

    public ApprovalCard()
    {
        _card.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _card.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        // The clip stands 4 past the content, so focus rings on the edge buttons are not cut.
        _height.Margin = new Thickness(-4);
        _face.Margin = new Thickness(4);
        _height.Child = _face;
        _card.Child = _height;
        // Shortcuts answer anywhere in the card, including after a click left focus on nothing in particular.
        _card.MouseDown += (_, e) => { if (ReferenceEquals(e.OriginalSource, _card) || e.OriginalSource is TextBlock) { _card.Focus(); } };
        PreviewKeyDown += OnKey;
        BuildMenu();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_root is not null) { _root.Child = null; }
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _root.Child = _card;
        OnRequest();
    }

    private void OnRequest()
    {
        if (_root is null) { return; }
        _generation++;
        _rows.Clear();
        _original.Clear();
        _error = null;
        _popup.IsOpen = false;
        if (_decision.Parent is Panel holder) { holder.Children.Remove(_decision); }
        if (Request is not { } request) { _face.Clear(); return; }
        foreach (var argument in request.Arguments) { _original[argument.Label] = argument.Value; }
        var body = Build(request);
        // Bringing a request in is one crossfade while the height follows.
        if (_face.Current is not null && AiUi.Moving(this))
        {
            _height.MorphNext();
            _face.Set(body, 3, .2);
        }
        else
        {
            _face.Clear();
            _face.Set(body);
        }
        if (Status != ApprovalStatus.Pending) { SetCurrentValue(StatusProperty, ApprovalStatus.Pending); }
        Decisions(false);
    }

    private StackPanel Build(ApprovalRequest request)
    {
        var column = new StackPanel();
        AutomationProperties.SetName(column, request.Title.Replace("`", ""));
        column.Children.Add(BuildMeta(request));
        var title = TitleLine(request);
        title.Margin = new Thickness(0, 12, 0, 0);
        column.Children.Add(title);
        if (!string.IsNullOrEmpty(request.Reason))
        {
            var reason = AiUi.Wrapped(request.Reason, 14, 19.6, "TextSecondary");
            reason.Margin = new Thickness(0, 6, 0, 0);
            column.Children.Add(reason);
        }
        if (request.Arguments.Count > 0) { column.Children.Add(BuildArguments(request)); }
        column.Children.Add(_decision);
        return column;
    }

    private Grid BuildMeta(ApprovalRequest request)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var who = new StackPanel { Orientation = Orientation.Horizontal };
        var mark = AiUi.Mark(request.ModelMark, 16, request.AgentName);
        mark.Margin = new Thickness(0, 0, 6, 0);
        who.Children.Add(mark);
        who.Children.Add(AiUi.Line(request.AgentName, 14, 19.6, "Foreground", false, false));
        var rest = string.Join("  ·  ", new[] { request.ModelName, request.Asked }.Where(part => !string.IsNullOrEmpty(part)));
        if (rest.Length > 0)
        {
            var quiet = AiUi.Line(rest, 14, 19.6, "TextMuted");
            quiet.Margin = new Thickness(7, 0, 0, 0);
            who.Children.Add(quiet);
        }
        grid.Children.Add(who);
        var (glyph, word, role) = RiskLook(request.Risk);
        var risk = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0) };
        var shield = AiUi.Glyph(glyph, 16, role);
        shield.Margin = new Thickness(0, 0, 4, 0);
        risk.Children.Add(shield);
        risk.Children.Add(AiUi.Line(word + " risk", 14, 19.6, "TextSecondary", false, false));
        AutomationProperties.SetName(risk, word + " risk");
        Grid.SetColumn(risk, 1);
        grid.Children.Add(risk);
        return grid;
    }

    internal static (string Glyph, string Word, string Role) RiskLook(ApprovalRisk risk) => risk switch
    {
        ApprovalRisk.Low => (Lucide.ShieldCheck, "Low", "Success"),
        ApprovalRisk.High => (Lucide.ShieldX, "High", "Danger"),
        _ => (Lucide.ShieldAlert, "Medium", "Warning"),
    };

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex Backticks();

    private static Grid TitleLine(ApprovalRequest request)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        if (request.ToolMark is { } tool)
        {
            var mark = AiUi.Mark(tool, 22, "");
            mark.Margin = new Thickness(0, 1, 8, 0);
            mark.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(mark);
        }
        var text = AiUi.Wrapped("", 22, 26.4, "Foreground", true);
        int at = 0;
        foreach (Match match in Backticks().Matches(request.Title))
        {
            if (match.Index > at) { text.Inlines.Add(new Run(request.Title[at..match.Index])); }
            var code = new Run(match.Groups[1].Value) { FontSize = 20.24 };
            code.SetResourceReference(TextElement.FontFamilyProperty, "ArcWpf.Font.Mono");
            text.Inlines.Add(code);
            at = match.Index + match.Length;
        }
        if (at < request.Title.Length) { text.Inlines.Add(new Run(request.Title[at..])); }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private Border BuildArguments(ApprovalRequest request)
    {
        var list = new StackPanel();
        foreach (var argument in request.Arguments)
        {
            var row = new ArgumentRow(this, argument, _original[argument.Label]);
            _rows.Add(row);
            list.Children.Add(row.Root);
        }
        var frame = new Border { Margin = new Thickness(0, 16, 0, 0), Padding = new Thickness(6), CornerRadius = new CornerRadius(16), Child = list };
        frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        return frame;
    }

    private void Decisions(bool animate)
    {
        if (_root is null || Request is null) { return; }
        _deny = _always = _approve = _retry = _again = null;
        foreach (var row in _rows) { row.SetLocked(Status != ApprovalStatus.Pending); }
        FrameworkElement view = Status switch
        {
            ApprovalStatus.Pending => PendingRow(),
            ApprovalStatus.Sending => SendingRow(),
            ApprovalStatus.Failed => FailedRow(),
            _ => OutcomeRow(),
        };
        if (animate && AiUi.Moving(this)) { _height.MorphNext(); _decision.Set(view); }
        else { _decision.Clear(); _decision.Set(view); }
    }

    private Grid PendingRow()
    {
        DecisionTextForTest = "";
        var grid = new Grid { Height = 36 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _deny = Quiet("Deny", true);
        _deny.HorizontalAlignment = HorizontalAlignment.Left;
        _deny.Click += (_, _) => Decide(ApprovalOutcome.Denied);
        grid.Children.Add(_deny);
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        _always = BuildAlways();
        right.Children.Add(_always);
        _approve = BuildApprove();
        _approve.Margin = new Thickness(6, 0, 0, 0);
        right.Children.Add(_approve);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private static ToolbarButton Quiet(string text, bool medium)
    {
        var button = new ToolbarButton(18) { Height = 36, Padding = new Thickness(13, 0, 13, 0), Roles = self => (self.IsPressed ? "ControlTrack" : self.IsMouseOver ? "SurfaceMuted" : null, null, "TextSecondary") };
        button.Content = AiUi.Line(text, 14, 19.6, "TextSecondary", medium, false);
        AutomationProperties.SetName(button, text);
        ThreadUi.PressScale(button, .96);
        return button;
    }

    private ToolbarButton BuildAlways()
    {
        var button = new ToolbarButton(18) { Height = 36, Padding = new Thickness(13, 0, 10, 0), Roles = self => (self.IsPressed ? "ControlTrack" : self.IsMouseOver || _popup.IsOpen ? "SurfaceMuted" : null, null, "TextSecondary") };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(AiUi.Line("Always allow", 14, 19.6, "TextSecondary", false, false));
        var chevron = new TurningChevron(16, "TextSecondary") { Margin = new Thickness(6, 0, 0, 0) };
        row.Children.Add(chevron);
        button.Content = row;
        AutomationProperties.SetName(button, "Always allow");
        button.Click += (_, _) => OpenMenu(button, chevron);
        button.PreviewKeyDown += (_, e) => { if (e.Key == Key.Down) { OpenMenu(button, chevron); e.Handled = true; } };
        return button;
    }

    private ToolbarButton BuildApprove()
    {
        var button = new ToolbarButton(18) { Height = 36, Padding = new Thickness(13, 0, 9, 0), Roles = self => (self.IsMouseOver ? "AccentStrong" : "Accent", null, "AccentForeground") };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(AiUi.Line("Approve", 14, 19.6, "AccentForeground", true, false));
        var key = new Border { Height = 18, Margin = new Thickness(7, 0, 0, 0), Padding = new Thickness(5, 0, 5, 0), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center, Opacity = 1 };
        void Tint()
        {
            var ink = Tween.Resolve(this, "ArcWpf.Brush.AccentForeground");
            key.Background = new SolidColorBrush(OkColor.Alpha(ink, .12));
            key.BorderBrush = new SolidColorBrush(OkColor.Alpha(ink, .3));
        }
        Tint();
        ThemeWatch.Add(key, Tint);
        var label = AiUi.Line("Enter", 11, 16, "AccentForeground", true, false);
        key.Child = label;
        row.Children.Add(key);
        button.Content = row;
        AutomationProperties.SetName(button, "Approve");
        AutomationProperties.SetAcceleratorKey(button, "Enter");
        button.Click += (_, _) => Decide(ApprovalOutcome.Approved);
        ThreadUi.PressScale(button, .96);
        return button;
    }

    private Grid SendingRow()
    {
        string agent = Request?.AgentName ?? "the agent";
        string text = _pending == ApprovalOutcome.Denied ? $"Sending your denial to {agent}" : $"Sending your approval to {agent}";
        DecisionTextForTest = text;
        var grid = new Grid { Height = 36 };
        grid.Children.Add(new TextShimmer { Text = text, IsActive = true, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        Announcer.Say(this, text);
        return grid;
    }

    private Grid FailedRow()
    {
        string text = string.IsNullOrEmpty(_error) ? "Couldn't reach the agent." : _error;
        DecisionTextForTest = text;
        var grid = new Grid { Height = 36 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var note = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = AiUi.Glyph(Lucide.CircleAlert, 16, "Danger");
        icon.Margin = new Thickness(0, 0, 6, 0);
        note.Children.Add(icon);
        note.Children.Add(AiUi.Line(text, 14, 19.6, "Danger"));
        grid.Children.Add(note);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };
        _deny = Quiet("Deny", true);
        _deny.Click += (_, _) => Decide(ApprovalOutcome.Denied);
        actions.Children.Add(_deny);
        _retry = AiUi.Outline("Retry", Lucide.RotateCcw, 36);
        _retry.Margin = new Thickness(6, 0, 0, 0);
        AutomationProperties.SetName(_retry, "Retry");
        _retry.Click += (_, _) => Decide(_pending);
        actions.Children.Add(_retry);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        Announcer.Say(this, text);
        return grid;
    }

    private Grid OutcomeRow()
    {
        var (glyph, word, role) = Status switch
        {
            ApprovalStatus.Approved => (Lucide.CircleCheck, "Approved", "Success"),
            ApprovalStatus.Denied => (Lucide.Ban, "Denied", "TextSecondary"),
            ApprovalStatus.AllowedByRule => (Lucide.ShieldCheck, "Allowed by rule", "Success"),
            _ => (Lucide.CircleAlert, "Not sent", "TextSecondary"),
        };
        DecisionTextForTest = word;
        var grid = new Grid { Height = 36 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var note = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = AiUi.Glyph(glyph, 16, role);
        icon.Margin = new Thickness(0, 0, 6, 0);
        note.Children.Add(icon);
        note.Children.Add(AiUi.Line(word, 14, 19.6, "Foreground", false, false));
        grid.Children.Add(note);
        if (Status is ApprovalStatus.Denied or ApprovalStatus.NotSent)
        {
            _again = AiUi.Outline("Request again", Lucide.RotateCcw, 36);
            AutomationProperties.SetName(_again, "Request again");
            _again.Click += (_, _) => RequestAgain();
            Grid.SetColumn(_again, 1);
            grid.Children.Add(_again);
        }
        Announcer.Say(this, word);
        return grid;
    }

    /// <summary>Answers the request as if the person had pressed the button. Only a pending or failed card answers.</summary>
    public async void Decide(ApprovalOutcome outcome)
    {
        if (Request is not { } request || Status is not (ApprovalStatus.Pending or ApprovalStatus.Failed)) { return; }
        foreach (var row in _rows) { row.Commit(); }
        _popup.IsOpen = false;
        _pending = outcome;
        int generation = ++_generation;
        bool edited = request.Arguments.Any(argument => argument.Value != _original[argument.Label]);
        var decision = new ApprovalDecision(request, outcome, request.Arguments.ToDictionary(argument => argument.Label, argument => argument.Value), edited);
        if (_card.IsKeyboardFocusWithin) { _card.Focus(); }
        if (SendDecision is { } send)
        {
            SetCurrentValue(StatusProperty, ApprovalStatus.Sending);
            try { await send(decision); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (generation != _generation) { return; }
                _error = ex.Message;
                SetCurrentValue(StatusProperty, ApprovalStatus.Failed);
                return;
            }
            catch (OperationCanceledException)
            {
                if (generation == _generation) { SetCurrentValue(StatusProperty, ApprovalStatus.NotSent); }
                return;
            }
            if (generation != _generation) { return; }
        }
        SetCurrentValue(StatusProperty, outcome switch
        {
            ApprovalOutcome.Approved => ApprovalStatus.Approved,
            ApprovalOutcome.Denied => ApprovalStatus.Denied,
            _ => ApprovalStatus.AllowedByRule,
        });
        Decided?.Invoke(this, decision);
    }

    private void RequestAgain()
    {
        if (Request is not { } request) { return; }
        _generation++;
        _error = null;
        // A new ask starts from what the agent proposed, not from the edits on the answer it did not get.
        foreach (var row in _rows) { row.RestoreOriginal(); }
        SetCurrentValue(StatusProperty, ApprovalStatus.Pending);
        Announcer.Say(this, "Requested again");
        RequestedAgain?.Invoke(this, request);
        _card.Focus();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (Status is not ApprovalStatus.Pending || _popup.IsOpen || Keyboard.Modifiers != ModifierKeys.None) { return; }
        // Typing an edited value never approves anything.
        if (Keyboard.FocusedElement is TextBox) { return; }
        if (e.Key == Key.A) { Decide(ApprovalOutcome.Approved); e.Handled = true; }
        else if (e.Key == Key.D) { Decide(ApprovalOutcome.Denied); e.Handled = true; }
        else if (e.Key == Key.Enter && Keyboard.FocusedElement is not ButtonBase) { Decide(ApprovalOutcome.Approved); e.Handled = true; }
    }

    private void BuildMenu()
    {
        var surface = new Border { Margin = new Thickness(Gutter), Padding = new Thickness(6), CornerRadius = new CornerRadius(26), BorderThickness = new Thickness(1), Child = _menuRows, MinWidth = 234 };
        surface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        surface.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        surface.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 36, ShadowDepth = 14, Direction = 270, Opacity = .11, Color = Colors.Black };
        // No background: the gutter lies over the trigger, and a press there must count as outside.
        var host = new Grid();
        host.Children.Add(surface);
        _popup.Child = host;
        _popup.CustomPopupPlacementCallback = Place;
        _popup.PreviewKeyDown += OnMenuKey;
    }

    private const double Gutter = 24;

    // Right edges meet, 6 below the trigger. The margin sits on the host's child, so the size given here includes the gutter.
    private CustomPopupPlacement[] Place(Size popup, Size target, Point offset)
    {
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        double gutter = Gutter * scale, gap = 6 * scale;
        double x = target.Width - popup.Width + gutter;
        return [new(new Point(x, target.Height + gap - gutter), PopupPrimaryAxis.Horizontal), new(new Point(x, -popup.Height + gutter - gap), PopupPrimaryAxis.Horizontal)];
    }

    private void OpenMenu(ToolbarButton trigger, TurningChevron chevron)
    {
        if (_popup.IsOpen) { _popup.IsOpen = false; return; }
        _menuRows.Children.Clear();
        _menuRows.Children.Add(MenuRow($"For {Request?.AgentName}", Lucide.User, ApprovalOutcome.AllowedForAgent));
        _menuRows.Children.Add(MenuRow($"For any agent in {Workspace}", Lucide.Globe, ApprovalOutcome.AllowedForWorkspace));
        _popup.PlacementTarget = trigger;
        void Opened(object? sender, EventArgs e) { _popup.Opened -= Opened; trigger.IsHitTestVisible = false; chevron.Point(true); trigger.Refresh(true); Dispatcher.BeginInvoke(() => (_menuRows.Children[0] as UIElement)?.Focus(), DispatcherPriority.Input); }
        void Closed(object? sender, EventArgs e)
        {
            _popup.Closed -= Closed;
            chevron.Point(false);
            trigger.Refresh(true);
            // A press on the trigger while open only closes it, instead of closing and reopening.
            Dispatcher.BeginInvoke(() => trigger.IsHitTestVisible = true, DispatcherPriority.Input);
        }
        _popup.Opened += Opened;
        _popup.Closed += Closed;
        _popup.IsOpen = true;
    }

    private ToolbarButton MenuRow(string text, string glyph, ApprovalOutcome outcome)
    {
        var button = new ToolbarButton(18) { Height = 36, Padding = new Thickness(11, 0, 11, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch, Roles = self => (self.IsPressed ? "ControlTrack" : self.IsMouseOver || self.IsKeyboardFocused ? "SurfaceMuted" : null, null, "Foreground") };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = AiUi.Glyph(glyph, 16, "TextSecondary");
        icon.Margin = new Thickness(0, 0, 10, 0);
        row.Children.Add(icon);
        row.Children.Add(AiUi.Line(text, 14, 19.6, "Foreground", false, false));
        button.Content = row;
        button.GotKeyboardFocus += (_, _) => button.Refresh(true);
        button.LostKeyboardFocus += (_, _) => button.Refresh(true);
        AutomationProperties.SetName(button, "Always allow " + text.ToLowerInvariant());
        button.Click += (_, _) => { _popup.IsOpen = false; Decide(outcome); };
        return button;
    }

    private void OnMenuKey(object sender, KeyEventArgs e)
    {
        var rows = _menuRows.Children.OfType<ToolbarButton>().ToList();
        int at = rows.FindIndex(row => row.IsKeyboardFocused);
        if (e.Key == Key.Escape)
        {
            var trigger = _always;
            _popup.IsOpen = false;
            trigger?.Focus();
            e.Handled = true;
        }
        else if (e.Key is Key.Down or Key.Up)
        {
            rows[((at < 0 ? 0 : at) + (e.Key == Key.Down ? 1 : rows.Count - 1)) % rows.Count].Focus();
            e.Handled = true;
        }
    }

    // A row of the arguments list. An editable value is a button that turns into a field in place.
    private sealed class ArgumentRow
    {
        private readonly ApprovalCard _card;
        private readonly string _original;
        private readonly Grid _slot = new();
        private readonly ContentControl _restoreSlot = new() { Focusable = false, Width = 28, Height = 28, Margin = new Thickness(4, 2, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        private bool _locked;

        public ApprovalArgument Argument { get; }
        public Grid Root { get; } = new() { MinHeight = 32 };
        public System.Windows.Controls.Button? ValueButton { get; private set; }
        public TextBox? Editor { get; private set; }
        public System.Windows.Controls.Button? Restore { get; private set; }

        public ArgumentRow(ApprovalCard card, ApprovalArgument argument, string original)
        {
            (_card, Argument, _original) = (card, argument, original);
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            Root.ColumnDefinitions.Add(new ColumnDefinition());
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            var key = AiUi.Line(argument.Label, 14, 19.6, "TextMuted");
            key.Margin = new Thickness(8, 6, 0, 0);
            key.VerticalAlignment = VerticalAlignment.Top;
            Root.Children.Add(key);
            Grid.SetColumn(_slot, 1);
            Root.Children.Add(_slot);
            Grid.SetColumn(_restoreSlot, 2);
            Root.Children.Add(_restoreSlot);
            ShowValue(false);
        }

        public void SetLocked(bool locked)
        {
            if (_locked == locked) { return; }
            _locked = locked;
            Commit();
            ShowValue(false);
        }

        private TextBlock ValueText()
        {
            var text = AiUi.Wrapped(Argument.Value, 13, 20, Argument.IsEditable && !_locked ? "Foreground" : "TextSecondary");
            if (Argument.IsMono) { text.SetResourceReference(TextBlock.FontFamilyProperty, "ArcWpf.Font.Mono"); }
            if (!Argument.IsMultiline) { text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis; }
            return text;
        }

        private void ShowValue(bool focus)
        {
            _slot.Children.Clear();
            Editor = null;
            ValueButton = null;
            bool edited = Argument.Value != _original;
            AutomationProperties.SetName(Root, Argument.Label);
            if (!Argument.IsEditable || _locked)
            {
                var plain = ValueText();
                plain.Margin = new Thickness(9, 6, 9, 6);
                _slot.Children.Add(plain);
            }
            else
            {
                var button = new ToolbarButton(10, 1) { MinHeight = 32, Padding = new Thickness(8, 5, 8, 5), HorizontalContentAlignment = HorizontalAlignment.Left, Content = ValueText(), Roles = self => (self.IsMouseOver ? "SurfaceRaised" : null, self.IsMouseOver ? "Border" : null, "Foreground") };
                AutomationProperties.SetName(button, $"{Argument.Label}: {Argument.Value}{(edited ? ", edited" : "")}. Edit");
                button.Click += (_, _) => ShowEditor();
                ValueButton = button;
                _slot.Children.Add(button);
                if (focus) { button.Focus(); }
            }
            Restore = null;
            _restoreSlot.Content = null;
            if (edited && !_locked)
            {
                var restore = AiUi.Icon(Lucide.RotateCcw, 28, 14, $"Restore {Argument.Label}", 14);
                restore.Click += (_, _) => { Argument.Value = _original; ShowValue(true); };
                Restore = restore;
                _restoreSlot.Content = restore;
            }
        }

        private void ShowEditor()
        {
            _slot.Children.Clear();
            ValueButton = null;
            var box = ThreadUi.PlainTextBox();
            box.Text = Argument.Value;
            box.FontSize = 13;
            box.AcceptsReturn = Argument.IsMultiline;
            box.TextWrapping = Argument.IsMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap;
            box.VerticalContentAlignment = VerticalAlignment.Center;
            if (Argument.IsMono) { box.SetResourceReference(Control.FontFamilyProperty, "ArcWpf.Font.Mono"); }
            AutomationProperties.SetName(box, Argument.Label);
            var frame = new Border { MinHeight = 32, Padding = new Thickness(8, 5, 8, 5), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = box };
            frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
            frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderStrong");
            _slot.Children.Add(frame);
            Editor = box;
            box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { Editor = null; ShowValue(true); e.Handled = true; }
                else if (e.Key == Key.Enter && (!Argument.IsMultiline || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))) { Commit(); ShowValue(true); e.Handled = true; }
            };
            box.LostKeyboardFocus += (_, _) => { if (ReferenceEquals(Editor, box)) { Commit(); ShowValue(false); } };
            box.Focus();
            box.SelectAll();
            _card.Dispatcher.BeginInvoke(() => { if (ReferenceEquals(Editor, box)) { box.Focus(); box.SelectAll(); } }, DispatcherPriority.Input);
        }

        public void Commit()
        {
            if (Editor is { } box) { Argument.Value = box.Text; Editor = null; }
        }

        public void RestoreOriginal()
        {
            Editor = null;
            Argument.Value = _original;
            ShowValue(false);
        }
    }
}
