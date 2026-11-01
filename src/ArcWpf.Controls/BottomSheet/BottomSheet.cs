using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public sealed class BottomSheetDetentChangedEventArgs(int index) : EventArgs
{
    public int Index { get; } = index;
}

public sealed class BottomSheet : Drawer
{
    public static readonly DependencyProperty SnapHeightProperty = DependencyProperty.Register(nameof(SnapHeight), typeof(double), typeof(BottomSheet), new FrameworkPropertyMetadata(double.NaN, GeometryChanged), value => value is double height && (double.IsNaN(height) || double.IsFinite(height) && height >= 0));
    public static readonly DependencyProperty InitialDetentProperty = DependencyProperty.Register(nameof(InitialDetent), typeof(int), typeof(BottomSheet), new FrameworkPropertyMetadata(0), value => value is int index && index >= 0);
    public static readonly DependencyProperty CurrentDetentProperty = DependencyProperty.Register(nameof(CurrentDetent), typeof(int), typeof(BottomSheet), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, DetentChanged), value => value is int index && index >= 0);
    private static readonly DependencyPropertyKey IsExpandedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsExpanded), typeof(bool), typeof(BottomSheet), new PropertyMetadata(false));
    public static readonly DependencyProperty IsExpandedProperty = IsExpandedPropertyKey.DependencyProperty;

    public double SnapHeight { get => (double)GetValue(SnapHeightProperty); set => SetValue(SnapHeightProperty, value); }
    public int InitialDetent { get => (int)GetValue(InitialDetentProperty); set => SetValue(InitialDetentProperty, value); }
    public int CurrentDetent { get => (int)GetValue(CurrentDetentProperty); set => SetValue(CurrentDetentProperty, value); }
    public bool IsExpanded => (bool)GetValue(IsExpandedProperty);
    public ObservableCollection<double> Detents { get; } = [.45, .92];
    public event EventHandler<BottomSheetDetentChangedEventArgs>? CurrentDetentChanged;
    internal bool IsSheetMotionRunning => _driver?.IsRunning == true;

    private FrameworkElement? _surface;
    private Border? _overlay;
    private FrameworkElement? _header;
    private System.Windows.Controls.Button? _grabber;
    private ScrollViewer? _body;
    private TranslateTransform? _translate;
    private SpringDriver? _driver;
    private ControlSpring? _position;
    private readonly OwnedMotion _fade = new();
    private readonly DragProjection _dragProjection = new(80, 60, .2);
    private bool _shown, _settingDetent, _dragActive, _dragMoved, _suppressClick, _bodyTouch;
    private double _height, _dragStart, _dragOrigin, _lastPointer;
    private int _dragFrom;
    private long _dragEndedAt;

    static BottomSheet() => DefaultStyleKeyProperty.OverrideMetadata(typeof(BottomSheet), new FrameworkPropertyMetadata(typeof(BottomSheet)));
    public BottomSheet()
    {
        SetCurrentValue(SideProperty, DrawerSide.Bottom);
        Detents.CollectionChanged += DetentsChanged;
        SizeChanged += (_, _) => UpdateGeometry();
        Closed += (_, _) => { _shown = false; _driver?.Stop(); _fade.Stop(); };
        Unloaded += (_, _) => { _driver?.Stop(); _fade.Stop(); _dragActive = false; };
    }

    private static void GeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((BottomSheet)d).UpdateGeometry();
    private static void DetentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var sheet = (BottomSheet)d;
        if (sheet._settingDetent) return;
        int requested = (int)e.NewValue;
        sheet.ChangeDetent(requested);
        if (requested == sheet.CurrentDetent && !Equals(e.OldValue, sheet.CurrentDetent))
            sheet.CurrentDetentChanged?.Invoke(sheet, new BottomSheetDetentChangedEventArgs(sheet.CurrentDetent));
    }
    private void DetentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateGeometry();
    private double[] Stops => double.IsFinite(SnapHeight)
        ? [1]
        : Detents.Where(value => double.IsFinite(value) && value > 0).Select(value => Math.Min(value, 1)).Order().Distinct().DefaultIfEmpty(.92).ToArray();
    private int LastDetent => Stops.Length - 1;
    private int BoundDetent(int index) => Math.Clamp(index, 0, LastDetent);
    private double ClosedOffset => _height + 40;
    private double OffsetFor(int index)
    {
        var stops = Stops;
        return _height * (1 - stops[BoundDetent(index)] / stops[^1]);
    }

    public void MoveToDetent(int index) => ChangeDetent(index);
    private void ChangeDetent(int index)
    {
        index = BoundDetent(index);
        if (CurrentDetent != index)
        {
            _settingDetent = true;
            try { SetCurrentValue(CurrentDetentProperty, index); }
            finally { _settingDetent = false; }
            CurrentDetentChanged?.Invoke(this, new BottomSheetDetentChangedEventArgs(index));
        }
        SetValue(IsExpandedPropertyKey, index == LastDetent);
        if (_body is not null) _body.VerticalScrollBarVisibility = IsExpanded ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        if (_grabber is not null)
        {
            AutomationProperties.SetName(_grabber, IsExpanded ? "Collapse sheet" : "Expand sheet");
            AutomationProperties.SetHelpText(_grabber, $"{Math.Round(Stops[index] * 100)} percent height");
        }
        if (IsOpen && _shown && _position is not null)
        {
            if (ArcMotion.GetLevel(this) == ArcMotionLevel.Full) _driver?.Animate(_position, OffsetFor(index), .42);
            else { _driver?.Stop(); _driver?.Snap(_position, OffsetFor(index)); }
        }
        if (!IsExpanded && _body is { VerticalOffset: > 0 }) _body.ScrollToTop();
    }

    private void UpdateGeometry()
    {
        if (_surface is null || ActualHeight <= 0) return;
        _height = double.IsFinite(SnapHeight) ? Math.Min(SnapHeight, ActualHeight) : ActualHeight * Stops[^1];
        _surface.Height = _height;
        _surface.MaxHeight = ActualHeight;
        ChangeDetent(CurrentDetent);
    }

    protected override bool TryCustomTransition(bool open, bool animate, int token)
    {
        if (_surface is null || _position is null || _driver is null) return false;
        UpdateGeometry();
        if (open && !_shown)
        {
            _shown = true;
            int initial = ReadLocalValue(CurrentDetentProperty) == DependencyProperty.UnsetValue ? InitialDetent : CurrentDetent;
            _position.Snap(ClosedOffset);
            ApplyPosition();
            ChangeDetent(initial);
        }
        _dragActive = false;
        _fade.Stop();
        var level = ArcMotion.GetLevel(this);
        double target = open ? OffsetFor(CurrentDetent) : ClosedOffset;
        if (animate && level == ArcMotionLevel.Full)
        {
            _surface.Opacity = 1;
            _driver.Settled = open ? null : () => FinishCustomClose(token);
            _driver.Animate(_position, target, open ? .42 : .3);
        }
        else
        {
            _driver.Settled = null;
            _driver.Stop();
            _driver.Snap(_position, target);
            bool fade = animate && level == ArcMotionLevel.Reduced;
            _fade.Double(_surface, OpacityProperty, open ? 1 : 0, .12, fade, enter: open,
                completed: open ? null : () => FinishCustomClose(token));
        }
        return true;
    }

    private void ApplyPosition()
    {
        if (_position is null) return;
        double y = _position.Position;
        if (_translate is not null) _translate.Y = y;
        if (_overlay is null || _height <= 0) return;
        double low = OffsetFor(0);
        double dim = y <= low ? 1 - .22 * (low > 0 ? y / low : 0) : .78 * (1 - (y - low) / (ClosedOffset - low));
        _overlay.Opacity = Math.Clamp(dim, 0, 1);
    }

    public override void OnApplyTemplate()
    {
        ReleaseSheetParts();
        base.OnApplyTemplate();
        _surface = GetTemplateChild("PART_Surface") as FrameworkElement;
        _overlay = GetTemplateChild("PART_Overlay") as Border;
        _header = GetTemplateChild("PART_Header") as FrameworkElement;
        _grabber = GetTemplateChild("PART_Grabber") as System.Windows.Controls.Button;
        _body = GetTemplateChild("PART_Body") as ScrollViewer;
        _translate = _surface?.RenderTransform as TranslateTransform;
        _driver = new SpringDriver(ApplyPosition);
        _position = _driver.Add(0, .42);
        if (_header is not null)
        {
            _header.PreviewMouseLeftButtonDown += HeaderDown;
            _header.PreviewMouseMove += HeaderMove;
            _header.PreviewMouseLeftButtonUp += HeaderUp;
            _header.PreviewTouchDown += HeaderTouchDown;
            _header.PreviewTouchMove += HeaderTouchMove;
            _header.PreviewTouchUp += HeaderTouchUp;
        }
        if (_grabber is not null) _grabber.Click += GrabberClick;
        if (_body is not null)
        {
            _body.PreviewMouseWheel += BodyWheel;
            _body.PreviewTouchDown += BodyTouchDown;
            _body.PreviewTouchMove += BodyTouchMove;
            _body.PreviewTouchUp += BodyTouchUp;
        }
        UpdateGeometry();
    }
    private void ReleaseSheetParts()
    {
        _driver?.Stop(); _fade.Stop();
        if (_header is not null)
        {
            _header.PreviewMouseLeftButtonDown -= HeaderDown;
            _header.PreviewMouseMove -= HeaderMove;
            _header.PreviewMouseLeftButtonUp -= HeaderUp;
            _header.PreviewTouchDown -= HeaderTouchDown;
            _header.PreviewTouchMove -= HeaderTouchMove;
            _header.PreviewTouchUp -= HeaderTouchUp;
            if (_header.IsMouseCaptured) _header.ReleaseMouseCapture();
        }
        if (_grabber is not null) _grabber.Click -= GrabberClick;
        if (_body is not null)
        {
            _body.PreviewMouseWheel -= BodyWheel;
            _body.PreviewTouchDown -= BodyTouchDown;
            _body.PreviewTouchMove -= BodyTouchMove;
            _body.PreviewTouchUp -= BodyTouchUp;
        }
        _surface = null; _overlay = null; _header = null; _grabber = null; _body = null; _translate = null;
        _position = null; _driver = null; _dragActive = false; _shown = false;
    }

    private static bool IsInteractive(DependencyObject? target, DependencyObject header, DependencyObject? grabber)
    {
        for (var node = target; node is not null && !ReferenceEquals(node, header); node = DrawerHost.GetParentNode(node))
            if (node is ButtonBase && !ReferenceEquals(node, grabber)) return true;
        return false;
    }
    private void HeaderDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsOpen || _header is null || e.StylusDevice is not null || e.ChangedButton != MouseButton.Left || IsInteractive(e.OriginalSource as DependencyObject, _header, _grabber)) return;
        BeginDrag(e.GetPosition(this).Y);
    }
    private void HeaderMove(object sender, MouseEventArgs e)
    {
        if (!_dragActive || _header is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { EndDrag(); return; }
        MoveDrag(e.GetPosition(this).Y);
        if (_dragMoved && !_header.IsMouseCaptured) _header.CaptureMouse();
    }
    private void HeaderUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragActive) return;
        MoveDrag(e.GetPosition(this).Y);
        EndDrag();
        if (_header?.IsMouseCaptured == true) _header.ReleaseMouseCapture();
    }
    private void HeaderTouchDown(object? sender, TouchEventArgs e)
    {
        if (IsOpen && _header is not null && !IsInteractive(e.OriginalSource as DependencyObject, _header, _grabber))
            BeginDrag(e.GetTouchPoint(this).Position.Y);
    }
    private void HeaderTouchMove(object? sender, TouchEventArgs e)
    {
        if (!_dragActive) return;
        MoveDrag(e.GetTouchPoint(this).Position.Y);
        if (_dragMoved) e.Handled = true;
    }
    private void HeaderTouchUp(object? sender, TouchEventArgs e)
    {
        if (!_dragActive) return;
        MoveDrag(e.GetTouchPoint(this).Position.Y);
        bool moved = _dragMoved;
        EndDrag();
        if (moved) e.Handled = true;
    }
    private void BodyTouchDown(object? sender, TouchEventArgs e)
    {
        if (!IsOpen || _body is null || _body.VerticalOffset > 1) return;
        _bodyTouch = true;
        BeginDrag(e.GetTouchPoint(this).Position.Y);
    }
    private void BodyTouchMove(object? sender, TouchEventArgs e)
    {
        if (!_bodyTouch || !_dragActive || _body is null) return;
        double pointer = e.GetTouchPoint(this).Position.Y;
        if (IsExpanded && (pointer < _dragStart || _body.VerticalOffset > 1))
        {
            _bodyTouch = false; _dragActive = false;
            return;
        }
        MoveDrag(pointer);
        if (_dragMoved) e.Handled = true;
    }
    private void BodyTouchUp(object? sender, TouchEventArgs e)
    {
        if (!_bodyTouch) return;
        _bodyTouch = false;
        if (!_dragActive) return;
        MoveDrag(e.GetTouchPoint(this).Position.Y);
        bool moved = _dragMoved;
        EndDrag();
        if (moved) e.Handled = true;
    }
    internal void BeginDrag(double pointer)
    {
        if (_position is null || _driver is null) return;
        _driver.Stop();
        _driver.Settled = null;
        _dragActive = true; _dragMoved = false; _dragStart = _lastPointer = pointer;
        _dragOrigin = _position.Position < 0 ? -Unrubber(-_position.Position) : _position.Position;
        _dragFrom = CurrentDetent;
        _dragProjection.Start();
    }
    internal void MoveDrag(double pointer)
    {
        if (!_dragActive || _position is null) return;
        double delta = pointer - _dragStart;
        if (!_dragMoved && Math.Abs(delta) < 3) return;
        _dragMoved = true;
        _dragProjection.Add(pointer - _lastPointer);
        _lastPointer = pointer;
        double raw = _dragOrigin + delta;
        _position.Snap(raw < 0 ? -Rubber(-raw) : raw);
        ApplyPosition();
    }
    internal void EndDrag()
    {
        if (!_dragActive || _position is null) return;
        _dragActive = false;
        if (!_dragMoved) { ChangeDetent(CurrentDetent); return; }
        _suppressClick = true;
        _dragEndedAt = Environment.TickCount64;
        double velocity = _position.Position < 0 ? 0 : _dragProjection.Velocity();
        double projected = _position.Position + velocity * .2;
        int target = 0;
        double distance = double.PositiveInfinity;
        for (int index = 0; index <= LastDetent + 1; index++)
        {
            double offset = index == LastDetent + 1 ? ClosedOffset : OffsetFor(index);
            double next = Math.Abs(offset - projected);
            if (next >= distance) continue;
            target = index; distance = next;
        }
        if (Math.Abs(velocity) > 320 && target == _dragFrom)
            target = velocity < 0 ? Math.Min(_dragFrom + 1, LastDetent) : _dragFrom == 0 ? LastDetent + 1 : _dragFrom - 1;
        if (target > LastDetent) Close();
        else ChangeDetent(target);
    }
    private static double Rubber(double distance) => (1 - 1 / (distance * .55 / 120 + 1)) * 120;
    private static double Unrubber(double stretch) => (1 / (1 - Math.Min(stretch, 119) / 120) - 1) * 120 / .55;
    private void GrabberClick(object sender, RoutedEventArgs e)
    {
        if (_suppressClick)
        {
            _suppressClick = false;
            if (Environment.TickCount64 - _dragEndedAt < 350) return;
        }
        ChangeDetent(IsExpanded ? 0 : LastDetent);
    }
    private void BodyWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsExpanded && e.Delta < 0) { ChangeDetent(LastDetent); e.Handled = true; }
    }
    internal override void FocusInitial()
    {
        if (InitialFocusElement is not null) { base.FocusInitial(); return; }
        _surface?.Focus();
    }
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (!IsOpen || IsExpanded || e.OriginalSource is not FrameworkElement focused || ReferenceEquals(focused, _surface)) return;
        if (_surface is null || !IsDescendantOf(focused, _body ?? _surface)) return;
        try
        {
            double bottom = focused.PointToScreen(new Point(0, focused.ActualHeight)).Y;
            double viewportBottom = PointToScreen(new Point(0, ActualHeight)).Y;
            if (bottom > viewportBottom - 8) ChangeDetent(LastDetent);
        }
        catch (InvalidOperationException) { }
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && _grabber is not null && IsOpen && IsDescendantOf(e.OriginalSource as DependencyObject, _grabber))
        {
            int next = e.Key switch { Key.Up => CurrentDetent + 1, Key.Down => CurrentDetent - 1, Key.Home => LastDetent, Key.End => 0, _ => -1 };
            if (next >= 0) { ChangeDetent(next); e.Handled = true; }
        }
        if (!e.Handled && IsOpen && (ReferenceEquals(e.OriginalSource, _surface) || ReferenceEquals(e.OriginalSource, _body)))
        {
            bool down = (e.Key is Key.Down or Key.Next) || (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Shift) == 0);
            bool up = (e.Key is Key.Up or Key.Prior) || (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
            if (down && !IsExpanded) { ChangeDetent(LastDetent); e.Handled = true; }
            else if (down || up)
            {
                double step = e.Key is Key.Down or Key.Up ? 48 : (_body?.ViewportHeight ?? 0) * .85;
                _body?.ScrollToVerticalOffset((_body?.VerticalOffset ?? 0) + (down ? step : -step));
                e.Handled = true;
            }
        }
        base.OnPreviewKeyDown(e);
    }
    private static bool IsDescendantOf(DependencyObject? node, DependencyObject ancestor)
    {
        for (; node is not null; node = DrawerHost.GetParentNode(node)) if (ReferenceEquals(node, ancestor)) return true;
        return false;
    }
}
