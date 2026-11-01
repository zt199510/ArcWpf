using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum HoverCardSide { Top, Bottom, Left, Right }

[TemplatePart(Name = "PART_Trigger", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
public class HoverCard : ContentControl
{
    public static readonly DependencyProperty CardContentProperty = DependencyProperty.Register(nameof(CardContent), typeof(object), typeof(HoverCard));
    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(nameof(Side), typeof(HoverCardSide), typeof(HoverCard), new PropertyMetadata(HoverCardSide.Bottom, SideChanged));
    public static readonly DependencyProperty OpenDelayProperty = DependencyProperty.Register(nameof(OpenDelay), typeof(int), typeof(HoverCard), new PropertyMetadata(180), value => value is int delay && delay >= 0);
    public static readonly DependencyProperty CloseDelayProperty = DependencyProperty.Register(nameof(CloseDelay), typeof(int), typeof(HoverCard), new PropertyMetadata(120), value => value is int delay && delay >= 0);
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(HoverCard), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOpenChanged));

    private FrameworkElement? _trigger;
    private Popup? _popup;
    private FrameworkElement? _card;
    private Window? _owner;
    private bool _repositioning;
    private readonly DispatcherTimer _openTimer = new(), _closeTimer = new();

    public object? CardContent { get => GetValue(CardContentProperty); set => SetValue(CardContentProperty, value); }
    public HoverCardSide Side { get => (HoverCardSide)GetValue(SideProperty); set => SetValue(SideProperty, value); }
    public int OpenDelay { get => (int)GetValue(OpenDelayProperty); set => SetValue(OpenDelayProperty, value); }
    public int CloseDelay { get => (int)GetValue(CloseDelayProperty); set => SetValue(CloseDelayProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    static HoverCard() => DefaultStyleKeyProperty.OverrideMetadata(typeof(HoverCard), new FrameworkPropertyMetadata(typeof(HoverCard)));
    public HoverCard()
    {
        _openTimer.Tick += OpenTick;
        _closeTimer.Tick += CloseTick;
        Unloaded += (_, _) => CloseImmediate();
        Loaded += (_, _) => AttachOwner();
    }
    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _trigger = GetTemplateChild("PART_Trigger") as FrameworkElement;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _card = _popup?.Child as FrameworkElement;
        if (_trigger is not null)
        {
            _trigger.MouseEnter += TriggerEnter;
            _trigger.MouseLeave += TriggerLeave;
            _trigger.GotKeyboardFocus += TriggerFocused;
            _trigger.LostKeyboardFocus += TriggerBlurred;
            _trigger.PreviewKeyDown += PreviewEscape;
        }
        if (_card is not null)
        {
            _card.MouseEnter += CardEnter;
            _card.MouseLeave += CardLeave;
            _card.PreviewKeyDown += PreviewEscape;
        }
        if (_popup is not null)
        {
            _popup.CustomPopupPlacementCallback = PlacePopup;
            _popup.Closed += PopupClosed;
            _popup.IsOpen = IsOpen;
        }
        AttachOwner();
    }
    private void ReleaseTemplate()
    {
        _openTimer.Stop(); _closeTimer.Stop();
        if (_trigger is not null)
        {
            _trigger.MouseEnter -= TriggerEnter;
            _trigger.MouseLeave -= TriggerLeave;
            _trigger.GotKeyboardFocus -= TriggerFocused;
            _trigger.LostKeyboardFocus -= TriggerBlurred;
            _trigger.PreviewKeyDown -= PreviewEscape;
        }
        if (_card is not null)
        {
            _card.MouseEnter -= CardEnter;
            _card.MouseLeave -= CardLeave;
            _card.PreviewKeyDown -= PreviewEscape;
        }
        if (_popup is not null)
        {
            _popup.Closed -= PopupClosed;
            _popup.CustomPopupPlacementCallback = null;
            _popup.IsOpen = false;
        }
        if (_owner is not null) _owner.Deactivated -= OwnerDeactivated;
        _owner = null; _trigger = null; _card = null; _popup = null;
    }
    private void AttachOwner()
    {
        var owner = Window.GetWindow(this);
        if (ReferenceEquals(owner, _owner)) return;
        if (_owner is not null) _owner.Deactivated -= OwnerDeactivated;
        _owner = owner;
        if (_owner is not null) _owner.Deactivated += OwnerDeactivated;
    }
    private static void OnOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (HoverCard)d;
        card._openTimer.Stop(); card._closeTimer.Stop();
        if (card._popup is not null) card._popup.IsOpen = (bool)e.NewValue;
    }
    private static void SideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (HoverCard)d;
        if (card._popup is not { IsOpen: true }) return;
        card._repositioning = true;
        try { card._popup.IsOpen = false; card._popup.IsOpen = true; }
        finally { card._repositioning = false; }
    }
    private void TriggerEnter(object sender, MouseEventArgs e) => ScheduleOpen();
    private void TriggerLeave(object sender, MouseEventArgs e) => ScheduleClose();
    private void CardEnter(object sender, MouseEventArgs e) => _closeTimer.Stop();
    private void CardLeave(object sender, MouseEventArgs e) => ScheduleClose();
    private void TriggerFocused(object sender, KeyboardFocusChangedEventArgs e) { if (IsEnabled) SetCurrentValue(IsOpenProperty, true); }
    private void TriggerBlurred(object sender, KeyboardFocusChangedEventArgs e) { if (IsOpen && _card?.IsKeyboardFocusWithin != true) ScheduleClose(); }
    private void ScheduleOpen()
    {
        _closeTimer.Stop();
        if (IsOpen || !IsEnabled) return;
        _openTimer.Stop();
        _openTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, OpenDelay));
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
        if (_trigger?.IsMouseOver == true && IsEnabled) SetCurrentValue(IsOpenProperty, true);
    }
    private void CloseTick(object? sender, EventArgs e)
    {
        _closeTimer.Stop();
        if (_trigger?.IsMouseOver != true && _card?.IsMouseOver != true && _trigger?.IsKeyboardFocusWithin != true && _card?.IsKeyboardFocusWithin != true)
            SetCurrentValue(IsOpenProperty, false);
    }
    private void PreviewEscape(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !IsOpen) return;
        CloseImmediate(); e.Handled = true;
    }
    private void PopupClosed(object? sender, EventArgs e)
    {
        if (!_repositioning && IsOpen) SetCurrentValue(IsOpenProperty, false);
    }
    private void OwnerDeactivated(object? sender, EventArgs e) => CloseImmediate();
    private void CloseImmediate()
    {
        _openTimer.Stop(); _closeTimer.Stop();
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
    }
    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
    {
        const double gap = 8;
        double centerX = (targetSize.Width - popupSize.Width) / 2;
        double centerY = (targetSize.Height - popupSize.Height) / 2;
        var above = new CustomPopupPlacement(new Point(centerX, -popupSize.Height - gap), PopupPrimaryAxis.Horizontal);
        var below = new CustomPopupPlacement(new Point(centerX, targetSize.Height + gap), PopupPrimaryAxis.Horizontal);
        var left = new CustomPopupPlacement(new Point(-popupSize.Width - gap, centerY), PopupPrimaryAxis.Vertical);
        var right = new CustomPopupPlacement(new Point(targetSize.Width + gap, centerY), PopupPrimaryAxis.Vertical);
        return Side switch
        {
            HoverCardSide.Top => [above, below],
            HoverCardSide.Left => [left, right],
            HoverCardSide.Right => [right, left],
            _ => [below, above]
        };
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new HoverCardPeer(this);
    private sealed class HoverCardPeer(HoverCard owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => "Hover card";
    }
}
