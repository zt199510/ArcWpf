using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum UserMenuStatus { Available, Busy, Away, Offline }
public enum UserMenuTheme { Light, Dark, System }
public enum UserMenuAlign { Start, Center, End }

public sealed class UserMenuUser { public string Name { get; set; } = "User"; public string Email { get; set; } = ""; public string? Plan { get; set; } public ImageSource? AvatarSource { get; set; } }
public sealed class UserMenuItem { public string Label { get; set; } = ""; public object? Icon { get; set; } public string? Keys { get; set; } public ICommand? Command { get; set; } public object? CommandParameter { get; set; } public bool IsEnabled { get; set; } = true; }

[TemplatePart(Name = "PART_Trigger", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_Items", Type = typeof(ItemsControl))]
public class UserMenu : Control
{
    public static readonly DependencyProperty UserProperty = DependencyProperty.Register(nameof(User), typeof(UserMenuUser), typeof(UserMenu), new FrameworkPropertyMetadata(null, OnVisualChanged));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(UserMenuStatus), typeof(UserMenu), new FrameworkPropertyMetadata(UserMenuStatus.Available, OnVisualChanged));
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(nameof(Theme), typeof(UserMenuTheme), typeof(UserMenu), new FrameworkPropertyMetadata(UserMenuTheme.System, OnVisualChanged));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(UserMenu), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOpenChanged));
    public static readonly DependencyProperty ShowStatusProperty = DependencyProperty.Register(nameof(ShowStatus), typeof(bool), typeof(UserMenu), new PropertyMetadata(false, OnVisualChanged));
    public static readonly DependencyProperty ShowThemeProperty = DependencyProperty.Register(nameof(ShowTheme), typeof(bool), typeof(UserMenu), new PropertyMetadata(true, OnVisualChanged));
    public static readonly DependencyProperty ShowNameProperty = DependencyProperty.Register(nameof(ShowName), typeof(bool), typeof(UserMenu), new PropertyMetadata(false, OnVisualChanged));
    public static readonly DependencyProperty AlignProperty = DependencyProperty.Register(nameof(Align), typeof(UserMenuAlign), typeof(UserMenu), new PropertyMetadata(UserMenuAlign.End));
    public static readonly DependencyProperty SignOutCommandProperty = DependencyProperty.Register(nameof(SignOutCommand), typeof(ICommand), typeof(UserMenu));
    public static readonly DependencyProperty SignOutCommandParameterProperty = DependencyProperty.Register(nameof(SignOutCommandParameter), typeof(object), typeof(UserMenu));
    public static readonly RoutedEvent ItemInvokedEvent = EventManager.RegisterRoutedEvent(nameof(ItemInvoked), RoutingStrategy.Bubble, typeof(EventHandler<UserMenuItemInvokedEventArgs>), typeof(UserMenu));
    public static readonly RoutedEvent StatusChangedEvent = EventManager.RegisterRoutedEvent(nameof(StatusChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(UserMenu));
    public static readonly RoutedEvent ThemeChangedEvent = EventManager.RegisterRoutedEvent(nameof(ThemeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(UserMenu));

    private System.Windows.Controls.Button? _trigger; private Popup? _popup; private Border? _surface; private ItemsControl? _items; private Window? _window; private bool _presented; private readonly OwnedMotion _motion = new();
    public UserMenuUser? User { get => (UserMenuUser?)GetValue(UserProperty); set => SetValue(UserProperty, value); }
    public UserMenuStatus Status { get => (UserMenuStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public UserMenuTheme Theme { get => (UserMenuTheme)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public bool ShowStatus { get => (bool)GetValue(ShowStatusProperty); set => SetValue(ShowStatusProperty, value); }
    public bool ShowTheme { get => (bool)GetValue(ShowThemeProperty); set => SetValue(ShowThemeProperty, value); }
    public bool ShowName { get => (bool)GetValue(ShowNameProperty); set => SetValue(ShowNameProperty, value); }
    public UserMenuAlign Align { get => (UserMenuAlign)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }
    public ICommand? SignOutCommand { get => (ICommand?)GetValue(SignOutCommandProperty); set => SetValue(SignOutCommandProperty, value); }
    public object? SignOutCommandParameter { get => GetValue(SignOutCommandParameterProperty); set => SetValue(SignOutCommandParameterProperty, value); }
    public ObservableCollection<UserMenuItem> Items { get; } = [];
    public event EventHandler<UserMenuItemInvokedEventArgs>? ItemInvoked { add => AddHandler(ItemInvokedEvent, value); remove => RemoveHandler(ItemInvokedEvent, value); }
    public event RoutedEventHandler StatusChanged { add => AddHandler(StatusChangedEvent, value); remove => RemoveHandler(StatusChangedEvent, value); }
    public event RoutedEventHandler ThemeChanged { add => AddHandler(ThemeChangedEvent, value); remove => RemoveHandler(ThemeChangedEvent, value); }

    static UserMenu() { DefaultStyleKeyProperty.OverrideMetadata(typeof(UserMenu), new FrameworkPropertyMetadata(typeof(UserMenu))); FocusableProperty.OverrideMetadata(typeof(UserMenu), new FrameworkPropertyMetadata(false)); }
    public UserMenu() { Items.CollectionChanged += (_, _) => Refresh(); Loaded += (_, _) => Refresh(); Unloaded += (_, _) => ReleaseWindowHandler(); }
    public override void OnApplyTemplate()
    {
        ReleaseTemplate(); base.OnApplyTemplate(); _trigger = GetTemplateChild("PART_Trigger") as System.Windows.Controls.Button; _popup = GetTemplateChild("PART_Popup") as Popup; _surface = GetTemplateChild("PART_Surface") as Border; _items = GetTemplateChild("PART_Items") as ItemsControl;
        if (_trigger is not null) { _trigger.Click += TriggerClick; _trigger.PreviewKeyDown += TriggerKeyDown; }
        if (_popup is not null) { _popup.Opened += PopupOpened; _popup.Closed += PopupClosed; _popup.CustomPopupPlacementCallback = PlacePopup; }
        AddHandler(System.Windows.Controls.Button.ClickEvent, new RoutedEventHandler(ButtonClick), true); AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(MenuKeyDown), true); Refresh();
    }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((UserMenu)d).Refresh();
    private static void OnOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var m = (UserMenu)d; if (m._popup is not null) { if ((bool)e.NewValue) m._popup.IsOpen = true; else m.ClosePopup(); } }
    private void Refresh() { if (_items is not null) _items.ItemsSource = Items; }
    private void TriggerClick(object? s, RoutedEventArgs e) { SetCurrentValue(IsOpenProperty, !IsOpen); e.Handled = true; }
    private void TriggerKeyDown(object? s, KeyEventArgs e) { if (e.Key is not (Key.Down or Key.Up)) return; e.Handled = true; SetCurrentValue(IsOpenProperty, true); Dispatcher.BeginInvoke(new Action(() => FocusStop(e.Key == Key.Down ? 0 : -1)), System.Windows.Threading.DispatcherPriority.Input); }
    private void PopupOpened(object? s, EventArgs e)
    {
        _presented = true; _window = Window.GetWindow(this); if (_window is not null) _window.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OwnerMouseDown), true); SetCurrentValue(IsOpenProperty, true);
        if (_surface is null) return; var full = ArcWpf.ArcMotion.GetLevel(this) == ArcWpf.ArcMotionLevel.Full; var scale = new ScaleTransform(full ? .94 : 1, full ? .94 : 1); _surface.RenderTransform = scale; _surface.RenderTransformOrigin = new Point(.8, 0); _surface.Opacity = full ? 0 : 1; _motion.Double(_surface, UIElement.OpacityProperty, 1, full ? .18 : .08, true, enter: true); _motion.Double(scale, ScaleTransform.ScaleXProperty, 1, .22, full, spring: full, enter: true); _motion.Double(scale, ScaleTransform.ScaleYProperty, 1, .22, full, spring: full, enter: true);
    }
    private void PopupClosed(object? s, EventArgs e) { ReleaseWindowHandler(); _presented = false; _motion.Stop(); _trigger?.Focus(); if (IsOpen) SetCurrentValue(IsOpenProperty, false); }
    private void ReleaseWindowHandler() { if (_window is not null) _window.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OwnerMouseDown)); _window = null; }
    private void ClosePopup() { if (_popup is null) return; _motion.Stop(); _popup.IsOpen = false; }
    private void OwnerMouseDown(object? s, MouseButtonEventArgs e) { if (!_presented || e.OriginalSource is not DependencyObject source) return; if (source is Visual v && (v.IsDescendantOf(this) || (_popup?.Child is Visual child && v.IsDescendantOf(child)))) return; SetCurrentValue(IsOpenProperty, false); }
    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset) { double x = Align == UserMenuAlign.Start ? 0 : Align == UserMenuAlign.Center ? (targetSize.Width - popupSize.Width) / 2 : targetSize.Width - popupSize.Width; return [new CustomPopupPlacement(new Point(x, targetSize.Height + 8), PopupPrimaryAxis.None), new CustomPopupPlacement(new Point(x, -popupSize.Height - 8), PopupPrimaryAxis.None)]; }
    private void ButtonClick(object? s, RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button b || b.Tag is not string tag) return;
        if (tag == "item" && b.DataContext is UserMenuItem item) { Invoke(item); e.Handled = true; }
        else if (tag.StartsWith("status:", StringComparison.Ordinal) && Enum.TryParse<UserMenuStatus>(tag[7..], out var status)) { SetStatus(status); e.Handled = true; }
        else if (tag.StartsWith("theme:", StringComparison.Ordinal) && Enum.TryParse<UserMenuTheme>(tag[6..], out var theme)) { SetTheme(theme); e.Handled = true; }
        else if (tag == "signout") { if (SignOutCommand?.CanExecute(SignOutCommandParameter) != false) SignOutCommand?.Execute(SignOutCommandParameter); SetCurrentValue(IsOpenProperty, false); e.Handled = true; }
    }
    private List<System.Windows.Controls.Button> Stops() => _popup?.Child is DependencyObject root ? FindButtons(root).Where(b => b.Tag is string tag && (tag == "item" || tag.StartsWith("status:") || tag.StartsWith("theme:") || tag == "signout") && b.IsEnabled).ToList() : [];
    private static IEnumerable<System.Windows.Controls.Button> FindButtons(DependencyObject root) { if (root is System.Windows.Controls.Button button) yield return button; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in FindButtons(VisualTreeHelper.GetChild(root, i))) yield return child; }
    private void FocusStop(int index) { var stops = Stops(); if (stops.Count == 0) return; if (index < 0) index = stops.Count - 1; stops[Math.Clamp(index, 0, stops.Count - 1)].Focus(); }
    private void MenuKeyDown(object? s, KeyEventArgs e) { if (!IsOpen) return; var source = e.OriginalSource as DependencyObject; var stops = Stops(); var current = source is null ? null : FindButtons(source).FirstOrDefault(); int index = current is null ? -1 : stops.IndexOf(current); if (e.Key is Key.Escape or Key.Tab) { SetCurrentValue(IsOpenProperty, false); e.Handled = true; return; } if (e.Key is not (Key.Down or Key.Up or Key.Home or Key.End) || stops.Count == 0) return; int next = e.Key == Key.Home ? 0 : e.Key == Key.End ? stops.Count - 1 : (index + (e.Key == Key.Down ? 1 : -1) + stops.Count) % stops.Count; stops[next].Focus(); e.Handled = true; }
    public void Invoke(UserMenuItem item) { if (!item.IsEnabled || item.Command?.CanExecute(item.CommandParameter) == false) return; item.Command?.Execute(item.CommandParameter); RaiseEvent(new UserMenuItemInvokedEventArgs(ItemInvokedEvent, this, item)); SetCurrentValue(IsOpenProperty, false); }
    public void SetStatus(UserMenuStatus status) { if (Status == status) return; SetCurrentValue(StatusProperty, status); RaiseEvent(new RoutedEventArgs(StatusChangedEvent, this)); }
    public void SetTheme(UserMenuTheme theme) { if (Theme == theme) return; SetCurrentValue(ThemeProperty, theme); RaiseEvent(new RoutedEventArgs(ThemeChangedEvent, this)); }
    private void ReleaseTemplate() { if (_trigger is not null) { _trigger.Click -= TriggerClick; _trigger.PreviewKeyDown -= TriggerKeyDown; } if (_popup is not null) { _popup.Opened -= PopupOpened; _popup.Closed -= PopupClosed; _popup.CustomPopupPlacementCallback = null; } RemoveHandler(System.Windows.Controls.Button.ClickEvent, new RoutedEventHandler(ButtonClick)); RemoveHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(MenuKeyDown)); ReleaseWindowHandler(); _motion.Stop(); _trigger = null; _popup = null; _surface = null; _items = null; }
    protected override AutomationPeer OnCreateAutomationPeer() => new UserMenuAutomationPeer(this);
    private sealed class UserMenuAutomationPeer(UserMenu owner) : FrameworkElementAutomationPeer(owner) { protected override string GetClassNameCore() => nameof(UserMenu); protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu; protected override string GetNameCore() => owner.User?.Name ?? "User menu"; }
}

public sealed class UserMenuItemInvokedEventArgs(RoutedEvent routedEvent, object source, UserMenuItem item) : RoutedEventArgs(routedEvent, source) { public UserMenuItem Item { get; } = item; }
public class UserMenuStatusDot : Control { public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(UserMenuStatus), typeof(UserMenuStatusDot), new PropertyMetadata(UserMenuStatus.Available)); public UserMenuStatus Status { get => (UserMenuStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); } static UserMenuStatusDot() { DefaultStyleKeyProperty.OverrideMetadata(typeof(UserMenuStatusDot), new FrameworkPropertyMetadata(typeof(UserMenuStatusDot))); } }

