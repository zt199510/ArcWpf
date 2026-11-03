using System.Diagnostics;
using System.Text.RegularExpressions;
using FlowItem = System.Windows.Documents.ListItem;
using FlowList = System.Windows.Documents.List;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ArcWpf.Controls;

/// <summary>What the editor reports after every change.</summary>
public sealed record RichTextValue(string Html, string Markdown, string Text, bool Empty);

public sealed class RichTextValueChangedEventArgs(RichTextValue value) : EventArgs
{
    public RichTextValue Value { get; } = value;
}

public sealed class RichTextHistoryEventArgs(bool canUndo, bool canRedo) : EventArgs
{
    public bool CanUndo { get; } = canUndo;
    public bool CanRedo { get; } = canRedo;
}

// physical(visualDuration, bounce) of the registry, for the bounce ControlSpring does not carry.
internal sealed class PhysicalSpring(UIElement owner, Action apply)
{
    private readonly Stopwatch _clock = new();
    private double _velocity, _target, _stiffness, _damping;
    private bool _running;
    public double Value { get; private set; }

    public void Jump(double value)
    {
        Stop();
        Value = _target = value;
        _velocity = 0;
        apply();
    }

    public void To(double target, double visualDuration, double bounce)
    {
        double root = 2 * Math.PI / (visualDuration * 1.2);
        _stiffness = root * root;
        _damping = 2 * (1 - bounce) * root;
        _target = target;
        _clock.Restart();
        if (!_running)
        {
            _running = true;
            CompositionTarget.Rendering += OnFrame;
        }
    }

    public void Stop()
    {
        if (!_running) { return; }
        _running = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (!owner.IsVisible && PresentationSource.FromVisual(owner) is null) { Stop(); return; }
        double elapsed = Math.Min(_clock.Elapsed.TotalSeconds, .1);
        _clock.Restart();
        for (double left = elapsed; left > 0; left -= .002)
        {
            double step = Math.Min(left, .002);
            _velocity += (-_stiffness * (Value - _target) - _damping * _velocity) * step;
            Value += _velocity * step;
        }
        if (Math.Abs(Value - _target) < .01 && Math.Abs(_velocity) < .5)
        {
            Value = _target;
            _velocity = 0;
            Stop();
        }
        apply();
    }
}

/// <summary>
/// A light rich text editor on the native RichTextBox (Arc's Rich text editor): Markdown shortcuts as you type, a floating selection toolbar
/// that morphs into a link field, a slash menu for blocks, its own undo history, and HTML plus Markdown output.
/// </summary>
public class RichTextEditor : Control
{
    private const string ZWSP = "\u200b";
    private const double Travel = 18;
    private static readonly (string Type, string Label, string Hint, string Keywords, string Icon)[] SlashItems =
    [
        ("p", "Text", "", "paragraph plain body", "pilcrow"),
        ("h1", "Heading 1", "#", "title h1 large", "heading-1"),
        ("h2", "Heading 2", "##", "subtitle h2 section", "heading-2"),
        ("h3", "Heading 3", "###", "h3 small", "heading-3"),
        ("ul", "Bulleted list", "-", "bullet unordered ul points", "list"),
        ("ol", "Numbered list", "1.", "ordered ol steps numbers", "list-ordered"),
        ("blockquote", "Quote", ">", "blockquote citation callout", "quote"),
        ("pre", "Code block", "```", "pre snippet monospace", "square-code"),
        ("hr", "Divider", "---", "rule hr line separator", "minus"),
    ];

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(RichTextEditor), new PropertyMetadata(null, (o, e) => ((RichTextEditor)o).OnValueChanged((string?)e.NewValue)));
    public static readonly DependencyProperty DefaultMarkdownProperty = DependencyProperty.Register(nameof(DefaultMarkdown), typeof(string), typeof(RichTextEditor), new PropertyMetadata(null));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(RichTextEditor), new PropertyMetadata("Start writing", (o, _) => ((RichTextEditor)o).QueueSync()));
    public static readonly DependencyProperty BlockHintProperty = DependencyProperty.Register(nameof(BlockHint), typeof(string), typeof(RichTextEditor), new PropertyMetadata("Type / for blocks", (o, _) => ((RichTextEditor)o).QueueSync()));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(RichTextEditor), new PropertyMetadata(false, (o, e) => ((RichTextEditor)o).OnReadOnly((bool)e.NewValue)));
    public static readonly DependencyProperty AutoFocusProperty = DependencyProperty.Register(nameof(AutoFocus), typeof(bool), typeof(RichTextEditor), new PropertyMetadata(false));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(RichTextEditor), new PropertyMetadata("Editor", (o, e) => { if (((RichTextEditor)o)._box is { } box) { AutomationProperties.SetName(box, (string)e.NewValue); } }));

    /// <summary>The content as HTML. The editor only rewrites itself when this differs from what it last reported.</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>Initial content as Markdown, used when no HTML value is given.</summary>
    public string? DefaultMarkdown { get => (string?)GetValue(DefaultMarkdownProperty); set => SetValue(DefaultMarkdownProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    /// <summary>The hint on an empty line while typing.</summary>
    public string BlockHint { get => (string)GetValue(BlockHintProperty); set => SetValue(BlockHintProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool AutoFocus { get => (bool)GetValue(AutoFocusProperty); set => SetValue(AutoFocusProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public event EventHandler<RichTextValueChangedEventArgs>? ValueChanged;
    /// <summary>Reports whether undo and redo are available, for your own toolbar.</summary>
    public event EventHandler<RichTextHistoryEventArgs>? HistoryChanged;

    private sealed class Snapshot { public required string Html; public (int From, int To)? Selection; public required string Kind; public required long At; }

    private readonly OwnedMotion _motion = new();
    private readonly List<Snapshot> _stack = [];
    private int _index = -1;
    private Decorator? _root;
    private Grid _host = null!;
    private RichTextBox? _box;
    private FlowDocument _document = null!;
    private RichTextFlow _flow = null!;
    private Canvas _decor = null!, _overlay = null!, _floating = null!;
    private TextBlock _hint = null!;
    private bool _busy, _dragging, _composing, _syncQueued, _loaded, _empty = true;
    private string? _typed;
    private string _lastInput = "";
    private string? _lastEmitted;

    static RichTextEditor() => DefaultStyleKeyProperty.OverrideMetadata(typeof(RichTextEditor), new FrameworkPropertyMetadata(typeof(RichTextEditor)));

    public RichTextEditor()
    {
        Unloaded += (_, _) => { _motion.Stop(); DetachWindow(); };
        Loaded += (_, _) => { AttachWindow(); QueueSync(); };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _root.Child = BuildChrome();
        _flow = new RichTextFlow(this);
        ThemeWatch.Add(this, () => { if (_box is not null) { Command(ReBuild, record: false); } });
        LoadHtml(Value ?? (DefaultMarkdown is { } markdown ? RichTextMarkup.MarkdownToHtml(markdown) : "<p><br></p>"));
        _stack.Clear();
        _stack.Add(new Snapshot { Html = RawHtml(), Selection = null, Kind = "command", At = 0 });
        _index = 0;
        _loaded = true;
        Emit();
        ReportHistory();
        if (AutoFocus) { Dispatcher.BeginInvoke(() => FocusEditor(), DispatcherPriority.Input); }
    }

    // ---- public surface ----

    public void FocusEditor() => _box?.Focus();
    public string GetHtml() => RichTextMarkup.Write(Clean());
    public string GetMarkdown() => RichTextMarkup.TreeToMarkdown(Clean());
    public string GetText() => RichTextMarkup.PlainOutput(Clean());
    public void SetHtml(string html) { LoadHtml(html); Record("command"); Emit(); }
    public void SetMarkdown(string markdown) { LoadHtml(RichTextMarkup.MarkdownToHtml(markdown)); Record("command"); Emit(); }
    public void ClearContent() { LoadHtml("<p><br></p>"); Record("command"); Emit(); }

    public void Undo()
    {
        if (_index <= 0) { return; }
        _index--;
        CloseSlash();
        Restore(_stack[_index]);
        Announcer.Say(this, "Undone");
    }

    public void Redo()
    {
        if (_index >= _stack.Count - 1) { return; }
        _index++;
        CloseSlash();
        Restore(_stack[_index]);
        Announcer.Say(this, "Redone");
    }

    private void OnValueChanged(string? value)
    {
        if (!_loaded || value is null || value == _lastEmitted) { return; }
        LoadHtml(value);
        Record("command");
        Emit();
    }

    private void OnReadOnly(bool value)
    {
        if (_box is null) { return; }
        _box.IsReadOnly = value;
        QueueSync();
    }

    // ---- output and history ----

    private RichNode Clean() => _box is null ? new RichNode("#root") : RichTextMarkup.CleanClone(_flow.Read(_document));
    private string RawHtml() => RichTextMarkup.Write(_flow.Read(_document));

    private void Emit()
    {
        var clone = Clean();
        string html = RichTextMarkup.Write(clone);
        string text = RichTextMarkup.PlainOutput(clone);
        bool empty = text.Length == 0 && !RichTextOps.Contains(clone, "hr");
        _lastEmitted = html;
        _empty = empty;
        SetCurrentValue(ValueProperty, html);
        ValueChanged?.Invoke(this, new RichTextValueChangedEventArgs(new RichTextValue(html, RichTextMarkup.TreeToMarkdown(clone), text, empty)));
    }

    private void ReportHistory() => HistoryChanged?.Invoke(this, new RichTextHistoryEventArgs(_index > 0, _index < _stack.Count - 1));

    private void Record(string kind, bool boundary = false)
    {
        var snap = new Snapshot { Html = RawHtml(), Selection = ReadOffsets(), Kind = kind, At = Environment.TickCount64 };
        var top = _index >= 0 ? _stack[_index] : null;
        if (top is not null && top.Html == snap.Html) { top.Selection = snap.Selection; return; }
        if (kind == "type" && top?.Kind == "type" && _index > 0 && snap.At - top.At < 1000 && !boundary)
        {
            snap.At = top.At;
            _stack[_index] = snap;
        }
        else
        {
            _stack.RemoveRange(_index + 1, _stack.Count - _index - 1);
            _stack.Add(snap);
            if (_stack.Count > 300) { _stack.RemoveAt(0); }
            _index = _stack.Count - 1;
        }
        ReportHistory();
    }

    private void Restore(Snapshot snap)
    {
        Rewrite(() => _flow.Build(_document, RichTextMarkup.Parse(snap.Html)));
        _box!.Focus();
        WriteOffsets(snap.Selection);
        Emit();
        ReportHistory();
        QueueSync();
    }

    private void LoadHtml(string html)
    {
        var tree = RichTextMarkup.SanitizeTree(html);
        if (tree.Children.Count == 0) { tree.Children.Add(RichTextOps.Empty("p")); }
        foreach (var node in RichTextOps.Descendants(tree).Where(node => node.Tag is "p" or "h1" or "h2" or "h3" or "li" or "blockquote" or "pre").ToList()) { RichTextOps.EnsureFilled(node); }
        Rewrite(() => _flow.Build(_document, tree));
        QueueSync();
    }

    private void Rewrite(Action change)
    {
        bool outer = !_busy;
        _busy = true;
        _box!.BeginChange();
        try { change(); }
        finally
        {
            _box.EndChange();
            if (outer) { _busy = false; }
        }
    }

    private void ReBuild()
    {
        var offsets = ReadOffsets();
        _flow.Build(_document, _flow.Read(_document));
        WriteOffsets(offsets);
    }

    private void Command(Action change, bool record = true)
    {
        if (_box is null) { return; }
        Rewrite(change);
        if (record)
        {
            Record("command");
            Emit();
        }
        QueueSync();
    }

    private (int From, int To)? ReadOffsets() => _box is null ? null : (RichTextFlow.Count(_document, _box.Selection.Start), RichTextFlow.Count(_document, _box.Selection.End));

    private void WriteOffsets((int From, int To)? offsets)
    {
        if (_box is null) { return; }
        if (offsets is not { } value)
        {
            var end = _document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
            _box.Selection.Select(end, end);
            return;
        }
        _box.Selection.Select(RichTextFlow.At(_document, value.From), RichTextFlow.At(_document, value.To));
    }

    private void PlaceAt(RichNode node, int offset = 0)
    {
        if (_flow.ParagraphOf(node) is not { } paragraph) { return; }
        var at = RichTextFlow.InParagraph(paragraph, offset);
        _box!.Selection.Select(at, at);
    }

    private (Paragraph Paragraph, RichNode Node)? CaretBlock()
    {
        _flow.Read(_document);
        var paragraph = _box!.Selection.Start.Paragraph;
        return paragraph is not null && _flow.NodeOf(paragraph) is { } node ? (paragraph, node) : null;
    }

    private static string VisibleOf(TextPointer start, TextPointer end) => RichTextFlow.TextBetween(start, end).Replace(ZWSP, "", StringComparison.Ordinal).Replace('\u00a0', ' ');

    // ---- chrome: the editor, painted boxes behind it, hints over it, floating layers above ----

    private Grid BuildChrome()
    {
        _host = new Grid();
        _host.SizeChanged += (_, e) => { UpdateNarrow(e.NewSize.Width); QueueSync(); };
        _decor = new Canvas { Margin = new Thickness(0, 2, 0, 2), IsHitTestVisible = false };
        _overlay = new Canvas { Margin = new Thickness(0, 2, 0, 2), IsHitTestVisible = false };
        _floating = new Canvas();
        _hint = new TextBlock { LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Visibility = Visibility.Collapsed };
        _hint.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        _overlay.Children.Add(_hint);

        _document = new FlowDocument { PagePadding = new Thickness(0), TextAlignment = TextAlignment.Left, FontSize = 16 };
        _document.SetResourceReference(FlowDocument.FontFamilyProperty, "ArcWpf.Font.Body");
        _box = new RichTextBox
        {
            OverridesDefaultStyle = true,
            Template = BoxTemplate(),
            Document = _document,
            Margin = new Thickness(0, 2, 0, 2),
            MinHeight = 124,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            FocusVisualStyle = null,
            AcceptsTab = false,
            AcceptsReturn = true,
            AllowDrop = false,
            IsUndoEnabled = false,
            IsReadOnly = IsReadOnly,
            IsReadOnlyCaretVisible = false,
            SelectionOpacity = 1,
            SpellCheck = { IsEnabled = true },
            Cursor = Cursors.IBeam,
        };
        // RichTextBox gives its document a default page padding on assignment.
        _document.PagePadding = new Thickness(0);
        AutomationProperties.SetName(_box, Label);
        _box.SetResourceReference(TextBoxBase.CaretBrushProperty, "ArcWpf.Brush.Accent");
        _box.SetResourceReference(TextBoxBase.SelectionBrushProperty, "ArcWpf.Brush.EditorSelection");
        _box.TextChanged += OnTextChanged;
        _box.SelectionChanged += (_, _) => QueueSync();
        _box.PreviewKeyDown += OnEditorKey;
        _box.PreviewTextInput += (_, e) => { _typed = e.Text; _composing = false; };
        TextCompositionManager.AddPreviewTextInputStartHandler(_box, (_, _) => _composing = true);
        _box.PreviewMouseDown += (_, _) => _dragging = true;
        _box.PreviewMouseUp += (_, _) => { _dragging = false; QueueSync(); };
        _box.LostMouseCapture += (_, _) => { if (_dragging && Mouse.LeftButton == MouseButtonState.Released) { _dragging = false; QueueSync(); } };
        _box.Loaded += (_, _) => _document.PagePadding = new Thickness(0);
        _box.GotFocus += (_, _) => QueueSync();
        _box.LostFocus += (_, _) => QueueSync();
        // The hidden scroller would swallow the wheel although it has nothing to scroll.
        _box.PreviewMouseWheel += (_, e) =>
        {
            e.Handled = true;
            (_box.Parent as UIElement ?? _host).RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this });
        };
        DataObject.AddPastingHandler(_box, OnPaste);
        // Only the formats the editor can write are reachable: everything else WPF binds by default stays off.
        foreach (var command in new ICommand[]
        {
            EditingCommands.AlignCenter, EditingCommands.AlignJustify, EditingCommands.AlignLeft, EditingCommands.AlignRight, EditingCommands.ToggleBold, EditingCommands.ToggleItalic,
            EditingCommands.ToggleUnderline, EditingCommands.ToggleBullets, EditingCommands.ToggleNumbering, EditingCommands.IncreaseFontSize, EditingCommands.DecreaseFontSize,
            EditingCommands.IncreaseIndentation, EditingCommands.DecreaseIndentation, EditingCommands.ToggleSubscript, EditingCommands.ToggleSuperscript,
            ApplicationCommands.Undo, ApplicationCommands.Redo,
        })
        {
            _box.CommandBindings.Add(new CommandBinding(command, (_, e) => e.Handled = true, (_, e) => { e.CanExecute = false; e.Handled = true; }));
        }

        BuildToolbar();
        BuildSlash();
        _host.Children.Add(_decor);
        _host.Children.Add(_box);
        _host.Children.Add(_overlay);
        _host.Children.Add(_floating);
        return _host;
    }

    private static ControlTemplate BoxTemplate()
    {
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(FocusableProperty, false);
        host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        var template = new ControlTemplate(typeof(RichTextBox)) { VisualTree = host };
        template.Seal();
        return template;
    }

    private bool HasFocus => _box is { } box && (box.IsFocused || box.IsKeyboardFocused);

    // ---- selection driven chrome ----

    private void QueueSync()
    {
        if (_syncQueued || _box is null) { return; }
        _syncQueued = true;
        Dispatcher.BeginInvoke(() => { _syncQueued = false; Sync(); }, DispatcherPriority.Background);
    }

    internal void SyncForTest() { _syncQueued = false; Sync(); }

    private (bool Bold, bool Italic, bool Strike, bool Code, string? Link, string Block) _formats = (false, false, false, false, null, "p");

    private void Sync()
    {
        if (_box is null || _busy) { return; }
        _box.UpdateLayout();
        PaintBoxes();
        var selection = _box.Selection;
        bool inside = HasFocus || _bar.IsKeyboardFocusWithin;
        var paragraph = selection.Start.Paragraph;
        UpdateHint(inside && selection.IsEmpty ? paragraph : null);
        if (!inside)
        {
            if (_face != "link") { HideBar(); }
            CloseSlash();
            return;
        }

        var run = StartRun();
        var link = FindLink(run);
        string block = paragraph?.Tag as string ?? "p";
        var list = paragraph?.Parent is FlowItem { Parent: FlowList owner } ? owner.Tag as string ?? "ul" : null;
        var next = (Bold: run?.FontWeight.ToOpenTypeWeight() >= 600, Italic: run?.FontStyle == FontStyles.Italic,
            Strike: run?.TextDecorations?.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough) == true,
            Code: run is not null && _flow.IsCode(run), Link: link?.Tag as string, Block: list ?? block);
        if (next != _formats)
        {
            _formats = next;
            RefreshTools();
        }

        if (_slash is { } open)
        {
            var caret = selection.Start;
            bool valid = selection.IsEmpty && caret.Paragraph == open.Start.Paragraph && open.Start.CompareTo(caret) < 0;
            string text = valid ? RichTextFlow.TextBetween(open.Start, caret) : "";
            valid = valid && text.StartsWith('/');
            string query = valid ? text[1..] : "";
            if (!valid || query.Length > 24 || Regex.IsMatch(query, @"^\s") || Regex.IsMatch(query, @"\s\s")) { CloseSlash(); }
            else if (query != open.Query) { open.Query = query; _slashActive = 0; RefreshSlash(true); }
        }

        bool inCode = (paragraph?.Tag as string) == "pre";
        if (selection.IsEmpty || _dragging || inCode || _slash is not null || IsReadOnly)
        {
            if (_face != "link") { HideBar(); }
            return;
        }
        var (top, whole) = SelectionRects();
        double bandTop = ClipBand().Top;
        bool below = top - bandTop < 60;
        ShowBar(whole.Left + whole.Width / 2, below ? whole.Bottom + 8 : top - 8, below);
    }

    private Run? StartRun()
    {
        var at = _box!.Selection.Start;
        if (!_box.Selection.IsEmpty && at.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
        {
            at = at.GetNextInsertionPosition(LogicalDirection.Forward) ?? at;
        }
        else if (_box.Selection.IsEmpty && at.Parent is not Run)
        {
            return at.GetAdjacentElement(LogicalDirection.Backward) as Run;
        }
        return at.Parent as Run;
    }

    private static Hyperlink? FindLink(TextElement? element)
    {
        for (var current = element; current is not null; current = current.Parent as TextElement)
        {
            if (current is Hyperlink link) { return link; }
        }
        return null;
    }

    // Top of the line box, in the editor's coordinates. WPF's rect is the font's natural box, sitting low in a BlockLineHeight line.
    private static double LineTop(TextPointer pointer, double lineHeight)
    {
        var rect = (pointer.GetLineStartPosition(0) ?? pointer).GetCharacterRect(LogicalDirection.Forward);
        if (rect.IsEmpty) { rect = pointer.GetCharacterRect(LogicalDirection.Forward); }
        return rect.Bottom + .2 * (lineHeight - rect.Height) - lineHeight;
    }

    // A browser's range rect is the font's box inside the line, not the line.
    private Rect Local(TextPointer pointer, LogicalDirection direction, double fontSize)
    {
        var rect = pointer.GetCharacterRect(direction);
        if (rect.IsEmpty) { return new Rect(); }
        double height = fontSize * 1.25;
        double line = pointer.Paragraph?.LineHeight ?? height;
        var at = _box!.TranslatePoint(new Point(rect.X, LineTop(pointer, line) + (line - height) / 2), _host);
        return new Rect(at.X, at.Y, rect.Width, height);
    }

    private (double Top, Rect Whole) SelectionRects()
    {
        var selection = _box!.Selection;
        double size = selection.Start.Paragraph?.FontSize ?? 16;
        var first = Local(selection.Start, LogicalDirection.Forward, size);
        double left = first.Left, right = first.Left, top = first.Top, bottom = first.Bottom;
        var at = selection.Start;
        for (int guard = 0; guard < 400; guard++)
        {
            var next = at.GetLineStartPosition(1);
            bool last = next is null || next.CompareTo(selection.End) >= 0;
            var edge = last ? selection.End : next!.GetInsertionPosition(LogicalDirection.Backward);
            var end = Local(edge, LogicalDirection.Backward, edge.Paragraph?.FontSize ?? size);
            var start = Local(at, LogicalDirection.Forward, at.Paragraph?.FontSize ?? size);
            left = Math.Min(left, Math.Min(start.Left, end.Left));
            right = Math.Max(right, Math.Max(start.Left, end.Left));
            bottom = Math.Max(bottom, end.Bottom);
            if (last) { break; }
            at = next!;
        }
        return (first.Top, new Rect(left, top, right - left, bottom - top));
    }

    // The visible band of the nearest clipping ancestors, so floating layers flip before they get cut off.
    private (double Top, double Bottom) ClipBand()
    {
        double top = double.NegativeInfinity, bottom = double.PositiveInfinity;
        for (DependencyObject? node = VisualTreeHelper.GetParent(this); node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is not FrameworkElement element || !(element.ClipToBounds || element is ScrollContentPresenter || element is Window)) { continue; }
            var at = element.TranslatePoint(new Point(0, 0), _host);
            top = Math.Max(top, at.Y);
            bottom = Math.Min(bottom, at.Y + element.ActualHeight);
        }
        return (double.IsInfinity(top) ? -100000 : top, double.IsInfinity(bottom) ? 100000 : bottom);
    }

    private void UpdateHint(Paragraph? caretParagraph)
    {
        var first = _document.Blocks.FirstBlock as Paragraph;
        Paragraph? target = null;
        string text = "";
        if (_empty && first is not null) { target = first; text = Placeholder; }
        if (caretParagraph is not null && caretParagraph.Tag as string != "pre" && VisibleOf(caretParagraph.ContentStart, caretParagraph.ContentEnd).Length == 0)
        {
            target = caretParagraph;
            text = _empty ? Placeholder : BlockHint;
        }
        if (target is null) { _hint.Visibility = Visibility.Collapsed; return; }
        var rect = target.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        if (rect.IsEmpty) { _hint.Visibility = Visibility.Collapsed; return; }
        _hint.Text = text;
        _hint.FontSize = target.FontSize;
        _hint.FontWeight = target.FontWeight;
        _hint.LineHeight = target.LineHeight;
        Canvas.SetLeft(_hint, rect.Left);
        Canvas.SetTop(_hint, LineTop(target.ContentStart, target.LineHeight));
        _hint.Visibility = Visibility.Visible;
    }

    // Inline code and code blocks have rounded boxes, which FlowDocument cannot draw; they are painted behind the transparent editor.
    private void PaintBoxes()
    {
        _decor.Children.Clear();
        foreach (var block in Paragraphs(_document.Blocks))
        {
            if (block.Tag as string == "pre")
            {
                var head = block.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var tail = block.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
                if (head.IsEmpty || tail.IsEmpty) { continue; }
                double first = LineTop(block.ContentStart, block.LineHeight), last = LineTop(block.ContentEnd, block.LineHeight);
                var box = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Width = _box!.ActualWidth, Height = last - first + block.LineHeight + 26 };
                box.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
                box.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
                Canvas.SetTop(box, first - 13);
                _decor.Children.Add(box);
                continue;
            }
            foreach (var run in Runs(block.Inlines).Where(_flow.IsCode)) { PaintCode(run); }
        }
    }

    private void PaintCode(Run run)
    {
        double? left = null, top = null, right = null, height = null;
        int startOffset = 0;
        void Flush()
        {
            if (left is null) { return; }
            double size = run.FontSize;
            double boxHeight = size * 1.401;
            var box = new Border { CornerRadius = new CornerRadius(6), Width = right!.Value - left.Value + size * .76, Height = boxHeight };
            box.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
            Canvas.SetLeft(box, left.Value - size * .38);
            // The source's box sits .8 below the line's centre.
            double line = (run.Parent as Paragraph ?? (run.Parent as Span)?.Parent as Paragraph)?.LineHeight ?? height!.Value;
            Canvas.SetTop(box, LineTop(run.ContentStart.GetPositionAtOffset(startOffset)!, line) + (line - boxHeight) / 2 + .8);
            _decor.Children.Add(box);
        }
        int length = run.Text.Length;
        for (int i = 0; i < length; i++)
        {
            var start = run.ContentStart.GetPositionAtOffset(i)!.GetCharacterRect(LogicalDirection.Forward);
            var end = run.ContentStart.GetPositionAtOffset(i + 1)!.GetCharacterRect(LogicalDirection.Backward);
            if (start.IsEmpty || end.IsEmpty) { return; }
            if (top is not null && Math.Abs(start.Top - top.Value) > 1) { Flush(); left = null; }
            if (left is null) { left = start.Left; top = start.Top; height = start.Height; startOffset = i; }
            right = end.Left;
        }
        Flush();
    }

    private static IEnumerable<Paragraph> Paragraphs(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            if (block is Paragraph paragraph) { yield return paragraph; }
            else if (block is FlowList list)
            {
                foreach (var item in list.ListItems)
                {
                    foreach (var inner in Paragraphs(item.Blocks)) { yield return inner; }
                }
            }
        }
    }

    private static IEnumerable<Run> Runs(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run) { yield return run; }
            else if (inline is Span span)
            {
                foreach (var inner in Runs(span.Inlines)) { yield return inner; }
            }
        }
    }

    // ---- typing ----

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_busy || _composing) { return; }
        string? data = _typed;
        _typed = null;
        string kind = data is not null ? "insertText" : "other";
        _busy = true;
        bool converted = false;
        _box!.BeginChange();
        try
        {
            RichTextFlow.ApplyMargins(_document);
            if (data is not null) { converted = Shortcut(data); }
        }
        finally
        {
            _box.EndChange();
            _busy = false;
        }
        if (converted)
        {
            Record("command");
            Announcer.Say(this, "Formatted");
        }
        else { Record("type", data == " " || kind != _lastInput); }
        _lastInput = kind;
        Emit();
        if (!converted && data is not null) { OpenSlashIfTyped(data); }
        QueueSync();
    }

    // Markdown shortcuts, applied right after the character that completes them.
    private bool Shortcut(string data)
    {
        var selection = _box!.Selection;
        if (!selection.IsEmpty || selection.Start.Paragraph is not { } paragraph || paragraph.Tag as string == "pre") { return false; }
        var caret = selection.Start;
        string before = VisibleOf(paragraph.ContentStart, caret);
        string whole = VisibleOf(paragraph.ContentStart, paragraph.ContentEnd);
        bool plain = paragraph.Tag as string is null or "p" && paragraph.Parent is FlowDocument;

        if (data == " " && plain)
        {
            (string Pattern, string Type)[] rules = [(@"^# $", "h1"), (@"^## $", "h2"), (@"^### $", "h3"), (@"^[-*+] $", "ul"), (@"^\d+[.)] $", "ol"), (@"^> $", "blockquote")];
            foreach (var (pattern, type) in rules)
            {
                if (!Regex.IsMatch(before, pattern)) { continue; }
                new TextRange(paragraph.ContentStart, caret).Text = "";
                var root = _flow.Read(_document);
                var next = RichTextOps.Convert(root, _flow.NodeOf(paragraph)!, type);
                _flow.Build(_document, root);
                PlaceAt(next);
                return true;
            }
        }
        if (data == "-" && plain && before == "---" && whole == "---")
        {
            var root = _flow.Read(_document);
            var rule = new RichNode("hr");
            RichTextOps.Replace(root, _flow.NodeOf(paragraph)!, rule);
            var after = RichTextOps.ParagraphAfter(root, rule);
            _flow.Build(_document, root);
            PlaceAt(after);
            return true;
        }
        if (data == "`" && plain && before == "```" && whole == "```")
        {
            var root = _flow.Read(_document);
            var node = _flow.NodeOf(paragraph)!;
            node.Children.Clear();
            var next = RichTextOps.Convert(root, node, "pre");
            _flow.Build(_document, root);
            PlaceAt(next);
            return true;
        }
        if (caret.Parent is not Run run || _flow.IsCode(run)) { return false; }
        string text = caret.GetTextInRun(LogicalDirection.Backward);
        (string Pattern, string Tag, int WholeGroup, int InnerGroup)[] inline =
        [
            (@"(\*\*|__)([^*_\s](?:[^*_]*[^*_\s])?)\1$", "strong", 0, 2),
            (@"~~([^~\s](?:[^~]*[^~\s])?)~~$", "s", 0, 1),
            (@"`([^`]+)`$", "code", 0, 1),
            (@"(?:^|[^*\w])(\*([^*\s](?:[^*]*[^*\s])?)\*)$", "em", 1, 2),
            (@"(?:^|[^_\w])(_([^_\s](?:[^_]*[^_\s])?)_)$", "em", 1, 2),
        ];
        foreach (var (pattern, tag, wholeGroup, innerGroup) in inline)
        {
            var match = Regex.Match(text, pattern);
            if (!match.Success) { continue; }
            string found = match.Groups[wholeGroup].Value, inner = match.Groups[innerGroup].Value;
            int end = RichTextFlow.LocalOffset(paragraph, caret);
            int from = end - found.Length;
            var root = _flow.Read(_document);
            var block = _flow.NodeOf(paragraph)!;
            var (head, rest) = RichTextOps.Split(block.Children, from);
            var (_, tail) = RichTextOps.Split(rest, found.Length);
            block.Children.Clear();
            block.Children.AddRange(head);
            block.Children.Add(RichNode.Of(tag, RichNode.OfText(inner)));
            // A zero width space after the mark keeps the next keystroke outside it; output strips it.
            block.Children.Add(RichNode.OfText(ZWSP));
            block.Children.AddRange(tail);
            _flow.Build(_document, root);
            PlaceAt(block, from + inner.Length + 1);
            return true;
        }
        return false;
    }

    // ---- commands ----

    internal void EnterKey(bool soft)
    {
        var selection = _box!.Selection;
        if (!selection.IsEmpty) { selection.Text = ""; }
        if (CaretBlock() is not { } caret) { return; }
        int offset = RichTextFlow.LocalOffset(caret.Paragraph, selection.Start);
        var (node, at) = RichTextOps.Enter(_flow.Root, caret.Node, offset, soft);
        _flow.Build(_document, _flow.Root);
        PlaceAt(node, at);
    }

    internal bool BackspaceAtStart()
    {
        var selection = _box!.Selection;
        if (!selection.IsEmpty || CaretBlock() is not { } caret) { return false; }
        if (VisibleOf(caret.Paragraph.ContentStart, selection.Start).Length > 0) { return false; }
        var root = _flow.Root;
        var block = caret.Node;
        if (block.Tag is "h1" or "h2" or "h3" or "blockquote" or "pre")
        {
            var next = RichTextOps.Convert(root, block, "p");
            _flow.Build(_document, root);
            PlaceAt(next);
            return true;
        }
        if (block.Tag == "li")
        {
            var next = RichTextOps.Outdent(root, block);
            _flow.Build(_document, root);
            PlaceAt(next);
            return true;
        }
        var parent = RichTextOps.Parent(root, block)!;
        int index = parent.Children.IndexOf(block);
        if (index > 0 && parent.Children[index - 1].Tag == "hr")
        {
            parent.Children.RemoveAt(index - 1);
            _flow.Build(_document, root);
            PlaceAt(block);
            return true;
        }
        if (index == 0 && RichTextOps.Visible(block).Length == 0 && parent.Children.Count > 1)
        {
            parent.Children.RemoveAt(0);
            var first = parent.Children[0];
            _flow.Build(_document, root);
            if (first.Tag != "hr") { PlaceAt(RichTextOps.IsList(first) ? first.Children[0] : first); }
            return true;
        }
        return false;
    }

    internal void ApplyBlock(string type)
    {
        var selection = _box!.Selection;
        if (CaretBlock() is not { } caret) { return; }
        var root = _flow.Root;
        var block = caret.Node;
        if (type == "hr")
        {
            var top = block;
            if (block.Tag == "li")
            {
                while (RichTextOps.Parent(root, top) is { } parent && !ReferenceEquals(parent, root)) { top = parent; }
            }
            var rule = new RichNode("hr");
            if (RichTextOps.Visible(block).Length == 0 && block.Tag != "li") { RichTextOps.Replace(root, block, rule); } else { RichTextOps.InsertAfter(root, top, rule); }
            var after = RichTextOps.ParagraphAfter(root, rule);
            _flow.Build(_document, root);
            PlaceAt(after);
            return;
        }
        var offsets = ReadOffsets();
        // Several selected blocks turn together; if every one already has the type, they all turn back into paragraphs.
        var blocks = new List<RichNode> { block };
        if (!selection.IsEmpty)
        {
            blocks = Paragraphs(_document.Blocks)
                .Where(paragraph => paragraph.ContentEnd.CompareTo(selection.Start) >= 0 && paragraph.ContentStart.CompareTo(selection.End) <= 0)
                .Select(paragraph => _flow.NodeOf(paragraph)!)
                .Where(node => node is not null && !RichTextOps.Contains(node, "li", "p"))
                .ToList();
        }
        bool allSame = blocks.All(candidate => RichTextOps.CurrentType(root, candidate) == type);
        foreach (var candidate in blocks)
        {
            if (allSame || RichTextOps.CurrentType(root, candidate) != type) { RichTextOps.Convert(root, candidate, type); }
        }
        _flow.Build(_document, root);
        WriteOffsets(offsets);
        if (type == "pre" && _box.Selection.Start.Paragraph is { } pre && _flow.NodeOf(pre) is { Tag: "pre" } preNode)
        {
            var parent = RichTextOps.Parent(root, preNode)!;
            if (ReferenceEquals(parent.Children[^1], preNode))
            {
                RichTextOps.ParagraphAfter(root, preNode);
                _flow.Build(_document, root);
                WriteOffsets(offsets);
            }
        }
    }

    internal void ToggleMark(string mark)
    {
        Command(() =>
        {
            var selection = _box!.Selection;
            switch (mark)
            {
                case "bold":
                {
                    bool on = selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight weight && weight.ToOpenTypeWeight() >= 600;
                    string tag = selection.Start.Paragraph?.Tag as string ?? "p";
                    selection.ApplyPropertyValue(TextElement.FontWeightProperty, on ? (tag is "h1" or "h2" or "h3" ? FontWeights.Medium : FontWeights.Normal) : FontWeights.SemiBold);
                    break;
                }
                case "italic":
                {
                    bool on = selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle style && style == FontStyles.Italic;
                    selection.ApplyPropertyValue(TextElement.FontStyleProperty, on ? FontStyles.Normal : FontStyles.Italic);
                    break;
                }
                case "strike":
                {
                    bool on = selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection found && found.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough);
                    selection.ApplyPropertyValue(Inline.TextDecorationsProperty, on ? new TextDecorationCollection() : TextDecorations.Strikethrough);
                    break;
                }
                case "code":
                {
                    if (StartRun() is { } run && _flow.IsCode(run))
                    {
                        var (from, to) = (run.ContentStart, run.ContentEnd);
                        new TextRange(from, to).ApplyPropertyValue(TextElement.FontFamilyProperty, _document.FontFamily);
                        selection.Select(from, to);
                    }
                    else
                    {
                        if (selection.IsEmpty || selection.Start.Paragraph != selection.End.Paragraph || selection.Start.Paragraph?.Tag as string == "pre") { return; }
                        selection.ApplyPropertyValue(TextElement.FontFamilyProperty, _flow.Mono);
                        selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                        selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                        selection.ApplyPropertyValue(Inline.TextDecorationsProperty, new TextDecorationCollection());
                    }
                    break;
                }
            }
            // Fresh runs take their full look from the tree; a bare caret keeps the pending format for the next keystroke.
            if (!selection.IsEmpty) { ReBuild(); }
        });
    }

    // ---- paste ----

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (IsReadOnly) { return; }
        var data = e.SourceDataObject;
        string html = data.GetDataPresent(DataFormats.Html) ? data.GetData(DataFormats.Html) as string ?? "" : "";
        string text = data.GetDataPresent(DataFormats.UnicodeText) ? data.GetData(DataFormats.UnicodeText) as string ?? "" : "";
        PasteContent(FragmentOf(html), text);
    }

    // The clipboard's HTML format wraps the copied part in a header and fragment comments.
    private static string FragmentOf(string clip)
    {
        int start = clip.IndexOf("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase);
        int end = clip.IndexOf("<!--EndFragment-->", StringComparison.OrdinalIgnoreCase);
        return start >= 0 && end > start ? clip[(start + 20)..end] : clip;
    }

    internal void PasteContent(string html, string text)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        Command(() =>
        {
            var selection = _box!.Selection;
            if (!selection.IsEmpty) { selection.Text = ""; }
            if (CaretBlock() is not { } caret) { return; }
            var root = _flow.Root;
            var block = caret.Node;
            int offset = RichTextFlow.LocalOffset(caret.Paragraph, selection.Start);
            bool markdownish = Regex.IsMatch(text, @"(^|\n)\s*(#{1,3}\s|[-*+]\s|\d+[.)]\s|>|```)|\*\*|`[^`]+`|\[[^\]]+\]\(");
            var fragment = html.Length > 0 ? RichTextMarkup.SanitizeTree(html) : markdownish || text.Contains("\n\n", StringComparison.Ordinal) ? RichTextMarkup.SanitizeTree(RichTextMarkup.MarkdownToHtml(text)) : null;
            if (fragment is { Children.Count: 0 }) { fragment = null; }
            bool single = fragment is { Children.Count: 1 } && fragment.Children[0].Tag == "p";
            // Plain text, code blocks, and list items take the text as it is; everything else keeps its structure.
            if (fragment is null || block.Tag == "pre" || (block.Tag == "li" && !single))
            {
                string plain = block.Tag == "pre" ? text : Regex.Replace(text, @"\s*\n\s*", " ");
                RichTextOps.InsertInline(block, offset, [RichNode.OfText(plain)]);
                StripFiller(block);
                MergePre(block);
                _flow.Build(_document, root);
                PlaceAt(block, offset + plain.Length);
                return;
            }
            if (single)
            {
                var nodes = fragment!.Children[0].Children;
                int length = nodes.Sum(RichTextOps.Length);
                RichTextOps.InsertInline(block, offset, nodes);
                StripFiller(block);
                _flow.Build(_document, root);
                PlaceAt(block, offset + length);
                return;
            }
            var (head, tail) = RichTextOps.Split(block.Children, offset);
            var rest = new RichNode(block.Tag == "li" ? "p" : block.Tag);
            rest.Children.AddRange(tail);
            block.Children.Clear();
            block.Children.AddRange(head);
            var top = block;
            RichNode anchor = top;
            foreach (var node in fragment!.Children)
            {
                RichTextOps.InsertAfter(root, anchor, node);
                anchor = node;
            }
            if (RichTextOps.Visible(rest).Length > 0 || RichTextOps.Contains(rest, "br"))
            {
                RichTextOps.InsertAfter(root, anchor, rest);
                RichTextOps.EnsureFilled(rest);
            }
            if (RichTextOps.Visible(block).Length == 0 && block.Tag == "p") { RichTextOps.Parent(root, block)!.Children.Remove(block); }
            else { RichTextOps.EnsureFilled(block); }
            var endNode = anchor;
            while (RichTextOps.IsList(endNode) && endNode.Children.Count > 0) { endNode = endNode.Children[^1]; }
            _flow.Build(_document, root);
            if (endNode.Tag != "hr") { PlaceAt(endNode, RichTextOps.Length(endNode)); }
        });
    }

    private static void StripFiller(RichNode block)
    {
        if (RichTextOps.Visible(block).Length > 0 && block.Children.Count > 0 && block.Children[^1].Tag == "br") { block.Children.RemoveAt(block.Children.Count - 1); }
    }

    private static void MergePre(RichNode block)
    {
        if (block.Tag != "pre") { return; }
        string text = RichTextMarkup.PreText(block);
        block.Children.Clear();
        block.Children.Add(RichNode.OfText(text));
    }

    // ---- keyboard ----

    private void OnEditorKey(object sender, KeyEventArgs e)
    {
        // A cancelled composition raises no final text input, so any ordinary key ends it.
        if (e.Key != Key.ImeProcessed) { _composing = false; }
        if (IsReadOnly) { return; }
        bool mod = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_slash is not null && _slashResults.Count > 0)
        {
            if (key is Key.Down or Key.Up)
            {
                e.Handled = true;
                _slashActive = (_slashActive + (key == Key.Down ? 1 : -1) + _slashResults.Count) % _slashResults.Count;
                MoveSlashHighlight(true);
                return;
            }
            if (key is Key.Enter or Key.Tab) { e.Handled = true; ChooseSlash(_slashResults[Math.Min(_slashActive, _slashResults.Count - 1)]); return; }
            if (key == Key.Escape) { e.Handled = true; CloseSlash(); return; }
        }
        if (mod && key == Key.Z) { e.Handled = true; if (shift) { Redo(); } else { Undo(); } return; }
        if (mod && key == Key.Y) { e.Handled = true; Redo(); return; }
        if (mod && !shift && key == Key.B) { e.Handled = true; ToggleMark("bold"); return; }
        if (mod && !shift && key == Key.I) { e.Handled = true; ToggleMark("italic"); return; }
        if (mod && shift && key == Key.X) { e.Handled = true; ToggleMark("strike"); return; }
        if (mod && key == Key.E) { e.Handled = true; ToggleMark("code"); return; }
        if (mod && key == Key.K) { e.Handled = true; OpenLink(); return; }
        if (mod && key == Key.U) { e.Handled = true; return; }
        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Alt) && key == Key.F10 && _barShown) { e.Handled = true; _formatTools[0].Focus(); return; }
        if (key == Key.Enter)
        {
            e.Handled = true;
            if (_slash is not null) { return; }
            Command(() => EnterKey(shift));
            return;
        }
        if (key == Key.Back)
        {
            bool handled = false;
            Rewrite(() => handled = BackspaceAtStart());
            if (handled)
            {
                e.Handled = true;
                Record("command");
                Emit();
                QueueSync();
            }
            return;
        }
        if (key == Key.Tab && CaretBlock() is { } caret)
        {
            if (caret.Node.Tag == "li")
            {
                e.Handled = true;
                Command(() =>
                {
                    var offsets = ReadOffsets();
                    var root = _flow.Root;
                    if (shift) { RichTextOps.Outdent(root, caret.Node); } else { RichTextOps.Indent(root, caret.Node); }
                    _flow.Build(_document, root);
                    WriteOffsets(offsets);
                });
            }
            else if (caret.Node.Tag == "pre" && !shift)
            {
                e.Handled = true;
                PasteIntoPre("  ");
            }
        }
    }

    internal void PasteIntoPre(string text) => Command(() =>
    {
        var selection = _box!.Selection;
        if (!selection.IsEmpty) { selection.Text = ""; }
        if (CaretBlock() is not { } caret) { return; }
        int offset = RichTextFlow.LocalOffset(caret.Paragraph, selection.Start);
        string content = caret.Node.Content;
        caret.Node.Children.Clear();
        caret.Node.Children.Add(RichNode.OfText(content[..offset] + text + content[offset..]));
        _flow.Build(_document, _flow.Root);
        PlaceAt(caret.Node, offset + text.Length);
    });

    // ---- floating toolbar: one surface whose size springs between its faces and whose position glides with the selection ----

    private Border _bar = null!;
    private Canvas _faces = null!;
    private StackPanel _formatFace = null!, _linkFace = null!, _blockTools = null!;
    private readonly List<ToolButton> _formatTools = [];
    private ToolButton _boldTool = null!, _italicTool = null!, _strikeTool = null!, _codeTool = null!, _linkTool = null!, _h1Tool = null!, _h2Tool = null!, _quoteTool = null!, _removeTool = null!;
    private TextBox _linkInput = null!;
    private TextBlock _linkPlaceholder = null!;
    private ArcChromeButton _apply = null!;
    private PhysicalSpring _tx = null!, _ty = null!, _tw = null!, _th = null!;
    private readonly ScaleTransform _barScale = new();
    private readonly TranslateTransform _barShift = new();
    private bool _barShown, _barBelow, _barPlaced;
    private string _face = "format";
    private (int From, int To)? _savedRange;

    internal sealed class ToolButton : ArcChromeButton
    {
        private readonly ScaleTransform _press = new();
        private readonly OwnedMotion _pressMotion = new();
        public bool Pressed { get; set; }

        public ToolButton(string label, string icon, string? shortcut, Action press) : base(11)
        {
            Width = Height = 32;
            Focusable = true;
            Padding = new Thickness(0);
            RenderTransformOrigin = new Point(.5, .5);
            RenderTransform = _press;
            ToolTip = shortcut is null ? label : $"{label} ({shortcut})";
            AutomationProperties.SetName(this, label);
            Roles = button =>
            {
                if (Pressed) { return ("AccentSubtle", null, "AccentStrong"); }
                return button.IsMouseOver || button.IsKeyboardFocused ? ("SurfaceMuted", null, "Foreground") : (null, null, "TextSecondary");
            };
            Content = ArcText.Icon(icon, 16, Ink);
            Click += (_, _) => press();
        }

        protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnIsPressedChanged(e);
            double scale = IsPressed ? .92 : 1;
            _pressMotion.Double(_press, ScaleTransform.ScaleXProperty, scale, .12, true);
            _pressMotion.Double(_press, ScaleTransform.ScaleYProperty, scale, .12, true);
        }
    }

    private ToolButton Tool(string label, string icon, string? shortcut, Action press, bool refocus = true)
    {
        var button = new ToolButton(label, icon, shortcut, () =>
        {
            press();
            if (refocus && _face == "format") { _box?.Focus(); }
        });
        button.GotKeyboardFocus += (_, _) => button.Refresh(true);
        button.LostKeyboardFocus += (_, _) => button.Refresh(true);
        return button;
    }

    private void BuildToolbar()
    {
        _tx = new PhysicalSpring(this, PlaceBar);
        _ty = new PhysicalSpring(this, PlaceBar);
        _tw = new PhysicalSpring(this, PlaceBar);
        _th = new PhysicalSpring(this, PlaceBar);
        _faces = new Canvas();
        _bar = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = new ClippedHost(15) { Child = _faces },
            Visibility = Visibility.Collapsed,
            RenderTransform = new TransformGroup { Children = { _barScale, _barShift } },
        };
        AutomationProperties.SetName(_bar, "Formatting");
        _bar.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        _bar.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _bar.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        _bar.PreviewKeyDown += OnToolbarKey;
        _floating.Children.Add(_bar);

        _boldTool = Tool("Bold", "bold", "Ctrl+B", () => ToggleMark("bold"));
        _italicTool = Tool("Italic", "italic", "Ctrl+I", () => ToggleMark("italic"));
        _strikeTool = Tool("Strikethrough", "strikethrough", "Ctrl+Shift+X", () => ToggleMark("strike"));
        _codeTool = Tool("Inline code", "code", "Ctrl+E", () => ToggleMark("code"));
        _linkTool = Tool("Link", "link", "Ctrl+K", OpenLink, refocus: false);
        _h1Tool = Tool("Heading 1", "heading-1", null, () => Command(() => ApplyBlock("h1")));
        _h2Tool = Tool("Heading 2", "heading-2", null, () => Command(() => ApplyBlock("h2")));
        _quoteTool = Tool("Quote", "quote", null, () => Command(() => ApplyBlock("blockquote")));
        var divider = new Border { Width = 1, Height = 18, Margin = new Thickness(4, 0, 6, 0) };
        divider.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Border");
        _blockTools = new StackPanel { Orientation = Orientation.Horizontal };
        _blockTools.Children.Add(divider);
        _blockTools.Children.Add(Spaced(_h1Tool));
        _blockTools.Children.Add(Spaced(_h2Tool));
        _blockTools.Children.Add(_quoteTool);
        _formatFace = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        foreach (var tool in new[] { _boldTool, _italicTool, _strikeTool, _codeTool, _linkTool })
        {
            _formatFace.Children.Add(Spaced(tool));
        }
        _formatFace.Children.Add(_blockTools);
        _formatTools.AddRange([_boldTool, _italicTool, _strikeTool, _codeTool, _linkTool, _h1Tool, _h2Tool, _quoteTool]);

        var back = Tool("Back to formatting", "arrow-left", null, () => CloseLink(), refocus: false);
        back.ToolTip = null;
        _linkInput = new TextBox
        {
            Width = 240,
            Height = 32,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(6, 0, 6, 0),
            FontSize = 14,
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null,
        };
        AutomationProperties.SetName(_linkInput, "Link address");
        _linkInput.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
        _linkInput.SetResourceReference(TextBoxBase.CaretBrushProperty, "ArcWpf.Brush.Accent");
        _linkInput.TextChanged += (_, _) => RefreshLink();
        _linkInput.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; CloseLink(); }
            else if (e.Key == Key.Enter) { e.Handled = true; ApplyLink(); }
        };
        _linkPlaceholder = new TextBlock { Text = "Paste or type a link", FontSize = 14, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        _linkPlaceholder.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        var field = new Grid { Margin = new Thickness(0, 0, 2, 0) };
        field.Children.Add(_linkInput);
        field.Children.Add(_linkPlaceholder);
        _removeTool = Tool("Remove link", "unlink", null, () => ApplyLink(true), refocus: false);
        _removeTool.ToolTip = null;
        _removeTool.Margin = new Thickness(0, 0, 2, 0);
        _apply = new ArcChromeButton(11)
        {
            Width = 32,
            Height = 32,
            Roles = _ => _linkInput.Text.Trim().Length > 0 ? ("Accent", null, "AccentForeground") : ("SurfaceMuted", null, "TextMuted"),
        };
        _apply.Content = ArcText.Icon("check", 16, _apply.Ink, 2);
        AutomationProperties.SetName(_apply, "Apply link");
        _apply.Click += (_, _) => ApplyLink();
        _linkFace = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4), Visibility = Visibility.Collapsed };
        _linkFace.Children.Add(Spaced(back));
        _linkFace.Children.Add(field);
        _linkFace.Children.Add(_removeTool);
        _linkFace.Children.Add(_apply);
        foreach (var face in new[] { _formatFace, _linkFace })
        {
            face.RenderTransform = new TranslateTransform();
            _faces.Children.Add(face);
        }
    }

    private static ToolButton Spaced(ToolButton tool)
    {
        tool.Margin = new Thickness(0, 0, 2, 0);
        return tool;
    }

    private void UpdateNarrow(double width)
    {
        // Narrow editors keep the inline marks; block types stay one slash away.
        _blockTools.Visibility = width <= 330 ? Visibility.Collapsed : Visibility.Visible;
        if (_barShown) { Dispatcher.BeginInvoke(() => ReportFace(true), DispatcherPriority.Loaded); }
    }

    private void RefreshTools()
    {
        void Set(ToolButton tool, bool pressed)
        {
            tool.Pressed = pressed;
            tool.Refresh(true);
        }
        Set(_boldTool, _formats.Bold);
        Set(_italicTool, _formats.Italic);
        Set(_strikeTool, _formats.Strike);
        Set(_codeTool, _formats.Code);
        Set(_linkTool, _formats.Link is not null);
        Set(_h1Tool, _formats.Block == "h1");
        Set(_h2Tool, _formats.Block == "h2");
        Set(_quoteTool, _formats.Block == "blockquote");
        string name = _formats.Link is not null ? "Edit link" : "Link";
        AutomationProperties.SetName(_linkTool, name);
        _linkTool.ToolTip = $"{name} (Ctrl+K)";
    }

    private void RefreshLink()
    {
        _linkPlaceholder.Visibility = _linkInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _apply.Refresh(true);
    }

    private Size FaceSize(FrameworkElement face)
    {
        face.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        // The last tool of the row has no trailing gap in the source.
        return new Size(face.DesiredSize.Width - (ReferenceEquals(face, _formatFace) && _blockTools.Visibility != Visibility.Visible ? 2 : 0), face.DesiredSize.Height);
    }

    private void ReportFace(bool animate)
    {
        var size = FaceSize(_face == "link" ? _linkFace : _formatFace);
        if (!animate || ArcMotion.GetLevel(this) != ArcMotionLevel.Full || _tw.Value == 0)
        {
            _tw.Jump(size.Width);
            _th.Jump(size.Height);
            return;
        }
        // MORPH: physical(spring.morph, .12).
        _tw.To(size.Width, .42, .12);
        _th.To(size.Height, .42, .12);
    }

    private void PlaceBar()
    {
        double width = _tw.Value, height = _th.Value;
        _bar.Width = Math.Max(0, width);
        _bar.Height = Math.Max(0, height);
        Canvas.SetLeft(_bar, _tx.Value - width / 2);
        Canvas.SetTop(_bar, _barBelow ? _ty.Value : _ty.Value - height);
    }

    private void ShowBar(double x, double y, bool below)
    {
        _barBelow = below;
        bool reduced = ArcMotion.GetLevel(this) != ArcMotionLevel.Full;
        if (!_barShown)
        {
            _barShown = true;
            _motion.Stop();
            _bar.Effect = null;
            _bar.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
            _bar.Visibility = Visibility.Visible;
            _formatFace.Visibility = _face == "format" ? Visibility.Visible : Visibility.Collapsed;
            _linkFace.Visibility = _face == "link" ? Visibility.Visible : Visibility.Collapsed;
            ResetFace(_formatFace);
            ResetFace(_linkFace);
            ReportFace(false);
            double half = _tw.Value / 2;
            _tx.Jump(Clamp(x, half));
            _ty.Jump(y);
            _bar.RenderTransformOrigin = new Point(.5, below ? 0 : 1);
            _bar.Opacity = 0;
            _motion.Double(_bar, OpacityProperty, 1, .16, true, enter: true);
            if (!reduced)
            {
                _barScale.ScaleX = _barScale.ScaleY = .94;
                _barShift.Y = below ? -4 : 4;
                _motion.Double(_barScale, ScaleTransform.ScaleXProperty, 1, .26, true, spring: true);
                _motion.Double(_barScale, ScaleTransform.ScaleYProperty, 1, .26, true, spring: true);
                _motion.Double(_barShift, TranslateTransform.YProperty, 0, .26, true, spring: true);
            }
            else
            {
                _barScale.ScaleX = _barScale.ScaleY = 1;
                _barShift.Y = 0;
            }
            return;
        }
        _bar.RenderTransformOrigin = new Point(.5, below ? 0 : 1);
        double target = Clamp(x, _tw.Value / 2);
        if (reduced)
        {
            _tx.Jump(target);
            _ty.Jump(y);
            return;
        }
        // GLIDE: physical(.3, 0).
        _tx.To(target, .3, 0);
        _ty.To(y, .3, 0);
    }

    private double Clamp(double x, double half) => Math.Max(half + 4, Math.Min(x, _host.ActualWidth - half - 4));

    private static void ResetFace(FrameworkElement face)
    {
        face.BeginAnimation(OpacityProperty, null);
        face.Opacity = 1;
        face.Effect = null;
        var shift = (TranslateTransform)face.RenderTransform;
        shift.BeginAnimation(TranslateTransform.XProperty, null);
        shift.X = 0;
    }

    private void HideBar()
    {
        if (!_barShown) { return; }
        _barShown = false;
        _tx.Stop();
        _ty.Stop();
        if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full)
        {
            _motion.Double(_bar, OpacityProperty, 0, .16, true, completed: FinishHide);
            return;
        }
        var ease = new KeySpline(.22, 1, .36, 1);
        _motion.Double(_bar, OpacityProperty, 0, .144, true, completed: FinishHide, ease: ease);
        _motion.Double(_barScale, ScaleTransform.ScaleXProperty, .96, .144, true, ease: ease);
        _motion.Double(_barScale, ScaleTransform.ScaleYProperty, .96, .144, true, ease: ease);
    }

    private void FinishHide()
    {
        if (!_barShown) { _bar.Visibility = Visibility.Collapsed; }
    }

    private void SwitchFace(string face)
    {
        if (_face == face) { return; }
        _face = face;
        var incoming = face == "link" ? _linkFace : _formatFace;
        var outgoing = face == "link" ? _formatFace : _linkFace;
        double direction = face == "link" ? 1 : -1;
        incoming.Visibility = Visibility.Visible;
        ReportFace(true);
        if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full)
        {
            incoming.Opacity = 0;
            _motion.Double(incoming, OpacityProperty, 1, .14, true);
            _motion.Double(outgoing, OpacityProperty, 0, .1, true, completed: () => { if (_face != FaceOf(outgoing)) { outgoing.Visibility = Visibility.Collapsed; } });
            return;
        }
        var standard = new KeySpline(.22, 1, .36, 1);
        var inShift = (TranslateTransform)incoming.RenderTransform;
        inShift.BeginAnimation(TranslateTransform.XProperty, null);
        inShift.X = direction * Travel;
        incoming.BeginAnimation(OpacityProperty, null);
        incoming.Opacity = 0;
        _motion.Double(inShift, TranslateTransform.XProperty, 0, .42, true, spring: true);
        _motion.Double(incoming, OpacityProperty, 1, .2, true, delay: .03, enter: true);
        ArcEffects.BlurIn(_motion, incoming, 2, .2, .03);
        var outShift = (TranslateTransform)outgoing.RenderTransform;
        _motion.Double(outShift, TranslateTransform.XProperty, direction * -Travel, .42, true, spring: true);
        _motion.Double(outgoing, OpacityProperty, 0, .12, true, ease: standard, completed: () => { if (_face != FaceOf(outgoing)) { outgoing.Visibility = Visibility.Collapsed; } });
        ArcEffects.BlurOut(_motion, outgoing, 2, .12);
    }

    private string FaceOf(FrameworkElement face) => ReferenceEquals(face, _linkFace) ? "link" : "format";

    private void OnToolbarKey(object sender, KeyEventArgs e)
    {
        if (_face != "format") { return; }
        int at = _formatTools.FindIndex(tool => tool.IsKeyboardFocused);
        var visible = _formatTools.Where(tool => tool.IsVisible).ToList();
        if (e.Key is Key.Right or Key.Left)
        {
            e.Handled = true;
            int index = visible.IndexOf(at >= 0 ? _formatTools[at] : visible[0]);
            visible[(index + (e.Key == Key.Right ? 1 : -1) + visible.Count) % visible.Count].Focus();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _box?.Focus();
            QueueSync();
        }
    }

    // ---- links ----

    internal void OpenLink()
    {
        if (_box is null) { return; }
        var selection = _box.Selection;
        var existing = FindLink(StartRun());
        if (existing is not null) { selection.Select(existing.ContentStart, existing.ContentEnd); }
        if (selection.IsEmpty) { return; }
        _savedRange = ReadOffsets();
        _linkInput.Text = existing?.Tag as string ?? "";
        _removeTool.Visibility = existing is not null ? Visibility.Visible : Visibility.Collapsed;
        RefreshLink();
        if (!_barShown)
        {
            _box.UpdateLayout();
            var (top, whole) = SelectionRects();
            bool below = top - ClipBand().Top < 60;
            _face = "link";
            ShowBar(whole.Left + whole.Width / 2, below ? whole.Bottom + 8 : top - 8, below);
        }
        else { SwitchFace("link"); }
        Dispatcher.BeginInvoke(() => _linkInput.Focus(), DispatcherPriority.Input);
    }

    internal string LinkDraftForTest { get => _linkInput.Text; set => _linkInput.Text = value; }

    internal void CloseLink(bool focusEditor = true)
    {
        SwitchFace("format");
        if (focusEditor && _savedRange is { } saved)
        {
            _box!.Focus();
            WriteOffsets(saved);
        }
        _savedRange = null;
        QueueSync();
    }

    internal void ApplyLink(bool remove = false)
    {
        string url = RichTextMarkup.NormalizeUrl(_linkInput.Text);
        if (_savedRange is not { } saved) { CloseLink(); return; }
        _box!.Focus();
        WriteOffsets(saved);
        Command(() =>
        {
            var selection = _box.Selection;
            var root = _flow.Read(_document);
            var start = selection.Start;
            var startParagraph = start.Paragraph;
            var targets = Paragraphs(_document.Blocks).Where(paragraph => paragraph.ContentEnd.CompareTo(selection.Start) >= 0 && paragraph.ContentStart.CompareTo(selection.End) <= 0).ToList();
            var startNode = _flow.NodeOf(startParagraph);
            var anchor = startNode is not null && startParagraph is not null ? RichTextOps.AnchorAt(startNode, RichTextFlow.LocalOffset(startParagraph, start)) : null;
            string selected = RichTextFlow.TextBetween(selection.Start, selection.End);
            if (remove || url.Length == 0)
            {
                if (anchor is not null && startNode is not null) { RichTextOps.RemoveAnchor(startNode, anchor); }
                else { ForEachRange(targets, selection, RichTextOps.UnlinkRange); }
            }
            else if (anchor is not null && selected == anchor.Content) { anchor.Href = url; }
            else { ForEachRange(targets, selection, (block, from, to) => RichTextOps.LinkRange(block, from, to, url)); }
            _flow.Build(_document, root);
            var after = RichTextFlow.At(_document, saved.To);
            _box.Selection.Select(after, after);
        });
        _savedRange = null;
        _face = "format";
        _formatFace.Visibility = Visibility.Visible;
        _linkFace.Visibility = Visibility.Collapsed;
        HideBar();
        Announcer.Say(this, remove || url.Length == 0 ? "Link removed" : "Link added");

        void ForEachRange(List<Paragraph> targets, TextSelection selection, Action<RichNode, int, int> apply)
        {
            foreach (var paragraph in targets)
            {
                if (_flow.NodeOf(paragraph) is not { } node) { continue; }
                int from = paragraph.ContentStart.CompareTo(selection.Start) >= 0 ? 0 : RichTextFlow.LocalOffset(paragraph, selection.Start);
                int to = paragraph.ContentEnd.CompareTo(selection.End) <= 0 ? RichTextOps.Length(node) : RichTextFlow.LocalOffset(paragraph, selection.End);
                if (to > from) { apply(node, from, to); }
            }
        }
    }

    private Window? _window;

    private void AttachWindow()
    {
        _window = Window.GetWindow(this);
        if (_window is not null) { _window.PreviewMouseDown += OnWindowPress; }
    }

    private void DetachWindow()
    {
        if (_window is not null) { _window.PreviewMouseDown -= OnWindowPress; }
        _window = null;
    }

    private void OnWindowPress(object sender, MouseButtonEventArgs e)
    {
        if (_face != "link" || (e.OriginalSource is DependencyObject source && _bar.IsAncestorOf(source)) || e.OriginalSource == _bar) { return; }
        _savedRange = null;
        _face = "format";
        _formatFace.Visibility = Visibility.Visible;
        ResetFace(_formatFace);
        _linkFace.Visibility = Visibility.Collapsed;
        HideBar();
    }

    // ---- slash menu ----

    private sealed class SlashState(TextPointer start) { public TextPointer Start { get; } = start; public string Query { get; set; } = ""; public double X, Y, Max; public bool Above; }

    private SlashState? _slash;
    private Border _slashPanel = null!, _slashHighlight = null!;
    private ScrollViewer _slashScroll = null!;
    private StackPanel _slashRows = null!;
    private List<(string Type, string Label, string Hint, string Keywords, string Icon)> _slashResults = [];
    private int _slashActive;
    private PhysicalSpring _panelHeight = null!, _hy = null!;
    private readonly ScaleTransform _slashScale = new();
    private readonly TranslateTransform _slashShift = new(), _highlightShift = new();
    private bool _slashShown;

    private void BuildSlash()
    {
        _panelHeight = new PhysicalSpring(this, PlaceSlash);
        _hy = new PhysicalSpring(this, () => _highlightShift.Y = _hy.Value);
        _slashHighlight = new Border { Height = 34, VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(12), RenderTransform = _highlightShift, IsHitTestVisible = false };
        _slashHighlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.EditorSlashHighlight");
        _slashRows = new StackPanel();
        var list = new Grid { Margin = new Thickness(6) };
        list.Children.Add(_slashHighlight);
        list.Children.Add(_slashRows);
        _slashScroll = new ScrollViewer { Focusable = false, Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _slashPanel = new Border
        {
            Width = 240,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = new ClippedHost(17) { Child = _slashScroll },
            Visibility = Visibility.Collapsed,
            RenderTransform = new TransformGroup { Children = { _slashScale, _slashShift } },
            Cursor = Cursors.Arrow,
        };
        AutomationProperties.SetName(_slashPanel, "Blocks");
        _slashPanel.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        _slashPanel.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _slashPanel.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        _floating.Children.Add(_slashPanel);
    }

    private void OpenSlashIfTyped(string data)
    {
        var selection = _box!.Selection;
        if (data != "/" || !selection.IsEmpty || selection.Start.Paragraph is not { } paragraph || paragraph.Tag as string == "pre") { return; }
        var caret = selection.Start;
        if (caret.Parent is Run run && _flow.IsCode(run)) { return; }
        string before = RichTextFlow.TextBetween(paragraph.ContentStart, caret);
        if (!before.EndsWith('/') || (before.Length > 1 && !Regex.IsMatch(before[^2].ToString(), @"[\s\u00a0\u200b]"))) { return; }
        var start = caret.GetPositionAtOffset(-1, LogicalDirection.Backward);
        if (start is null) { return; }
        _box.UpdateLayout();
        var mark = Local(start, LogicalDirection.Forward, paragraph.FontSize);
        var band = ClipBand();
        bool above = band.Bottom - mark.Bottom < 330 && mark.Top - band.Top > band.Bottom - mark.Bottom;
        double room = above ? mark.Top - band.Top - 12 : band.Bottom - mark.Bottom - 12;
        _slash = new SlashState(start.GetPositionAtOffset(0, LogicalDirection.Backward)!)
        {
            X = Math.Max(0, Math.Min(mark.Left - 8, _host.ActualWidth - 244)),
            Y = above ? mark.Top - 6 : mark.Bottom + 6,
            Above = above,
            Max = Math.Max(150, room),
        };
        _slashActive = 0;
        HideBar();
        RefreshSlash(false);
    }

    private void RefreshSlash(bool animate)
    {
        if (_slash is not { } open) { return; }
        string query = open.Query.Trim().ToLowerInvariant();
        _slashResults = query.Length == 0 ? SlashItems.ToList() : SlashItems.Where(item => $"{item.Label} {item.Keywords}".ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Any(word => word.StartsWith(query, StringComparison.Ordinal)) || item.Label.ToLowerInvariant().Contains(query, StringComparison.Ordinal)).ToList();
        if (_slashResults.Count == 0) { HideSlashPanel(); return; }
        _slashActive = Math.Min(_slashActive, _slashResults.Count - 1);
        _slashRows.Children.Clear();
        for (int index = 0; index < _slashResults.Count; index++) { _slashRows.Children.Add(MakeSlashRow(_slashResults[index], index)); }
        double target = Math.Min(_slashResults.Count * 34 + 12, open.Max);
        bool full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        if (!_slashShown)
        {
            _slashShown = true;
            _motion.Stop();
            _slashPanel.Visibility = Visibility.Visible;
            _panelHeight.Jump(target);
            _hy.Jump(_slashActive * 34);
            _slashPanel.Opacity = 0;
            _motion.Double(_slashPanel, OpacityProperty, 1, .16, true, enter: true);
            _slashScale.CenterX = 16;
            if (full)
            {
                _slashScale.ScaleX = _slashScale.ScaleY = .96;
                _slashShift.Y = open.Above ? 4 : -4;
                _motion.Double(_slashScale, ScaleTransform.ScaleXProperty, 1, .26, true, spring: true);
                _motion.Double(_slashScale, ScaleTransform.ScaleYProperty, 1, .26, true, spring: true);
                _motion.Double(_slashShift, TranslateTransform.YProperty, 0, .26, true, spring: true);
            }
            else
            {
                _slashScale.ScaleX = _slashScale.ScaleY = 1;
                _slashShift.Y = 0;
            }
        }
        else if (full && animate) { _panelHeight.To(target, .4, 0); }
        else { _panelHeight.Jump(target); }
        PlaceSlash();
        MoveSlashHighlight(animate);
    }

    private Border MakeSlashRow((string Type, string Label, string Hint, string Keywords, string Icon) item, int index)
    {
        bool active = index == _slashActive;
        var ink = new SolidColorBrush();
        var row = new Border { Height = 34, CornerRadius = new CornerRadius(12), Background = Brushes.Transparent, Padding = new Thickness(10, 0, 10, 0), Cursor = Cursors.Hand };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new ArcDataGridIcon { Icon = item.Icon, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        icon.SetResourceReference(ArcDataGridIcon.StrokeProperty, active ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextSecondary");
        var label = ArcText.Block(item.Label, 14, 19.6, active ? "Foreground" : "TextSecondary", medium: true);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.Margin = new Thickness(10, 0, 10, 0);
        Grid.SetColumn(label, 1);
        grid.Children.Add(icon);
        grid.Children.Add(label);
        if (item.Hint.Length > 0)
        {
            var hint = ArcText.Block(item.Hint, 12, 16, "TextMuted");
            hint.SetResourceReference(TextBlock.FontFamilyProperty, "ArcWpf.Font.Mono");
            hint.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(hint, 2);
            grid.Children.Add(hint);
        }
        row.Child = grid;
        row.Tag = (icon, label);
        row.MouseMove += (_, _) =>
        {
            if (_slashActive == index) { return; }
            _slashActive = index;
            MoveSlashHighlight(true);
        };
        row.MouseLeftButtonDown += (_, e) => e.Handled = true;
        row.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            ChooseSlash(item);
        };
        return row;
    }

    private void MoveSlashHighlight(bool animate)
    {
        double top = _slashActive * 34;
        for (int index = 0; index < _slashRows.Children.Count; index++)
        {
            if (((Border)_slashRows.Children[index]).Tag is not ValueTuple<ArcDataGridIcon, TextBlock> parts) { continue; }
            bool active = index == _slashActive;
            parts.Item1.SetResourceReference(ArcDataGridIcon.StrokeProperty, active ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextSecondary");
            parts.Item2.SetResourceReference(TextBlock.ForegroundProperty, active ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextSecondary");
        }
        if (animate && ArcMotion.GetLevel(this) == ArcMotionLevel.Full) { _hy.To(top, .3, 0); } else { _hy.Jump(top); }
        // A short panel scrolls to keep the active row in view.
        double rowTop = top + 6, rowBottom = rowTop + 34;
        if (rowTop - 6 < _slashScroll.VerticalOffset) { _slashScroll.ScrollToVerticalOffset(rowTop - 6); }
        else if (rowBottom + 6 > _slashScroll.VerticalOffset + _slashScroll.ViewportHeight && _slashScroll.ViewportHeight > 0) { _slashScroll.ScrollToVerticalOffset(rowBottom + 6 - _slashScroll.ViewportHeight); }
    }

    private void PlaceSlash()
    {
        if (_slash is not { } open) { return; }
        double height = _panelHeight.Value;
        _slashPanel.Height = Math.Max(0, height);
        _slashPanel.MaxWidth = Math.Max(0, _host.ActualWidth - 8);
        _slashScale.CenterY = open.Above ? height : 0;
        Canvas.SetLeft(_slashPanel, open.X);
        Canvas.SetTop(_slashPanel, open.Above ? open.Y - height : open.Y);
    }

    private void HideSlashPanel()
    {
        if (!_slashShown) { return; }
        _slashShown = false;
        void Done() { if (!_slashShown) { _slashPanel.Visibility = Visibility.Collapsed; } }
        if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full)
        {
            _motion.Double(_slashPanel, OpacityProperty, 0, .16, true, completed: Done);
            return;
        }
        var ease = new KeySpline(.22, 1, .36, 1);
        _motion.Double(_slashPanel, OpacityProperty, 0, .144, true, completed: Done, ease: ease);
        _motion.Double(_slashScale, ScaleTransform.ScaleXProperty, .97, .144, true, ease: ease);
        _motion.Double(_slashScale, ScaleTransform.ScaleYProperty, .97, .144, true, ease: ease);
    }

    private void CloseSlash()
    {
        _slash = null;
        HideSlashPanel();
    }

    private void ChooseSlash((string Type, string Label, string Hint, string Keywords, string Icon) item)
    {
        if (_slash is not { } open || _box is null) { return; }
        var caret = _box.Selection.Start;
        _slash = null;
        HideSlashPanel();
        Command(() =>
        {
            if (open.Start.CompareTo(caret) < 0) { new TextRange(open.Start, caret).Text = ""; }
            var at = open.Start.GetInsertionPosition(LogicalDirection.Forward);
            _box.Selection.Select(at, at);
            ApplyBlock(item.Type);
        });
        Announcer.Say(this, $"{item.Label} added");
    }

    // ---- test surface ----

    internal RichTextBox? BoxForTest => _box;
    internal FlowDocument DocumentForTest => _document;
    internal bool IsToolbarShown => _barShown;
    internal bool IsSlashShown => _slashShown;
    internal string FaceForTest => _face;
    internal int SlashActiveForTest => _slashActive;
    internal IReadOnlyList<string> SlashLabelsForTest => _slashResults.Select(item => item.Label).ToList();
    internal bool HintShownForTest => _hint.Visibility == Visibility.Visible;
    internal string HintTextForTest => _hint.Text;
    internal int HistoryCountForTest => _stack.Count;
    internal Size ToolbarSizeForTest => new(_tw.Value, _th.Value);
    internal Point ToolbarAnchorForTest => new(_tx.Value, _ty.Value);

    internal void SelectForTest(int from, int to)
    {
        _box!.Selection.Select(RichTextFlow.At(_document, from), RichTextFlow.At(_document, to));
    }

    // Native typing without a keyboard: the same order of events the editor sees from PreviewTextInput and TextChanged.
    internal void TypeForTest(string text)
    {
        foreach (char c in text)
        {
            _typed = c.ToString();
            _box!.BeginChange();
            _box.Selection.Text = c.ToString();
            var end = _box.Selection.End;
            _box.Selection.Select(end, end);
            _box.EndChange();
        }
    }

    internal void EnterForTest(bool soft = false) => Command(() => EnterKey(soft));

    internal void BackspaceForTest()
    {
        bool handled = false;
        Rewrite(() => handled = BackspaceAtStart());
        if (handled)
        {
            Record("command");
            Emit();
        }
    }

    internal void ChooseSlashForTest(string label) => ChooseSlash(_slashResults.First(item => item.Label == label));

    internal void IndentForTest(bool outdent = false)
    {
        if (CaretBlock() is not { } caret) { return; }
        Command(() =>
        {
            var offsets = ReadOffsets();
            var root = _flow.Root;
            if (outdent) { RichTextOps.Outdent(root, caret.Node); } else { RichTextOps.Indent(root, caret.Node); }
            _flow.Build(_document, root);
            WriteOffsets(offsets);
        });
    }
}
