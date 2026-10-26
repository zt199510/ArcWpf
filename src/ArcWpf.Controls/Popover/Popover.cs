using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_Surface", Type = typeof(FrameworkElement))]
public partial class Popover : ContentControl
{
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Popover),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OpenChanged));
    public static readonly DependencyProperty PlacementTargetProperty = DependencyProperty.Register(nameof(PlacementTarget), typeof(FrameworkElement), typeof(Popover), new FrameworkPropertyMetadata(null, AnchorChanged));
    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(nameof(Side), typeof(PopoverSide), typeof(Popover), new FrameworkPropertyMetadata(PopoverSide.Bottom, GeometryChanged), v => v is PopoverSide side && Enum.IsDefined(side));
    public static readonly DependencyProperty AlignmentProperty = DependencyProperty.Register(nameof(Alignment), typeof(PopoverAlignment), typeof(Popover), new FrameworkPropertyMetadata(PopoverAlignment.Start, GeometryChanged), v => v is PopoverAlignment alignment && Enum.IsDefined(alignment));
    public static readonly DependencyProperty SideOffsetProperty = DependencyProperty.Register(nameof(SideOffset), typeof(double), typeof(Popover), new FrameworkPropertyMetadata(6d, GeometryChanged), ValidDistance);
    public static readonly DependencyProperty CollisionPaddingProperty = DependencyProperty.Register(nameof(CollisionPadding), typeof(double), typeof(Popover), new FrameworkPropertyMetadata(10d, GeometryChanged), ValidDistance);
    public static readonly DependencyProperty CloseOnOutsideClickProperty = DependencyProperty.Register(nameof(CloseOnOutsideClick), typeof(bool), typeof(Popover), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty CloseOnEscapeProperty = DependencyProperty.Register(nameof(CloseOnEscape), typeof(bool), typeof(Popover), new FrameworkPropertyMetadata(true));
    private static readonly DependencyPropertyKey ActualSidePropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActualSide), typeof(PopoverSide), typeof(Popover), new FrameworkPropertyMetadata(PopoverSide.Bottom));
    public static readonly DependencyProperty ActualSideProperty = ActualSidePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsPresentedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPresented), typeof(bool), typeof(Popover), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsPresentedProperty = IsPresentedPropertyKey.DependencyProperty;
    public static readonly RoutedEvent OpenedEvent = EventManager.RegisterRoutedEvent(nameof(Opened), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Popover));
    public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent(nameof(Closed), RoutingStrategy.Bubble, typeof(EventHandler<PopoverClosedEventArgs>), typeof(Popover));
    public event RoutedEventHandler Opened { add => AddHandler(OpenedEvent, value); remove => RemoveHandler(OpenedEvent, value); }
    public event EventHandler<PopoverClosedEventArgs> Closed { add => AddHandler(ClosedEvent, value); remove => RemoveHandler(ClosedEvent, value); }

    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public FrameworkElement? PlacementTarget { get => (FrameworkElement?)GetValue(PlacementTargetProperty); set => SetValue(PlacementTargetProperty, value); }
    public PopoverSide Side { get => (PopoverSide)GetValue(SideProperty); set => SetValue(SideProperty, value); }
    public PopoverAlignment Alignment { get => (PopoverAlignment)GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }
    public double SideOffset { get => (double)GetValue(SideOffsetProperty); set => SetValue(SideOffsetProperty, value); }
    public double CollisionPadding { get => (double)GetValue(CollisionPaddingProperty); set => SetValue(CollisionPaddingProperty, value); }
    public bool CloseOnOutsideClick { get => (bool)GetValue(CloseOnOutsideClickProperty); set => SetValue(CloseOnOutsideClickProperty, value); }
    public bool CloseOnEscape { get => (bool)GetValue(CloseOnEscapeProperty); set => SetValue(CloseOnEscapeProperty, value); }
    public PopoverSide ActualSide => (PopoverSide)GetValue(ActualSideProperty);
    public bool IsPresented => (bool)GetValue(IsPresentedProperty);

    private Popup? _popup;
    private FrameworkElement? _root;
    private FrameworkElement? _surface;
    private Window? _owner;
    private FrameworkElement? _anchor;
    private IInputElement? _previousFocus;
    private readonly PopoverLifecycle _cycle = new();
    private PopoverCloseReason _closeReason;
    private bool _settingOpen;
    private bool _openedRaised;
    private bool _releasingTemplate;
    private DispatcherOperation? _placementUpdate;
    private DispatcherOperation? _focusUpdate;
    private Rect _lastAnchor = Rect.Empty;
    private Size _lastOwnerSize;
    private static readonly ConditionalWeakTable<Window, ActiveSlot> Active = new();
    private sealed class ActiveSlot { public WeakReference<Popover>? Current; }
    private static bool ValidDistance(object value) => value is double distance && double.IsFinite(distance) && distance >= 0;

    static Popover() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Popover), new FrameworkPropertyMetadata(typeof(Popover)));
    public Popover() => Unloaded += (_, _) => RequestClose(PopoverCloseReason.Unloaded, true);
    protected override Size MeasureOverride(Size constraint) { base.MeasureOverride(constraint); return new Size(); }

    public void Close() => RequestClose(PopoverCloseReason.Programmatic);
    private static void OpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Popover)d;
        if (control._settingOpen) { return; }
        if ((bool)e.NewValue) { control.OpenCore(); } else { control.RequestClose(PopoverCloseReason.Programmatic); }
    }
    private static void AnchorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Popover)d;
        if (control._cycle.Phase != PopoverPhase.Closed) { control.RequestClose(PopoverCloseReason.AnchorUnavailable, true); }
    }
    private static void GeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Popover)d).QueuePlacement();
    private void SetOpen(bool value)
    {
        _settingOpen = true;
        try { SetCurrentValue(IsOpenProperty, value); } finally { _settingOpen = false; }
    }

    public override void OnApplyTemplate()
    {
        _releasingTemplate = true;
        try { ReleaseTemplate(); } finally { _releasingTemplate = false; }
        base.OnApplyTemplate();
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _surface = GetTemplateChild("PART_Surface") as FrameworkElement;
        _root = _popup?.Child as FrameworkElement;
        if (_popup is not null)
        {
            _popup.AllowsTransparency = true;
            _popup.PopupAnimation = PopupAnimation.None;
            _popup.StaysOpen = true;
            _popup.Placement = PlacementMode.Custom;
            _popup.FlowDirection = FlowDirection.LeftToRight;
            _popup.CustomPopupPlacementCallback = PlacePopup;
            _popup.Opened += NativeOpened;
            _popup.Closed += NativeClosed;
        }
        if (_root is not null)
        {
            Bridge(DataContextProperty, new PropertyPath(DataContextProperty));
            Bridge(FlowDirectionProperty, new PropertyPath(FlowDirectionProperty));
            Bridge(ArcMotion.LevelProperty, new PropertyPath(ArcMotion.LevelProperty));
            _root.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(ContentKeyDown));
            _root.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(ContentPreviewKeyDown));
            _root.SizeChanged += ContentSizeChanged;
        }
        InitializeAnimation();
    }

    private void Bridge(DependencyProperty property, PropertyPath path) =>
        BindingOperations.SetBinding(_root!, property, new Binding { Source = this, Path = path, Mode = BindingMode.OneWay });

    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        _releasingTemplate = true;
        try { ReleaseTemplate(); base.OnTemplateChanged(oldTemplate, newTemplate); }
        finally { _releasingTemplate = false; }
    }

    private void ReleaseTemplate()
    {
        if (_cycle.Phase != PopoverPhase.Closed) { RequestClose(PopoverCloseReason.TemplateChanged, true); }
        StopAnimation();
        if (_surface is not null && _previousSurfaceTransform is not null) { _surface.SetCurrentValue(RenderTransformProperty, _previousSurfaceTransform); }
        _scale = null;
        _translate = null;
        _previousSurfaceTransform = null;
        if (_popup is not null)
        {
            _popup.Opened -= NativeOpened;
            _popup.Closed -= NativeClosed;
            _popup.CustomPopupPlacementCallback = null;
        }
        if (_root is not null)
        {
            _root.RemoveHandler(Keyboard.KeyDownEvent, new KeyEventHandler(ContentKeyDown));
            _root.RemoveHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(ContentPreviewKeyDown));
            _root.SizeChanged -= ContentSizeChanged;
            BindingOperations.ClearBinding(_root, DataContextProperty);
            BindingOperations.ClearBinding(_root, FlowDirectionProperty);
            BindingOperations.ClearBinding(_root, ArcMotion.LevelProperty);
        }
        _popup = null;
        _root = null;
        _surface = null;
    }

    private void OpenCore()
    {
        if (_releasingTemplate) { SetOpen(false); return; }
        ApplyTemplate();
        // Initial template application closes stale state; this call represents the current request.
        if (_popup is null || _root is null || !IsLoaded || PlacementTarget is not { IsLoaded: true, IsVisible: true } anchor ||
            Window.GetWindow(anchor) is not { } owner || Window.GetWindow(this) != owner || !AnchorVisible(anchor, owner))
        {
            SetOpen(false);
            Trace.TraceWarning("ArcWpf.Popover cannot open: template, loaded anchor or matching owner window is unavailable.");
            return;
        }
        if (_cycle.Phase is PopoverPhase.Open or PopoverPhase.Opening) { return; }
        SetOpen(true);
        bool fresh = _cycle.BeginOpen();
        int version = _cycle.Version;
        if (fresh)
        {
            _previousFocus = Keyboard.FocusedElement;
            _anchor = anchor;
            _owner = owner;
            var slot = Active.GetOrCreateValue(owner);
            Popover? previous = null;
            slot.Current?.TryGetTarget(out previous);
            slot.Current = new WeakReference<Popover>(this);
            if (previous is not null && previous != this) { previous.RequestClose(PopoverCloseReason.Replaced, true); }
            if (version != _cycle.Version || !IsOpen || slot.Current is null || !slot.Current.TryGetTarget(out var current) || current != this) { return; }
            Subscribe();
            _popup.PlacementTarget = anchor;
        }
        _root.IsHitTestVisible = true;
        UpdatePlacement();
        if (version != _cycle.Version || !IsOpen) { return; }
        if (fresh)
        {
            _openedRaised = false;
            PrepareAnimation();
            _popup.IsOpen = true;
        }
        else { RunAnimation(true, version); }
    }

    private void NativeOpened(object? sender, EventArgs e)
    {
        if (!IsOpen || _cycle.Phase != PopoverPhase.Opening || _popup?.IsOpen != true) { return; }
        SetValue(IsPresentedPropertyKey, true);
        int version = _cycle.Version;
        _focusUpdate?.Abort();
        _focusUpdate = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            _focusUpdate = null;
            if (version != _cycle.Version || !IsOpen) { return; }
            UpdatePlacement();
            if (version != _cycle.Version || !IsOpen) { return; }
            FocusContent();
            if (version != _cycle.Version || !IsOpen) { return; }
            if (!_openedRaised)
            {
                _openedRaised = true;
                RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
            }
            if (version == _cycle.Version && IsOpen) { RunAnimation(true, version); }
        }));
    }

    internal void RequestClose(PopoverCloseReason reason, bool immediate = false)
    {
        SetOpen(false);
        if (_cycle.Phase == PopoverPhase.Closed) { return; }
        if (_cycle.Phase == PopoverPhase.Closing && !immediate) { return; }
        _closeReason = reason;
        int version = _cycle.BeginClose();
        _focusUpdate?.Abort();
        _focusUpdate = null;
        if (_root is not null) { _root.IsHitTestVisible = false; }
        RestoreFocus(reason);
        if (version != _cycle.Version || IsOpen) { return; }
        if (immediate || !IsPresented) { FinishClose(version); }
        else { RunAnimation(false, version); }
    }

    private void FinishClose(int version)
    {
        if (!_cycle.CompleteClose(version)) { return; }
        bool notify = IsPresented;
        var reason = _closeReason;
        StopAnimation();
        Unsubscribe();
        _openedRaised = false;
        SetValue(IsPresentedPropertyKey, false);
        if (_popup is not null) { _popup.IsOpen = false; }
        if (notify) { RaiseEvent(new PopoverClosedEventArgs(ClosedEvent, this, reason)); }
    }

    private void NativeClosed(object? sender, EventArgs e)
    {
        if (_cycle.Phase != PopoverPhase.Closed) { RequestClose(PopoverCloseReason.AnchorUnavailable, true); }
    }

    private void Subscribe()
    {
        if (_owner is null || _anchor is null) { return; }
        _owner.Deactivated += OwnerDeactivated;
        _owner.Closed += OwnerClosed;
        _owner.LocationChanged += OwnerMoved;
        _owner.SizeChanged += OwnerSized;
        _owner.DpiChanged += OwnerDpiChanged;
        _anchor.Unloaded += AnchorUnloaded;
        _anchor.IsVisibleChanged += AnchorVisibilityChanged;
        _anchor.LayoutUpdated += AnchorLayoutUpdated;
        InputManager.Current.PreProcessInput += OutsideInput;
    }
    private void Unsubscribe()
    {
        _placementUpdate?.Abort();
        _placementUpdate = null;
        _focusUpdate?.Abort();
        _focusUpdate = null;
        InputManager.Current.PreProcessInput -= OutsideInput;
        if (_anchor is not null)
        {
            _anchor.Unloaded -= AnchorUnloaded;
            _anchor.IsVisibleChanged -= AnchorVisibilityChanged;
            _anchor.LayoutUpdated -= AnchorLayoutUpdated;
        }
        if (_owner is not null)
        {
            _owner.Deactivated -= OwnerDeactivated;
            _owner.Closed -= OwnerClosed;
            _owner.LocationChanged -= OwnerMoved;
            _owner.SizeChanged -= OwnerSized;
            _owner.DpiChanged -= OwnerDpiChanged;
            if (Active.TryGetValue(_owner, out var slot) && slot.Current?.TryGetTarget(out var current) == true && current == this) { slot.Current = null; }
        }
        _owner = null;
        _anchor = null;
        _previousFocus = null;
        _lastAnchor = Rect.Empty;
    }
    private void OwnerDeactivated(object? sender, EventArgs e) => RequestClose(PopoverCloseReason.OwnerDeactivated, true);
    private void OwnerClosed(object? sender, EventArgs e) => RequestClose(PopoverCloseReason.OwnerClosed, true);
    private void AnchorUnloaded(object sender, RoutedEventArgs e) => RequestClose(PopoverCloseReason.AnchorUnavailable, true);
    private void AnchorVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { if (_anchor?.IsVisible != true) { RequestClose(PopoverCloseReason.AnchorUnavailable, true); } }
    private void OwnerMoved(object? sender, EventArgs e) => QueuePlacement();
    private void OwnerSized(object sender, SizeChangedEventArgs e) => QueuePlacement();
    private void OwnerDpiChanged(object sender, DpiChangedEventArgs e) => QueuePlacement();
    private void ContentSizeChanged(object sender, SizeChangedEventArgs e) => QueuePlacement();
    private void AnchorLayoutUpdated(object? sender, EventArgs e)
    {
        if (_anchor is null || _owner is null) { return; }
        if (!AnchorVisible(_anchor, _owner)) { RequestClose(PopoverCloseReason.AnchorUnavailable, true); return; }
        var bounds = ScreenBounds(_anchor);
        if (bounds != _lastAnchor || _lastOwnerSize != _owner.RenderSize) { QueuePlacement(); }
    }
    private void QueuePlacement()
    {
        if (_cycle.Phase == PopoverPhase.Closed || _placementUpdate is not null) { return; }
        _placementUpdate = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => { _placementUpdate = null; UpdatePlacement(); }));
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FlowDirectionProperty) { QueuePlacement(); }
        if (e.Property == ArcMotion.LevelProperty && _cycle.Phase != PopoverPhase.Closed)
        {
            if (IsOpen) { RunAnimation(true, _cycle.Version); } else { FinishClose(_cycle.Version); }
        }
    }
}
