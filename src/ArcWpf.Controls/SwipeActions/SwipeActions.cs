using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class SwipeAction
{
    public string Label { get; set; } = string.Empty;
    /// <summary>A Lucide icon name, or any element.</summary>
    public object? Icon { get; set; }
    /// <summary>Fill of the revealed action. Pair Danger with a label that names the destructive result.</summary>
    public SwipeActionTone Tone { get; set; }
    /// <summary>The row springs home after the action instead of sliding away, for actions such as Mark as unread.</summary>
    public bool KeepRow { get; set; }
    /// <summary>Runs for a full swipe, a tap on the revealed action, or the menu. Call <see cref="SwipeActionsRow.Collapse"/> here unless KeepRow is set; a row left in place comes back.</summary>
    public Action? OnSelect { get; set; }
}

public enum SwipeActionTone { Neutral, Accent, Danger }

/// <summary>A list whose rows reveal actions on a horizontal swipe. Only one row stays open at a time, and every row has a menu with the same actions so the gesture is never required.</summary>
[TemplatePart(Name = "PART_Clip", Type = typeof(Border))]
public class SwipeActions : ItemsControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(SwipeActions), new FrameworkPropertyMetadata("Swipe actions", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    private Border? _clip;
    private SwipeActionsRow? _open;
    private Window? _window;
    private readonly OwnedMotion _motion = new();
    private readonly ThemedBrush _edge, _fill;
    private bool _empty;
    internal SwipeActionsRow? OpenRow => _open;
    internal Brush EdgeBrush => _edge.Brush;
    internal Brush FillBrush => _fill.Brush;

    static SwipeActions() => DefaultStyleKeyProperty.OverrideMetadata(typeof(SwipeActions), new FrameworkPropertyMetadata(typeof(SwipeActions)));

    public SwipeActions()
    {
        _edge = new ThemedBrush(this, _motion);
        _fill = new ThemedBrush(this, _motion);
        Loaded += (_, _) => { _window = Window.GetWindow(this); UpdateFrame(false); };
        Unloaded += (_, _) => { Unhook(); _window = null; };
        ThemeWatch.Add(this, () => UpdateFrame(false));
    }

    protected override DependencyObject GetContainerForItemOverride() => new SwipeActionsRow();
    protected override bool IsItemItsOwnContainerOverride(object item) => item is SwipeActionsRow;
    private IEnumerable<SwipeActionsRow> Rows => Items.OfType<SwipeActionsRow>();

    public override void OnApplyTemplate()
    {
        if (_clip is not null) { _clip.SizeChanged -= OnClipSize; }
        base.OnApplyTemplate();
        _clip = GetTemplateChild("PART_Clip") as Border;
        if (GetTemplateChild("PART_Frame") is Border frame) { frame.BorderBrush = _edge.Brush; frame.Background = _fill.Brush; }
        if (_clip is not null) { _clip.SizeChanged += OnClipSize; }
    }

    // The surface clips every row, so revealed actions never spill past its rounded corners.
    private void OnClipSize(object sender, SizeChangedEventArgs e) => _clip!.Clip = new RectangleGeometry(new Rect(e.NewSize), 25, 25);

    protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        UpdateFrame(IsLoaded);
    }

    /// <summary>When the last row is on its way out the frame fades with it, and it hides once the list is empty.</summary>
    internal void UpdateFrame(bool animate)
    {
        _empty = Rows.All(row => row.IsLeaving || row.IsCollapsedRow) && Items.Count > 0 || Items.Count == 0;
        _edge.To(_empty ? null : "Border", animate, .24);
        _fill.To(_empty ? null : "Surface", animate, .24);
        bool gone = Items.Count == 0 || Rows.All(row => row.IsCollapsedRow);
        if (GetTemplateChild("PART_Frame") is Border frame) { frame.Visibility = gone ? Visibility.Collapsed : Visibility.Visible; }
    }

    internal void SetOpen(SwipeActionsRow? row)
    {
        if (ReferenceEquals(_open, row)) { return; }
        var previous = _open;
        _open = row;
        if (row is null) { Unhook(); } else { Hook(); }
        previous?.CloseForOthers();
    }

    internal void ClearOpen(SwipeActionsRow row) { if (ReferenceEquals(_open, row)) { SetOpen(null); } }

    private bool _hooked;
    private void Hook()
    {
        if (_hooked || (_window ??= Window.GetWindow(this)) is null) { return; }
        _hooked = true;
        _window.PreviewMouseDown += OnWindowPress;
        _window.PreviewKeyDown += OnWindowKey;
    }

    private void Unhook()
    {
        if (!_hooked || _window is null) { return; }
        _hooked = false;
        _window.PreviewMouseDown -= OnWindowPress;
        _window.PreviewKeyDown -= OnWindowKey;
    }

    private void OnWindowPress(object sender, MouseButtonEventArgs e)
    {
        if (_open is null || (e.OriginalSource is DependencyObject source && _open.IsAncestorOf(source))) { return; }
        SetOpen(null);
    }

    private void OnWindowKey(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { SetOpen(null); } }

    /// <summary>Hands focus to the next staying row's menu button, or the previous, or the list.</summary>
    internal void FocusNeighbour(SwipeActionsRow row)
    {
        var rows = Rows.ToList();
        int index = rows.IndexOf(row);
        var next = rows.Skip(index + 1).FirstOrDefault(Stays) ?? rows.Take(index).LastOrDefault(Stays);
        if (next is not null) { next.FocusMore(); return; }
        Focusable = true;
        Focus();
    }

    private static bool Stays(SwipeActionsRow row) => !row.IsLeaving && !row.IsCollapsedRow;
}

/// <summary>One row. Leading actions sit under the left edge and trailing actions under the right; the outermost action on each side commits on a full swipe.</summary>
[TemplatePart(Name = "PART_Content", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Layers", Type = typeof(SwipeLayerHost))]
[TemplatePart(Name = "PART_Dock", Type = typeof(DockPanel))]
public class SwipeActionsRow : ContentControl
{
    /// <summary>Width of one action while a row rests open.</summary>
    internal const double ActionWidth = 76;
    private const double Slop = 8, Projection = .1, Flick = 900;

    public static readonly DependencyProperty RowLabelProperty = DependencyProperty.Register(nameof(RowLabel), typeof(string), typeof(SwipeActionsRow), new FrameworkPropertyMetadata("Row", (d, _) => ((SwipeActionsRow)d).UpdateMoreName()));
    public static readonly DependencyProperty LeadingProperty = DependencyProperty.Register(nameof(Leading), typeof(ObservableCollection<SwipeAction>), typeof(SwipeActionsRow), new FrameworkPropertyMetadata(null, (d, _) => ((SwipeActionsRow)d).RebuildLayers()));
    public static readonly DependencyProperty TrailingProperty = DependencyProperty.Register(nameof(Trailing), typeof(ObservableCollection<SwipeAction>), typeof(SwipeActionsRow), new FrameworkPropertyMetadata(null, (d, _) => ((SwipeActionsRow)d).RebuildLayers()));
    public static readonly DependencyProperty FullSwipeProperty = DependencyProperty.Register(nameof(FullSwipe), typeof(bool), typeof(SwipeActionsRow), new FrameworkPropertyMetadata(true));
    public string RowLabel { get => (string)GetValue(RowLabelProperty); set => SetValue(RowLabelProperty, value); }
    public ObservableCollection<SwipeAction> Leading { get => (ObservableCollection<SwipeAction>)GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }
    public ObservableCollection<SwipeAction> Trailing { get => (ObservableCollection<SwipeAction>)GetValue(TrailingProperty); set => SetValue(TrailingProperty, value); }
    public bool FullSwipe { get => (bool)GetValue(FullSwipeProperty); set => SetValue(FullSwipeProperty, value); }

    private enum Side { None, Leading, Trailing }
    private sealed class DragState { public double StartX, StartY, Origin; public bool? Horizontal; public readonly List<(long Time, double X)> Samples = []; }

    private readonly SpringDriver _driver, _heightDriver;
    private readonly ControlSpring _x, _cover, _height;
    private readonly TranslateTransform _contentMove = new();
    private readonly OwnedMotion _motion = new();
    private readonly List<SwipeActionLayer> _layers = [];
    private readonly DispatcherTimer _restore = new() { Interval = TimeSpan.FromMilliseconds(1400) };
    private FrameworkElement? _content;
    private SwipeLayerHost? _host;
    private ArcChromeButton? _more;
    private SwipeRowMenu? _menu;
    private DragState? _drag;
    private (Side Side, int Index)? _covering;
    private bool _armed, _swallow, _focusNeighbour, _homing;

    static SwipeActionsRow() => DefaultStyleKeyProperty.OverrideMetadata(typeof(SwipeActionsRow), new FrameworkPropertyMetadata(typeof(SwipeActionsRow)));

    public SwipeActionsRow()
    {
        _driver = new SpringDriver(Apply);
        _x = _driver.Add(0, ArcSpring.Snappy);
        _cover = _driver.Add(0, ArcSpring.Morph);
        _driver.Settled = () => { if (_homing && _x.Target == 0) { _homing = false; _cover.Snap(0); Apply(); } };
        _heightDriver = new SpringDriver(InvalidateMeasure);
        _height = _heightDriver.Add(1, ArcSpring.Smooth);
        ClipToBounds = true;
        Focusable = false;
        Leading = [];
        Trailing = [];
        Leading.CollectionChanged += (_, _) => RebuildLayers();
        Trailing.CollectionChanged += (_, _) => RebuildLayers();
        _restore.Tick += (_, _) => { _restore.Stop(); if (IsLeaving) { IsLeaving = false; Group?.UpdateFrame(true); Settle(0); } };
        Unloaded += (_, _) => { _restore.Stop(); _driver.Finish(); _heightDriver.Finish(); Group?.ClearOpen(this); };
    }

    private SwipeActions? Group => ItemsControl.ItemsControlFromItemContainer(this) as SwipeActions;
    private bool Moving => IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
    private double RowWidth => ActualWidth > 0 ? ActualWidth : double.IsNaN(Width) ? 320 : Width;
    /// <summary>The row has been swiped away or collapsed, and is not counted among the list's live rows.</summary>
    public bool IsLeavingOrGone => IsLeaving || IsCollapsedRow;
    internal bool IsLeaving { get; private set; }
    internal bool IsCollapsedRow { get; private set; }
    internal bool IsArmed => _armed;
    internal double CoverProgress => _cover.Position;
    internal IReadOnlyList<SwipeActionLayer> Layers => _layers;
    public double OffsetForTest => _x.Position;
    internal void AdvanceMotion(double seconds) => _driver.Advance(seconds);

    private IList<SwipeAction> ActionsOf(Side side) => side == Side.Leading ? Leading : Trailing;
    private double OpenWidth(Side side) => ActionsOf(side).Count * ActionWidth;
    private int Outermost(Side side) => side == Side.Leading ? 0 : Trailing.Count - 1;
    private bool CanCommit(Side side) => FullSwipe && ActionsOf(side).Count > 0;
    private double Threshold(Side side) => Math.Max(OpenWidth(side) + 48, RowWidth * .56);
    private static Side SideOf(double value) => value > 0 ? Side.Leading : value < 0 ? Side.Trailing : Side.None;
    /// <summary>Past its last stop the row still follows, but every pixel costs more.</summary>
    private static double RubberBand(double overshoot, double dimension) => (1 - 1 / (overshoot * .55 / dimension + 1)) * dimension;

    public override void OnApplyTemplate()
    {
        if (_content is not null)
        {
            _content.PreviewMouseLeftButtonDown -= OnPress;
            _content.PreviewMouseMove -= OnMove;
            _content.PreviewMouseLeftButtonUp -= OnRelease;
            _content.LostMouseCapture -= OnLostCapture;
        }
        base.OnApplyTemplate();
        _content = GetTemplateChild("PART_Content") as FrameworkElement;
        _host = GetTemplateChild("PART_Layers") as SwipeLayerHost;
        if (_content is not null)
        {
            _content.RenderTransform = _contentMove;
            _content.PreviewMouseLeftButtonDown += OnPress;
            _content.PreviewMouseMove += OnMove;
            _content.PreviewMouseLeftButtonUp += OnRelease;
            _content.LostMouseCapture += OnLostCapture;
        }
        if (GetTemplateChild("PART_Dock") is DockPanel dock)
        {
            _more = new ArcChromeButton(12) { Width = 32, Height = 32, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _more.Roles = b => _drag?.Horizontal == true ? (null, null, "TextMuted") : b.IsMouseOver || b.IsPressed || _menu is not null ? ("SurfaceMuted", null, "Foreground") : (null, null, "TextMuted");
            _more.Content = ArcText.Icon("ellipsis", 18, _more.Ink);
            _more.Click += (_, _) => ToggleMenu();
            DockPanel.SetDock(_more, Dock.Right);
            dock.Children.Insert(0, _more);
            UpdateMoreName();
        }
        RebuildLayers();
    }

    private void UpdateMoreName() { if (_more is not null) { AutomationProperties.SetName(_more, $"More actions for {RowLabel}"); } }
    internal void FocusMore() => _more?.Focus();

    private void RebuildLayers()
    {
        _layers.Clear();
        _host?.Children.Clear();
        if (_more is not null) { _more.Visibility = (Leading?.Count ?? 0) + (Trailing?.Count ?? 0) > 0 ? Visibility.Visible : Visibility.Collapsed; }
        if (_host is null || Leading is null || Trailing is null) { return; }
        foreach (var side in new[] { Side.Leading, Side.Trailing })
        {
            var actions = ActionsOf(side);
            for (int i = 0; i < actions.Count; i++)
            {
                int index = i;
                var layer = new SwipeActionLayer(actions[i], side == Side.Leading, side == Side.Leading ? actions.Count - 1 - i : i, actions.Count);
                layer.Click += (_, _) => Commit(side, index);
                Panel.SetZIndex(layer, layer.Rank + 1);
                _layers.Add(layer);
                _host.Children.Add(layer);
            }
        }
        Apply();
    }

    private void Apply()
    {
        double x = _x.Position, cover = _cover.Position;
        _contentMove.X = x;
        foreach (var layer in _layers)
        {
            var side = layer.IsLeading ? Side.Leading : Side.Trailing;
            int? rank = _covering is { } covering && covering.Side == side ? (side == Side.Leading ? ActionsOf(side).Count - 1 - covering.Index : covering.Index) : null;
            layer.Update(x, cover, rank);
        }
    }

    // ---- Motion ----
    private void StretchTo(double value)
    {
        if (Moving) { _driver.Animate(_cover, value, ArcSpring.Morph); return; }
        _cover.Snap(value);
        Apply();
    }

    /// <summary>Springs the row to a stop, carrying the release velocity.</summary>
    private void Settle(double target, double velocity = 0)
    {
        if (_armed || (target != 0 && _cover.Position != 0)) { StretchTo(0); }
        _armed = false;
        double from = _x.Position;
        if (!Moving)
        {
            _driver.Snap(_x, target);
            if (target == 0) { _cover.Snap(0); _driver.Stop(); Apply(); }
        }
        else
        {
            // Heading home, the spring may not carry the row past zero, or the opposite actions would flash.
            bool toward = target == 0 && Math.Sign(velocity) == -Math.Sign(from);
            _x.Launch(toward ? Math.Sign(velocity) * Math.Min(Math.Abs(velocity), Math.Abs(from) * 12) : velocity);
            _homing = target == 0;
            _driver.Animate(_x, target, target == 0 ? ArcSpring.Smooth : ArcSpring.Snappy);
        }
        if (target == 0) { Group?.ClearOpen(this); } else { Group?.SetOpen(this); }
    }

    /// <summary>The chosen action stretches over the whole row while the row leaves in its direction; the list then closes the gap.</summary>
    private void Commit(Side side, int index, double velocity = 0)
    {
        var actions = ActionsOf(side);
        if (index < 0 || index >= actions.Count || IsLeaving) { return; }
        var action = actions[index];
        int direction = side == Side.Leading ? 1 : -1;
        _armed = false;
        _covering = (side, index);
        StretchTo(1);
        if (action.KeepRow) { action.OnSelect?.Invoke(); Settle(0, velocity); return; }
        IsLeaving = true;
        Group?.UpdateFrame(true);
        double end = direction * (RowWidth + 2);
        if (!Moving) { _driver.Snap(_x, end); }
        else
        {
            _homing = false;
            _x.Launch(Math.Sign(velocity) == direction ? velocity : 0);
            _driver.Animate(_x, end, ArcSpring.Smooth);
        }
        Group?.ClearOpen(this);
        action.OnSelect?.Invoke();
        // A list that keeps the item gets its row back instead of an empty band of colour.
        if (IsLeaving && !IsCollapsedRow && !_collapsing) { _restore.Stop(); _restore.Start(); }
    }

    private void Arm(double value)
    {
        var side = SideOf(value);
        double limit = side == Side.None ? double.PositiveInfinity : Threshold(side);
        bool next = side != Side.None && CanCommit(side) && Math.Abs(value) > (_armed ? limit - 20 : limit);
        if (next == _armed || side == Side.None)
        {
            if (side == Side.None && _armed) { _armed = false; StretchTo(0); }
            return;
        }
        _armed = next;
        if (next) { _covering = (side, Outermost(side)); }
        StretchTo(next ? 1 : 0);
    }

    private double Constrain(double raw)
    {
        var side = SideOf(raw);
        if (side == Side.None) { return 0; }
        double dimension = RowWidth, distance = Math.Abs(raw);
        double limit = ActionsOf(side).Count == 0 ? 0 : CanCommit(side) ? dimension : OpenWidth(side);
        return Math.Sign(raw) * (distance <= limit ? distance : limit + RubberBand(distance - limit, dimension));
    }

    private void Release(double velocity)
    {
        double value = _x.Position;
        var side = SideOf(value);
        if (side == Side.None || ActionsOf(side).Count == 0) { Settle(0, velocity); return; }
        int direction = side == Side.Leading ? 1 : -1;
        double distance = Math.Abs(value), outward = velocity * direction, open = OpenWidth(side), projected = distance + outward * Projection;
        bool flung = distance > open && outward > Flick && projected > Threshold(side);
        // A flick back toward home cancels an armed full swipe.
        if (CanCommit(side) && ((_armed && outward > -Flick) || flung)) { Commit(side, Outermost(side), velocity); return; }
        Settle(projected > open / 2 ? direction * open : 0, velocity);
    }

    internal void CloseForOthers()
    {
        if (!IsLeaving && _drag?.Horizontal != true && _x.Position != 0) { Settle(0); }
    }

    public void Close()
    {
        _drag = null;
        Settle(0);
    }

    /// <summary>Applies a release at this offset, as a swipe ending there at rest would.</summary>
    public void CommitForTest(double delta)
    {
        double value = Constrain(delta);
        _driver.Snap(_x, value);
        Arm(value);
        Release(0);
    }

    internal void SwipeForTest(double offset, double velocity)
    {
        double value = Constrain(offset);
        _driver.Snap(_x, value);
        Arm(value);
        Release(velocity);
    }

    // ---- Presence ----
    private bool _collapsing;

    /// <summary>Closes the gap after the row has left. Height springs after a short delay, then the row hides. Call from the action's OnSelect.</summary>
    public void Collapse()
    {
        if (IsCollapsedRow || _collapsing) { return; }
        _restore.Stop();
        IsLeaving = true;
        _collapsing = true;
        Group?.UpdateFrame(true);
        if (_focusNeighbour) { _focusNeighbour = false; Group?.FocusNeighbour(this); }
        void Done() { if (!_collapsing) { return; } _collapsing = false; IsCollapsedRow = true; Visibility = Visibility.Collapsed; Group?.UpdateFrame(true); }
        if (!IsLoaded) { Done(); return; }
        if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full) { _motion.Double(this, OpacityProperty, 0, .15, true, completed: Done); return; }
        _heightDriver.Settled = null;
        var delay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(.12) };
        delay.Tick += (_, _) => { delay.Stop(); if (_collapsing) { _heightDriver.Animate(_height, 0, ArcSpring.Smooth); } };
        delay.Start();
        _motion.Double(this, OpacityProperty, 0, .12, true, completed: Done, delay: .34);
    }

    /// <summary>Brings a collapsed or leaving row back: the height reopens as the row slides home.</summary>
    public void Restore()
    {
        _restore.Stop();
        _collapsing = false;
        bool wasHidden = IsCollapsedRow;
        IsCollapsedRow = false;
        IsLeaving = false;
        Visibility = Visibility.Visible;
        if (!IsLoaded || ArcMotion.GetLevel(this) != ArcMotionLevel.Full)
        {
            _height.Snap(1);
            _x.Snap(0); _cover.Snap(0);
            Apply();
            _motion.Double(this, OpacityProperty, 1, .15, IsLoaded);
            InvalidateMeasure();
        }
        else
        {
            if (wasHidden) { _height.Snap(0); _x.Snap(0); _cover.Snap(0); Apply(); Opacity = 0; }
            _heightDriver.Animate(_height, 1, ArcSpring.Smooth);
            _motion.Double(this, OpacityProperty, 1, .24, true, enter: true);
            if (!wasHidden) { Settle(0); }
        }
        Group?.UpdateFrame(true);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var size = base.MeasureOverride(new Size(constraint.Width, double.PositiveInfinity));
        _natural = size.Height;
        return new Size(size.Width, size.Height * Math.Clamp(_height.Position, 0, 1));
    }

    // The content keeps its natural height while the row closes over it.
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        if (VisualChildrenCount > 0 && GetVisualChild(0) is UIElement child) { child.Arrange(new Rect(0, 0, arrangeBounds.Width, Math.Max(_natural, arrangeBounds.Height))); }
        return arrangeBounds;
    }

    private double _natural;

    // ---- Pointer ----
    private void OnPress(object sender, MouseButtonEventArgs e)
    {
        _swallow = false;
        if (IsLeaving) { return; }
        // Catch the row mid-flight: the next move retargets from wherever it is now.
        _driver.Stop();
        var point = e.GetPosition(this);
        _drag = new DragState { StartX = point.X, StartY = point.Y, Origin = _x.Position };
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        var drag = _drag;
        if (drag is null || drag.Horizontal == false) { return; }
        if (e.LeftButton != MouseButtonState.Pressed) { _drag = null; return; }
        var point = e.GetPosition(this);
        if (drag.Horizontal is null)
        {
            double dx = point.X - drag.StartX, dy = point.Y - drag.StartY;
            if (Math.Sqrt(dx * dx + dy * dy) < Slop) { return; }
            if (Math.Abs(dy) > Math.Abs(dx)) { drag.Horizontal = false; return; }
            // Measure from here, so crossing the slop never jumps the row.
            drag.Horizontal = true;
            drag.StartX = point.X;
            _content?.CaptureMouse();
            if (_content is not null) { _content.Cursor = Cursors.SizeWE; }
            _more?.Refresh(true);
            Group?.SetOpen(this);
        }
        double value = Constrain(drag.Origin + point.X - drag.StartX);
        _x.Snap(value);
        Apply();
        drag.Samples.Add((Environment.TickCount64, point.X));
        if (drag.Samples.Count > 12) { drag.Samples.RemoveAt(0); }
        Arm(value);
    }

    private void OnRelease(object sender, MouseButtonEventArgs e)
    {
        var drag = _drag;
        if (drag is null) { return; }
        _drag = null;
        if (drag.Horizontal == true)
        {
            drag.Samples.Add((Environment.TickCount64, e.GetPosition(this).X));
            FinishDrag();
            Release(VelocityOf(drag.Samples));
            e.Handled = true;
            return;
        }
        // A tap on an open row closes it instead of acting on what is underneath.
        if (drag.Horizontal is null && Math.Abs(_x.Position) > .5)
        {
            Mouse.Capture(null);
            Settle(0);
            e.Handled = true;
        }
        else if (_x.Target != _x.Position) { Settle(_x.Target); }
    }

    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (_drag?.Horizontal != true) { return; }
        _drag = null;
        FinishDrag();
        Release(0);
    }

    private void FinishDrag()
    {
        if (_content is not null)
        {
            _content.Cursor = Cursors.Hand;
            if (_content.IsMouseCaptured) { _content.ReleaseMouseCapture(); }
        }
        _more?.Refresh(true);
    }

    internal static double VelocityOf(List<(long Time, double X)> samples)
    {
        if (samples.Count == 0) { return 0; }
        var last = samples[^1];
        var first = samples.Find(sample => last.Time - sample.Time <= 80);
        double elapsed = (last.Time - first.Time) / 1000d;
        return elapsed > 0 ? (last.X - first.X) / elapsed : 0;
    }

    // ---- Menu ----
    private void ToggleMenu()
    {
        if (_menu is not null) { CloseMenu(); return; }
        if (_more is null) { return; }
        var items = Leading.Select((action, index) => (action, Side.Leading, index)).Concat(Trailing.Select((action, index) => (action, Side.Trailing, index))).ToList();
        _menu = new SwipeRowMenu(_more, items.Select(item => item.action).ToList(), ArcMotion.GetLevel(this) != ArcMotionLevel.Full);
        _menu.Chosen += order =>
        {
            var (action, side, index) = items[order];
            _focusNeighbour = !action.KeepRow;
            CloseMenu();
            Commit(side, index);
        };
        _menu.Dismissed += focus => { CloseMenu(); if (focus) { _more.Focus(); } };
        _menu.Open();
        _more.Refresh(true);
    }

    internal bool IsMenuOpen => _menu is not null;
    internal SwipeRowMenu? Menu => _menu;
    internal void OpenMenuForTest() => ToggleMenu();

    private void CloseMenu()
    {
        _menu?.Close();
        _menu = null;
        _more?.Refresh(true);
    }
}

/// <summary>Arranges every layer a full row-width beyond the edge it hides behind; each then slides in by transform.</summary>
public sealed class SwipeLayerHost : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren) { child.Measure(new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height)); }
        return new Size();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (SwipeActionLayer layer in InternalChildren) { layer.Arrange(new Rect(layer.IsLeading ? -finalSize.Width : finalSize.Width, 0, finalSize.Width, finalSize.Height)); }
        return finalSize;
    }
}

/// <summary>One revealed action. Rank 0 sits against the content and the highest rank at the outer edge. The actions share the revealed space evenly, and the covering action can stretch across the row.</summary>
internal sealed class SwipeActionLayer : ArcChromeButton
{
    private const double Action = SwipeActionsRow.ActionWidth;
    private readonly int _count;
    private readonly TranslateTransform _shift = new(), _glyphMove = new();
    private readonly ScaleTransform _iconScale = new(.6, .6, 10, 10);
    private readonly FrameworkElement _icon;
    private readonly TextBlock _label;
    public bool IsLeading { get; }
    public int Rank { get; }
    internal double Shift => _shift.X;
    internal double IconReveal => _icon.Opacity;
    internal double LabelReveal => _label.Opacity;

    public SwipeActionLayer(SwipeAction action, bool leading, int rank, int count) : base(0)
    {
        IsLeading = leading;
        Rank = rank;
        _count = count;
        IsTabStop = false;
        Focusable = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
                Roles = _ => action.Tone switch { SwipeActionTone.Accent => ("Accent", null, "AccentForeground"), SwipeActionTone.Danger => ("DataGridDanger", null, "Background"), _ => ("SwipeNeutral", null, "Foreground") };
        RenderTransform = _shift;
        _icon = action.Icon is UIElement custom ? new ContentPresenter { Content = custom, Width = 20, Height = 20 } : ArcText.Icon(action.Icon as string ?? "ellipsis", 20, Ink);
        _icon.RenderTransform = _iconScale;
        _icon.Opacity = 0;
        _label = new TextBlock { Text = action.Label, Foreground = Ink, FontSize = 12, FontWeight = FontWeights.Medium, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(0, 3, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0 };
        var glyph = new StackPanel { Width = Action, VerticalAlignment = VerticalAlignment.Center, RenderTransform = _glyphMove, HorizontalAlignment = leading ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = leading ? new Thickness(0, 0, -Action / 2, 0) : new Thickness(-Action / 2, 0, 0, 0) };
        glyph.Children.Add(_icon);
        glyph.Children.Add(_label);
        Content = new Grid { Children = { glyph } };
    }

    // The menu button is the accessible path, so the revealed copy stays out of the automation tree.
    protected override System.Windows.Automation.Peers.AutomationPeer? OnCreateAutomationPeer() => null;

    private static double Clamp01(double value) => Math.Min(1, Math.Max(0, value));

    public void Update(double x, double cover, int? coverRank)
    {
        int direction = IsLeading ? 1 : -1;
        double revealed = Math.Max(0, x * direction), share = revealed / _count, inset = (_count - Rank) * revealed / _count;
        if (coverRank == Rank) { inset += cover * (revealed - inset); }
        else if (coverRank is not null && Rank > coverRank) { inset *= 1 - cover; }
        _shift.X = direction * inset;
        // The glyph rides the center of its share, then hugs the content edge while this action covers the row.
        _glyphMove.X = -direction * (coverRank == Rank ? share / 2 + cover * (Action / 2 - share / 2) : share / 2);
        double icon = Clamp01((share - Action * .25) / (Action * .55)), label = Clamp01((share - Action * .72) / (Action * .24));
        if (coverRank == Rank) { icon = Math.Max(icon, cover); label = Math.Max(label, cover); }
        else if (coverRank is not null) { icon *= 1 - cover; label *= 1 - cover; }
        _icon.Opacity = icon;
        _iconScale.ScaleX = _iconScale.ScaleY = .6 + .4 * icon;
        _label.Opacity = label;
    }
}

/// <summary>The row's More actions menu. It grows from its trigger and loops on the arrow keys.</summary>
internal sealed class SwipeRowMenu
{
    private const double Bleed = 28;
    private readonly FrameworkElement _anchor;
    private readonly Popup _popup;
    private readonly Grid _root = new() { Margin = new Thickness(Bleed) };
    private readonly List<ArcChromeButton> _items = [];
    private readonly OwnedMotion _motion = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _move = new();
    private readonly bool _reduced;
    private Window? _window;
    public event Action<int>? Chosen;
    public event Action<bool>? Dismissed;
    internal FrameworkElement Root => _root;
    internal int ItemCount => _items.Count;

    public SwipeRowMenu(FrameworkElement anchor, IReadOnlyList<SwipeAction> actions, bool reduced)
    {
        _anchor = anchor;
        _reduced = reduced;
        var list = new StackPanel();
        for (int i = 0; i < actions.Count; i++) { list.Children.Add(BuildItem(actions[i], i)); }
        var transforms = new TransformGroup();
        transforms.Children.Add(_scale);
        transforms.Children.Add(_move);
        var tray = new Border { MinWidth = 176, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(26), Padding = new Thickness(5), Child = list };
        tray.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        tray.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        var shadow = new Border { CornerRadius = new CornerRadius(26) };
        shadow.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        shadow.SetResourceReference(UIElement.EffectProperty, "ArcWpf.Shadow.Floating");
        _root.Children.Add(shadow);
        _root.Children.Add(tray);
        _root.RenderTransform = transforms;
        _root.RenderTransformOrigin = new Point(1, 0);
        _root.PreviewKeyDown += OnKey;
        _popup = new Popup { AllowsTransparency = true, Child = _root, PlacementTarget = anchor, Placement = PlacementMode.Custom, StaysOpen = true, CustomPopupPlacementCallback = (popup, target, _) => [new CustomPopupPlacement(new Point(target.Width - popup.Width + Bleed, target.Height + 6 - Bleed), PopupPrimaryAxis.Vertical)] };
    }

    private ArcChromeButton BuildItem(SwipeAction action, int order)
    {
        bool danger = action.Tone == SwipeActionTone.Danger;
        var item = new ArcChromeButton(20) { Height = 36, Padding = new Thickness(11, 0, 11, 0), HorizontalContentAlignment = HorizontalAlignment.Left, HorizontalAlignment = HorizontalAlignment.Stretch, FocusVisualStyle = null };
        item.Roles = b => (b.IsMouseOver || b.IsKeyboardFocused ? danger ? "SwipeMenuDangerHover" : "SurfaceMuted" : null, null, danger ? "DataGridDanger" : "Foreground");
        item.GotKeyboardFocus += (_, _) => item.Refresh(true);
        item.LostKeyboardFocus += (_, _) => item.Refresh(true);
        var icon = new ThemedBrush(item, new OwnedMotion());
        icon.To(danger ? "DataGridDanger" : "TextSecondary", false);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        FrameworkElement glyph = action.Icon is UIElement custom ? new ContentPresenter { Content = custom, Width = 16, Height = 16 } : ArcText.Icon(action.Icon as string ?? "ellipsis", 16, icon.Brush);
        glyph.Margin = new Thickness(0, 0, 11, 0);
        var label = new TextBlock { Text = action.Label, Foreground = item.Ink, FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(glyph);
        row.Children.Add(label);
        item.Content = row;
        AutomationProperties.SetName(item, action.Label);
        item.Click += (_, _) => Chosen?.Invoke(order);
        item.MouseEnter += (_, _) => item.Focus();
        _items.Add(item);
        return item;
    }

    public void Open()
    {
        _popup.IsOpen = true;
        _window = Window.GetWindow(_anchor);
        if (_window is not null) { _window.PreviewMouseDown += OnWindowPress; _window.Deactivated += OnDeactivated; }
        if (_reduced) { _root.Opacity = 0; _motion.Double(_root, UIElement.OpacityProperty, 1, .12, true); }
        else
        {
            _root.Opacity = 0;
            _scale.ScaleX = _scale.ScaleY = .97;
            _move.Y = -5;
            _motion.Double(_root, UIElement.OpacityProperty, 1, .16, true, enter: true);
            _motion.Double(_scale, ScaleTransform.ScaleXProperty, 1, .36, true, spring: true);
            _motion.Double(_scale, ScaleTransform.ScaleYProperty, 1, .36, true, spring: true);
            _motion.Double(_move, TranslateTransform.YProperty, 0, .36, true, spring: true);
            for (int i = 0; i < _items.Count; i++)
            {
                double delay = Math.Min(i, 4) * .035;
                var shift = new TranslateTransform(0, -2);
                _items[i].RenderTransform = shift;
                _items[i].Opacity = 0;
                _motion.Double(_items[i], UIElement.OpacityProperty, 1, .24, true, delay: delay, enter: true);
                _motion.Double(shift, TranslateTransform.YProperty, 0, .24, true, delay: delay, enter: true);
            }
        }
        _anchor.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { if (_items.Count > 0 && Keyboard.FocusedElement == _anchor && InputManager.Current.MostRecentInputDevice is KeyboardDevice) { _items[0].Focus(); } });
    }

    public void Close()
    {
        if (_window is not null) { _window.PreviewMouseDown -= OnWindowPress; _window.Deactivated -= OnDeactivated; }
        _root.IsHitTestVisible = false;
        if (_reduced) { _popup.IsOpen = false; return; }
        _motion.Double(_root, UIElement.OpacityProperty, 0, .12, true, completed: () => _popup.IsOpen = false);
        _motion.Double(_scale, ScaleTransform.ScaleXProperty, .985, .12, true);
        _motion.Double(_scale, ScaleTransform.ScaleYProperty, .985, .12, true);
    }

    private void OnWindowPress(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && (ReferenceEquals(source, _anchor) || _anchor.IsAncestorOf(source))) { return; }
        Dismissed?.Invoke(false);
    }

    private void OnDeactivated(object? sender, EventArgs e) { if (!_root.IsKeyboardFocusWithin) { Dismissed?.Invoke(false); } }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; Dismissed?.Invoke(true); return; }
        int count = _items.Count, at = _items.FindIndex(item => item.IsKeyboardFocused);
        int next = e.Key switch { Key.Down => (at + 1) % count, Key.Up => (at - 1 + count) % count, Key.Home => 0, Key.End => count - 1, _ => -1 };
        if (next < 0) { return; }
        e.Handled = true;
        _items[next].Focus();
    }
}
