using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

/// <summary>One row of a <see cref="ReorderableList"/>.</summary>
public sealed class ReorderableListItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public sealed class ReorderableListItemMovedEventArgs(RoutedEvent routedEvent, object source, object item, int oldIndex, int newIndex) : RoutedEventArgs(routedEvent, source)
{
    public object Item { get; } = item;
    public int OldIndex { get; } = oldIndex;
    public int NewIndex { get; } = newIndex;
}

// reorderable-list.tsx (frozen 2026-10-31): rows reorder by dragging the grip handle or with ArrowUp/ArrowDown on it.
// The row lifts (scale 1.02, floating shadow) while pressed, dragged or just moved; the others glide to their slots
// and each position number rolls in the direction its row travelled.
[TemplatePart(Name = "PART_Host", Type = typeof(Border))]
public class ReorderableList : Control
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IList), typeof(ReorderableList), new FrameworkPropertyMetadata(null, OnItemsSourceChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ReorderableList), new FrameworkPropertyMetadata("Reorder items"));
    public static readonly RoutedEvent ItemMovedEvent = EventManager.RegisterRoutedEvent(nameof(ItemMoved), RoutingStrategy.Bubble, typeof(EventHandler<ReorderableListItemMovedEventArgs>), typeof(ReorderableList));

    /// <summary>A mutable list of <see cref="ReorderableListItem"/>; the list reorders it in place.</summary>
    public IList? ItemsSource { get => (IList?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    /// <summary>Accessible name of the list.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public event EventHandler<ReorderableListItemMovedEventArgs> ItemMoved { add => AddHandler(ItemMovedEvent, value); remove => RemoveHandler(ItemMovedEvent, value); }

    internal const double GAP = 8, SMOOTH = .4, SNAPPY = .26, LIFT = 1.02, LIFT_HOLD = .26;
    // Drop settle: bounceStiffness 520 settles in about 3.89 / sqrt(520) seconds; ControlSpring stays critically damped.
    internal static readonly double Settle = 3.89 / Math.Sqrt(520);
    private readonly RowsPanel _panel = new() { Gap = GAP };
    private readonly Dictionary<object, Row> _rows = new(ReferenceEqualityComparer.Instance);
    private INotifyCollectionChanged? _observed;
    private bool _moving;
    private Row? _drag;
    private double _grab;
    private Point _down;

    static ReorderableList() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ReorderableList), new FrameworkPropertyMetadata(typeof(ReorderableList))); }

    public ReorderableList() { Unloaded += (_, _) => { foreach (var row in _rows.Values) row.Finish(); }; }

    internal ArcMotionLevel Level => ArcMotion.GetLevel(this);
    internal IReadOnlyList<UIElement> Order => _panel.Order;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_panel.Parent is Border old) old.Child = null;
        if (GetTemplateChild("PART_Host") is Border host) host.Child = _panel;
        Sync(false);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var list = (ReorderableList)d;
        if (list._observed is not null) list._observed.CollectionChanged -= list.SourceChanged;
        list._observed = e.NewValue as INotifyCollectionChanged;
        if (list._observed is not null) list._observed.CollectionChanged += list.SourceChanged;
        list.Sync(false);
    }

    private void SourceChanged(object? sender, NotifyCollectionChangedEventArgs e) { if (!_moving) Sync(true); }

    // Rows are keyed by item, so an outside reorder glides existing rows instead of rebuilding them.
    private void Sync(bool animate)
    {
        var items = ItemsSource?.Cast<object>().ToList() ?? [];
        foreach (var gone in _rows.Keys.Where(item => !items.Contains(item)).ToList())
        {
            _rows[gone].Finish();
            _panel.Children.Remove(_rows[gone]);
            _rows.Remove(gone);
        }
        var before = Tops();
        var order = new List<UIElement>();
        foreach (var item in items)
        {
            if (!_rows.TryGetValue(item, out var row))
            {
                row = new Row(this, item);
                _rows[item] = row;
                _panel.Children.Add(row);
            }
            order.Add(row);
        }
        _panel.Order = order;
        Relayout(before, animate, null);
    }

    private Dictionary<Row, double> Tops() => _rows.Values.ToDictionary(row => row, row => _panel.TopOf(row) + row.Offset);

    // FLIP: every row that changed slot starts where it was seen and springs to its new slot.
    private void Relayout(Dictionary<Row, double> before, bool animate, Row? held)
    {
        _panel.Measure(new Size(_panel.ActualWidth > 0 ? _panel.ActualWidth : double.PositiveInfinity, double.PositiveInfinity));
        for (int i = 0; i < _panel.Order.Count; i++)
        {
            var row = (Row)_panel.Order[i];
            row.SetPosition(i, _panel.Order.Count, animate);
            if (row == held || !before.TryGetValue(row, out var seen)) continue;
            row.Glide(seen - _panel.TopOf(row), animate && Level == ArcMotionLevel.Full);
        }
    }

    internal void Move(Row row, int direction, bool keyboard)
    {
        var list = ItemsSource;
        if (list is null) return;
        int index = list.IndexOf(row.Item), next = index + direction;
        if (index < 0 || next < 0 || next >= list.Count) return;
        var before = Tops();
        _moving = true;
        try
        {
            var other = list[next];
            list[next] = row.Item;
            list[index] = other;
        }
        finally { _moving = false; }
        _panel.Order = list.Cast<object>().Select(item => (UIElement)_rows[item]).ToList();
        Relayout(before, IsLoaded, keyboard ? null : row);
        if (keyboard) row.Lift();
        RaiseEvent(new ReorderableListItemMovedEventArgs(ItemMovedEvent, this, row.Item, index, next));
    }

    // The row follows the pointer; crossing a neighbour's midpoint swaps the two and the neighbour glides away.
    internal void DragStart(Row row, MouseButtonEventArgs e)
    {
        _down = e.GetPosition(_panel);
        _drag = row;
        _grab = _down.Y - (_panel.TopOf(row) + row.Offset);
    }

    internal void DragMove(Row row, MouseEventArgs e)
    {
        if (_drag != row) return;
        var point = e.GetPosition(_panel);
        if (!row.Dragging)
        {
            if (Math.Abs(point.Y - _down.Y) < 3) return;
            row.Dragging = true;
        }
        row.Follow(point.Y - _grab - _panel.TopOf(row));
        int index = _panel.Order.IndexOf(row);
        double center = _panel.TopOf(row) + row.Offset + row.ActualHeight / 2;
        if (index + 1 < _panel.Order.Count && center > Middle(_panel.Order[index + 1])) { Move(row, 1, false); row.Follow(point.Y - _grab - _panel.TopOf(row)); }
        else if (index > 0 && center < Middle(_panel.Order[index - 1])) { Move(row, -1, false); row.Follow(point.Y - _grab - _panel.TopOf(row)); }
    }

    private double Middle(UIElement row) => _panel.TopOf(row) + row.RenderSize.Height / 2;

    internal void DragEnd(Row row)
    {
        if (_drag != row) return;
        _drag = null;
        row.Dragging = false;
        row.Drop(Level == ArcMotionLevel.Full);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ReorderableListPeer(this);

    private sealed class ReorderableListPeer(ReorderableList owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
        protected override string GetNameCore() => owner.Label;
        protected override string GetClassNameCore() => nameof(ReorderableList);
    }

    // Stacks rows in Order rather than child order, so a reorder never detaches the focused or captured handle.
    internal sealed class RowsPanel : Panel
    {
        public double Gap { get; set; }
        private List<UIElement> _order = [];
        public List<UIElement> Order { get => _order; set { _order = value; InvalidateMeasure(); } }

        public double TopOf(UIElement element)
        {
            double top = 0;
            foreach (var row in _order) { if (row == element) return top; top += row.DesiredSize.Height + Gap; }
            return top;
        }

        protected override Size MeasureOverride(Size available)
        {
            double width = 0, height = 0;
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(available.Width, double.PositiveInfinity));
                width = Math.Max(width, child.DesiredSize.Width);
            }
            foreach (var row in _order) height += row.DesiredSize.Height;
            return new Size(width, height + Math.Max(0, _order.Count - 1) * Gap);
        }

        protected override Size ArrangeOverride(Size final)
        {
            foreach (var row in _order) row.Arrange(new Rect(0, TopOf(row), final.Width, row.DesiredSize.Height));
            return final;
        }
    }

    internal sealed class Row : Grid
    {
        // lucide grip-vertical: six dots on the 24-unit grid.
        private static readonly Geometry Grip = Frozen("M9,12 m-1,0 a1,1 0 1 0 2,0 a1,1 0 1 0 -2,0 M9,5 m-1,0 a1,1 0 1 0 2,0 a1,1 0 1 0 -2,0 M9,19 m-1,0 a1,1 0 1 0 2,0 a1,1 0 1 0 -2,0 M15,12 m-1,0 a1,1 0 1 0 2,0 a1,1 0 1 0 -2,0 M15,5 m-1,0 a1,1 0 1 0 2,0 a1,1 0 1 0 -2,0 M15,19 m-1,0 a1,1 0 1 0 2,0 a1,1 0 1 0 -2,0");
        private readonly ReorderableList _owner;
        private readonly System.Windows.Controls.Button _handle;
        private readonly Border _shadow;
        private readonly Grid _digits;
        private readonly TranslateTransform _move = new();
        private readonly ScaleTransform _scale = new(1, 1);
        private readonly SpringDriver _driver;
        private readonly ControlSpring _y, _s;
        private TextBlock? _number;
        private int _index = -1;
        private bool _pressed, _lifted, _dragging;
        private System.Windows.Threading.DispatcherTimer? _liftTimer;

        public object Item { get; }
        public double Offset => _move.Y;

        public Row(ReorderableList owner, object item)
        {
            _owner = owner;
            Item = item;
            _driver = new SpringDriver(Apply);
            _y = _driver.Add(0, SMOOTH);
            _s = _driver.Add(1, SNAPPY);
            RenderTransformOrigin = new Point(.5, .5);
            RenderTransform = new TransformGroup { Children = { _scale, _move } };

            // ::after: the floating shadow lives on its own layer and fades in, so the moving row never repaints it.
            _shadow = new Border { Opacity = 0, Margin = new Thickness(-1), IsHitTestVisible = false };
            _shadow.SetResourceReference(Border.CornerRadiusProperty, "ArcWpf.Radius.Control");
            _shadow.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
            _shadow.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
            var face = new Border { MinHeight = 56, BorderThickness = new Thickness(1), Padding = new Thickness(7, 7, 16, 7) };
            face.SetResourceReference(Border.CornerRadiusProperty, "ArcWpf.Radius.Control");
            face.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
            face.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
            face.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Resting");

            _handle = new System.Windows.Controls.Button { Width = 40, Height = 40, Margin = new Thickness(0, 0, 12, 0), Cursor = Cursors.Hand, Content = Icon() };
            _handle.SetResourceReference(StyleProperty, new ComponentResourceKey(typeof(ReorderableList), "Handle"));
            DockPanel.SetDock(_handle, Dock.Left);
            _handle.PreviewMouseLeftButtonDown += HandleDown;
            _handle.PreviewMouseMove += (_, e) => _owner.DragMove(this, e);
            _handle.PreviewMouseLeftButtonUp += (_, _) => Release();
            _handle.LostMouseCapture += (_, _) => Release();
            _handle.PreviewKeyDown += HandleKey;

            _digits = new Grid { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ClipToBounds = false };
            DockPanel.SetDock(_digits, Dock.Right);
            var (title, detail) = Copy(item);
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            copy.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis });
            if (!string.IsNullOrEmpty(detail))
            {
                var small = new TextBlock { Text = detail, FontSize = 12, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
                small.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
                copy.Children.Add(small);
            }
            face.Child = new DockPanel { LastChildFill = true, Children = { _handle, _digits, copy } };
            Children.Add(_shadow);
            Children.Add(face);
        }

        private static (string Title, string? Detail) Copy(object item) => item is ReorderableListItem row ? (row.Title, row.Detail) : (item?.ToString() ?? string.Empty, null);

        private static Viewbox Icon()
        {
            var path = new Path { Data = Grip, Width = 24, Height = 24, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            path.SetBinding(Shape.StrokeProperty, new System.Windows.Data.Binding(nameof(Foreground)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(System.Windows.Controls.Button), 1) });
            return new Viewbox { Width = 16, Height = 16, Child = path };
        }

        private static Geometry Frozen(string data) { var geometry = Geometry.Parse(data); geometry.Freeze(); return geometry; }

        public bool Dragging { get => _dragging; set { _dragging = value; Raise(); } }

        // Position "01".."nn"; a new index rolls in from the direction the row travelled (.5em, subtle blur, .24 enter).
        public void SetPosition(int index, int count, bool animate)
        {
            var (title, _) = Copy(Item);
            AutomationProperties.SetName(_handle, $"Reorder {title}. Position {index + 1} of {count}. Use arrow keys.");
            if (index == _index) return;
            int direction = index > _index ? 1 : -1;
            bool first = _index < 0;
            _index = index;
            var old = _number;
            var text = (index + 1).ToString("00");
            bool motion = animate && !first && _owner.Level == ArcMotionLevel.Full;
            if (!motion && old is not null) { old.Text = text; return; }
            var fresh = new TextBlock { Text = text, FontSize = 12, RenderTransform = new TranslateTransform() };
            Typography.SetNumeralAlignment(fresh, FontNumeralAlignment.Tabular);
            fresh.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            _digits.Children.Add(fresh);
            _number = fresh;
            if (old is null) return;
            if (!motion) { _digits.Children.Remove(old); return; }
            TimePicker.Fly(fresh, 0, 1, .5 * 12 * direction, 0, 2, 0, .24, ArcEasing.Enter, null);
            TimePicker.Fly(old, old.Opacity, 0, 0, -.5 * 12 * direction, 0, 2, .12, ArcEasing.Standard, () => _digits.Children.Remove(old));
        }

        public void Glide(double from, bool animate)
        {
            if (Math.Abs(from) < .5) return;
            if (!animate) { _driver.Snap(_y, 0); return; }
            _y.Snap(from);
            _driver.Animate(_y, 0, SMOOTH);
        }

        public void Follow(double offset) { _y.Snap(offset); Apply(); }

        public void Drop(bool animate)
        {
            if (!animate) { _driver.Snap(_y, 0); return; }
            _driver.Animate(_y, 0, Settle);
        }

        // A keyboard move lifts the row for a moment and keeps focus on its handle.
        public void Lift()
        {
            _lifted = true;
            Raise();
            if (!_handle.IsKeyboardFocused) _handle.Focus();
            _liftTimer?.Stop();
            _liftTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(LIFT_HOLD) };
            _liftTimer.Tick += (_, _) => { _liftTimer!.Stop(); _lifted = false; Raise(); };
            _liftTimer.Start();
        }

        public void Finish() { _liftTimer?.Stop(); _driver.Finish(); }

        private void HandleDown(object sender, MouseButtonEventArgs e)
        {
            _pressed = true;
            Raise();
            _handle.Focus();
            _handle.CaptureMouse();
            _owner.DragStart(this, e);
            e.Handled = true;
        }

        private void Release()
        {
            if (!_pressed) return;
            _pressed = false;
            if (_handle.IsMouseCaptured) _handle.ReleaseMouseCapture();
            _owner.DragEnd(this);
            Raise();
        }

        private void HandleKey(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Up or Key.Down)) return;
            e.Handled = true;
            _owner.Move(this, e.Key == Key.Up ? -1 : 1, true);
        }

        // Raised = pressed, dragging or just moved: scale 1.02 on the snappy spring, the floating shadow fades in (.16) and out (.24).
        private void Raise()
        {
            bool raised = _pressed || _dragging || _lifted;
            _handle.Tag = raised ? "lifted" : null;
            Panel.SetZIndex(this, raised ? 1 : 0);
            var level = _owner.Level;
            double scale = raised && level == ArcMotionLevel.Full ? LIFT : 1;
            if (level == ArcMotionLevel.Full && IsLoaded) _driver.Animate(_s, scale, SNAPPY); else _driver.Snap(_s, scale);
            double opacity = raised ? 1 : 0;
            if (level == ArcMotionLevel.Full) _shadow.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromSeconds(raised ? .16 : .24)) { EasingFunction = ArcEasing.Standard });
            else { _shadow.BeginAnimation(OpacityProperty, null); _shadow.Opacity = opacity; }
        }

        private void Apply()
        {
            _move.Y = _y.Position;
            _scale.ScaleX = _scale.ScaleY = _s.Position;
        }
    }
}
