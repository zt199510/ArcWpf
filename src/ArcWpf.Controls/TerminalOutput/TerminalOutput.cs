using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum TerminalStatus { Idle, Running, Succeeded, Failed, Stopped }

/// <summary>
/// Shows a command's output as it arrives: colors, progress lines that rewrite themselves, and a view that follows the newest line until you scroll away.
/// It displays output and doesn't run anything; the host runs the process and passes its text to <see cref="Write"/>, from any thread.
/// </summary>
[TemplatePart(Name = "PART_Command", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_StatusIcon", Type = typeof(Decorator))]
[TemplatePart(Name = "PART_Status", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Actions", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Notes", Type = typeof(Panel))]
[TemplatePart(Name = "PART_RowsHost", Type = typeof(Decorator))]
[TemplatePart(Name = "PART_JumpHost", Type = typeof(Decorator))]
[TemplatePart(Name = "PART_Empty", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_HorizontalBar", Type = typeof(ScrollBar))]
public class TerminalOutput : Control
{
    // A virtualizing list measured with unlimited height would realize every row, so an unbounded parent gets this instead.
    private const double UnboundedBodyHeight = 320;
    // Each tick parses at most this much, so a flood of output can't hold up the window. What arrives faster than it can be shown is skipped, and a note says how much.
    private const int MaxCharsPerTick = 256 * 1024, MaxBacklog = 8 * 1024 * 1024;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(33);

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(string), typeof(TerminalOutput), new FrameworkPropertyMetadata("", (d, _) => ((TerminalOutput)d).UpdateHeader()));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(TerminalStatus), typeof(TerminalOutput), new FrameworkPropertyMetadata(TerminalStatus.Idle, (d, e) => ((TerminalOutput)d).StatusChanged((TerminalStatus)e.NewValue)));
    public static readonly DependencyProperty ExitCodeProperty = DependencyProperty.Register(nameof(ExitCode), typeof(int?), typeof(TerminalOutput), new FrameworkPropertyMetadata(null, (d, _) => ((TerminalOutput)d).UpdateHeader()));
    public static readonly DependencyProperty CanStopProperty = DependencyProperty.Register(nameof(CanStop), typeof(bool), typeof(TerminalOutput), new FrameworkPropertyMetadata(true, (d, _) => ((TerminalOutput)d).UpdateHeader()));
    public static readonly DependencyProperty MaxLinesProperty = DependencyProperty.Register(nameof(MaxLines), typeof(int), typeof(TerminalOutput), new FrameworkPropertyMetadata(10000), value => value is > 0);
    public static readonly DependencyProperty MaxBodyHeightProperty = DependencyProperty.Register(nameof(MaxBodyHeight), typeof(double), typeof(TerminalOutput), new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(nameof(EmptyText), typeof(string), typeof(TerminalOutput), new FrameworkPropertyMetadata("No output", (d, _) => ((TerminalOutput)d).UpdateEmpty()));

    private readonly TerminalBuffer _buffer = new();
    private readonly TerminalLines _lines = [];
    private readonly TerminalRowList _rows;
    private readonly StringBuilder _pending = new();
    private readonly object _gate = new();
    private readonly DispatcherTimer _flush;
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _copied;
    private readonly Stopwatch _elapsed = new();
    private readonly ToolbarButton _copy, _stop, _jump;
    private TextBlock? _command, _status, _empty;
    private Decorator? _statusIcon, _rowsHost, _jumpHost;
    private Panel? _actions, _notes;
    private ScrollBar? _bar;
    private FrameworkElement? _clip;
    private bool _following = true, _scheduled;
    private double _widest;
    private long _skippedChars;
    private long _droppedLines;

    static TerminalOutput() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TerminalOutput), new FrameworkPropertyMetadata(typeof(TerminalOutput)));

    public TerminalOutput()
    {
        _rows = new TerminalRowList(_buffer) { ItemsSource = _lines };
        _flush = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = FlushInterval };
        _flush.Tick += (_, _) => Flush(MaxCharsPerTick);
        _clock = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateHeader();
        _copied = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromSeconds(1.2) };
        _copied.Tick += (_, _) => { _copied.Stop(); _copy!.Content = Lucide.Glyph(Lucide.Copy, 16); };
        _copy = AiUi.Icon(Lucide.Copy, 28, 16, "Copy output");
        _copy.ToolTip = "Copy output";
        _copy.Click += (_, _) => CopyAll();
        _stop = AiUi.Icon(Lucide.Square, 28, 14, "Stop");
        _stop.ToolTip = "Stop";
        _stop.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
        _jump = new ToolbarButton(18, 1) { Width = 36, Height = 36, Padding = new Thickness(0), Content = Lucide.Glyph(Lucide.ArrowDown, 16), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed };
        _jump.Roles = self => (self.IsMouseOver ? "SurfaceMuted" : "SurfaceRaised", "Border", "Foreground");
        _jump.ToolTip = "Jump to latest";
        AutomationProperties.SetName(_jump, "Jump to latest");
        _jump.Click += (_, _) => { _following = true; MoveToEnd(); UpdateJump(); };
        _rows.HorizontalRequested += ScrollBy;
        _rows.CopyRequested += CopySelection;
        _rows.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Scrolled));
        SizeChanged += (_, _) => { UpdateBar(); ClipFrame(); };
        Unloaded += (_, _) => { _clock.Stop(); _copied.Stop(); };
        Loaded += (_, _) => { UpdateHeader(); if (Status == TerminalStatus.Running) { _clock.Start(); } Schedule(); };
    }

    /// <summary>The command line, shown in the header.</summary>
    public string Command { get => (string)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public TerminalStatus Status { get => (TerminalStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public int? ExitCode { get => (int?)GetValue(ExitCodeProperty); set => SetValue(ExitCodeProperty, value); }
    /// <summary>Whether the Stop button shows while running.</summary>
    public bool CanStop { get => (bool)GetValue(CanStopProperty); set => SetValue(CanStopProperty, value); }
    /// <summary>Lines kept. Past this the oldest go, and a note says how many.</summary>
    public int MaxLines { get => (int)GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }
    public double MaxBodyHeight { get => (double)GetValue(MaxBodyHeightProperty); set => SetValue(MaxBodyHeightProperty, value); }
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    /// <summary>True while the view stays on the newest line. Scrolling up turns it off; returning to the end or Jump to latest turns it back on.</summary>
    public bool IsFollowing => _following;

    /// <summary>Raised by the Stop button. The host stops the process and then sets <see cref="Status"/>.</summary>
    public event EventHandler? StopRequested;

    internal TerminalBuffer BufferForTest => _buffer;
    internal DrawnRowList RowsForTest => _rows;
    internal int ShownLinesForTest => _lines.Count;
    internal bool JumpShownForTest => _jump.Visibility == Visibility.Visible;
    internal bool StopShownForTest => _stop.Visibility == Visibility.Visible;
    internal string StatusTextForTest => _status?.Text ?? "";
    internal string CopyTextForTest() => SelectionText();
    internal ToolbarButton StopForTest => _stop;
    internal ToolbarButton JumpForTest => _jump;

    /// <summary>Adds output. Safe from any thread, and an escape sequence may be split across calls.</summary>
    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        bool schedule = false;
        lock (_gate)
        {
            _pending.Append(text);
            if (_pending.Length > MaxBacklog)
            {
                int skip = _pending.Length - MaxBacklog / 2;
                _pending.Remove(0, skip);
                _skippedChars += skip;
            }
            if (!_scheduled) { _scheduled = schedule = true; }
        }
        if (schedule && !Dispatcher.HasShutdownStarted) { Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(_flush.Start)); }
    }

    public void WriteLine(string text) => Write(text + "\n");

    /// <summary>Shows everything written so far right away, instead of on the next tick.</summary>
    public void Flush() => Flush(int.MaxValue);

    /// <summary>Removes all output, for reusing the view for another command.</summary>
    public void Clear()
    {
        lock (_gate) { _pending.Clear(); _skippedChars = 0; }
        _droppedLines = 0;
        _buffer.Clear();
        _lines.Clear();
        _widest = 0;
        _following = true;
        if (_bar is not null) { _bar.Value = 0; }
        UpdateBar();
        UpdateNotes();
        UpdateEmpty();
        UpdateJump();
    }

    /// <summary>All output as it reads on screen, one line per row.</summary>
    public string GetText()
    {
        Flush();
        return string.Join(Environment.NewLine, _lines.Select(line => line.Text));
    }

    public override void OnApplyTemplate()
    {
        if (_rowsHost is not null) { _rowsHost.Child = null; }
        if (_jumpHost is not null) { _jumpHost.Child = null; }
        _actions?.Children.Clear();
        if (_bar is not null) { _bar.ValueChanged -= BarMoved; }
        base.OnApplyTemplate();
        _command = GetTemplateChild("PART_Command") as TextBlock;
        _status = GetTemplateChild("PART_Status") as TextBlock;
        _statusIcon = GetTemplateChild("PART_StatusIcon") as Decorator;
        _empty = GetTemplateChild("PART_Empty") as TextBlock;
        _actions = GetTemplateChild("PART_Actions") as Panel;
        _notes = GetTemplateChild("PART_Notes") as Panel;
        _rowsHost = GetTemplateChild("PART_RowsHost") as Decorator;
        _jumpHost = GetTemplateChild("PART_JumpHost") as Decorator;
        _bar = GetTemplateChild("PART_HorizontalBar") as ScrollBar;
        _clip = GetTemplateChild("PART_Clip") as FrameworkElement;
        if (_rowsHost is not null) { _rowsHost.Child = _rows; }
        if (_jumpHost is not null) { _jumpHost.Child = _jump; }
        if (_bar is not null) { _bar.ValueChanged += BarMoved; }
        if (_actions is not null) { _actions.Children.Add(_copy); _actions.Children.Add(_stop); }
        if (_clip is not null) { _clip.SizeChanged += (_, _) => ClipFrame(); }
        _rows.MeasureCell();
        UpdateHeader();
        UpdateNotes();
        UpdateEmpty();
        UpdateBar();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _rows.MaxHeight = double.IsInfinity(constraint.Height) ? Math.Min(MaxBodyHeight, UnboundedBodyHeight) : MaxBodyHeight;
        return base.MeasureOverride(constraint);
    }

    private void Schedule()
    {
        lock (_gate) { if (_pending.Length > 0) { _scheduled = true; _flush.Start(); } }
    }

    private void Flush(int limit)
    {
        string chunk;
        bool more;
        lock (_gate)
        {
            int take = Math.Min(limit, _pending.Length);
            chunk = _pending.ToString(0, take);
            _pending.Remove(0, take);
            more = _pending.Length > 0;
            if (!more) { _scheduled = false; _flush.Stop(); }
        }
        if (chunk.Length > 0) { Apply(chunk); }
    }

    private void Apply(string chunk)
    {
        _buffer.Feed(chunk);
        int overflow = _buffer.Lines.Count - MaxLines;
        // Trimming a tenth at a time keeps removal from the head rare, because it resets the list.
        if (overflow > MaxLines / 10)
        {
            bool keepPlace = !_following;
            double offset = _rows.Scroller?.VerticalOffset ?? 0;
            _buffer.TrimHead(overflow);
            _lines.RemoveHead(Math.Min(overflow, _lines.Count));
            _droppedLines += overflow;
            if (keepPlace) { _rows.Scroller?.ScrollToVerticalOffset(Math.Max(0, offset - overflow)); }
        }
        // Rewriting a recent line can widen it, so these are looked at again along with the new ones.
        int measureFrom = Math.Max(0, _lines.Count - 64), shown = _buffer.ShownCount;
        for (int i = _lines.Count; i < shown; i++) { _lines.Add(_buffer.Lines[i]); }
        for (int i = measureFrom; i < _lines.Count; i++) { _widest = Math.Max(_widest, Columns(_lines[i])); }
        _rows.InvalidateRows();
        UpdateBar();
        UpdateNotes();
        UpdateEmpty();
        if (_following) { MoveToEnd(); }
        UpdateJump();
    }

    private double Columns(TerminalLine line) => line.Cells() * _rows.Cell * 1.1 + TerminalRowList.LeftInset + 20;

    private void MoveToEnd()
    {
        if (_rows.Scroller is not { } scroller) { return; }
        scroller.ScrollToEnd();
    }

    private void Scrolled(object sender, ScrollChangedEventArgs e)
    {
        var scroller = (ScrollViewer)e.OriginalSource;
        bool atEnd = scroller.VerticalOffset >= scroller.ScrollableHeight - .5;
        if (atEnd) { _following = true; }
        // New lines change the extent, which isn't the person scrolling away.
        else if (e.VerticalChange < 0 && e.ExtentHeightChange == 0) { _following = false; }
        // The list only knows its height after layout, so the move made when lines arrived can fall short.
        else if (_following && e.ExtentHeightChange != 0) { scroller.ScrollToEnd(); }
        UpdateJump();
    }

    private void UpdateJump() => _jump.Visibility = !_following && (_rows.Scroller?.ScrollableHeight ?? 0) > 0 ? Visibility.Visible : Visibility.Collapsed;

    private void StatusChanged(TerminalStatus status)
    {
        if (status == TerminalStatus.Running)
        {
            _elapsed.Restart();
            if (IsLoaded) { _clock.Start(); }
        }
        else
        {
            _elapsed.Stop();
            _clock.Stop();
            // Finished means complete: what was written before this should be on screen now.
            Flush();
        }
        UpdateHeader();
        UpdateEmpty();
        if (_status is not null) { UIElementAutomationPeer.FromElement(_status)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged); }
    }

    private void UpdateHeader()
    {
        if (_command is not null)
        {
            _command.Text = DiffText.Show(Command ?? "", out _);
            _command.ToolTip = string.IsNullOrEmpty(_command.Text) ? null : _command.Text;
        }
        string time = _elapsed.Elapsed.TotalSeconds < 10 ? _elapsed.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s"
            : _elapsed.Elapsed.TotalMinutes < 1 ? ((int)_elapsed.Elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s"
            : $"{(int)_elapsed.Elapsed.TotalMinutes}m {_elapsed.Elapsed.Seconds}s";
        bool timed = _elapsed.Elapsed > TimeSpan.Zero;
        string label = Status switch
        {
            TerminalStatus.Running => timed ? "Running · " + ((int)_elapsed.Elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s" : "Running",
            TerminalStatus.Succeeded => timed ? "Done · " + time : "Done",
            TerminalStatus.Failed => (ExitCode is { } code ? "Exit " + code.ToString(CultureInfo.InvariantCulture) : "Failed") + (timed ? " · " + time : ""),
            TerminalStatus.Stopped => timed ? "Stopped · " + time : "Stopped",
            _ => "",
        };
        if (_status is not null)
        {
            _status.Text = label;
            _status.SetResourceReference(TextBlock.ForegroundProperty, Status == TerminalStatus.Failed ? "ArcWpf.Brush.ArcDanger" : "ArcWpf.Brush.TextSecondary");
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
        if (_statusIcon is not null && !(Status == TerminalStatus.Running && _statusIcon.Child is AiSpinner))
        {
            // Status never rests on color alone: each one has its own icon.
            _statusIcon.Child = Status switch
            {
                TerminalStatus.Running => new AiSpinner(14),
                TerminalStatus.Succeeded => AiUi.Glyph(Lucide.Check, 14, "ArcSuccess", 2),
                TerminalStatus.Failed => AiUi.Glyph(Lucide.CircleAlert, 14, "ArcDanger"),
                TerminalStatus.Stopped => AiUi.Glyph(Lucide.Square, 12, "TextMuted"),
                _ => null,
            };
        }
        _stop.Visibility = Status == TerminalStatus.Running && CanStop ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(this, string.IsNullOrEmpty(Command) ? "Terminal output" : "Output of " + Command);
    }

    private void UpdateEmpty()
    {
        if (_empty is null) { return; }
        bool none = _lines.Count == 0;
        _empty.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
        _rows.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
        _empty.Text = Status == TerminalStatus.Running ? "Waiting for output…" : EmptyText;
    }

    private void UpdateNotes()
    {
        if (_notes is null) { return; }
        var notes = new List<string>();
        long skipped;
        lock (_gate) { skipped = _skippedChars; }
        if (_droppedLines > 0) { notes.Add($"Earlier output is gone: {_droppedLines.ToString("N0", CultureInfo.InvariantCulture)} lines past the {MaxLines.ToString("N0", CultureInfo.InvariantCulture)} kept."); }
        if (skipped > 0) { notes.Add($"Output came faster than it could be shown, so {skipped.ToString("N0", CultureInfo.InvariantCulture)} characters were skipped."); }
        if (_buffer.UsedScreenControls) { notes.Add("This program draws to a full terminal screen, so this view shows its text only and it may read out of order."); }
        if (_notes.Children.Count == notes.Count && notes.Select((note, i) => ((TextBlock)((Border)_notes.Children[i]).Child).Text == note).All(same => same)) { return; }
        _notes.Children.Clear();
        foreach (string note in notes)
        {
            var line = new Border { Padding = new Thickness(14, 6, 14, 6), BorderThickness = new Thickness(0, 0, 0, 1) };
            line.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
            line.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
            line.Child = AiUi.Wrapped(note, 12, 16.8, "TextSecondary");
            _notes.Children.Add(line);
        }
    }

    private void UpdateBar()
    {
        if (_bar is null) { return; }
        double viewport = Math.Max(0, _rows.ActualWidth > 0 ? _rows.ActualWidth : ActualWidth);
        double range = Math.Max(0, _widest - viewport);
        _bar.ViewportSize = viewport;
        _bar.Maximum = range;
        _bar.Visibility = range > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_bar.Value > range) { _bar.Value = range; }
        _rows.ScrollX = Math.Min(_rows.ScrollX, range);
    }

    private void ScrollBy(double delta)
    {
        if (_bar is not null) { _bar.Value = Math.Clamp(_bar.Value + delta, 0, _bar.Maximum); }
    }

    private void BarMoved(object sender, RoutedPropertyChangedEventArgs<double> e) => _rows.ScrollX = e.NewValue;

    private void ClipFrame()
    {
        if (_clip is null || _clip.ActualWidth <= 0) { return; }
        _clip.Clip = new RectangleGeometry(new Rect(0, 0, _clip.ActualWidth, _clip.ActualHeight), 13, 13);
    }

    private string SelectionText()
    {
        var selected = _rows.SelectedItems.Cast<TerminalLine>().ToHashSet();
        return string.Join(Environment.NewLine, _lines.Where(selected.Contains).Select(line => line.Text));
    }

    private void CopySelection() => SetClipboard(SelectionText());

    private void CopyAll()
    {
        if (!SetClipboard(GetText())) { return; }
        _copy.Content = Lucide.Glyph(Lucide.Check, 16);
        _copied.Stop();
        _copied.Start();
    }

    private static bool SetClipboard(string text)
    {
        if (text.Length == 0) { return false; }
        // Another program can be holding the clipboard open; losing one copy is better than crashing the host.
        try { Clipboard.SetText(text); return true; }
        catch (System.Runtime.InteropServices.ExternalException) { return false; }
    }

    // Removing from the head one at a time would raise thousands of events, each shifting the whole list.
    private sealed class TerminalLines : ObservableCollection<TerminalLine>
    {
        public void RemoveHead(int count)
        {
            if (count <= 0) { return; }
            ((List<TerminalLine>)Items).RemoveRange(0, count);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}

internal sealed class TerminalPalette(FrameworkElement owner)
{
    private readonly Dictionary<(int Color, bool Dim), Brush> _brushes = [];
    private readonly Brush[] _named = Enumerable.Range(0, 16).Select(i => owner.TryFindResource("ArcWpf.Brush.TerminalAnsi" + i) as Brush ?? Brushes.Gray).ToArray();
    public Brush Ink { get; } = owner.TryFindResource("ArcWpf.Brush.Foreground") as Brush ?? Brushes.Black;
    public Brush Back { get; } = owner.TryFindResource("ArcWpf.Brush.Surface") as Brush ?? Brushes.White;
    public Brush Mark { get; } = owner.TryFindResource("ArcWpf.Brush.ArcWarning") as Brush ?? Brushes.Orange;
    public Brush Selection { get; } = owner.TryFindResource("ArcWpf.Brush.DiffSelection") as Brush ?? Brushes.LightGray;

    // A program picks ink and background separately, and the theme has moved both. Badges like black on green have to stay legible.
    public static Brush Readable(Brush ink, Brush back)
    {
        Color i = ((SolidColorBrush)ink).Color, b = ((SolidColorBrush)back).Color;
        if (Contrast(i, b) >= 3) { return ink; }
        return Contrast(Colors.Black, b) >= Contrast(Colors.White, b) ? Brushes.Black : Brushes.White;
    }

    private static double Contrast(Color a, Color b)
    {
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }

    private static double Luminance(Color c)
    {
        static double Linear(byte v) { double s = v / 255.0; return s <= .03928 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
        return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
    }

    public Brush Of(int color, bool dim, Brush fallback)
    {
        if (color < 0 && !dim) { return fallback; }
        if (_brushes.TryGetValue((color, dim), out var brush)) { return brush; }
        Color value = color < 0 ? ((SolidColorBrush)fallback).Color
            : color < 16 ? ((SolidColorBrush)_named[color]).Color
            : Color.FromRgb((byte)(color >> 16), (byte)(color >> 8), (byte)color);
        if (dim) { value.A = 150; }
        var made = new SolidColorBrush(value);
        made.Freeze();
        _brushes[(color, dim)] = made;
        return made;
    }
}

internal sealed class TerminalRowList : DrawnRowList
{
    public const double LeftInset = 16;
    private TerminalPalette? _palette;

    public TerminalRowList(TerminalBuffer buffer)
    {
        Buffer = buffer;
        ThemeWatch.Add(this, () => { _palette = null; ThemeVersion++; InvalidateRows(); });
    }

    public TerminalBuffer Buffer { get; }
    public int ThemeVersion { get; private set; }
    public TerminalPalette Palette => _palette ??= new TerminalPalette(this);

    protected override DependencyObject GetContainerForItemOverride() => new TerminalRowItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is TerminalRowItem;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        var container = (TerminalRowItem)element;
        var line = (TerminalLine)item;
        container.Line = line;
        AutomationProperties.SetName(container, line.Text);
        base.PrepareContainerForItemOverride(element, item);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        base.ClearContainerForItemOverride(element, item);
        ((TerminalRowItem)element).Line = null;
    }
}

internal sealed class TerminalRowItem : DrawnRowItem
{
    public TerminalLine? Line { get; set; }

    private sealed record Drawing(FormattedText Text, List<(int Start, int Length, Brush Back)> Backs);

    protected override void OnRender(DrawingContext dc)
    {
        if (ItemsControl.ItemsControlFromItemContainer(this) is not TerminalRowList list || Line is not { } line || ActualWidth <= 0) { return; }
        double width = ActualWidth, height = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        var drawing = Build(list, line);
        double x = TerminalRowList.LeftInset - list.ScrollX, y = Math.Round((height - drawing.Text.Height) / 2);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
        foreach (var (start, length, back) in drawing.Backs)
        {
            if (drawing.Text.BuildHighlightGeometry(new Point(x, y), start, length) is { } shape) { dc.DrawGeometry(back, null, shape); }
        }
        dc.DrawText(drawing.Text, new Point(x, y));
        dc.Pop();
        if (IsSelected) { dc.DrawRectangle(list.Palette.Selection, null, new Rect(0, 0, width, height)); }
    }

    private static Drawing Build(TerminalRowList list, TerminalLine line)
    {
        if (line.Drawn is Drawing cached && line.DrawnVersion == line.Version && line.DrawnTheme == list.ThemeVersion) { return cached; }
        var palette = list.Palette;
        string shown = line.Text;
        var text = new FormattedText(shown, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, list.Face, DrawnRowList.TextSize, palette.Ink, list.PixelsPerDip);
        var backs = new List<(int, int, Brush)>();
        int length = line.Length;
        for (int start = 0; start < length;)
        {
            int id = line.StyleAt(start), end = start + 1;
            while (end < length && line.StyleAt(end) == id) { end++; }
            if (id != 0) { Style(text, palette, list.Buffer.StyleOf(id), start, end - start, backs); }
            start = end;
        }
        var drawing = new Drawing(text, backs);
        line.Drawn = drawing;
        line.DrawnVersion = line.Version;
        line.DrawnTheme = list.ThemeVersion;
        return drawing;
    }

    private static void Style(FormattedText text, TerminalPalette palette, TerminalStyle style, int start, int length, List<(int, int, Brush)> backs)
    {
        var flags = style.Flags;
        bool inverse = (flags & TerminalFlags.Inverse) != 0, dim = (flags & TerminalFlags.Dim) != 0;
        var ink = palette.Of(inverse ? style.Background : style.Foreground, dim, inverse ? palette.Back : palette.Ink);
        var back = inverse ? palette.Of(style.Foreground, false, palette.Ink) : style.Background >= 0 ? palette.Of(style.Background, false, palette.Back) : null;
        if (back is not null) { ink = TerminalPalette.Readable(ink, back); }
        text.SetForegroundBrush((flags & TerminalFlags.Mark) != 0 ? palette.Mark : ink, start, length);
        if (back is not null) { backs.Add((start, length, back)); }
        if ((flags & TerminalFlags.Bold) != 0) { text.SetFontWeight(FontWeights.SemiBold, start, length); }
        if ((flags & TerminalFlags.Italic) != 0) { text.SetFontStyle(FontStyles.Italic, start, length); }
        if ((flags & TerminalFlags.Underline) != 0) { text.SetTextDecorations(TextDecorations.Underline, start, length); }
        if ((flags & TerminalFlags.Strike) != 0) { text.SetTextDecorations(TextDecorations.Strikethrough, start, length); }
    }
}
