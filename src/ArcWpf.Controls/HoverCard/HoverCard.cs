using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum HoverCardSide { Top, Bottom, Left, Right }
public enum HoverCardAlignment { Start, Center, End }

[TemplatePart(Name = "PART_Trigger", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
public partial class HoverCard : ContentControl
{
    public static readonly DependencyProperty CardContentProperty = DependencyProperty.Register(nameof(CardContent), typeof(object), typeof(HoverCard), new PropertyMetadata(null, OnCardContentChanged));
    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(nameof(Side), typeof(HoverCardSide), typeof(HoverCard), new PropertyMetadata(HoverCardSide.Bottom, SideChanged));
    public static readonly DependencyProperty AlignmentProperty = DependencyProperty.Register(nameof(Alignment), typeof(HoverCardAlignment), typeof(HoverCard), new PropertyMetadata(HoverCardAlignment.Start, SideChanged));
    public static readonly DependencyProperty OpenDelayProperty = DependencyProperty.Register(nameof(OpenDelay), typeof(int), typeof(HoverCard), new PropertyMetadata(500), value => value is int delay && delay >= 0);
    public static readonly DependencyProperty CloseDelayProperty = DependencyProperty.Register(nameof(CloseDelay), typeof(int), typeof(HoverCard), new PropertyMetadata(140), value => value is int delay && delay >= 0);
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(HoverCard), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOpenChanged));
    private static readonly DependencyPropertyKey ActualSidePropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActualSide), typeof(HoverCardSide), typeof(HoverCard), new PropertyMetadata(HoverCardSide.Bottom));
    public static readonly DependencyProperty ActualSideProperty = ActualSidePropertyKey.DependencyProperty;

    private FrameworkElement? _trigger;
    private Popup? _popup;
    private Border? _frame;
    private FrameworkElement? _card;
    private Window? _owner;
    private bool _dismissed;
    private bool _warm;
    private bool _touchDown;
    private int _transition;
    private ScaleTransform? _scale;
    private TranslateTransform? _move;
    private readonly OwnedMotion _motion = new();
    private readonly DispatcherTimer _openTimer = new(), _closeTimer = new();
    private static readonly ConditionalWeakTable<Window, ActiveSlot> Active = new();
    private sealed class ActiveSlot
    {
        public WeakReference<HoverCard>? Current;
        public long ClosedAt;
    }

    public object? CardContent { get => GetValue(CardContentProperty); set => SetValue(CardContentProperty, value); }
    public HoverCardSide Side { get => (HoverCardSide)GetValue(SideProperty); set => SetValue(SideProperty, value); }
    public HoverCardAlignment Alignment { get => (HoverCardAlignment)GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }
    public int OpenDelay { get => (int)GetValue(OpenDelayProperty); set => SetValue(OpenDelayProperty, value); }
    public int CloseDelay { get => (int)GetValue(CloseDelayProperty); set => SetValue(CloseDelayProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public HoverCardSide ActualSide => (HoverCardSide)GetValue(ActualSideProperty);
    internal bool HasPendingOpen => _openTimer.IsEnabled;

    static HoverCard() => DefaultStyleKeyProperty.OverrideMetadata(typeof(HoverCard), new FrameworkPropertyMetadata(typeof(HoverCard)));
    public HoverCard()
    {
        _openTimer.Tick += OpenTick;
        _closeTimer.Tick += CloseTick;
        Unloaded += (_, _) => CloseImmediate();
        Loaded += (_, _) => { AttachOwner(); if (IsOpen) Present(); };
    }
    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _trigger = GetTemplateChild("PART_Trigger") as FrameworkElement;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _frame = _popup?.Child as Border;
        _card = _frame?.Child as FrameworkElement;
        if (_frame is not null)
        {
            _frame.MouseEnter += CardEnter;
            _frame.MouseLeave += CardLeave;
            BindingOperations.SetBinding(_frame, DataContextProperty, new Binding(nameof(DataContext)) { Source = this });
            BindingOperations.SetBinding(_frame, FlowDirectionProperty, new Binding(nameof(FlowDirection)) { Source = this });
            BindingOperations.SetBinding(_frame, ArcMotion.LevelProperty, new Binding { Source = this, Path = new PropertyPath(ArcMotion.LevelProperty) });
        }
        if (_trigger is not null)
        {
            _trigger.MouseEnter += TriggerEnter;
            _trigger.MouseLeave += TriggerLeave;
            _trigger.GotKeyboardFocus += TriggerFocused;
            _trigger.LostKeyboardFocus += TriggerBlurred;
            _trigger.PreviewKeyDown += PreviewEscape;
            _trigger.MouseLeftButtonUp += TriggerClicked;
            _trigger.TouchDown += TriggerTouchDown;
            _trigger.TouchUp += TriggerTouchUp;
            _trigger.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(TriggerButtonClicked), true);
        }
        if (_card is not null)
        {
            _card.MouseEnter += CardEnter;
            _card.MouseLeave += CardLeave;
            _card.PreviewKeyDown += PreviewEscape;
            _card.SizeChanged += CardSized;
            _scale = new ScaleTransform(1, 1);
            _move = new TranslateTransform();
            var transform = new TransformGroup();
            transform.Children.Add(_scale);
            transform.Children.Add(_move);
            _card.RenderTransform = transform;
        }
        if (_popup is not null)
        {
            _popup.CustomPopupPlacementCallback = PlacePopup;
            _popup.Closed += PopupClosed;
            if (IsOpen && IsLoaded) Present();
        }
        AttachOwner();
    }
    private void ReleaseTemplate()
    {
        _openTimer.Stop(); _closeTimer.Stop();
        StopPlacementTracking();
        _motion.Stop();
        if (_trigger is not null)
        {
            _trigger.MouseEnter -= TriggerEnter;
            _trigger.MouseLeave -= TriggerLeave;
            _trigger.GotKeyboardFocus -= TriggerFocused;
            _trigger.LostKeyboardFocus -= TriggerBlurred;
            _trigger.PreviewKeyDown -= PreviewEscape;
            _trigger.MouseLeftButtonUp -= TriggerClicked;
            _trigger.TouchDown -= TriggerTouchDown;
            _trigger.TouchUp -= TriggerTouchUp;
            _trigger.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(TriggerButtonClicked));
        }
        if (_card is not null)
        {
            _card.MouseEnter -= CardEnter;
            _card.MouseLeave -= CardLeave;
            _card.PreviewKeyDown -= PreviewEscape;
            _card.SizeChanged -= CardSized;
        }
        if (_frame is not null)
        {
            _frame.MouseEnter -= CardEnter;
            _frame.MouseLeave -= CardLeave;
            BindingOperations.ClearBinding(_frame, DataContextProperty);
            BindingOperations.ClearBinding(_frame, FlowDirectionProperty);
            BindingOperations.ClearBinding(_frame, ArcMotion.LevelProperty);
        }
        if (_popup is not null)
        {
            _popup.Closed -= PopupClosed;
            _popup.CustomPopupPlacementCallback = null;
            _popup.IsOpen = false;
        }
        DetachOwner();
        _trigger = null; _frame = null; _card = null; _popup = null; _scale = null; _move = null;
    }
    private void AttachOwner()
    {
        var owner = Window.GetWindow(this);
        if (ReferenceEquals(owner, _owner)) return;
        DetachOwner();
        _owner = owner;
        if (_owner is not null)
        {
            _owner.Deactivated += OwnerDeactivated;
            _owner.Closed += OwnerClosed;
            _owner.LocationChanged += OwnerMoved;
            _owner.SizeChanged += OwnerSized;
            _owner.DpiChanged += OwnerDpiChanged;
            _owner.PreviewMouseDown += OutsideMouseDown;
        }
    }
    private static void OnOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (HoverCard)d;
        card._openTimer.Stop(); card._closeTimer.Stop();
        if ((bool)e.NewValue) card.Present(); else card.BeginExit();
    }
    private static void SideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (HoverCard)d;
        if (card._popup is not { IsOpen: true }) return;
        card.QueuePlacement();
    }
    private void TriggerEnter(object sender, MouseEventArgs e) => ScheduleOpen();
    private void TriggerLeave(object sender, MouseEventArgs e) { _dismissed = false; ScheduleClose(); }
    private void CardEnter(object sender, MouseEventArgs e) => _closeTimer.Stop();
    private void CardLeave(object sender, MouseEventArgs e) => ScheduleClose();
    private void TriggerFocused(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (IsEnabled && InputManager.Current.MostRecentInputDevice is KeyboardDevice) Show();
    }
    private void TriggerBlurred(object sender, KeyboardFocusChangedEventArgs e) { if (IsOpen && _card?.IsKeyboardFocusWithin != true) ScheduleClose(); }
    private void TriggerClicked(object sender, MouseButtonEventArgs e)
    {
        if (!_touchDown && IsEnabled && !IsOpen) Show();
    }
    private void TriggerButtonClicked(object sender, RoutedEventArgs e)
    {
        if (!_touchDown && IsEnabled && !IsOpen) Show();
    }
    private void TriggerTouchDown(object? sender, TouchEventArgs e) => _touchDown = true;
    private void TriggerTouchUp(object? sender, TouchEventArgs e)
    {
        // WPF can promote this touch to a mouse click in the same input cycle.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => _touchDown = false);
        if (!IsEnabled) return;
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
        else Show();
    }
    private void ScheduleOpen()
    {
        _closeTimer.Stop();
        if (IsOpen || !IsEnabled || _dismissed) return;
        _openTimer.Stop();
        bool warm = _owner is not null && IsWarm(Active.GetOrCreateValue(_owner));
        _openTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, warm ? 80 : OpenDelay));
        _openTimer.Start();
    }
    private void ScheduleClose()
    {
        _openTimer.Stop();
        if (!IsOpen) return;
        _closeTimer.Stop();
        _closeTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, CloseDelay));
        _closeTimer.Start();
    }
    private void OpenTick(object? sender, EventArgs e)
    {
        _openTimer.Stop();
        if (_trigger?.IsMouseOver == true && IsEnabled && !_dismissed) Show();
    }
    private void CloseTick(object? sender, EventArgs e)
    {
        _closeTimer.Stop();
        if (_trigger?.IsMouseOver != true && _frame?.IsMouseOver != true && _card?.IsMouseOver != true && _trigger?.IsKeyboardFocusWithin != true && _card?.IsKeyboardFocusWithin != true)
            SetCurrentValue(IsOpenProperty, false);
    }
    private void PreviewEscape(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !IsOpen) return;
        _dismissed = true;
        CloseImmediate(); e.Handled = true;
    }
    private void PopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
    }
    private void OwnerDeactivated(object? sender, EventArgs e) => CloseImmediate();
    private void Show()
    {
        _dismissed = false;
        SetCurrentValue(IsOpenProperty, true);
    }
    private static bool IsWarm(ActiveSlot slot)
    {
        if (slot.Current?.TryGetTarget(out var current) == true && current.IsOpen) return true;
        return slot.ClosedAt != 0 && Stopwatch.GetElapsedTime(slot.ClosedAt) < TimeSpan.FromMilliseconds(300);
    }
    private void Present()
    {
        if (!IsLoaded || _popup is null || _card is null || _scale is null || _move is null) return;
        AttachOwner();
        if (_owner is not null)
        {
            var slot = Active.GetOrCreateValue(_owner);
            _warm = IsWarm(slot);
            if (slot.Current?.TryGetTarget(out var previous) == true && !ReferenceEquals(previous, this)) previous.CloseImmediate();
            slot.Current = new WeakReference<HoverCard>(this);
        }
        ++_transition;
        bool fresh = !_popup.IsOpen;
        UpdatePlacement();
        if (!IsOpen) return;
        LayoutUpdated -= TrackAnchor;
        LayoutUpdated += TrackAnchor;
        _card.IsHitTestVisible = true;
        var level = ArcMotion.GetLevel(this);
        bool full = level == ArcMotionLevel.Full && !_warm;
        if (fresh)
        {
            _card.Opacity = level == ArcMotionLevel.Off ? 1 : 0;
            _scale.ScaleX = _scale.ScaleY = full ? .96 : 1;
            _move.X = full ? ActualSide == HoverCardSide.Left ? 4 : ActualSide == HoverCardSide.Right ? -4 : 0 : 0;
            _move.Y = full ? ActualSide == HoverCardSide.Top ? 4 : ActualSide == HoverCardSide.Bottom ? -4 : 0 : 0;
            _popup.IsOpen = true;
        }
        bool animate = IsLoaded && level != ArcMotionLevel.Off;
        _motion.Double(_card, OpacityProperty, 1, level == ArcMotionLevel.Reduced ? .15 : _warm ? .09 : .16, animate, enter: true);
        _motion.Double(_scale, ScaleTransform.ScaleXProperty, 1, full ? .4 : 0, animate && full, spring: full);
        _motion.Double(_scale, ScaleTransform.ScaleYProperty, 1, full ? .4 : 0, animate && full, spring: full);
        _motion.Double(_move, TranslateTransform.XProperty, 0, full ? .4 : 0, animate && full, spring: full);
        _motion.Double(_move, TranslateTransform.YProperty, 0, full ? .4 : 0, animate && full, spring: full);
    }
    private void BeginExit()
    {
        if (_popup?.IsOpen != true || _card is null || _scale is null || _move is null) return;
        MarkClosed();
        StopPlacementTracking();
        int version = ++_transition;
        _card.IsHitTestVisible = false;
        var level = ArcMotion.GetLevel(this);
        bool animate = IsLoaded && level != ArcMotionLevel.Off;
        if (!animate) { FinishExit(version); return; }
        bool full = level == ArcMotionLevel.Full && !_warm;
        _motion.Double(_card, OpacityProperty, 0, level == ArcMotionLevel.Reduced ? .1 : .12, true, completed: () => FinishExit(version));
        _motion.Double(_scale, ScaleTransform.ScaleXProperty, full ? .96 : 1, .12, full);
        _motion.Double(_scale, ScaleTransform.ScaleYProperty, full ? .96 : 1, .12, full);
        _motion.Double(_move, TranslateTransform.XProperty, full ? ActualSide == HoverCardSide.Left ? 4 : ActualSide == HoverCardSide.Right ? -4 : 0 : 0, .12, full);
        _motion.Double(_move, TranslateTransform.YProperty, full ? ActualSide == HoverCardSide.Top ? 4 : ActualSide == HoverCardSide.Bottom ? -4 : 0 : 0, .12, full);
    }
    private void FinishExit(int version)
    {
        if (version != _transition || IsOpen) return;
        _motion.Stop();
        if (_popup is not null) _popup.IsOpen = false;
    }
    private void MarkClosed()
    {
        if (_owner is null) return;
        var slot = Active.GetOrCreateValue(_owner);
        if (slot.Current?.TryGetTarget(out var current) == true && ReferenceEquals(current, this))
        {
            slot.Current = null;
            slot.ClosedAt = Stopwatch.GetTimestamp();
        }
    }
    private void CloseImmediate()
    {
        _openTimer.Stop(); _closeTimer.Stop();
        StopPlacementTracking();
        MarkClosed();
        ++_transition;
        _motion.Stop();
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
        if (_popup is not null) _popup.IsOpen = false;
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FlowDirectionProperty) QueuePlacement();
        if (e.Property != ArcMotion.LevelProperty || _card is null) return;
        var level = ArcMotion.GetLevel(this);
        if (level != ArcMotionLevel.Full)
        {
            _motion.Stop();
            if (_scale is not null) _scale.ScaleX = _scale.ScaleY = 1;
            if (_move is not null) _move.X = _move.Y = 0;
            if (IsOpen)
            {
                _motion.Double(_card, OpacityProperty, 1, .15, level == ArcMotionLevel.Reduced && IsLoaded, enter: true);
            }
            else if (level == ArcMotionLevel.Reduced && _popup?.IsOpen == true)
            {
                int version = _transition;
                _motion.Double(_card, OpacityProperty, 0, .1, IsLoaded, completed: () => FinishExit(version));
            }
            else FinishExit(_transition);
        }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new HoverCardPeer(this);
    private sealed class HoverCardPeer(HoverCard owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => "Hover card";
    }
}
