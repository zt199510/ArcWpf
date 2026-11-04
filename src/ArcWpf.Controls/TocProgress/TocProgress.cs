using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>A heading in document order. Id is the x:Name of the heading element, unless Target points at it directly.</summary>
public sealed class TocProgressItem
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>1, 2 or 3: the lane the line bends into and the label indent.</summary>
    public int Level { get; set; } = 1;
    public FrameworkElement? Target { get; set; }
}

public sealed class TocProgressItemCollection : Collection<TocProgressItem>;

public sealed class TocProgressEventArgs(string id) : EventArgs
{
    public string Id { get; } = id;
}

// Arc Pro: no source. Built from the public docs and preview geometry; see docs/references/components/TocProgress.md.
public class TocProgress : Control
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IList<TocProgressItem>), typeof(TocProgress), new FrameworkPropertyMetadata(null, (d, _) => ((TocProgress)d).Rebuild()));
    public static readonly DependencyProperty ScrollContainerProperty = DependencyProperty.Register(nameof(ScrollContainer), typeof(FrameworkElement), typeof(TocProgress), new FrameworkPropertyMetadata(null, (d, _) => ((TocProgress)d).Rehook()));
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(nameof(Offset), typeof(double), typeof(TocProgress), new FrameworkPropertyMetadata(96d, (d, _) => ((TocProgress)d).Follow()));
    public static readonly DependencyProperty ActiveIdProperty = DependencyProperty.Register(nameof(ActiveId), typeof(string), typeof(TocProgress), new FrameworkPropertyMetadata(null, (d, _) => ((TocProgress)d).PaintRows(true)));
    public static readonly DependencyProperty ReadingTimeProperty = DependencyProperty.Register(nameof(ReadingTime), typeof(double?), typeof(TocProgress), new FrameworkPropertyMetadata(null, (d, _) => ((TocProgress)d).Follow()));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(TocProgress), new FrameworkPropertyMetadata("On this page", (d, _) => ((TocProgress)d).SyncHead()));
    public static readonly DependencyProperty ShowTimeLeftProperty = DependencyProperty.Register(nameof(ShowTimeLeft), typeof(bool), typeof(TocProgress), new FrameworkPropertyMetadata(true, (d, _) => ((TocProgress)d).SyncHead()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TocProgress), new FrameworkPropertyMetadata("On this page"));
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(TocProgress), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((TocProgress)d)._line?.InvalidateVisual()));

    public IList<TocProgressItem>? Items { get => (IList<TocProgressItem>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    /// <summary>What scrolls the article: a ScrollViewer, or anything holding one, such as a ScrollArea. Defaults to the first heading's nearest ScrollViewer.</summary>
    public FrameworkElement? ScrollContainer { get => (FrameworkElement?)GetValue(ScrollContainerProperty); set => SetValue(ScrollContainerProperty, value); }
    /// <summary>Distance from the top of the viewport at which a heading counts as reached.</summary>
    public double Offset { get => (double)GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }
    /// <summary>The current heading, controlled. The line keeps following the scroll either way.</summary>
    public string? ActiveId { get => (string?)GetValue(ActiveIdProperty); set => SetValue(ActiveIdProperty, value); }
    /// <summary>Reading time in minutes. Defaults to an estimate from the words in the scroll container at 230 words a minute.</summary>
    public double? ReadingTime { get => (double?)GetValue(ReadingTimeProperty); set => SetValue(ReadingTimeProperty, value); }
    /// <summary>Heading above the list and the accessible name. Empty hides it.</summary>
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool ShowTimeLeft { get => (bool)GetValue(ShowTimeLeftProperty); set => SetValue(ShowTimeLeftProperty, value); }
    /// <summary>Accessible name when Title is empty.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Recolors the read line and the marker.</summary>
    public Brush? Accent { get => (Brush?)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public event EventHandler<TocProgressEventArgs>? ActiveChanged;
    public event EventHandler<TocProgressEventArgs>? Navigate;

    private sealed class LineSurface(TocProgress owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc) => owner.DrawLine(dc);
    }

    // A row is a link in the automation tree, focusable, and takes the full width as its target.
    private sealed class Row : Border
    {
        public readonly TocProgress Owner;
        public readonly TocProgressItem Item;
        public readonly SolidColorBrush Ink = new(), Wash = new(Colors.Transparent);
        public readonly FocusVisible Focused;
        public readonly TextBlock Text;
        private readonly Border _ring = new() { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(2), Margin = new Thickness(-2), IsHitTestVisible = false, Visibility = Visibility.Collapsed };

        public Row(TocProgress owner, TocProgressItem item)
        {
            (Owner, Item) = (owner, item);
            Focusable = true;
            FocusVisualStyle = null;
            Cursor = Cursors.Hand;
            Background = Wash;
            CornerRadius = new CornerRadius(10);
            Text = new TextBlock { Text = item.Label, FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap, Foreground = Ink, Margin = new Thickness(22 + 12 * (Math.Clamp(item.Level, 1, 3) - 1), 4, 8, 4), IsHitTestVisible = false };
            _ring.BorderBrush = FocusVisible.RingBrush(owner);
            var body = new Grid();
            body.Children.Add(Text);
            body.Children.Add(_ring);
            Child = body;
            Focused = new FocusVisible(this, () => { _ring.Visibility = Focused!.Shown ? Visibility.Visible : Visibility.Collapsed; Owner.PaintRows(true); });
            MouseEnter += (_, _) => Owner.PaintRows(true);
            MouseLeave += (_, _) => Owner.PaintRows(true);
            MouseLeftButtonUp += (_, _) => { Focused.Pointer(); Owner.Choose(this); };
            PreviewKeyDown += (_, e) => { if (Owner.HandleRowKey(this, e.Key)) { e.Handled = true; } };
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new InvokePeer(this, AutomationControlType.Hyperlink, () => Item.Label, () => Owner.Choose(this), () => Owner.CurrentIndex >= 0 && Owner.CurrentIndex < Owner._rows.Count && ReferenceEquals(Owner._rows[Owner.CurrentIndex], this) ? "current location" : "");
    }

    private const double LaneStart = 5, LaneGap = 12, BendHalf = 7, EndRun = 10, MarkerRadius = 3.5, KnockoutRadius = 5.5;
    private const int BendSegments = 14;
    private readonly List<Row> _rows = [];
    private readonly StackPanel _list = new();
    private readonly LineSurface _line;
    private readonly Grid _body = new();
    private readonly TrackedText _title;
    private readonly SolidColorBrush _ink = new(), _muted = new(), _success = new(), _track = new(), _accent = new();
    private readonly StackPanel _time = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly ArcDataGridIcon _check = new() { Icon = "check", Size = 13, Margin = new Thickness(0, 0, 4, 0), Visibility = Visibility.Collapsed };
    private SwapText? _timeText;
    private Grid? _head;
    private readonly FrameLoop _loop;
    private readonly GestureSpring _position = new(0, .001, .01), _scroll = new(0, .3, 3);
    private Decorator? _host;
    private ScrollViewer? _viewer;
    private List<Point> _points = [];
    private double[] _rowStart = [], _tops = [], _lengths = [];
    private double _total, _progressLength, _contentHeight;
    private int _lastActive = -1;
    private bool _scrolling, _measurePending;
    private double? _estimate;
    private string _timeShown = "";
    private FrameworkElement? _focusAfter;

    static TocProgress() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TocProgress), new FrameworkPropertyMetadata(typeof(TocProgress)));

    public TocProgress()
    {
        _title = CopyMotion.Text("", 14, 1.4, -.14, FontWeights.Medium, _ink);
        _line = new LineSurface(this) { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        _loop = new FrameLoop(Frame);
        Loaded += (_, _) => { Inks(); Rehook(); RequestMeasure(); };
        Unloaded += (_, _) => { _loop.Stop(); Unhook(); };
        ThemeWatch.Add(this, () => { Inks(); PaintRows(false); _line.InvalidateVisual(); });
    }

    internal double PositionForTest => _position.Position;
    internal double GoalForTest => _position.Target;
    internal int CurrentForTest => CurrentIndex;
    internal string TimeForTest => _timeShown;
    internal IReadOnlyList<Point> LinePointsForTest => _points;
    internal double[] RowStartsForTest => _rowStart;
    internal double TotalLengthForTest => _total;
    internal double ProgressForTest => _progressLength;
    internal Point TipForTest => TipAt(_progressLength);
    internal FrameworkElement RowForTest(int index) => _rows[index];
    internal Color RowInkForTest(int index) => _rows[index].Ink.Color;
    internal string RowTextForTest(int index) => _rows[index].Text.Text;
    internal bool ScrollingForTest => _scrolling;
    internal bool RunningForTest => _loop.IsRunning;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    internal void ChooseForTest(int index) => Choose(_rows[index]);
    internal void KeyForTest(int index, Key key) => HandleRowKey(_rows[index], key);
    internal void MeasureForTest() => Measure2();

    protected override AutomationPeer OnCreateAutomationPeer() => new NavPeer(this);

    private sealed class NavPeer(TocProgress owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetLocalizedControlTypeCore() => "navigation";
        protected override string GetNameCore() => string.IsNullOrEmpty(owner.Title) ? owner.Label : owner.Title;
    }

    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;

    private int CurrentIndex
    {
        get
        {
            if (ActiveId is { } active) { int controlled = _rows.FindIndex(row => row.Item.Id == active); if (controlled >= 0) { return controlled; } }
            return _rows.Count == 0 ? -1 : Math.Clamp((int)Math.Floor(_position.Position + .001), 0, _rows.Count - 1);
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Build();
    }

    private void Build()
    {
        if (_host is null) { return; }
        Inks();
        _timeText = new SwapText(12, 1.4, -.14, FontWeights.Normal, _muted);
        _check.Stroke = _success;
        _time.Children.Clear();
        _time.Children.Add(_check);
        _time.Children.Add(_timeText);
        _head = new Grid { Margin = new Thickness(2, 0, 0, 10), MinHeight = 19.8 };
        _title.HorizontalAlignment = HorizontalAlignment.Left;
        _time.HorizontalAlignment = HorizontalAlignment.Right;
        _head.Children.Add(_title);
        _head.Children.Add(_time);
        _body.Children.Clear();
        _body.Children.Add(_line);
        _body.Children.Add(_list);
        _list.SizeChanged += (_, _) => RequestMeasure();
        var root = new StackPanel();
        root.Children.Add(_head);
        root.Children.Add(_body);
        _host.Child = root;
        Rebuild();
    }

    private void Inks()
    {
        _ink.Color = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        _success.Color = Tween.Resolve(this, "ArcWpf.Brush.ArcSuccess");
        _track.Color = Tween.Resolve(this, "ArcWpf.Brush.TocTrack");
        _accent.Color = Tween.Resolve(this, "ArcWpf.Brush.Accent");
    }

    private void SyncHead()
    {
        if (_head is null) { return; }
        _title.Text = Title ?? "";
        _head.Visibility = string.IsNullOrEmpty(Title) && !ShowTimeLeft ? Visibility.Collapsed : Visibility.Visible;
        _time.Visibility = ShowTimeLeft ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Rebuild()
    {
        if (_host is null) { return; }
        SyncHead();
        _list.Children.Clear();
        _rows.Clear();
        foreach (var item in Items ?? [])
        {
            var row = new Row(this, item);
            _rows.Add(row);
            _list.Children.Add(row);
        }
        Roving();
        PaintRows(false);
        Rehook();
        RequestMeasure();
    }

    private void Roving() { }

    private ScrollViewer? ResolveViewer()
    {
        if (ScrollContainer is ScrollViewer direct) { return direct; }
        if (ScrollContainer is ScrollArea area) { area.ApplyTemplate(); if (area.Viewport is { } viewport) { return viewport; } }
        if (ScrollContainer is { } container) { return FindViewer(container); }
        var first = TargetOf(Items?.FirstOrDefault());
        for (DependencyObject? at = first; at is not null; at = VisualTreeHelper.GetParent(at)) { if (at is ScrollViewer ancestor) { return ancestor; } }
        return null;
    }

    private static ScrollViewer? FindViewer(DependencyObject from)
    {
        if (from is ScrollViewer viewer) { return viewer; }
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++) { if (FindViewer(VisualTreeHelper.GetChild(from, i)) is { } found) { return found; } }
        return null;
    }

    private FrameworkElement? TargetOf(TocProgressItem? item)
    {
        if (item is null) { return null; }
        if (item.Target is { } direct) { return direct; }
        if (string.IsNullOrEmpty(item.Id)) { return null; }
        // Names resolve in the scope that declared the article; fall back to walking the content.
        if (ScrollContainer?.FindName(item.Id) is FrameworkElement scoped) { return scoped; }
        if (_viewer?.Content is DependencyObject content) { return FindNamed(content, item.Id); }
        return null;
    }

    private static FrameworkElement? FindNamed(DependencyObject from, string name)
    {
        if (from is FrameworkElement element && element.Name == name) { return element; }
        foreach (object child in LogicalTreeHelper.GetChildren(from))
        {
            if (child is DependencyObject node && FindNamed(node, name) is { } found) { return found; }
        }
        return null;
    }

    private void Rehook()
    {
        if (!IsLoaded) { return; }
        Unhook();
        _viewer = ResolveViewer();
        if (_viewer is null) { return; }
        _viewer.ScrollChanged += OnScrollChanged;
        _viewer.PreviewMouseWheel += OnUserScroll;
        _viewer.PreviewMouseDown += OnUserScroll;
        RequestMeasure();
    }

    private void Unhook()
    {
        if (_viewer is null) { return; }
        _viewer.ScrollChanged -= OnScrollChanged;
        _viewer.PreviewMouseWheel -= OnUserScroll;
        _viewer.PreviewMouseDown -= OnUserScroll;
        _viewer = null;
    }

    // Reading takes over from a chosen heading the moment the reader scrolls.
    private void OnUserScroll(object sender, InputEventArgs e) { _scrolling = false; _scroll.Jump(_viewer?.VerticalOffset ?? 0); }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Rows, the line and the heading positions are re-measured whenever anything resizes.
        if (e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0 || e.ViewportWidthChange != 0) { RequestMeasure(); }
        else { Follow(); }
    }

    private void RequestMeasure()
    {
        if (_measurePending) { return; }
        _measurePending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => { _measurePending = false; if (IsLoaded) { Measure2(); } }));
    }

    private void Measure2()
    {
        if (_viewer is null) { _viewer = ResolveViewer(); if (_viewer is null) { return; } _viewer.ScrollChanged += OnScrollChanged; }
        BuildLine();
        var content = _viewer.Content as Visual;
        var tops = new List<double>();
        foreach (var row in _rows)
        {
            var target = TargetOf(row.Item);
            tops.Add(target is not null && content is not null && target.IsDescendantOf(content) ? target.TransformToAncestor(content).Transform(new Point()).Y : tops.LastOrDefault());
        }
        _tops = [.. tops];
        _contentHeight = _viewer.ExtentHeight;
        _estimate = null;
        Follow();
        if (Math.Abs(_position.Position - _position.Target) < .001) { _position.Jump(_position.Target); ApplyPosition(); }
    }

    // A single polyline: straight down each lane, with a smooth S across each change of level, centred on the boundary between rows.
    private void BuildLine()
    {
        if (_rows.Count == 0) { _points = []; _line.InvalidateVisual(); return; }
        double Lane(Row row) => LaneStart + LaneGap * (Math.Clamp(row.Item.Level, 1, 3) - 1);
        var points = new List<Point>();
        var starts = new double[_rows.Count];
        double y = 0;
        var centers = new double[_rows.Count];
        var bottoms = new double[_rows.Count];
        for (int i = 0; i < _rows.Count; i++) { double h = _rows[i].ActualHeight; centers[i] = y + h / 2; y += h; bottoms[i] = y; }
        points.Add(new Point(Lane(_rows[0]), centers[0]));
        for (int i = 0; i + 1 < _rows.Count; i++)
        {
            double from = Lane(_rows[i]), to = Lane(_rows[i + 1]), edge = bottoms[i];
            if (from != to)
            {
                points.Add(new Point(from, edge - BendHalf));
                for (int s = 1; s <= BendSegments; s++)
                {
                    double t = (double)s / BendSegments, eased = t * t * (3 - 2 * t);
                    points.Add(new Point(from + (to - from) * eased, edge - BendHalf + BendHalf * 2 * t));
                }
            }
            points.Add(new Point(to, centers[i + 1]));
            starts[i + 1] = points.Count - 1;
        }
        points.Add(new Point(Lane(_rows[^1]), centers[^1] + EndRun));
        var lengths = new double[points.Count];
        for (int i = 1; i < points.Count; i++) { lengths[i] = lengths[i - 1] + (points[i] - points[i - 1]).Length; }
        _points = points;
        _lengths = lengths;
        _total = lengths[^1];
        _rowStart = [.. starts.Select(index => lengths[(int)index])];
        _line.Width = 40;
        _line.Height = Math.Max(y, centers[^1] + EndRun);
        ApplyPosition();
    }

    // The reading position is a section index plus how far through it you are. Near the end the reading line eases from the offset
    // down to the bottom of the viewport, so short last sections are still reached when the scroll bottoms out.
    internal double ReadingPosition()
    {
        if (_viewer is null || _tops.Length == 0) { return 0; }
        double max = _viewer.ScrollableHeight, at = _viewer.VerticalOffset, view = _viewer.ViewportHeight, offset = Offset;
        double tail = Math.Max(1, view - offset), near = Math.Clamp(1 - (max - at) / tail, 0, 1);
        double line = at + offset + tail * near * near;
        if (line < _tops[0]) { return 0; }
        int index = 0;
        for (int i = 0; i < _tops.Length; i++) { if (_tops[i] <= line) { index = i; } }
        double start = _tops[index], end = index + 1 < _tops.Length ? _tops[index + 1] : Math.Max(_contentHeight, start + 1);
        return index + Math.Clamp((line - start) / Math.Max(1, end - start), 0, 1);
    }

    private void Follow()
    {
        if (_host is null) { return; }
        double goal = ReadingPosition();
        if (Reduced || !IsLoaded) { _position.Jump(goal); }
        else if (Math.Abs(goal - _position.Target) > 1e-6) { _position.To(goal, SpringTune.Snappy); _loop.Start(); }
        ApplyPosition();
        UpdateTime();
    }

    private bool Frame(double seconds)
    {
        _position.Step(seconds);
        if (_scrolling && _viewer is not null)
        {
            _scroll.Step(seconds);
            _viewer.ScrollToVerticalOffset(_scroll.Position);
            if (!_scroll.Active) { _scrolling = false; Land(); }
        }
        ApplyPosition();
        return _position.Active || _scrolling;
    }

    private double LengthAt(double position)
    {
        if (_rowStart.Length == 0) { return 0; }
        int index = Math.Clamp((int)Math.Floor(position), 0, _rowStart.Length - 1);
        double start = _rowStart[index], end = index + 1 < _rowStart.Length ? _rowStart[index + 1] : _total;
        return start + (end - start) * Math.Clamp(position - index, 0, 1);
    }

    private void ApplyPosition()
    {
        _progressLength = LengthAt(_position.Position);
        _line.InvalidateVisual();
        int current = CurrentIndex;
        if (current != _lastActive)
        {
            _lastActive = current;
            PaintRows(IsLoaded);
            if (current >= 0 && current < _rows.Count && IsLoaded) { ActiveChanged?.Invoke(this, new TocProgressEventArgs(_rows[current].Item.Id)); }
        }
    }

    private Point TipAt(double length)
    {
        if (_points.Count == 0) { return new Point(); }
        for (int i = 1; i < _points.Count; i++)
        {
            if (_lengths[i] >= length)
            {
                double span = _lengths[i] - _lengths[i - 1], t = span <= 0 ? 0 : (length - _lengths[i - 1]) / span;
                return _points[i - 1] + (_points[i] - _points[i - 1]) * t;
            }
        }
        return _points[^1];
    }

    private void DrawLine(DrawingContext dc)
    {
        if (_points.Count < 2) { return; }
        var tip = TipAt(_progressLength);
        var all = new StreamGeometry();
        using (var context = all.Open())
        {
            context.BeginFigure(_points[0], false, false);
            for (int i = 1; i < _points.Count; i++) { context.LineTo(_points[i], true, true); }
        }
        all.Freeze();
        // The line is knocked out around the marker, so the dot floats clear of it.
        var bounds = new Rect(-8, -8, _line.ActualWidth + 16, _line.ActualHeight + 16);
        dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(bounds), new EllipseGeometry(tip, KnockoutRadius, KnockoutRadius)));
        dc.DrawGeometry(null, new Pen(_track, 1.5) { LineJoin = PenLineJoin.Round }, all);
        var read = Accent ?? _accent;
        if (_progressLength > .01)
        {
            var filled = new StreamGeometry();
            using (var context = filled.Open())
            {
                context.BeginFigure(_points[0], false, false);
                for (int i = 1; i < _points.Count; i++)
                {
                    if (_lengths[i] >= _progressLength) { context.LineTo(tip, true, true); break; }
                    context.LineTo(_points[i], true, true);
                }
            }
            filled.Freeze();
            dc.DrawGeometry(null, new Pen(read, 2) { LineJoin = PenLineJoin.Round }, filled);
        }
        dc.Pop();
        dc.DrawEllipse(read, null, tip, MarkerRadius, MarkerRadius);
    }

    // Read, current and upcoming headings differ in text contrast, never by the line alone.
    private void PaintRows(bool animate)
    {
        int current = CurrentIndex;
        bool cross = animate && IsLoaded && !Reduced;
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            string ink = i == current ? "Foreground" : i < current || row.IsMouseOver ? "TextSecondary" : "TextMuted";
            Tween.Color(row.Ink, Tween.Resolve(this, "ArcWpf.Brush." + ink), Tween.Standard, cross);
            var quiet = Tween.Resolve(this, "ArcWpf.Brush.SurfaceMuted");
            Tween.Color(row.Wash, row.Focused.Shown ? quiet : OkColor.Alpha(quiet, 0), Tween.Fast, cross);
        }
    }

    private double MinutesTotal()
    {
        if (ReadingTime is { } given) { return given; }
        if (_estimate is { } known) { return known; }
        int words = 0;
        void Count(DependencyObject node)
        {
            if (node is TextBlock block) { words += block.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length; }
            foreach (object child in LogicalTreeHelper.GetChildren(node)) { if (child is DependencyObject inner) { Count(inner); } }
        }
        if (_viewer?.Content is DependencyObject content) { Count(content); }
        return (_estimate = words / 230d).Value;
    }

    // Minutes left along the article; ends on Finished with a check once the scroll bottoms out.
    private void UpdateTime()
    {
        if (_timeText is null || _viewer is null) { return; }
        double max = _viewer.ScrollableHeight, at = _viewer.VerticalOffset;
        bool finished = max > 0 && max - at < 1;
        double share = _total > 0 ? LengthAt(ReadingPosition()) / _total : 0;
        int minutes = Math.Max(1, (int)Math.Ceiling(MinutesTotal() * (1 - share)));
        string text = finished ? "Finished" : $"{minutes} min left";
        if (text == _timeShown) { return; }
        int direction = 1;
        _timeShown = text;
        _timeText.Set(text, direction);
        _check.Visibility = finished ? Visibility.Visible : Visibility.Collapsed;
    }

    // Choosing a heading scrolls the article smoothly while the line traces the way, then moves focus there without a second jump.
    private void Choose(Row row)
    {
        Navigate?.Invoke(this, new TocProgressEventArgs(row.Item.Id));
        if (_viewer is null) { Rehook(); }
        var target = TargetOf(row.Item);
        int index = _rows.IndexOf(row);
        if (_viewer is null || target is null || index < 0 || index >= _tops.Length) { return; }
        double wanted = Math.Clamp(_tops[index] - Offset, 0, _viewer.ScrollableHeight);
        _focusAfter = target;
        if (Reduced || !IsLoaded) { _viewer.ScrollToVerticalOffset(wanted); Land(); return; }
        _scroll.Jump(_viewer.VerticalOffset);
        _scroll.To(wanted, SpringTune.Physical(.5));
        _scrolling = true;
        _loop.Start();
    }

    private void Land()
    {
        if (_focusAfter is not { } target) { return; }
        _focusAfter = null;
        target.Focusable = true;
        target.FocusVisualStyle = null;
        if (target.IsVisible) { target.Focus(); }
    }

    private bool HandleRowKey(Row row, Key key)
    {
        int index = _rows.IndexOf(row);
        int? next = key switch { Key.Down => index + 1, Key.Up => index - 1, Key.Home => 0, Key.End => _rows.Count - 1, _ => null };
        if (next is { } target)
        {
            var goal = _rows[Math.Clamp(target, 0, _rows.Count - 1)];
            goal.Focused.Key();
            goal.Focus();
            goal.Focused.Key();
            return true;
        }
        if (key == Key.Enter) { row.Focused.Key(); Choose(row); return true; }
        return false;
    }
}
