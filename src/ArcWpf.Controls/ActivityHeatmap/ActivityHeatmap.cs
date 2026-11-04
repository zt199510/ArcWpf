using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

/// <summary>One calendar day and how busy it was.</summary>
public readonly record struct ActivityDay(DateOnly Date, int Count);

/// <summary>Reports the day someone chose.</summary>
public sealed class ActivityDateEventArgs(DateOnly date) : EventArgs
{
    public DateOnly Date { get; } = date;
}

// The calendar arithmetic of activity-heatmap.tsx.
internal sealed class HeatmapModel
{
    public DateOnly Start;
    public int Length, Lead, Weeks, Total;
    public int[] Counts = [], Levels = [], PerLevel = [0, 0, 0, 0, 0];
    public (int A, int B, int C) Bounds;
    public (int Month, int Col, string Label)[] Months = [];

    public static HeatmapModel Build(IReadOnlyList<ActivityDay> days, DayOfWeek weekStart, (int, int, int)? thresholds)
    {
        var model = new HeatmapModel { Start = days.Count > 0 ? days.Min(day => day.Date) : new DateOnly(2025, 1, 1) };
        model.Length = days.Count > 0 ? days.Max(day => day.Date).DayNumber - model.Start.DayNumber + 1 : 0;
        model.Counts = new int[model.Length];
        foreach (var day in days) { model.Counts[day.Date.DayNumber - model.Start.DayNumber] += Math.Max(0, day.Count); }
        int max = model.Counts.DefaultIfEmpty(0).Max();
        model.Bounds = thresholds ?? (Math.Max(1, (int)Math.Ceiling(max * .25)), Math.Max(2, (int)Math.Ceiling(max * .5)), Math.Max(3, (int)Math.Ceiling(max * .75)));
        model.Levels = [.. model.Counts.Select(count => count <= 0 ? 0 : count <= model.Bounds.A ? 1 : count <= model.Bounds.B ? 2 : count <= model.Bounds.C ? 3 : 4)];
        model.Lead = ((int)model.Start.DayOfWeek - (int)weekStart + 7) % 7;
        model.Weeks = (int)Math.Ceiling((model.Lead + model.Length) / 7d);
        var months = new List<(int, int, string)>();
        for (int index = 0; index < model.Length; index++)
        {
            var date = model.Start.AddDays(index);
            if (index == 0 || date.Day == 1) { months.Add((date.Year * 12 + date.Month - 1, (model.Lead + index) / 7, date.ToString("MMM", CultureInfo.InvariantCulture))); }
        }
        // A partial first month keeps its label only when there is room before the next one.
        if (months.Count > 1 && months[1].Item2 - months[0].Item2 < 3) { months.RemoveAt(0); }
        model.Months = [.. months];
        foreach (int level in model.Levels) { model.PerLevel[level]++; }
        model.Total = model.Counts.Sum();
        return model;
    }
}

// activity-heatmap.tsx (frozen 2026-11-04): a contribution calendar. One square per day, weeks as columns, four accent tints; cells wave in once, a tooltip glides between them, and a new range recolors in a sweep.
public class ActivityHeatmap : Control
{
    public static readonly DependencyProperty DaysProperty = DependencyProperty.Register(nameof(Days), typeof(IReadOnlyList<ActivityDay>), typeof(ActivityHeatmap), new FrameworkPropertyMetadata(null, (d, _) => ((ActivityHeatmap)d).OnDays()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ActivityHeatmap), new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty PeriodProperty = DependencyProperty.Register(nameof(Period), typeof(string), typeof(ActivityHeatmap), new FrameworkPropertyMetadata("", (d, _) => ((ActivityHeatmap)d).Summary()));
    public static readonly DependencyProperty UnitOneProperty = DependencyProperty.Register(nameof(UnitOne), typeof(string), typeof(ActivityHeatmap), new FrameworkPropertyMetadata("contribution", (d, _) => ((ActivityHeatmap)d).Summary()));
    public static readonly DependencyProperty UnitOtherProperty = DependencyProperty.Register(nameof(UnitOther), typeof(string), typeof(ActivityHeatmap), new FrameworkPropertyMetadata("contributions", (d, _) => ((ActivityHeatmap)d).Summary()));
    public static readonly DependencyProperty ThresholdsProperty = DependencyProperty.Register(nameof(Thresholds), typeof(IReadOnlyList<int>), typeof(ActivityHeatmap), new FrameworkPropertyMetadata(null, (d, _) => ((ActivityHeatmap)d).OnDays()));
    public static readonly DependencyProperty WeekStartProperty = DependencyProperty.Register(nameof(WeekStart), typeof(DayOfWeek), typeof(ActivityHeatmap), new FrameworkPropertyMetadata(DayOfWeek.Sunday, (d, _) => ((ActivityHeatmap)d).OnDays()));
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(nameof(SelectedDate), typeof(DateOnly?), typeof(ActivityHeatmap), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ActivityHeatmap)d).AimRing()));
    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(nameof(Actions), typeof(object), typeof(ActivityHeatmap), new FrameworkPropertyMetadata(null, (d, _) => ((ActivityHeatmap)d).OnActions()));

    /// <summary>One entry per day, oldest first. Days missing inside the range count as zero.</summary>
    public IReadOnlyList<ActivityDay>? Days { get => (IReadOnlyList<ActivityDay>?)GetValue(DaysProperty); set => SetValue(DaysProperty, value); }
    /// <summary>Accessible name for the grid, such as "Contributions in 2025".</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Finishes the summary line: "1,284 contributions in {Period}".</summary>
    public string Period { get => (string)GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }
    public string UnitOne { get => (string)GetValue(UnitOneProperty); set => SetValue(UnitOneProperty, value); }
    public string UnitOther { get => (string)GetValue(UnitOtherProperty); set => SetValue(UnitOtherProperty, value); }
    /// <summary>Three upper bounds for levels one to three; anything above the last is level four. Defaults to quarters of the busiest day.</summary>
    public IReadOnlyList<int>? Thresholds { get => (IReadOnlyList<int>?)GetValue(ThresholdsProperty); set => SetValue(ThresholdsProperty, value); }
    /// <summary>The day each column starts on.</summary>
    public DayOfWeek WeekStart { get => (DayOfWeek)GetValue(WeekStartProperty); set => SetValue(WeekStartProperty, value); }
    /// <summary>The chosen day, marked with a ring.</summary>
    public DateOnly? SelectedDate { get => (DateOnly?)GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    /// <summary>Controls beside the summary, such as a range switch.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <summary>Raised when someone clicks a day or presses Enter on it.</summary>
    public event EventHandler<ActivityDateEventArgs>? DateChosen;

    private const double Gap = 3, LabelColumn = 30, MonthsHeight = 16, RowGap = 8, WaveTravel = .42, CellTransition = .32;

    private sealed class Cell
    {
        public int Column, Row, Index;
        public bool Empty;
        public int Level;
        public double Wave, WaveBack;
        public TimedColor Color = new(Colors.Transparent);
        public Timed Scale = new(1), Opacity = new(1);
    }

    private sealed class MonthLabel(string key, TrackedText view, double column)
    {
        public string Key { get; } = key;
        public TrackedText View { get; } = view;
        public Timed Column { get; } = new(column);
    }

    private readonly ChartSurface _grid, _legendSwatches, _rail;
    private readonly Canvas _months = new() { IsHitTestVisible = false, ClipToBounds = true, Height = MonthsHeight };
    private readonly Grid _weekdays = new() { IsHitTestVisible = false };
    private readonly Grid _plotRow = new();
    private readonly Canvas _tipLayer = new() { IsHitTestVisible = false };
    private readonly Border _bubble = new() { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(12, 7, 12, 8), Opacity = 0, RenderTransformOrigin = new Point(.5, 1), IsHitTestVisible = false };
    private readonly Grid _tipBody = new() { ClipToBounds = false, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly FrameLoop _loop;
    private readonly FocusVisible _focus, _swatchFocus;
    private readonly InViewOnce _inView;
    private readonly List<Cell> _cells = [];
    private readonly List<MonthLabel> _monthLabels = [];
    private readonly GestureSpring _tipX = new(0), _tipY = new(0), _tipWidth = new(0, .05, .5);
    private readonly Timed _tipFade = new(0), _tipScale = new(.96), _ringX = new(0), _ringY = new(0), _ringFade = new(0), _offset = new(0);
    private readonly DispatcherTimer _hideTimer = new(DispatcherPriority.Normal), _previewTimer = new(DispatcherPriority.Normal), _revealTimer = new(DispatcherPriority.Normal);
    private readonly SolidColorBrush _ink = new(), _secondary = new(), _muted = new(), _bubbleInk = new(), _bubbleSecond = new(), _bubbleEdge = new(), _bubbleFill = new();
    private readonly Color[] _levels = new Color[5];
    private Decorator? _host;
    private RollText? _total, _periodText, _caption;
    private TrackedText? _noun, _in;
    private ContentPresenter? _actionsHost;
    private HeatmapModel _model = new();
    private Color _focusColor, _hoverOutline, _inkColor;
    private TrackedText? _tipPrimary, _tipSecondary;
    private string _tipKey = "";
    private int _tipValue;
    private int _hovered = -1, _tipCell = -1, _focusIndex = -1, _legendFocus;
    private int? _preview, _pinned, _swatchHover;
    private DateOnly _rangeStart;
    private bool _back, _tipOpen, _revealing, _revealed, _hasModel, _dragging, _railHover;
    private double _dragStart, _dragOffset;
    private double _cell = 9, _width;

    static ActivityHeatmap() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ActivityHeatmap), new FrameworkPropertyMetadata(typeof(ActivityHeatmap)));

    public ActivityHeatmap()
    {
        _grid = new ChartSurface(PaintGrid, surface => new HeatmapPeer(surface, this)) { Focusable = true, FocusVisualStyle = null };
        _legendSwatches = new ChartSurface(PaintSwatches, surface => new FrameworkElementAutomationPeer(surface)) { Width = 75, Height = 20, Focusable = true, FocusVisualStyle = null, Cursor = Cursors.Hand };
        AutomationProperties.SetName(_legendSwatches, "Highlight days by level");
        _rail = new ChartSurface(PaintRail) { Height = 6, Opacity = 0, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, -6) };
        _bubble.Child = _tipBody;
        _bubble.RenderTransform = new ScaleTransform(.96, .96);
        _tipLayer.Children.Add(_bubble);
        _loop = new FrameLoop(Frame);
        _focus = new FocusVisible(_grid, OnFocusShown);
        _swatchFocus = new FocusVisible(_legendSwatches, () => { if (_swatchFocus is { Shown: true }) { PreviewLevel(_legendFocus); } _legendSwatches.InvalidateVisual(); });
        _inView = new InViewOnce(_grid, Reveal);
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); CloseTip(); };
        _previewTimer.Interval = TimeSpan.FromMilliseconds(90);
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); _preview = null; Highlight(); };
        _revealTimer.Interval = TimeSpan.FromSeconds(WaveTravel + .7);
        _revealTimer.Tick += (_, _) => { _revealTimer.Stop(); _revealing = false; };

        _grid.MouseMove += (_, e) => { if (!_dragging) { Hover(e.GetPosition(_grid)); } };
        _grid.MouseLeave += (_, _) => LeaveGrid();
        _grid.MouseLeftButtonDown += (_, e) => { _focus.Pointer(); if (CellAt(e.GetPosition(_grid)) is { } hit) { _focusIndex = hit.Index; _grid.Focus(); _grid.InvalidateVisual(); } };
        _grid.MouseLeftButtonUp += (_, e) => { if (CellAt(e.GetPosition(_grid)) is { } hit) { Choose(hit.Index); } };
        _grid.KeyDown += (_, e) => { if (GridKey(e.Key)) { e.Handled = true; _focus.Key(); } };
        _grid.LostKeyboardFocus += (_, e) => { if (!ReferenceEquals(e.NewFocus, _grid)) { HideSoon(0); _grid.InvalidateVisual(); } };
        _grid.MouseWheel += (_, e) => { if (Keyboard.Modifiers == ModifierKeys.Shift && MaxOffset > 0) { Scroll(_offset.Target - e.Delta / 3d); e.Handled = true; } };
        _grid.TouchDown += (_, e) => { _grid.CaptureTouch(e.TouchDevice); (_dragStart, _dragOffset) = (e.GetTouchPoint(_grid).Position.X, _offset.Value); e.Handled = true; };
        _grid.TouchMove += (_, e) => { Jump(_dragOffset - (e.GetTouchPoint(_grid).Position.X - _dragStart)); e.Handled = true; };
        _grid.TouchUp += (_, e) => { _grid.ReleaseTouchCapture(e.TouchDevice); if (Math.Abs(e.GetTouchPoint(_grid).Position.X - _dragStart) < 6 && CellAt(e.GetTouchPoint(_grid).Position) is { } hit) { Show(hit.Index); Choose(hit.Index); HideSoon(2.4); } e.Handled = true; };

        _rail.MouseEnter += (_, _) => { _railHover = true; RailFade(); };
        _rail.MouseLeave += (_, _) => { _railHover = false; RailFade(); };
        _rail.MouseLeftButtonDown += (_, e) => { _rail.CaptureMouse(); _dragging = true; (_dragStart, _dragOffset) = (e.GetPosition(_rail).X, _offset.Value); e.Handled = true; };
        _rail.MouseMove += (_, e) => { if (_dragging) { Jump(_dragOffset + (e.GetPosition(_rail).X - _dragStart) * RailRatio); } };
        _rail.MouseLeftButtonUp += (_, _) => { _dragging = false; _rail.ReleaseMouseCapture(); };

        _legendSwatches.MouseMove += (_, e) => { int at = Math.Clamp((int)(e.GetPosition(_legendSwatches).X / 15), 0, 4); if (at != _swatchHover) { _swatchHover = at; PreviewLevel(at); } };
        _legendSwatches.MouseLeave += (_, _) => { _swatchHover = null; PreviewLevel(null); };
        _legendSwatches.MouseLeftButtonDown += (_, e) =>
        {
            _swatchFocus.Pointer();
            _legendSwatches.Focus();
            int at = Math.Clamp((int)(e.GetPosition(_legendSwatches).X / 15), 0, 4);
            _legendFocus = at;
            Pin(at);
        };
        _legendSwatches.KeyDown += (_, e) => { if (SwatchKey(e.Key)) { e.Handled = true; _swatchFocus.Key(); } };
        _legendSwatches.LostKeyboardFocus += (_, _) => PreviewLevel(null);

        SizeChanged += (_, e) => { if (e.WidthChanged) { Refit(e.NewSize.Width); } };
        Loaded += (_, _) => { Colorize(); if (!Moving) { Reveal(); } };
        Unloaded += (_, _) => { _loop.Stop(); _hideTimer.Stop(); _previewTimer.Stop(); _revealTimer.Stop(); };
        ThemeWatch.Add(this, Colorize);
    }

    internal HeatmapModel ModelForTest => _model;
    internal int HighlightForTest => (int?)(_preview ?? _pinned) ?? -1;
    internal string CaptionForTest => _caption?.TextForTest ?? "";
    internal string SummaryForTest => $"{_total?.TextForTest} {_noun?.Text} in {_periodText?.TextForTest}";
    internal bool TipOpenForTest => _tipOpen;
    internal string TipForTest => $"{_tipPrimary?.Text}|{_tipSecondary?.Text}";
    internal double CellForTest => _cell;
    internal double OffsetForTest => _offset.Value;
    internal int CellsForTest => _cells.Count;
    internal int FocusForTest => _focusIndex;
    internal double OpacityOfForTest(int index) => _cells.First(cell => cell.Index == index && !cell.Empty).Opacity.Value;
    internal double OpacityTargetOfForTest(int index) => _cells.First(cell => cell.Index == index && !cell.Empty).Opacity.Target;
    internal Color ColorOfForTest(int index) => _cells.First(cell => cell.Index == index && !cell.Empty).Color.Target;
    internal Color LevelColorForTest(int level) => _levels[level];
    internal double TipXForTest => _tipX.Target;
    internal double RingOpacityForTest => _ringFade.Target;
    internal FrameworkElement GridForTest => _grid;
    internal FrameworkElement SwatchesForTest => _legendSwatches;
    internal Point CenterOfForTest(int index) => CellRect(_cells.First(cell => cell.Index == index && !cell.Empty)).Center();
    internal void HoverForTest(Point point) => Hover(point);
    internal void LeaveForTest() => LeaveGrid();
    internal void KeyForTest(Key key) => GridKey(key);
    internal void FocusIndexForTest(int index) { _focusIndex = index; Show(index); }
    internal void PreviewForTest(int? level) => PreviewLevel(level);
    internal void PinForTest(int level) => Pin(level);
    internal void RevealForTest() => Reveal();
    internal void HideNowForTest() { _hideTimer.Stop(); CloseTip(); }
    internal void AdvanceForTest(double seconds) { for (double left = seconds; left > 0; left -= .05) { _loop.Advance(Math.Min(left, .05)); } }

    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private double Pitch => _cell + Gap;
    private double GridWidth => _model.Weeks * Pitch - Gap;
    private double Viewport => Math.Max(0, _width - LabelColumn - Gap);
    private double MaxOffset => Math.Max(0, GridWidth - Viewport);
    private double RailRatio => Viewport > 0 ? GridWidth / Viewport : 1;
    private string Noun(int count) => count == 1 ? UnitOne : UnitOther;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        if (_host is null) { return; }

        _total = new RollText(14, 1.4, -.16, FontWeights.Medium, morph: true);
        _noun = CopyMotion.Text("", 14, 1.4, -.16, FontWeights.Medium, _ink);
        _noun.Margin = new Thickness(4.2, 0, 0, 0);
        _in = CopyMotion.Text("in", 14, 1.4, -.16, FontWeights.Normal, _secondary);
        _in.Margin = new Thickness(4.2, 0, 4.2, 0);
        _periodText = new RollText(14, 1.4, -.16, FontWeights.Normal, whole: true);
        var summary = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        foreach (var part in new FrameworkElement[] { _total, _noun, _in, _periodText }) { summary.Children.Add(part); }
        _actionsHost = new ContentPresenter { VerticalAlignment = VerticalAlignment.Center };
        var header = new SpreadRow(16, 12);
        header.Children.Add(summary);
        header.Children.Add(_actionsHost);

        // The grid paints its own scrolled cells, so the weekday column stays put while they pass beneath it.
        _plotRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelColumn + Gap) });
        _plotRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        _plotRow.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MonthsHeight + RowGap) });
        _plotRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(_months, 1);
        Grid.SetRow(_weekdays, 1);
        Grid.SetRow(_grid, 1);
        Grid.SetColumnSpan(_grid, 2);
        Grid.SetRow(_rail, 1);
        Grid.SetColumn(_rail, 1);
        _plotRow.Children.Add(_months);
        _plotRow.Children.Add(_grid);
        _plotRow.Children.Add(_weekdays);
        _plotRow.Children.Add(_rail);
        _plotRow.MouseEnter += (_, _) => RailFade();
        _plotRow.MouseLeave += (_, _) => RailFade();

        _caption = new RollText(12, 1, -.16, FontWeights.Normal, whole: true) { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        var less = CopyMotion.Text("Less", 12, 1, -.16, FontWeights.Normal, _muted);
        var more = CopyMotion.Text("More", 12, 1, -.16, FontWeights.Normal, _muted);
        less.VerticalAlignment = more.VerticalAlignment = VerticalAlignment.Center;
        _legendSwatches.Margin = new Thickness(8, 0, 8, 0);
        var scale = new StackPanel { Orientation = Orientation.Horizontal };
        scale.Children.Add(less);
        scale.Children.Add(_legendSwatches);
        scale.Children.Add(more);
        var legend = new Grid { Height = 20 };
        legend.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        legend.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(scale, 1);
        legend.Children.Add(_caption);
        legend.Children.Add(scale);

        var figure = new StackPanel();
        header.Margin = new Thickness(0, 0, 0, 16);
        legend.Margin = new Thickness(0, 16, 0, 0);
        figure.Children.Add(header);
        figure.Children.Add(_plotRow);
        figure.Children.Add(legend);
        var root = new Grid();
        root.Children.Add(figure);
        root.Children.Add(_tipLayer);
        _host.Child = root;
        _bubble.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Raised");
        Colorize();
        OnActions();
        OnDays();
    }

    private void OnActions() { if (_actionsHost is not null) { _actionsHost.Content = Actions; } }

    private void OnDays()
    {
        if (_host is null) { return; }
        var thresholds = Thresholds is { Count: 3 } given ? (given[0], given[1], given[2]) : ((int, int, int)?)null;
        var model = HeatmapModel.Build(Days ?? [], WeekStart, thresholds);
        // A new range sweeps forward when it is later than the last one and back when it is earlier.
        if (_hasModel && model.Start != _rangeStart) { _back = model.Start < _rangeStart; }
        _rangeStart = model.Start;
        _model = model;
        Cells(!_hasModel);
        Months(!_hasModel);
        _hasModel = true;
        _hovered = -1;
        if (_focusIndex >= _model.Length) { _focusIndex = -1; }
        Fit();
        Weekdays();
        Summary();
        Highlight();
        AimRing();
        if (_tipOpen && _tipCell >= 0) { if (_tipCell < _model.Length) { Show(_tipCell); } else { CloseTip(); } }
        Wake();
    }

    private Color LevelOf(Cell cell) => cell.Empty ? _levels[0] : _levels[cell.Level];

    // One square per day. Reused squares recolor in a diagonal sweep; days outside the range shrink away in it and grow back when a range needs them.
    private void Cells(bool first)
    {
        int maxDiagonal = Math.Max(1, _model.Weeks - 1 + 6);
        double step = Math.Min(.012, WaveTravel / maxDiagonal);
        var keep = _cells.ToDictionary(cell => (cell.Column, cell.Row));
        _cells.Clear();
        for (int row = 0; row < 7; row++)
        {
            for (int column = 0; column < _model.Weeks; column++)
            {
                int index = column * 7 + row - _model.Lead;
                bool inRange = index >= 0 && index < _model.Length;
                bool known = keep.TryGetValue((column, row), out var cell);
                cell ??= new Cell { Column = column, Row = row };
                cell.Index = inRange ? index : -1;
                cell.Empty = !inRange;
                cell.Level = inRange ? _model.Levels[index] : 0;
                cell.Wave = Math.Round((column + row) * step, 3);
                cell.WaveBack = Math.Round((maxDiagonal - column - row) * step, 3);
                double delay = _back ? cell.WaveBack : cell.Wave;
                var color = LevelOf(cell);
                double scale = cell.Empty ? 0 : 1;
                if (!known || !Moving)
                {
                    cell.Color.Jump(color);
                    cell.Scale.Jump(_revealed ? scale : cell.Empty ? 0 : .5);
                    cell.Opacity.Jump(_revealed ? 1 : 0);
                }
                else
                {
                    cell.Color.Go(color, CellTransition, ArcEasing.Standard, delay);
                    cell.Scale.Go(scale, Tween.SpringTokenSeconds, Tween.SpringToken, delay);
                }
                _cells.Add(cell);
            }
        }
    }

    private void Months(bool first)
    {
        var seen = new Dictionary<int, int>();
        var live = new List<MonthLabel>();
        foreach (var (month, col, label) in _model.Months)
        {
            int calendar = month % 12;
            seen[calendar] = seen.GetValueOrDefault(calendar) + 1;
            string key = $"{calendar}-{seen[calendar] - 1}";
            var entry = _monthLabels.Find(item => item.Key == key);
            if (entry is null)
            {
                var view = CopyMotion.Text(label, 12, 16d / 12, -.16, FontWeights.Normal, _muted);
                _months.Children.Add(view);
                entry = new MonthLabel(key, view, col);
            }
            // Month labels glide to their new week when a range starts on a different weekday.
            entry.Column.Go(col, Moving ? Tween.SpringTokenSeconds : 0, Tween.SpringToken);
            live.Add(entry);
        }
        foreach (var gone in _monthLabels.Where(item => !live.Contains(item))) { _months.Children.Remove(gone.View); }
        _monthLabels.Clear();
        _monthLabels.AddRange(live);
    }

    private void Weekdays()
    {
        _weekdays.Children.Clear();
        _weekdays.RowDefinitions.Clear();
        for (int row = 0; row < 7; row++)
        {
            int weekday = (row + (int)WeekStart) % 7;
            _weekdays.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_cell + (row < 6 ? Gap : 0)) });
            if (weekday % 2 != 1) { continue; }
            var text = CopyMotion.Text(((DayOfWeek)weekday).ToString()[..3], 12, 1, -.16, FontWeights.Normal, _muted);
            text.VerticalAlignment = VerticalAlignment.Top;
            text.Margin = new Thickness(0, (_cell - 12) / 2, 0, 0);
            Grid.SetRow(text, row);
            _weekdays.Children.Add(text);
        }
    }

    private void Refit(double width)
    {
        _width = width;
        Fit();
    }

    private void Fit()
    {
        if (_width <= 0) { return; }
        double cell = Math.Clamp((_width - LabelColumn - Gap * 53) / 53, 9, 15);
        bool grew = cell != _cell;
        _cell = cell;
        _grid.Height = 7 * _cell + 6 * Gap;
        if (grew) { Weekdays(); }
        // The scroller rests at its right edge, on the most recent weeks.
        _offset.Jump(MaxOffset);
        AimRing(true);
        PlaceMonths();
        RailFade();
        _grid.InvalidateVisual();
    }

    private void PlaceMonths()
    {
        foreach (var month in _monthLabels) { Canvas.SetLeft(month.View, month.Column.Value * Pitch - _offset.Value); }
    }

    private Rect CellRect(Cell cell) => new(LabelColumn + Gap + cell.Column * Pitch - _offset.Value, cell.Row * Pitch, _cell, _cell);

    private Cell? CellAt(Point point)
    {
        double x = point.X - LabelColumn - Gap + _offset.Value;
        if (point.X < LabelColumn + Gap || x < 0 || point.Y < 0) { return null; }
        int column = (int)(x / Pitch), row = (int)(point.Y / Pitch);
        if (row > 6 || x - column * Pitch > _cell || point.Y - row * Pitch > _cell) { return null; }
        int index = column * 7 + row - _model.Lead;
        return index >= 0 && index < _model.Length ? _cells.Find(cell => cell.Column == column && cell.Row == row) : null;
    }

    private void Hover(Point point)
    {
        var hit = CellAt(point);
        int index = hit?.Index ?? -1;
        if (index == _hovered) { return; }
        _hovered = index;
        if (hit is not null) { Show(index); }
        _grid.InvalidateVisual();
    }

    private void LeaveGrid()
    {
        _hovered = -1;
        _grid.InvalidateVisual();
        // A keyboard user keeps their bubble on the focused day.
        if (_focus.Shown && _focusIndex >= 0) { Show(_focusIndex); }
        else { HideSoon(.11); }
    }

    private void OnFocusShown()
    {
        if (!_focus.Shown || _model.Length == 0) { _grid.InvalidateVisual(); return; }
        int selected = SelectedDate is { } date ? date.DayNumber - _model.Start.DayNumber : -1;
        if (_focusIndex < 0) { _focusIndex = selected >= 0 && selected < _model.Length ? selected : _model.Length - 1; }
        Reach(_focusIndex);
        Show(_focusIndex);
        _grid.InvalidateVisual();
    }

    private bool GridKey(Key key)
    {
        if (_model.Length == 0) { return false; }
        int from = _focusIndex >= 0 ? _focusIndex : _model.Length - 1;
        int? next = key switch
        {
            Key.Up => from - 1,
            Key.Down => from + 1,
            Key.Left => from - 7,
            Key.Right => from + 7,
            Key.Home => 0,
            Key.End => _model.Length - 1,
            _ => null,
        };
        if (next is { } to)
        {
            _focusIndex = Math.Clamp(to, 0, _model.Length - 1);
            Reach(_focusIndex);
            Show(_focusIndex);
            Announcer.Say(this, Spoken(_focusIndex));
            _grid.InvalidateVisual();
            return true;
        }
        if (key is Key.Enter or Key.Space) { Choose(from); return true; }
        if (key == Key.Escape && _tipOpen) { CloseTip(); return true; }
        return false;
    }

    private string Spoken(int index)
    {
        int count = _model.Counts[index];
        return $"{(count > 0 ? ChartMath.Grouped(count) : "No")} {Noun(count)}, {_model.Start.AddDays(index).ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture)}";
    }

    private void Choose(int index)
    {
        var date = _model.Start.AddDays(index);
        SetCurrentValue(SelectedDateProperty, date);
        DateChosen?.Invoke(this, new ActivityDateEventArgs(date));
    }

    // Keeps a day reached from the keyboard in view while the grid is scrolled.
    private void Reach(int index)
    {
        int column = (index + _model.Lead) / 7;
        double left = column * Pitch, right = left + _cell;
        if (left < _offset.Value) { Scroll(left); }
        else if (right > _offset.Value + Viewport) { Scroll(right - Viewport); }
    }

    private void Jump(double offset)
    {
        _offset.Jump(Math.Clamp(offset, 0, MaxOffset));
        Follow();
    }

    private void Scroll(double offset)
    {
        offset = Math.Clamp(offset, 0, MaxOffset);
        if (Moving) { _offset.Go(offset, Tween.Fast, ArcEasing.Standard); Wake(); } else { Jump(offset); }
        if (!Moving) { return; }
    }

    private void Follow()
    {
        PlaceMonths();
        AimRing(true);
        // The bubble stays pinned to its cell while the grid scrolls.
        if (_tipOpen && _tipCell >= 0) { PlaceTip(false); }
        _grid.InvalidateVisual();
        _rail.InvalidateVisual();
    }

    private void RailFade()
    {
        double to = MaxOffset > 0 && (_plotRow.IsMouseOver || _railHover || _dragging) ? 1 : 0;
        _rail.IsHitTestVisible = MaxOffset > 0;
        _rail.BeginAnimation(OpacityProperty, null);
        _rail.Opacity = to;
    }

    // Legend: hovering or focusing a level previews it on the grid, clicking pins it. The caption says how many days sit at that level.
    private void PreviewLevel(int? level)
    {
        if (level is { } at) { _previewTimer.Stop(); _preview = at; Highlight(); return; }
        _previewTimer.Stop();
        if (Moving) { _previewTimer.Start(); } else { _preview = null; Highlight(); }
    }

    private void Pin(int level)
    {
        _pinned = _pinned == level ? null : level;
        Highlight();
    }

    private void Highlight()
    {
        int? highlight = _preview ?? _pinned;
        foreach (var cell in _cells)
        {
            if (cell.Empty) { continue; }
            double to = highlight is { } level && cell.Level != level ? .2 : 1;
            if (_revealed) { cell.Opacity.Go(to, Moving ? _revealing ? Tween.Fast : Tween.Fast : 0, ArcEasing.Standard); }
        }
        _caption?.Set(CaptionText(highlight), _secondary, "", 1);
        _legendSwatches.InvalidateVisual();
        Wake();
    }

    private string CaptionText(int? highlight)
    {
        if (highlight is not { } level) { return ""; }
        var (a, b, c) = _model.Bounds;
        string[] ranges = [$"no {UnitOther}", a == 1 ? $"1 {UnitOne}" : $"1–{a} {UnitOther}", $"{a + 1}–{b} {UnitOther}", $"{b + 1}–{c} {UnitOther}", $"{c + 1}+ {UnitOther}"];
        string days = $"{ChartMath.Grouped(_model.PerLevel[level])} {(_model.PerLevel[level] == 1 ? "day" : "days")}";
        // Narrow cards keep the day count and let the highlighted squares say which level it is.
        return _width > 0 && _width <= 440 ? days : $"{days} with {ranges[level]}";
    }

    private bool SwatchKey(Key key)
    {
        int? next = key switch { Key.Left => _legendFocus - 1, Key.Right => _legendFocus + 1, Key.Home => 0, Key.End => 4, _ => null };
        if (key == Key.Escape) { PreviewLevel(null); return true; }
        if (key is Key.Enter or Key.Space) { Pin(_legendFocus); return true; }
        if (next is not { } to) { return false; }
        _legendFocus = Math.Clamp(to, 0, 4);
        PreviewLevel(_legendFocus);
        return true;
    }

    private void Summary()
    {
        if (_total is null || _noun is null || _periodText is null) { return; }
        _total.Set(ChartMath.Grouped(_model.Total), _ink, "");
        _noun.Text = Noun(_model.Total);
        _periodText.Set(Period ?? "", _secondary, "", 1);
        AutomationProperties.SetHelpText(_grid, $"Darker squares mean more {UnitOther}.");
        Announcer.Say(this, $"{ChartMath.Grouped(_model.Total)} {Noun(_model.Total)} in {Period}");
    }

    // Tooltip: one bubble for the whole grid. It jumps into place when it opens and glides on a spring between cells after that.
    private void Show(int index)
    {
        _hideTimer.Stop();
        if (index < 0 || index >= _model.Length) { return; }
        int count = _model.Counts[index];
        var date = _model.Start.AddDays(index);
        string key = date.ToString("O", CultureInfo.InvariantCulture);
        bool wasOpen = _tipOpen;
        int change = wasOpen ? Math.Sign(count - _tipValue) : 0;
        string primary = count > 0 ? $"{ChartMath.Grouped(count)} {Noun(count)}" : $"No {UnitOther}";
        string secondary = date.ToString("ddd, MMM d, yyyy", CultureInfo.InvariantCulture);
        if (key != _tipKey || _tipPrimary is null) { SwapTip(primary, secondary, wasOpen ? change : 0, wasOpen); }
        (_tipKey, _tipValue, _tipCell, _tipOpen) = (key, count, index, true);
        PlaceTip(wasOpen && Moving);
        _tipFade.Go(1, Moving ? Tween.Fast : 0, ArcEasing.Enter);
        _tipScale.Go(1, Moving ? Tween.Snappy : 0, ArcEasing.Standard);
        Wake();
    }

    private void SwapTip(string primary, string secondary, int change, bool animate)
    {
        int direction = change == 0 ? 1 : change;
        foreach (var old in new[] { _tipPrimary, _tipSecondary })
        {
            if (old is null) { continue; }
            if (!animate || !Moving) { _tipBody.Children.Remove(old); continue; }
            CopyMotion.Out(old, direction, this, () => _tipBody.Children.Remove(old));
        }
        _tipPrimary = CopyMotion.Text(primary, 14, 1.3, -.16, FontWeights.Medium, _bubbleInk);
        _tipSecondary = CopyMotion.Text(secondary, 12, 1.35, -.16, FontWeights.Normal, _bubbleSecond);
        _tipSecondary.Margin = new Thickness(0, 14 * 1.3, 0, 0);
        _tipPrimary.VerticalAlignment = _tipSecondary.VerticalAlignment = VerticalAlignment.Top;
        _tipPrimary.HorizontalAlignment = _tipSecondary.HorizontalAlignment = HorizontalAlignment.Left;
        _tipBody.Children.Add(_tipPrimary);
        _tipBody.Children.Add(_tipSecondary);
        if (animate && Moving) { CopyMotion.In(_tipPrimary, direction, this); CopyMotion.In(_tipSecondary, direction, this); }
    }

    private void PlaceTip(bool glide)
    {
        if (_tipPrimary is null || _tipSecondary is null || _tipCell < 0 || !IsLoaded && _width <= 0) { return; }
        _tipPrimary.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _tipSecondary.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = Math.Ceiling(Math.Max(_tipPrimary.DesiredSize.Width, _tipSecondary.DesiredSize.Width));
        var cell = _cells.Find(item => item.Index == _tipCell && !item.Empty);
        if (cell is null) { return; }
        var box = CellRect(cell);
        var origin = _grid.IsLoaded && _host?.Child is UIElement root ? _grid.TransformToVisual(root).Transform(new Point()) : new Point();
        double half = width / 2 + 13;
        double x = Math.Clamp(origin.X + box.Left + box.Width / 2, Math.Min(half + 2, _width / 2), Math.Max(_width - half - 2, _width / 2));
        double y = origin.Y + box.Top;
        if (glide)
        {
            _tipX.To(x, SpringTune.Snappy);
            _tipY.To(y, SpringTune.Snappy);
            _tipWidth.To(width, SpringTune.Morph);
        }
        else { _tipX.Jump(x); _tipY.Jump(y); _tipWidth.Jump(width); }
        MoveTip();
    }

    private void MoveTip()
    {
        _tipBody.Width = Math.Max(0, _tipWidth.Position);
        _tipBody.Height = 14 * 1.3 + 12 * 1.35;
        double bubbleWidth = _tipBody.Width + 26;
        double bubbleHeight = _tipBody.Height + 17;
        Canvas.SetLeft(_bubble, _tipX.Position - bubbleWidth / 2);
        Canvas.SetTop(_bubble, _tipY.Position - 8 - bubbleHeight);
        _bubble.Opacity = _tipFade.Value;
        var scale = (ScaleTransform)_bubble.RenderTransform;
        scale.ScaleX = scale.ScaleY = _tipScale.Value;
    }

    private void HideSoon(double seconds)
    {
        _hideTimer.Stop();
        if (seconds <= 0) { CloseTip(); return; }
        _hideTimer.Interval = TimeSpan.FromSeconds(seconds);
        _hideTimer.Start();
    }

    private void CloseTip()
    {
        _tipOpen = false;
        _tipCell = -1;
        _tipFade.Go(0, Moving ? Tween.Instant : 0, ArcEasing.Standard);
        _tipScale.Go(.96, Moving ? Tween.Instant : 0, ArcEasing.Standard);
        Wake();
    }

    // The selection ring glides between days. When it first appears it fades in where it lands, not flying in from its last spot.
    private void AimRing() => AimRing(false);
    private void AimRing(bool jump)
    {
        int index = SelectedDate is { } date ? date.DayNumber - _model.Start.DayNumber : -1;
        bool shown = index >= 0 && index < _model.Length && _revealed;
        if (index >= 0 && index < _model.Length)
        {
            double x = (index + _model.Lead) / 7 * Pitch, y = (index + _model.Lead) % 7 * Pitch;
            if (jump || !Moving || _ringFade.Target == 0) { _ringX.Jump(x); _ringY.Jump(y); }
            else { _ringX.Go(x, Tween.SpringTokenSeconds, Tween.SpringToken); _ringY.Go(y, Tween.SpringTokenSeconds, Tween.SpringToken); }
        }
        _ringFade.Go(shown ? 1 : 0, Moving ? Tween.Fast : 0, ArcEasing.Standard);
        Wake();
    }

    // The cells wave in once they are seen, along the diagonal.
    private void Reveal()
    {
        if (_revealed) { return; }
        _revealed = true;
        if (!Moving)
        {
            foreach (var cell in _cells) { cell.Opacity.Jump(1); cell.Scale.Jump(cell.Empty ? 0 : 1); }
            _grid.InvalidateVisual();
        }
        else
        {
            _revealing = true;
            _revealTimer.Start();
            foreach (var cell in _cells)
            {
                double delay = _back ? cell.WaveBack : cell.Wave;
                cell.Opacity.Go(1, .36, ArcEasing.Enter, delay);
                cell.Scale.Go(cell.Empty ? 0 : 1, Tween.SpringTokenSeconds, Tween.SpringToken, delay);
            }
        }
        AimRing();
        Highlight();
        Wake();
    }

    private void Colorize()
    {
        _inkColor = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _ink.Color = _inkColor;
        _secondary.Color = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        var surface = Tween.Resolve(this, "ArcWpf.Brush.Surface");
        var background = Tween.Resolve(this, "ArcWpf.Brush.Background");
        var accent = Tween.Resolve(this, "ArcWpf.Brush.Accent");
        // Levels are tints of the accent over a neutral empty square, so the grid follows the selected accent and both themes.
        _levels[0] = OkColor.Mix(_inkColor, surface, .065);
        _levels[1] = OkColor.Mix(accent, _levels[0], .26);
        _levels[2] = OkColor.Mix(accent, _levels[0], .5);
        _levels[3] = OkColor.Mix(accent, _levels[0], .76);
        _levels[4] = accent;
        _hoverOutline = OkColor.Alpha(_inkColor, .45);
        _focusColor = FocusVisible.RingBrush(this).Color;
        _bubbleFill.Color = _inkColor;
        _bubbleInk.Color = background;
        _bubbleSecond.Color = OkColor.Mix(background, _inkColor, .68);
        _bubbleEdge.Color = OkColor.Mix(background, _inkColor, .14);
        _bubble.Background = _bubbleFill;
        _bubble.BorderBrush = _bubbleEdge;
        foreach (var cell in _cells) { cell.Color.Jump(LevelOf(cell)); }
        _grid.InvalidateVisual();
        _legendSwatches.InvalidateVisual();
        _rail.InvalidateVisual();
    }

    private void Wake() { if (Moving) { _loop.Start(); } else { Settle(); } }

    private void Settle()
    {
        PlaceMonths();
        if (_tipPrimary is not null) { MoveTip(); }
        _grid.InvalidateVisual();
    }

    private bool Frame(double seconds)
    {
        bool busy = false;
        foreach (var cell in _cells)
        {
            if (cell.Color.Active || cell.Scale.Active || cell.Opacity.Active)
            {
                cell.Color.Step(seconds);
                cell.Scale.Step(seconds);
                cell.Opacity.Step(seconds);
                busy = true;
            }
        }
        foreach (var month in _monthLabels) { month.Column.Step(seconds); busy |= month.Column.Active; }
        foreach (var timed in new[] { _tipFade, _tipScale, _ringX, _ringY, _ringFade }) { timed.Step(seconds); busy |= timed.Active; }
        if (_offset.Active) { _offset.Step(seconds); busy = true; Follow(); }
        foreach (var spring in new[] { _tipX, _tipY, _tipWidth }) { spring.Step(seconds); busy |= spring.Active; }
        PlaceMonths();
        MoveTip();
        _grid.InvalidateVisual();
        return busy;
    }

    private void PaintGrid(DrawingContext dc, Size size)
    {
        if (_width <= 0) { return; }
        double radius = _cell * .28;
        // Cells pass under the pinned weekday column.
        dc.PushClip(new RectangleGeometry(new Rect(LabelColumn + Gap, -6, Math.Max(0, size.Width - LabelColumn - Gap + 6), size.Height + 12)));
        foreach (var cell in _cells)
        {
            double scale = cell.Scale.Value, opacity = cell.Opacity.Value;
            if (scale <= 0 || opacity <= 0) { continue; }
            var box = CellRect(cell);
            if (box.Right < LabelColumn || box.Left > size.Width + 6) { continue; }
            double side = _cell * scale;
            var drawn = new Rect(box.Left + (_cell - side) / 2, box.Top + (_cell - side) / 2, side, side);
            dc.DrawRoundedRectangle(ChartMath.Solid(ChartMath.Faded(cell.Color.Value, opacity)), null, drawn, radius * scale, radius * scale);
        }
        if (_hovered >= 0 && _cells.Find(cell => cell.Index == _hovered && !cell.Empty) is { } hovered)
        {
            var box = CellRect(hovered);
            box.Inflate(1.75, 1.75);
            dc.DrawRoundedRectangle(null, ChartMath.Crisp(_hoverOutline, 1.5), box, radius + 1.75, radius + 1.75);
        }
        if (_focus.Shown && _focusIndex >= 0 && _cells.Find(cell => cell.Index == _focusIndex && !cell.Empty) is { } focused)
        {
            var box = CellRect(focused);
            box.Inflate(2, 2);
            dc.DrawRoundedRectangle(null, ChartMath.Crisp(_focusColor, 2), box, radius + 2, radius + 2);
        }
        if (_ringFade.Value > 0)
        {
            var box = new Rect(LabelColumn + Gap - 3 + _ringX.Value - _offset.Value, _ringY.Value - 3, _cell + 6, _cell + 6);
            box.Inflate(-.75, -.75);
            dc.DrawRoundedRectangle(null, ChartMath.Crisp(ChartMath.Faded(_inkColor, _ringFade.Value), 1.5), box, radius + 2.25, radius + 2.25);
        }
        dc.Pop();
    }

    private void PaintSwatches(DrawingContext dc, Size size)
    {
        for (int level = 0; level < 5; level++)
        {
            double x = level * 15;
            dc.DrawRoundedRectangle(ChartMath.Solid(_levels[level]), null, new Rect(x + 2, 4.5, 11, 11), 3, 3);
            if (_pinned == level) { dc.DrawRoundedRectangle(null, ChartMath.Crisp(_inkColor, 1.5), new Rect(x + .75, .75, 13.5, 18.5), 3.25, 3.25); }
            if (_swatchFocus.Shown && _legendFocus == level) { dc.DrawRoundedRectangle(null, ChartMath.Crisp(_focusColor, 2), new Rect(x, 0, 15, 20), 4, 4); }
        }
    }

    // A thin bar stands in for the browser's scrollbar on a grid too wide for its card.
    private void PaintRail(DrawingContext dc, Size size)
    {
        if (MaxOffset <= 0 || size.Width <= 0) { return; }
        double length = size.Width / RailRatio, at = _offset.Value / MaxOffset * (size.Width - length);
        dc.DrawRoundedRectangle(ChartMath.Solid(Tween.Resolve(this, "ArcWpf.Brush.BorderStrong")), null, new Rect(at, 2, length, 4), 2, 2);
    }

    private sealed class HeatmapPeer(FrameworkElement owner, ActivityHeatmap chart) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;
        protected override string GetNameCore() { string own = base.GetNameCore(); return string.IsNullOrEmpty(own) ? chart.Label : own; }
        protected override string GetItemStatusCore() => chart._focusIndex >= 0 && chart._focusIndex < chart._model.Length ? chart.Spoken(chart._focusIndex) : "";
        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}

internal static class RectExtensions
{
    public static Point Center(this Rect rect) => new(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
}
