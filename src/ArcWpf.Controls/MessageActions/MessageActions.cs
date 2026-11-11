using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum MessageRating { None, Up, Down }

public sealed record MessageFeedback(MessageRating Rating, IReadOnlyList<string> Reasons, string Note);

/// <summary>
/// The action row under a message: copy, regenerate or stop, thumbs up and down, and quote. A thumb opens Arc's follow-up panel,
/// which asks why, then thanks and folds away. While a reply generates the regenerate slot turns into Stop and the thumbs rest.
/// </summary>
[TemplatePart(Name = "PART_Root", Type = typeof(Decorator))]
public class MessageActions : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(MessageActions), new FrameworkPropertyMetadata(null, (d, _) => ((MessageActions)d).Chrome()));
    public static readonly DependencyProperty IsGeneratingProperty = Prop(nameof(IsGenerating), false);
    public static readonly DependencyProperty CanRegenerateProperty = Prop(nameof(CanRegenerate), true);
    public static readonly DependencyProperty CanQuoteProperty = Prop(nameof(CanQuote), true);
    public static readonly DependencyProperty CanRateProperty = Prop(nameof(CanRate), true);
    public static readonly DependencyProperty AsksWhyProperty = Prop(nameof(AsksWhy), true);
    public static readonly DependencyProperty RatingProperty = DependencyProperty.Register(nameof(Rating), typeof(MessageRating), typeof(MessageActions), new FrameworkPropertyMetadata(MessageRating.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((MessageActions)d).OnRatingChanged()));

    private static DependencyProperty Prop(string name, bool fallback) => DependencyProperty.Register(name, typeof(bool), typeof(MessageActions), new FrameworkPropertyMetadata(fallback, (d, _) => ((MessageActions)d).Chrome()));

    /// <summary>What Copy puts on the clipboard. Without text there is no Copy.</summary>
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsGenerating { get => (bool)GetValue(IsGeneratingProperty); set => SetValue(IsGeneratingProperty, value); }
    public bool CanRegenerate { get => (bool)GetValue(CanRegenerateProperty); set => SetValue(CanRegenerateProperty, value); }
    public bool CanQuote { get => (bool)GetValue(CanQuoteProperty); set => SetValue(CanQuoteProperty, value); }
    public bool CanRate { get => (bool)GetValue(CanRateProperty); set => SetValue(CanRateProperty, value); }
    /// <summary>A thumb opens the follow-up panel.</summary>
    public bool AsksWhy { get => (bool)GetValue(AsksWhyProperty); set => SetValue(AsksWhyProperty, value); }
    public MessageRating Rating { get => (MessageRating)GetValue(RatingProperty); set => SetValue(RatingProperty, value); }
    public IReadOnlyList<string> NegativeReasons { get; set; } = ["Inaccurate", "Not helpful", "Too long", "Unsafe", "Other"];
    public IReadOnlyList<string> PositiveReasons { get; set; } = ["Accurate", "Easy to follow", "Good examples", "Saved me time"];
    public string PrivacyNote { get; set; } = "Shares this reply for review";
    public string ThanksMessage { get; set; } = "It helps improve future replies.";
    public TimeSpan ThanksDuration { get; set; } = TimeSpan.FromMilliseconds(1600);
    /// <summary>Saves the reasons and note. Throw to show the error with Try again.</summary>
    public Func<MessageFeedback, Task>? SubmitFeedback { get; set; }

    public event EventHandler? RegenerateRequested;
    public event EventHandler? StopRequested;
    /// <summary>Carries <see cref="Text"/>, for the composer to quote.</summary>
    public event EventHandler<string>? QuoteRequested;
    public event EventHandler<MessageRating>? RatingChanged;
    public event EventHandler<MessageFeedback>? FeedbackSubmitted;

    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal };
    private readonly CopyAction _copy;
    private readonly FadeSwap _action = new() { Width = 36, Height = 36, Margin = new Thickness(0, 0, 2, 0) };
    private readonly ToolbarButton _regenerate, _stop, _quote;
    private readonly Thumb _up, _down;
    private readonly SpringHeightBox _panelBox = new(Tween.Standard);
    private readonly FadeSwap _panelFace = new();
    private readonly Decorator _panelHolder = new();
    private readonly DispatcherTimer _thanks = new(DispatcherPriority.Normal);
    private readonly HashSet<string> _picked = [];
    private readonly List<ToolbarButton> _chips = [];
    private ToolbarButton? _submit;
    private TextBox? _note;
    private TextBlock? _placeholder, _error, _privacy;
    private Border? _panelFrame;
    private Decorator? _root;
    private bool _sending, _panelOpen;
    private int _generation;

    internal Action<string>? WriteForTest { get => _copy.WriteForTest; set => _copy.WriteForTest = value; }
    internal CopyAction CopyForTest => _copy;
    internal ToolbarButton RegenerateForTest => _regenerate;
    internal ToolbarButton StopForTest => _stop;
    internal ToolbarButton QuoteForTest => _quote;
    internal System.Windows.Controls.Button UpForTest => _up.Button;
    internal System.Windows.Controls.Button DownForTest => _down.Button;
    internal bool ShowsStopForTest => ReferenceEquals((_action.Current as Decorator)?.Child, _stop);
    internal bool PanelOpenForTest => _panelOpen;
    internal string PanelKindForTest { get; private set; } = "";
    internal IReadOnlyList<ToolbarButton> ChipsForTest => _chips;
    internal ToolbarButton? SubmitForTest => _submit;
    internal TextBox? NoteForTest => _note;
    internal string SubmitLabelForTest => ((_submit?.Content as FadeSwap)?.Current as TextBlock)?.Text ?? (_submit?.Content as TextBlock)?.Text ?? "";
    internal string? ErrorForTest => _error is { Visibility: Visibility.Visible } ? _error.Text : null;
    internal double FilledOpacityForTest(bool up) => (up ? _up : _down).Filled.Opacity;
    internal FrameworkElement PanelForTest => _panelBox;
    internal void ExpireThanksForTest() => Fold();

    static MessageActions() => DefaultStyleKeyProperty.OverrideMetadata(typeof(MessageActions), new FrameworkPropertyMetadata(typeof(MessageActions)));

    public MessageActions()
    {
        _copy = new CopyAction(() => Text ?? "", "Copy", 36, 18) { Margin = new Thickness(0, 0, 2, 0) };
        _regenerate = AiUi.Icon(Lucide.RotateCcw, 36, 16, "Regenerate", 18);
        _regenerate.Click += (_, _) => RegenerateRequested?.Invoke(this, EventArgs.Empty);
        _stop = AiUi.Icon(Lucide.Square, 36, 16, "Stop", 18);
        _stop.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
        _quote = AiUi.Icon(Lucide.TextQuote, 36, 16, "Quote", 18);
        _quote.Click += (_, _) => QuoteRequested?.Invoke(this, Text ?? "");
        _up = new Thumb(this, Lucide.ThumbsUp, MessageRating.Up, "Good response");
        _down = new Thumb(this, Lucide.ThumbsDown, MessageRating.Down, "Bad response");
        foreach (var button in new[] { _regenerate, _stop, _quote }) { ThreadUi.PressScale(button, .94); }
        _up.Button.Margin = _down.Button.Margin = new Thickness(0, 0, 2, 0);
        _row.Children.Add(_copy);
        _row.Children.Add(_action);
        _row.Children.Add(_up.Button);
        _row.Children.Add(_down.Button);
        _row.Children.Add(_quote);
        _panelBox.Child = _panelHolder;
        _thanks.Tick += (_, _) => { _thanks.Stop(); Fold(); };
        Unloaded += (_, _) => _thanks.Stop();
        PreviewKeyDown += OnKey;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_root is not null) { _root.Child = null; }
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        var column = _row.Parent as StackPanel;
        if (column is null) { column = new StackPanel(); column.Children.Add(_row); column.Children.Add(_panelBox); }
        _root.Child = column;
        Chrome();
        ShowRating(false);
    }

    private void Chrome()
    {
        _copy.Visibility = string.IsNullOrEmpty(Text) ? Visibility.Collapsed : Visibility.Visible;
        var want = IsGenerating ? _stop : CanRegenerate ? _regenerate : null;
        if (!ReferenceEquals((_action.Current as Decorator)?.Child, want))
        {
            // A fresh holder each swap, so a quick change back cannot catch a button still fading out of the old one.
            if (_action.Current is Decorator old) { old.Child = null; }
            _action.Set(new Decorator { Child = want });
        }
        _action.Visibility = want is null ? Visibility.Collapsed : Visibility.Visible;
        _quote.Visibility = CanQuote && !IsGenerating ? Visibility.Visible : Visibility.Collapsed;
        _up.Button.Visibility = _down.Button.Visibility = CanRate ? Visibility.Visible : Visibility.Collapsed;
        _up.Button.IsEnabled = _down.Button.IsEnabled = !IsGenerating;
        if (IsGenerating && _panelOpen) { Fold(); }
    }

    private void ShowRating(bool animate)
    {
        _up.Show(Rating == MessageRating.Up, animate);
        _down.Show(Rating == MessageRating.Down, animate);
    }

    private void OnRatingChanged()
    {
        _generation++;
        _sending = false;
        ShowRating(true);
        if (Rating == MessageRating.None && _panelOpen) { Fold(); }
    }

    private void Press(MessageRating rating)
    {
        var next = Rating == rating ? MessageRating.None : rating;
        SetCurrentValue(RatingProperty, next);
        RatingChanged?.Invoke(this, next);
        Announcer.Say(this, next == MessageRating.None ? "Rating cleared" : next == MessageRating.Up ? "Marked as a good response" : "Marked as a bad response");
        if (next == MessageRating.None) { if (_panelOpen) { Fold(); } return; }
        if (!AsksWhy) { return; }
        bool keyboard = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        if (next == MessageRating.Up && PositiveReasons.Count == 0) { Fold(); return; }
        OpenPanel(next, keyboard);
    }

    private void OpenPanel(MessageRating rating, bool moveFocus)
    {
        _thanks.Stop();
        _picked.Clear();
        _chips.Clear();
        _sending = false;
        _error = null;
        _note = null;
        _placeholder = null;
        _privacy = null;
        _submit = null;
        var face = rating == MessageRating.Down ? BuildDown() : BuildUp();
        PanelKindForTest = rating == MessageRating.Down ? "down" : "up";
        Present(face);
        if (moveFocus && rating == MessageRating.Down && _chips.Count > 0) { Dispatcher.BeginInvoke(() => _chips[0].Focus(), DispatcherPriority.Input); }
    }

    private void Present(FrameworkElement face)
    {
        _panelOpen = true;
        bool moving = AiUi.Moving(this);
        // A thumb pressed while the panel folds turns it back around.
        if (_panelBox.IsCollapsing) { _panelBox.Cancel(); Tween.To(_panelBox, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter); }
        if (_panelHolder.Child is null)
        {
            _panelHolder.Child = _panelFrame ??= Frame(_panelFace);
            _panelFace.Set(face);
            if (moving) { _panelBox.Reveal(); _panelBox.Opacity = 0; Tween.To(_panelBox, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter); }
            else { _panelBox.InvalidateMeasure(); }
            return;
        }
        // Between the form and the thanks line: one crossfade while the height springs.
        if (moving) { _panelBox.MorphNext(); }
        _panelFace.Set(face);
    }

    private static Border Frame(UIElement child)
    {
        var frame = new Border { Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(12), CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), Child = child };
        frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        return frame;
    }

    private void Fold()
    {
        _generation++;
        _sending = false;
        _thanks.Stop();
        if (!_panelOpen && _panelHolder.Child is null) { return; }
        _panelOpen = false;
        PanelKindForTest = "";
        void Clear()
        {
            _panelHolder.Child = null;
            _panelFace.Clear();
            Tween.Set(_panelBox, OpacityProperty, 1);
            _panelBox.InvalidateMeasure();
        }
        if (AiUi.Moving(this))
        {
            Tween.To(_panelBox, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard);
            _panelBox.Collapse(Clear, true);
        }
        else { Clear(); }
    }

    private FrameworkElement BuildDown()
    {
        var column = new StackPanel();
        var head = new Grid { Height = 24 };
        head.Children.Add(AiUi.Line("What went wrong?", 14, 19.6, "Foreground", true));
        var close = AiUi.Icon(Lucide.X, 24, 14, "Close", 12);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => CloseAndReturn();
        head.Children.Add(close);
        column.Children.Add(head);
        column.Children.Add(BuildChips(NegativeReasons));
        column.Children.Add(BuildNote());
        var foot = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        foot.ColumnDefinitions.Add(new ColumnDefinition());
        foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _privacy = AiUi.Line(PrivacyNote, 12, 16.8, "TextMuted");
        _error = AiUi.Line("", 12, 16.8, "Danger");
        _error.Visibility = Visibility.Collapsed;
        var left = new Grid();
        left.Children.Add(_privacy);
        left.Children.Add(_error);
        foot.Children.Add(left);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var cancel = Quiet("Cancel");
        cancel.Click += (_, _) => CloseAndReturn();
        actions.Children.Add(cancel);
        _submit = Send("Submit");
        _submit.Margin = new Thickness(4, 0, 0, 0);
        actions.Children.Add(_submit);
        Grid.SetColumn(actions, 1);
        foot.Children.Add(actions);
        column.Children.Add(foot);
        SyncSubmit();
        return column;
    }

    private FrameworkElement BuildUp()
    {
        var column = new StackPanel();
        var head = new Grid { Height = 24 };
        head.Children.Add(AiUi.Line("What was good?", 14, 19.6, "Foreground", true));
        var skip = Quiet("Skip", 24);
        skip.HorizontalAlignment = HorizontalAlignment.Right;
        skip.Click += (_, _) => CloseAndReturn();
        head.Children.Add(skip);
        column.Children.Add(head);
        column.Children.Add(BuildChips(PositiveReasons));
        var error = AiUi.Line("", 12, 16.8, "Danger");
        error.Visibility = Visibility.Collapsed;
        error.Margin = new Thickness(0, 8, 0, 0);
        _error = error;
        column.Children.Add(error);
        _submit = Send("Send");
        _submit.HorizontalAlignment = HorizontalAlignment.Right;
        _submit.Margin = new Thickness(0, 12, 0, 0);
        _submit.Visibility = Visibility.Collapsed;
        column.Children.Add(_submit);
        return column;
    }

    private WrapPanel BuildChips(IReadOnlyList<string> reasons)
    {
        var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (string reason in reasons)
        {
            var chip = new ToolbarButton(14, 1) { Height = 28, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 8, 4) };
            var label = AiUi.Line(reason, 12, 16.8, "TextSecondary", false, false);
            chip.Content = label;
            chip.Roles = _ =>
            {
                bool on = _picked.Contains(reason);
                return (on ? "Foreground" : chip.IsMouseOver ? "SurfaceMuted" : null, on ? "Foreground" : "Border", on ? "Surface" : "TextSecondary");
            };
            chip.Click += (_, _) => Toggle(reason, chip, label);
            ThreadUi.PressScale(chip, .96);
            AutomationProperties.SetName(chip, reason);
            AutomationProperties.SetItemStatus(chip, "not selected");
            wrap.Children.Add(chip);
            _chips.Add(chip);
        }
        return wrap;
    }

    private void Toggle(string reason, ToolbarButton chip, TextBlock label)
    {
        bool on = _picked.Add(reason);
        if (!on) { _picked.Remove(reason); }
        label.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush." + (on ? "Surface" : "TextSecondary"));
        AutomationProperties.SetItemStatus(chip, on ? "selected" : "not selected");
        chip.Refresh(true);
        // A chip labelled Other asks the note to say what.
        if (reason == "Other" && _note is not null)
        {
            if (_placeholder is not null) { _placeholder.Text = on ? "Tell us what went wrong" : "Add details (optional)"; }
            if (on) { _note.Focus(); }
        }
        SyncSubmit();
    }

    private FrameworkElement BuildNote()
    {
        _note = ThreadUi.PlainTextBox();
        _note.AcceptsReturn = true;
        _note.TextWrapping = TextWrapping.Wrap;
        _note.MaxLength = 1000;
        _note.FontSize = 14;
        _note.VerticalContentAlignment = VerticalAlignment.Top;
        _note.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AutomationProperties.SetName(_note, "Details");
        _placeholder = AiUi.Line("Add details (optional)", 14, 19.6, "TextMuted");
        _placeholder.VerticalAlignment = VerticalAlignment.Top;
        _placeholder.IsHitTestVisible = false;
        _note.TextChanged += (_, _) => { _placeholder!.Visibility = _note.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; SyncSubmit(); };
        var grid = new Grid();
        grid.Children.Add(_placeholder);
        grid.Children.Add(_note);
        var frame = new Border { Height = 72, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(13, 8, 13, 8), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Child = grid, Cursor = Cursors.IBeam };
        frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        return frame;
    }

    private static ToolbarButton Quiet(string text, double height = 32)
    {
        var button = new ToolbarButton(height / 2) { Height = height, Padding = new Thickness(12, 0, 12, 0), Roles = self => (self.IsPressed ? "ControlTrack" : self.IsMouseOver ? "SurfaceMuted" : null, null, "TextSecondary") };
        button.Content = AiUi.Line(text, 14, 19.6, "TextSecondary", true, false);
        AutomationProperties.SetName(button, text);
        return button;
    }

    private ToolbarButton Send(string text)
    {
        var label = AiUi.Line(text, 14, 19.6, "AccentForeground", true, false);
        var button = new ToolbarButton(16) { Height = 32, Padding = new Thickness(13, 0, 13, 0), Content = label };
        button.Roles = self => self.IsEnabled ? (self.IsMouseOver ? "AccentStrong" : "Accent", null, "AccentForeground") : ("SurfaceMuted", null, "TextMuted");
        button.Click += async (_, _) => await SubmitAsync();
        ThreadUi.PressScale(button, .96);
        AutomationProperties.SetName(button, text);
        return button;
    }

    private void SyncSubmit()
    {
        if (_submit is null) { return; }
        bool ready = _picked.Count > 0 || (_note?.Text.Trim().Length ?? 0) > 0;
        if (_note is null) { _submit.Visibility = ready ? Visibility.Visible : Visibility.Collapsed; }
        _submit.IsEnabled = ready && !_sending;
        if (_submit.Content is TextBlock label) { label.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush." + (_submit.IsEnabled ? "AccentForeground" : "TextMuted")); }
    }

    private async Task SubmitAsync()
    {
        if (_sending || !_panelOpen) { return; }
        var feedback = new MessageFeedback(Rating, [.. _picked], _note?.Text.Trim() ?? "");
        int generation = ++_generation;
        _sending = true;
        SyncSubmit();
        if (_error is not null) { _error.Visibility = Visibility.Collapsed; }
        if (_privacy is not null) { _privacy.Visibility = Visibility.Visible; }
        Announcer.Say(this, "Sending feedback");
        try { if (SubmitFeedback is { } send) { await send(feedback); } }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (generation != _generation) { return; }
            _sending = false;
            ShowFailure();
            return;
        }
        catch (OperationCanceledException) { return; }
        if (generation != _generation) { return; }
        _sending = false;
        FeedbackSubmitted?.Invoke(this, feedback);
        ShowThanks();
    }

    private void ShowFailure()
    {
        if (_error is not null) { _error.Text = "Couldn't send your feedback."; _error.Visibility = Visibility.Visible; }
        if (_privacy is not null) { _privacy.Visibility = Visibility.Collapsed; }
        if (_submit?.Content is TextBlock label) { label.Text = "Try again"; AutomationProperties.SetName(_submit, "Try again"); }
        SyncSubmit();
        _panelBox.MorphNext();
        Announcer.Say(this, "Couldn't send your feedback.");
    }

    private void ShowThanks()
    {
        PanelKindForTest = "thanks";
        var row = new StackPanel { Orientation = Orientation.Horizontal, Height = 32 };
        var check = AiUi.Glyph(Lucide.Check, 14, "Success", 2);
        check.Margin = new Thickness(0, 0, 10, 0);
        row.Children.Add(check);
        row.Children.Add(AiUi.Line("Thanks for the feedback", 14, 19.6, "Foreground", true, false));
        var note = AiUi.Line(ThanksMessage, 12, 16.8, "TextMuted");
        note.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(note);
        if (AiUi.Moving(this) && check.Child is Path path)
        {
            // The tick draws once in 280 ms.
            path.StrokeDashArray = [20];
            path.StrokeDashOffset = 20;
            Tween.To(path, Shape.StrokeDashOffsetProperty, 0, .28, ArcEasing.Enter);
        }
        Present(row);
        Announcer.Say(this, "Thanks for the feedback. " + ThanksMessage);
        _thanks.Interval = ThanksDuration;
        _thanks.Start();
    }

    private void CloseAndReturn()
    {
        _generation++;
        _sending = false;
        var thumb = Rating == MessageRating.Down ? _down : Rating == MessageRating.Up ? _up : null;
        Fold();
        thumb?.Button.Focus();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (!_panelOpen) { return; }
        if (e.Key == Key.Escape) { CloseAndReturn(); e.Handled = true; }
        else if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && ReferenceEquals(Keyboard.FocusedElement, _note) && _submit is { IsEnabled: true })
        {
            _ = SubmitAsync();
            e.Handled = true;
        }
    }

    // An outline thumb with its filled twin fading over it, so nothing moves or resizes.
    private sealed class Thumb
    {
        public ToolbarButton Button { get; }
        public FrameworkElement Filled { get; }
        private bool _on;

        public Thumb(MessageActions owner, string glyph, MessageRating rating, string name)
        {
            Button = AiUi.Icon(glyph, 36, 16, name, 18);
            Button.Roles = self => (_on || self.IsPressed ? "SurfaceMuted" : self.IsMouseOver ? "SurfaceMuted" : null, null, _on || self.IsMouseOver ? "Foreground" : "TextSecondary");
            var fill = new Path { Data = Geometry.Parse(glyph), Width = 24, Height = 24, StrokeThickness = 1.75, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.None };
            fill.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Foreground");
            fill.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.Foreground");
            Filled = new Viewbox { Width = 16, Height = 16, Child = fill, IsHitTestVisible = false, Opacity = 0 };
            var stack = new Grid();
            stack.Children.Add(Lucide.Glyph(glyph, 16));
            stack.Children.Add(Filled);
            Button.Content = stack;
            Button.Click += (_, _) => owner.Press(rating);
            ThreadUi.PressScale(Button, .94);
            AutomationProperties.SetItemStatus(Button, "not pressed");
        }

        public void Show(bool on, bool animate)
        {
            _on = on;
            AutomationProperties.SetItemStatus(Button, on ? "pressed" : "not pressed");
            Button.Refresh(animate);
            if (animate && AiUi.Moving(Button)) { Tween.To(Filled, UIElement.OpacityProperty, on ? 1 : 0, Tween.Fast, ArcEasing.Standard); }
            else { Tween.Set(Filled, UIElement.OpacityProperty, on ? 1 : 0); }
        }
    }
}
