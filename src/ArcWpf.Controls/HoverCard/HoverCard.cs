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
    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(nameof(Side), typeof(HoverCardSide), typeof(HoverCard), new PropertyMetadata(HoverCardSide.Bottom));
    public static readonly DependencyProperty OpenDelayProperty = DependencyProperty.Register(nameof(OpenDelay), typeof(int), typeof(HoverCard), new PropertyMetadata(180));
    public static readonly DependencyProperty CloseDelayProperty = DependencyProperty.Register(nameof(CloseDelay), typeof(int), typeof(HoverCard), new PropertyMetadata(120));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(HoverCard), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOpenChanged));
    private FrameworkElement? _trigger; private Popup? _popup; private DispatcherTimer? _openTimer, _closeTimer;
    public object? CardContent { get => GetValue(CardContentProperty); set => SetValue(CardContentProperty, value); }
    public HoverCardSide Side { get => (HoverCardSide)GetValue(SideProperty); set => SetValue(SideProperty, value); }
    public int OpenDelay { get => (int)GetValue(OpenDelayProperty); set => SetValue(OpenDelayProperty, value); }
    public int CloseDelay { get => (int)GetValue(CloseDelayProperty); set => SetValue(CloseDelayProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    static HoverCard() { DefaultStyleKeyProperty.OverrideMetadata(typeof(HoverCard), new FrameworkPropertyMetadata(typeof(HoverCard))); }
    public HoverCard() { MouseEnter += TriggerEnter; MouseLeave += TriggerLeave; PreviewKeyDown += KeyDown; }
    public override void OnApplyTemplate() { base.OnApplyTemplate(); _trigger = GetTemplateChild("PART_Trigger") as FrameworkElement; _popup = GetTemplateChild("PART_Popup") as Popup; if (_popup is not null) _popup.CustomPopupPlacementCallback = PlacePopup; }
    private static void OnOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (HoverCard)d; if (c._popup is not null) c._popup.IsOpen = (bool)e.NewValue; }
    private void TriggerEnter(object? sender, MouseEventArgs e) { _closeTimer?.Stop(); if (IsOpen) return; _openTimer ??= new DispatcherTimer(); _openTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, OpenDelay)); _openTimer.Tick -= OpenTick; _openTimer.Tick += OpenTick; _openTimer.Start(); }
    private void TriggerLeave(object? sender, MouseEventArgs e) { _openTimer?.Stop(); if (!IsOpen) return; _closeTimer ??= new DispatcherTimer(); _closeTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, CloseDelay)); _closeTimer.Tick -= CloseTick; _closeTimer.Tick += CloseTick; _closeTimer.Start(); }
    private void OpenTick(object? s, EventArgs e) { _openTimer?.Stop(); SetCurrentValue(IsOpenProperty, true); }
    private void CloseTick(object? s, EventArgs e) { _closeTimer?.Stop(); if (!IsMouseOver) SetCurrentValue(IsOpenProperty, false); }
    private void KeyDown(object? s, KeyEventArgs e) { if (e.Key == Key.Escape && IsOpen) { SetCurrentValue(IsOpenProperty, false); e.Handled = true; } }
    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
    {
        var point = Side switch { HoverCardSide.Top => new Point((targetSize.Width - popupSize.Width) / 2, -popupSize.Height - 8), HoverCardSide.Left => new Point(-popupSize.Width - 8, (targetSize.Height - popupSize.Height) / 2), HoverCardSide.Right => new Point(targetSize.Width + 8, (targetSize.Height - popupSize.Height) / 2), _ => new Point((targetSize.Width - popupSize.Width) / 2, targetSize.Height + 8) }; return [new CustomPopupPlacement(point, PopupPrimaryAxis.None)];
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new HoverCardPeer(this);
    private sealed class HoverCardPeer(HoverCard owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group; protected override string GetNameCore() => "Hover card"; }
}
