using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

// One month of date-range-picker.tsx: title, weekday row, six weeks of days over a highlight layer.
// The highlight is one bar per week that stretches (STRETCH spring) plus a solid end at each side that glides (GLIDE spring).
internal sealed class RangeMonth : IDisposable
{
    private sealed record Day(DateTime Date, System.Windows.Controls.Button Button, Border Span, TextBlock Number, Ellipse Today, ScaleTransform Press, bool Disabled);
    private sealed class Bar(Border element)
    {
        public Border Element { get; } = element;
        public ControlSpring Left { get; set; } = null!;
        public ControlSpring Width { get; set; } = null!;
        public bool WasEmpty { get; set; } = true;
    }
    private sealed class Thumb(Grid element, TranslateTransform move, ScaleTransform scale)
    {
        public Grid Element { get; } = element;
        public TranslateTransform Move { get; } = move;
        public ScaleTransform Scale { get; } = scale;
        public ControlSpring X { get; set; } = null!;
        public ControlSpring Y { get; set; } = null!;
        public ControlSpring S { get; set; } = null!;
        public bool Hidden { get; set; } = true;
    }

    private static readonly double Stretch = ControlSpring.Physical(.3), Glide = ControlSpring.Physical(.3);
    private readonly DateRangePicker _owner;
    private readonly double _cell;
    private readonly DateTime _first;
    private readonly List<Day> _days = [];
    private readonly Bar[] _bars = new Bar[6];
    private readonly Thumb[] _thumbs = new Thumb[2];
    private readonly SpringDriver _springs;
    private readonly OwnedMotion _motion = new();
    private DateRange? _range;

    public DateTime Month { get; }
    public FrameworkElement Element { get; }

    public RangeMonth(DateRangePicker owner, DateTime month, double cell)
    {
        _owner = owner; _cell = cell; Month = DateRangePicker.MonthStart(month);
        var culture = CultureOf(owner);
        _first = Month.AddDays(-(((int)Month.DayOfWeek - (int)owner.FirstDayOfWeek + 7) % 7));
        _springs = new SpringDriver(Apply);

        var root = new StackPanel { Width = cell * 7 };
        var title = new TextBlock { Text = Month.ToString("MMMM yyyy", culture), Height = 32, LineHeight = 32, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = FontWeights.Medium, TextAlignment = TextAlignment.Center };
        title.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        root.Children.Add(title);
        AutomationProperties.SetName(root, title.Text);

        var weekdays = new UniformGrid7(cell, 1, 28);
        for (int col = 0; col < 7; col++)
        {
            var date = _first.AddDays(col);
            string name = culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek);
            var label = new TextBlock { Text = name.Length > 2 ? name[..2] : name, FontSize = 12, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            AutomationProperties.SetName(label, culture.DateTimeFormat.GetDayName(date.DayOfWeek));
            weekdays.Add(label, 0, col);
        }
        root.Children.Add(weekdays);

        var weeks = new Grid { Height = cell * 6 };
        var layer = new Canvas { IsHitTestVisible = false };
        for (int row = 0; row < 6; row++)
        {
            var element = new Border { Height = cell - 6, CornerRadius = new CornerRadius(cell), Opacity = 0 };
            element.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.AccentSubtle");
            Canvas.SetTop(element, row * cell + 3);
            layer.Children.Add(element);
            _bars[row] = new Bar(element) { Left = _springs.Add(0, Stretch), Width = _springs.Add(0, Stretch) };
        }
        for (int i = 0; i < 2; i++)
        {
            var dot = new Ellipse { Margin = new Thickness(3) };
            dot.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Accent");
            var move = new TranslateTransform(); var scale = new ScaleTransform(.8, .8, cell / 2, cell / 2);
            var element = new Grid { Width = cell, Height = cell, Opacity = 0, Children = { dot }, RenderTransform = new TransformGroup { Children = { scale, move } } };
            layer.Children.Add(element);
            _thumbs[i] = new Thumb(element, move, scale) { X = _springs.Add(0, Glide), Y = _springs.Add(0, Glide), S = _springs.Add(.8, Glide) };
        }
        weeks.Children.Add(layer);

        var grid = new UniformGrid7(cell, 6, cell);
        for (int index = 0; index < 42; index++)
        {
            var date = _first.AddDays(index);
            if (date.Month != Month.Month) continue;
            var day = BuildDay(date, culture);
            grid.Add(day.Button, index / 7, index % 7);
            _days.Add(day);
        }
        weeks.Children.Add(grid);
        root.Children.Add(weeks);
        Element = root;
    }

    private static CultureInfo CultureOf(FrameworkElement element)
    {
        try { return element.Language.GetSpecificCulture(); }
        catch (InvalidOperationException) { return CultureInfo.GetCultureInfo("en-US"); }
    }

    private Day BuildDay(DateTime date, CultureInfo culture)
    {
        bool disabled = _owner.IsDisabled(date);
        var press = new ScaleTransform(1, 1);
        var span = new Border { Margin = new Thickness(3), CornerRadius = new CornerRadius(_cell), RenderTransformOrigin = new Point(.5, .5), RenderTransform = press };
        var number = new TextBlock { Text = date.Day.ToString(culture), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Typography.SetNumeralAlignment(number, FontNumeralAlignment.Tabular);
        var today = new Ellipse { Width = 4, Height = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 5), Visibility = Visibility.Collapsed };
        var button = new System.Windows.Controls.Button { Width = _cell, Height = _cell, Content = new Grid { Children = { span, number, today } }, Style = (Style)_owner.FindResource("ArcWpf.DateRangePicker.Day"), IsEnabled = !disabled, IsTabStop = false };
        AutomationProperties.SetName(button, date.ToString("dddd, MMMM d, yyyy", culture));
        var day = new Day(date, button, span, number, today, press, disabled);
        button.Click += (_, _) => _owner.Pick(date);
        button.MouseEnter += (_, _) => { if (!disabled) _owner.HoverDay(date); Paint(day); };
        button.MouseLeave += (_, _) => Paint(day);
        button.GotKeyboardFocus += (_, _) => { _owner.FocusedDay(date); Paint(day); };
        button.LostKeyboardFocus += (_, _) => Paint(day);
        button.PreviewKeyDown += (_, e) => _owner.DayKey(e, date);
        // Press: the day's circle dips to .9 on the instant duration.
        button.PreviewMouseLeftButtonDown += (_, _) => PressTo(day, .9);
        button.PreviewMouseLeftButtonUp += (_, _) => PressTo(day, 1);
        button.LostMouseCapture += (_, _) => PressTo(day, 1);
        Paint(day);
        return day;
    }

    private void PressTo(Day day, double scale)
    {
        bool motion = ArcMotion.GetLevel(_owner) == ArcMotionLevel.Full && _owner.IsLoaded;
        _motion.Double(day.Press, ScaleTransform.ScaleXProperty, scale, .12, motion);
        _motion.Double(day.Press, ScaleTransform.ScaleYProperty, scale, .12, motion);
    }

    private bool IsEdge(DateTime date) => _range is { } r && (DateRangePicker.DayDiff(date, r.Start) == 0 || DateRangePicker.DayDiff(date, r.End) == 0);

    private void Paint(Day day)
    {
        bool edge = IsEdge(day.Date);
        bool isToday = DateRangePicker.DayDiff(day.Date, DateTime.Today) == 0;
        day.Number.SetResourceReference(TextBlock.ForegroundProperty, day.Disabled ? "ArcWpf.Brush.TextMuted" : edge ? "ArcWpf.Brush.AccentForeground" : "ArcWpf.Brush.Foreground");
        day.Number.FontWeight = edge ? FontWeights.Medium : FontWeights.Normal;
        day.Button.Opacity = day.Disabled ? .5 : 1;
        day.Today.Visibility = isToday ? Visibility.Visible : Visibility.Collapsed;
        day.Today.SetResourceReference(Shape.FillProperty, edge ? "ArcWpf.Brush.AccentForeground" : "ArcWpf.Brush.Accent");
        bool hot = !day.Disabled && !edge && (day.Button.IsMouseOver || day.Button.IsKeyboardFocused);
        if (hot) day.Span.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.RangeDayHover");
        else day.Span.Background = Brushes.Transparent;
        bool inRange = _range is { } r && DateRangePicker.DayDiff(day.Date, r.Start) >= 0 && DateRangePicker.DayDiff(day.Date, r.End) <= 0;
        AutomationProperties.SetItemStatus(day.Button, inRange ? "Selected" : string.Empty);
    }

    public void SetTabStop(string key)
    {
        foreach (var day in _days) day.Button.IsTabStop = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) == key;
    }

    public System.Windows.Controls.Button? Find(string key) => _days.FirstOrDefault(day => day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) == key && !day.Disabled)?.Button;

    private (int Row, int Col) CellOf(DateTime date) { int index = DateRangePicker.DayDiff(date, _first); return (index / 7, index % 7); }

    public void Update(DateRange? range, DateTime today, bool animate)
    {
        _range = range;
        foreach (var day in _days) Paint(day);
        bool reduced = !animate;
        // Each week's slice in DIP; a week wholly outside collapses at the edge the range would grow in from.
        for (int row = 0; row < 6; row++)
        {
            var bar = _bars[row];
            var days = Enumerable.Range(0, 7).Select(col => _first.AddDays(row * 7 + col)).Where(date => date.Month == Month.Month).ToList();
            (double Left, double Width)? segment = null;
            if (range is { } r && days.Count > 0)
            {
                var rowFirst = days[0]; var rowLast = days[^1];
                var from = DateRangePicker.DayDiff(r.Start, rowFirst) > 0 ? r.Start : rowFirst;
                var to = DateRangePicker.DayDiff(r.End, rowLast) < 0 ? r.End : rowLast;
                int ColOf(DateTime date) => DateRangePicker.DayDiff(date, _first) % 7;
                if (DateRangePicker.DayDiff(from, to) <= 0) { int c0 = ColOf(from), c1 = ColOf(to); segment = (c0 * _cell, (c1 - c0 + 1) * _cell); }
                else if (DateRangePicker.DayDiff(rowLast, r.Start) < 0) segment = ((ColOf(rowLast) + 1) * _cell, 0);
                else segment = (ColOf(rowFirst) * _cell, 0);
            }
            if (segment is not { } s)
            {
                bar.WasEmpty = true;
                _motion.Double(bar.Element, UIElement.OpacityProperty, 0, reduced ? 0 : .14, !reduced);
                continue;
            }
            if (bar.WasEmpty || reduced) { bar.Left.Snap(s.Left); bar.Width.Snap(s.Width); }
            else { _springs.Animate(bar.Left, s.Left, Stretch); _springs.Animate(bar.Width, s.Width, Stretch); }
            bar.WasEmpty = false;
            _motion.Double(bar.Element, UIElement.OpacityProperty, 1, reduced ? 0 : .16, !reduced);
        }
        (int, int)? start = range is { } a && a.Start.Year == Month.Year && a.Start.Month == Month.Month ? CellOf(a.Start) : null;
        (int, int)? end = range is { } b && DateRangePicker.DayDiff(b.End, b.Start) != 0 && b.End.Year == Month.Year && b.End.Month == Month.Month ? CellOf(b.End) : null;
        MoveThumb(_thumbs[0], start, reduced);
        MoveThumb(_thumbs[1], end, reduced);
        Apply();
    }

    // A solid end glides from day to day and only fades when the end leaves this month.
    private void MoveThumb(Thumb thumb, (int Row, int Col)? cell, bool reduced)
    {
        if (cell is not { } c)
        {
            thumb.Hidden = true;
            _motion.Double(thumb.Element, UIElement.OpacityProperty, 0, reduced ? 0 : .12, !reduced);
            if (reduced) thumb.S.Snap(.8); else _springs.Animate(thumb.S, .8, .12 / 1.2);
            return;
        }
        if (thumb.Hidden || reduced) { thumb.X.Snap(c.Col); thumb.Y.Snap(c.Row); }
        else { _springs.Animate(thumb.X, c.Col, Glide); _springs.Animate(thumb.Y, c.Row, Glide); }
        thumb.Hidden = false;
        _motion.Double(thumb.Element, UIElement.OpacityProperty, 1, reduced ? 0 : .16, !reduced);
        if (reduced) thumb.S.Snap(1); else _springs.Animate(thumb.S, 1, Glide);
    }

    private void Apply()
    {
        foreach (var bar in _bars)
        {
            Canvas.SetLeft(bar.Element, bar.Left.Position);
            bar.Element.Width = Math.Max(0, bar.Width.Position);
        }
        foreach (var thumb in _thumbs)
        {
            thumb.Move.X = thumb.X.Position * _cell; thumb.Move.Y = thumb.Y.Position * _cell;
            thumb.Scale.ScaleX = thumb.Scale.ScaleY = thumb.S.Position;
        }
    }

    public void Dispose() { _springs.Stop(); _motion.Stop(); }

    // A fixed 7-column grid of cell-sized slots.
    private sealed class UniformGrid7 : Grid
    {
        public UniformGrid7(double cell, int rows, double rowHeight)
        {
            for (int col = 0; col < 7; col++) ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cell) });
            for (int row = 0; row < rows; row++) RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowHeight) });
        }
        public void Add(UIElement element, int row, int col) { SetRow(element, row); SetColumn(element, col); Children.Add(element); }
    }
}
