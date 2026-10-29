using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum UserMenuStatus { Available, Busy, Away, Offline }
public enum UserMenuTheme { Light, Dark, System }
public enum UserMenuAlign { Start, Center, End }

public sealed class UserMenuUser
{
    public string Name { get; set; } = "User";
    public string Email { get; set; } = "";
    public string? Plan { get; set; }
    public ImageSource? AvatarSource { get; set; }
}

public sealed class UserMenuItem
{
    public string Label { get; set; } = "";
    public object? Icon { get; set; }
    /// <summary>Space-separated shortcut hint, such as "⌘ ,". Each part renders as its own key.</summary>
    public string? Keys { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    public bool IsEnabled { get; set; } = true;
    public IReadOnlyList<string> KeyParts => UserMenu.SplitKeys(Keys);
}

[TemplatePart(Name = "PART_Trigger", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_TriggerHover", Type = typeof(UIElement))]
[TemplatePart(Name = "PART_TriggerPress", Type = typeof(UIElement))]
[TemplatePart(Name = "PART_Chevron", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_Header", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_List", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Highlight", Type = typeof(Border))]
[TemplatePart(Name = "PART_Items", Type = typeof(ItemsControl))]
[TemplatePart(Name = "PART_StatusRow", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_StatusSegments", Type = typeof(Panel))]
[TemplatePart(Name = "PART_StatusThumb", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_ThemeRow", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_ThemeSegments", Type = typeof(Panel))]
[TemplatePart(Name = "PART_ThemeThumb", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_SignOut", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_SignOutLabel", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Spinner", Type = typeof(FrameworkElement))]
public class UserMenu : Control
{
    public static readonly DependencyProperty UserProperty = DependencyProperty.Register(nameof(User), typeof(UserMenuUser), typeof(UserMenu), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(UserMenuStatus), typeof(UserMenu), new FrameworkPropertyMetadata(UserMenuStatus.Available, (d, _) => ((UserMenu)d).UpdateSegments(true)));
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(nameof(Theme), typeof(UserMenuTheme), typeof(UserMenu), new FrameworkPropertyMetadata(UserMenuTheme.System, (d, _) => ((UserMenu)d).UpdateSegments(true)));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(UserMenu), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));
    public static readonly DependencyProperty ShowStatusProperty = DependencyProperty.Register(nameof(ShowStatus), typeof(bool), typeof(UserMenu), new PropertyMetadata(false));
    public static readonly DependencyProperty ShowThemeProperty = DependencyProperty.Register(nameof(ShowTheme), typeof(bool), typeof(UserMenu), new PropertyMetadata(true));
    public static readonly DependencyProperty ShowNameProperty = DependencyProperty.Register(nameof(ShowName), typeof(bool), typeof(UserMenu), new PropertyMetadata(false));
    public static readonly DependencyProperty AlignProperty = DependencyProperty.Register(nameof(Align), typeof(UserMenuAlign), typeof(UserMenu), new PropertyMetadata(UserMenuAlign.End));
    public static readonly DependencyProperty SignOutCommandProperty = DependencyProperty.Register(nameof(SignOutCommand), typeof(ICommand), typeof(UserMenu));
    public static readonly DependencyProperty SignOutCommandParameterProperty = DependencyProperty.Register(nameof(SignOutCommandParameter), typeof(object), typeof(UserMenu));
    public static readonly DependencyProperty SignOutKeysProperty = DependencyProperty.Register(nameof(SignOutKeys), typeof(string), typeof(UserMenu), new PropertyMetadata(null, (d, e) => d.SetValue(SignOutKeyPartsPropertyKey, SplitKeys((string?)e.NewValue))));
    private static readonly DependencyPropertyKey SignOutKeyPartsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SignOutKeyParts), typeof(IReadOnlyList<string>), typeof(UserMenu), new PropertyMetadata(Array.Empty<string>()));
    public static readonly DependencyProperty SignOutKeyPartsProperty = SignOutKeyPartsPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsSigningOutPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsSigningOut), typeof(bool), typeof(UserMenu), new PropertyMetadata(false, (d, _) => ((UserMenu)d).UpdateSignOut(true)));
    public static readonly DependencyProperty IsSigningOutProperty = IsSigningOutPropertyKey.DependencyProperty;
    public static readonly RoutedEvent ItemInvokedEvent = EventManager.RegisterRoutedEvent(nameof(ItemInvoked), RoutingStrategy.Bubble, typeof(EventHandler<UserMenuItemInvokedEventArgs>), typeof(UserMenu));
    public static readonly RoutedEvent StatusChangedEvent = EventManager.RegisterRoutedEvent(nameof(StatusChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(UserMenu));
    public static readonly RoutedEvent ThemeChangedEvent = EventManager.RegisterRoutedEvent(nameof(ThemeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(UserMenu));

    public UserMenuUser? User { get => (UserMenuUser?)GetValue(UserProperty); set => SetValue(UserProperty, value); }
    public UserMenuStatus Status { get => (UserMenuStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public UserMenuTheme Theme { get => (UserMenuTheme)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    /// <summary>Arc shows the presence dot and the status switch only when a status is passed.</summary>
    public bool ShowStatus { get => (bool)GetValue(ShowStatusProperty); set => SetValue(ShowStatusProperty, value); }
    public bool ShowTheme { get => (bool)GetValue(ShowThemeProperty); set => SetValue(ShowThemeProperty, value); }
    public bool ShowName { get => (bool)GetValue(ShowNameProperty); set => SetValue(ShowNameProperty, value); }
    public UserMenuAlign Align { get => (UserMenuAlign)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }
    public ICommand? SignOutCommand { get => (ICommand?)GetValue(SignOutCommandProperty); set => SetValue(SignOutCommandProperty, value); }
    public object? SignOutCommandParameter { get => GetValue(SignOutCommandParameterProperty); set => SetValue(SignOutCommandParameterProperty, value); }
    /// <summary>Space-separated shortcut hint for sign out, like <see cref="UserMenuItem.Keys"/>.</summary>
    public string? SignOutKeys { get => (string?)GetValue(SignOutKeysProperty); set => SetValue(SignOutKeysProperty, value); }
    public IReadOnlyList<string> SignOutKeyParts => (IReadOnlyList<string>)GetValue(SignOutKeyPartsProperty);
    /// <summary>Arc's promise-returning onSignOut: the item shows progress and the menu closes once the task settles.</summary>
    public Func<Task>? SignOutAsync { get; set; }
    public bool IsSigningOut => (bool)GetValue(IsSigningOutProperty);
    public ObservableCollection<UserMenuItem> Items { get; } = [];
    public event EventHandler<UserMenuItemInvokedEventArgs>? ItemInvoked { add => AddHandler(ItemInvokedEvent, value); remove => RemoveHandler(ItemInvokedEvent, value); }
    public event RoutedEventHandler StatusChanged { add => AddHandler(StatusChangedEvent, value); remove => RemoveHandler(StatusChangedEvent, value); }
    public event RoutedEventHandler ThemeChanged { add => AddHandler(ThemeChangedEvent, value); remove => RemoveHandler(ThemeChangedEvent, value); }

    private enum OpenReason { None, Pointer, First, Last }

    // Template parts. The trigger and rows are plain WPF buttons; the bare name would mean ArcWpf.Controls.Button.
    private System.Windows.Controls.Button? _trigger, _signOut;
    private UIElement? _triggerHover, _triggerPress;
    private FrameworkElement? _chevron, _header, _statusRow, _themeRow, _statusThumb, _themeThumb, _spinner;
    private Popup? _popup;
    private Border? _surface, _highlight;
    private Panel? _list, _statusSegments, _themeSegments;
    private ItemsControl? _items;
    private TextBlock? _signOutLabel;

    private readonly ScaleTransform _surfaceScale = new(1, 1);
    private readonly TranslateTransform _highlightMove = new();
    private readonly RotateTransform _chevronRotation = new(), _spinnerRotation = new();
    private readonly TranslateTransform _statusThumbMove = new(), _themeThumbMove = new(), _signOutLabelMove = new();
    private readonly OwnedMotion _panelMotion = new(), _rowMotion = new(), _highlightMotion = new(), _chromeMotion = new();
    private OpenReason _reason;
    private bool _presented, _closing, _returnFocus, _highlightShown;
    private Window? _window;
    private string _typed = "";
    private DispatcherTimer? _typedTimer;
    private ButtonBase? _pressWatch;

    // CSS --ease-spring and Motion's critically damped springs share one curve; it settles at about 2.07 × visualDuration.
    private static double Spring(double visualDuration) => visualDuration * 2.07;
    private static readonly KeySpline ExitEase = FrozenSpline(.4, 0, 1, 1);
    private static KeySpline FrozenSpline(double x1, double y1, double x2, double y2) { var spline = new KeySpline(x1, y1, x2, y2); spline.Freeze(); return spline; }
    private ArcMotionLevel Level => ArcMotion.GetLevel(this);
    private bool Full => Level == ArcMotionLevel.Full;

    static UserMenu()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(UserMenu), new FrameworkPropertyMetadata(typeof(UserMenu)));
        FocusableProperty.OverrideMetadata(typeof(UserMenu), new FrameworkPropertyMetadata(false));
    }

    public UserMenu() => Unloaded += (_, _) => { ReleaseWindow(); _typedTimer?.Stop(); };

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _trigger = GetTemplateChild("PART_Trigger") as System.Windows.Controls.Button;
        _triggerHover = GetTemplateChild("PART_TriggerHover") as UIElement;
        _triggerPress = GetTemplateChild("PART_TriggerPress") as UIElement;
        _chevron = GetTemplateChild("PART_Chevron") as FrameworkElement;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _surface = GetTemplateChild("PART_Surface") as Border;
        _header = GetTemplateChild("PART_Header") as FrameworkElement;
        _list = GetTemplateChild("PART_List") as Panel;
        _highlight = GetTemplateChild("PART_Highlight") as Border;
        _items = GetTemplateChild("PART_Items") as ItemsControl;
        _statusRow = GetTemplateChild("PART_StatusRow") as FrameworkElement;
        _statusSegments = GetTemplateChild("PART_StatusSegments") as Panel;
        _statusThumb = GetTemplateChild("PART_StatusThumb") as FrameworkElement;
        _themeRow = GetTemplateChild("PART_ThemeRow") as FrameworkElement;
        _themeSegments = GetTemplateChild("PART_ThemeSegments") as Panel;
        _themeThumb = GetTemplateChild("PART_ThemeThumb") as FrameworkElement;
        _signOut = GetTemplateChild("PART_SignOut") as System.Windows.Controls.Button;
        _signOutLabel = GetTemplateChild("PART_SignOutLabel") as TextBlock;
        _spinner = GetTemplateChild("PART_Spinner") as FrameworkElement;

        if (_trigger is not null)
        {
            _trigger.Click += TriggerClick;
            _trigger.PreviewKeyDown += TriggerKeyDown;
            _trigger.MouseEnter += TriggerPointerChanged;
            _trigger.MouseLeave += TriggerPointerChanged;
            _pressWatch = _trigger;
            System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ButtonBase)).AddValueChanged(_trigger, TriggerPointerChanged);
        }
        if (_chevron is not null) { _chevron.RenderTransformOrigin = new Point(.5, .5); _chevron.RenderTransform = _chevronRotation; }
        if (_popup is not null) { _popup.Opened += PopupOpened; _popup.Closed += PopupClosed; _popup.CustomPopupPlacementCallback = PlacePopup; }
        if (_surface is not null)
        {
            _surface.RenderTransform = _surfaceScale;
            _surface.PreviewKeyDown += SurfaceKeyDown;
            _surface.GotKeyboardFocus += SurfaceGotFocus;
            _surface.PreviewMouseMove += SurfaceMouseMove;
            _surface.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(SurfaceClick));
            _surface.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(SegmentChecked));
        }
        if (_list is not null) _list.MouseLeave += ListMouseLeave;
        if (_highlight is not null)
        {
            // Sized and moved to the active row in the list's own coordinates; top alignment keeps Y absolute.
            _highlight.VerticalAlignment = VerticalAlignment.Top;
            _highlight.Height = 0;
            _highlight.Opacity = 0;
            _highlight.RenderTransform = _highlightMove;
        }
        if (_statusThumb is not null) _statusThumb.RenderTransform = _statusThumbMove;
        if (_themeThumb is not null) _themeThumb.RenderTransform = _themeThumbMove;
        if (_signOutLabel is not null) _signOutLabel.RenderTransform = _signOutLabelMove;
        if (_spinner is not null) { _spinner.RenderTransformOrigin = new Point(.5, .5); _spinner.RenderTransform = _spinnerRotation; }
        if (_items is not null) _items.ItemsSource = Items;
        UpdateSegments(false);
        UpdateSignOut(false);
        UpdateTriggerFill(false);
        if (IsOpen && _popup is not null) _popup.IsOpen = true;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsOpenProperty) { UpdateTriggerFill(true); _chromeMotion.Double(_chevronRotation, RotateTransform.AngleProperty, (bool)e.NewValue ? 180 : 0, Spring(.28), Full && IsVisible, spring: true); }
    }

    public void Invoke(UserMenuItem item)
    {
        if (_closing || !item.IsEnabled || item.Command?.CanExecute(item.CommandParameter) == false) return;
        Close(returnFocus: true);
        item.Command?.Execute(item.CommandParameter);
        RaiseEvent(new UserMenuItemInvokedEventArgs(ItemInvokedEvent, this, item));
    }

    public void SetStatus(UserMenuStatus status)
    {
        if (Status == status) return;
        SetCurrentValue(StatusProperty, status);
        RaiseEvent(new RoutedEventArgs(StatusChangedEvent, this));
    }

    public void SetTheme(UserMenuTheme theme)
    {
        if (Theme == theme) return;
        SetCurrentValue(ThemeProperty, theme);
        RaiseEvent(new RoutedEventArgs(ThemeChangedEvent, this));
    }

    // ── Open and close ───────────────────────────────────────────────────────────────────────────────

    private void Open(OpenReason reason) { _reason = reason; SetCurrentValue(IsOpenProperty, true); }

    private void Close(bool returnFocus) { _returnFocus |= returnFocus; SetCurrentValue(IsOpenProperty, false); }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var menu = (UserMenu)d;
        if (menu._popup is null) return;
        if ((bool)e.NewValue) { menu.CancelExit(); menu._popup.IsOpen = true; return; }
        if (!menu.BeginExit()) menu._popup.IsOpen = false;
        menu.FinishFocus();
    }

    private void TriggerClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsOpen) { Close(returnFocus: false); return; }
        // Enter and Space open onto the first row; a pointer click focuses the panel without a highlight.
        Open(InputManager.Current.MostRecentInputDevice is KeyboardDevice ? OpenReason.First : OpenReason.Pointer);
    }

    private void TriggerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Down or Key.Up)) return;
        e.Handled = true;
        if (IsOpen) FocusStop(e.Key == Key.Down ? 0 : -1);
        else Open(e.Key == Key.Down ? OpenReason.First : OpenReason.Last);
    }

    private void PopupOpened(object? sender, EventArgs e)
    {
        _presented = true;
        _window = Window.GetWindow(this);
        if (_window is not null)
        {
            _window.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(WindowMouseDown), true);
            _window.Deactivated += WindowDeactivated;
        }
        SetCurrentValue(IsOpenProperty, true);
        SetHighlight(null);
        AnimateIn();
        // Focus moves in only when a person opened the menu; a menu opened through IsOpen never steals focus.
        var reason = _reason;
        _reason = OpenReason.None;
        if (reason == OpenReason.None) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!IsOpen) return;
            if (reason == OpenReason.Pointer) _surface?.Focus();
            else FocusStop(reason == OpenReason.First ? 0 : -1);
        });
    }

    private void PopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
        _presented = false;
        _closing = false;
        ReleaseWindow();
        ResetMotion();
        FinishFocus();
    }

    private void FinishFocus()
    {
        if (!_returnFocus) return;
        _returnFocus = false;
        _trigger?.Focus();
    }

    private void WindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_presented || e.OriginalSource is not Visual source) return;
        if (_trigger is not null && source.IsDescendantOf(_trigger)) return;
        if (_popup?.Child is Visual child && source.IsDescendantOf(child)) return;
        Close(returnFocus: false);
    }

    private void WindowDeactivated(object? sender, EventArgs e) => Close(returnFocus: false);

    private void ReleaseWindow()
    {
        if (_window is null) return;
        _window.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(WindowMouseDown));
        _window.Deactivated -= WindowDeactivated;
        _window = null;
    }

    // The panel is placed against the trigger and scales from the avatar's center, flipping above when there is no room below.
    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
    {
        if (_trigger is null || _surface is null || _trigger.ActualWidth <= 0) return [];
        // Callback sizes are in device units; the frame padding leaves room for the floating shadow.
        double unit = targetSize.Width / _trigger.ActualWidth;
        var frame = (_popup?.Child as Border)?.Padding ?? default;
        double width = _surface.ActualWidth * unit, height = _surface.ActualHeight * unit, gap = 8 * unit;
        double left = Align switch
        {
            UserMenuAlign.Start => 0,
            UserMenuAlign.Center => (targetSize.Width - width) / 2,
            _ => targetSize.Width - width
        };
        var screen = _trigger.PointToScreen(new Point(0, 0));
        var area = WorkAreaFor(screen);
        bool above = screen.Y + targetSize.Height + gap + height > area.Bottom - 12 * unit && screen.Y - gap - height > area.Top + 12 * unit;
        double top = above ? -gap - height : targetSize.Height + gap;

        // Same origin rule as Arc: the avatar's center, or 20px in from the right edge of a named end-aligned trigger.
        double face = Math.Min(targetSize.Width, 40 * unit) / 2;
        double originX = Align == UserMenuAlign.End && _trigger.ActualWidth > 40 ? targetSize.Width - 20 * unit - left : face - left;
        _surface.RenderTransformOrigin = new Point(Math.Clamp(originX / width, 0, 1), above ? 1 : 0);

        var placed = new Point(left - frame.Left * unit, top - frame.Top * unit);
        var flipped = new Point(placed.X, (above ? targetSize.Height + gap : -gap - height) - frame.Top * unit);
        return [new CustomPopupPlacement(placed, PopupPrimaryAxis.None), new CustomPopupPlacement(flipped, PopupPrimaryAxis.None)];
    }

    private Rect WorkAreaFor(Point devicePoint)
    {
        // SystemParameters.WorkArea is in DIPs of the primary monitor; convert it to device units to match PointToScreen.
        var area = SystemParameters.WorkArea;
        if (PresentationSource.FromVisual(this)?.CompositionTarget is { } target)
        {
            var toDevice = target.TransformToDevice;
            area = new Rect(toDevice.Transform(area.TopLeft), toDevice.Transform(area.BottomRight));
        }
        return area;
    }

    // ── Motion ───────────────────────────────────────────────────────────────────────────────────────

    // Full: the panel grows from .94 on a critically damped spring (visualDuration .3) while it fades in over 160ms;
    // rows follow 30ms later, 16ms apart, rising 3px over 200ms. Reduced: a 120ms fade. Off: nothing moves.
    private void AnimateIn()
    {
        if (_surface is null) return;
        ResetMotion();
        _surface.UpdateLayout();
        var level = Level;
        _surface.Opacity = level == ArcMotionLevel.Off ? 1 : 0;
        _surfaceScale.ScaleX = _surfaceScale.ScaleY = level == ArcMotionLevel.Full ? .94 : 1;
        if (level == ArcMotionLevel.Off) return;
        bool full = level == ArcMotionLevel.Full;
        _panelMotion.Double(_surface, OpacityProperty, 1, full ? .16 : .12, true, enter: true);
        _panelMotion.Double(_surfaceScale, ScaleTransform.ScaleXProperty, 1, Spring(.3), full, spring: true);
        _panelMotion.Double(_surfaceScale, ScaleTransform.ScaleYProperty, 1, Spring(.3), full, spring: true);
        if (!full) return;
        int index = 0;
        foreach (var row in Rows())
        {
            var move = new TranslateTransform(0, 3);
            row.RenderTransform = move;
            row.Opacity = 0;
            double delay = .03 + index++ * .016;
            _rowMotion.Double(row, OpacityProperty, 1, .2, true, delay: delay, enter: true);
            _rowMotion.Double(move, TranslateTransform.YProperty, 0, .2, true, delay: delay, enter: true);
        }
    }

    // Closing is a short fade (120ms, scale .97 in Full); the popup stays up until it ends and a reopen reverses it.
    private bool BeginExit()
    {
        if (!_presented || _surface is null || _popup is not { IsOpen: true } || !IsLoaded || Level == ArcMotionLevel.Off) return false;
        _closing = true;
        _surface.IsHitTestVisible = false;
        SetHighlight(null);
        _panelMotion.Double(_surface, OpacityProperty, 0, .12, true, completed: FinishExit, ease: ExitEase);
        if (Full)
        {
            _panelMotion.Double(_surfaceScale, ScaleTransform.ScaleXProperty, .97, .12, true, ease: ExitEase);
            _panelMotion.Double(_surfaceScale, ScaleTransform.ScaleYProperty, .97, .12, true, ease: ExitEase);
        }
        return true;
    }

    private void FinishExit()
    {
        if (!_closing) return;
        _closing = false;
        if (!IsOpen && _popup is not null) _popup.IsOpen = false;
    }

    private void CancelExit()
    {
        if (!_closing || _surface is null) return;
        _closing = false;
        _surface.IsHitTestVisible = true;
        _panelMotion.Double(_surface, OpacityProperty, 1, .16, Level != ArcMotionLevel.Off, enter: true);
        _panelMotion.Double(_surfaceScale, ScaleTransform.ScaleXProperty, 1, Spring(.3), Full, spring: true);
        _panelMotion.Double(_surfaceScale, ScaleTransform.ScaleYProperty, 1, Spring(.3), Full, spring: true);
    }

    private void ResetMotion()
    {
        _panelMotion.Stop(); _rowMotion.Stop(); _highlightMotion.Stop();
        if (_surface is not null) { _surface.Opacity = 1; _surface.IsHitTestVisible = true; }
        _surfaceScale.ScaleX = _surfaceScale.ScaleY = 1;
        foreach (var row in Rows()) { row.Opacity = 1; row.RenderTransform = Transform.Identity; }
    }

    // The trigger answers hover and press with color, never with scale: SurfaceMuted while hovered or open, a darker mix while pressed.
    private void TriggerPointerChanged(object? sender, EventArgs e) => UpdateTriggerFill(true);

    private void UpdateTriggerFill(bool animate)
    {
        if (_trigger is null) return;
        animate &= Level != ArcMotionLevel.Off && IsVisible;
        bool pressed = _trigger.IsPressed;
        if (_triggerHover is not null) _chromeMotion.Double(_triggerHover, OpacityProperty, !pressed && (IsOpen || _trigger.IsMouseOver) ? 1 : 0, .16, animate);
        if (_triggerPress is not null) _chromeMotion.Double(_triggerPress, OpacityProperty, pressed ? 1 : 0, .16, animate);
    }

    // ── Rows, highlight and keyboard ─────────────────────────────────────────────────────────────────

    private IEnumerable<FrameworkElement> Rows()
    {
        if (_header is not null) yield return _header;
        foreach (var stop in Stops()) yield return stop;
    }

    /// <summary>Keyboard stops in menu order: account rows, the status and theme switches (one stop each), then sign out.</summary>
    private List<FrameworkElement> Stops()
    {
        var stops = new List<FrameworkElement>();
        if (_items is not null)
            foreach (var item in Items)
                if (_items.ItemContainerGenerator.ContainerFromItem(item) is DependencyObject container && FindChild<System.Windows.Controls.Button>(container) is { } button)
                    stops.Add(button);
        if (_statusRow is { IsVisible: true }) stops.Add(_statusRow);
        if (_themeRow is { IsVisible: true }) stops.Add(_themeRow);
        if (_signOut is not null) stops.Add(_signOut);
        return stops;
    }

    private static string StopLabel(FrameworkElement stop) => stop switch
    {
        { DataContext: UserMenuItem item } when stop is ButtonBase => item.Label,
        _ when stop.Name == "PART_StatusRow" => "Status",
        _ when stop.Name == "PART_ThemeRow" => "Theme",
        _ => "Sign out"
    };

    private FrameworkElement? StopOf(DependencyObject? source)
    {
        var stops = Stops();
        for (; source is not null; source = VisualTreeHelper.GetParent(source))
            if (source is FrameworkElement element && stops.Contains(element)) return element;
        return null;
    }

    private bool IsGroup(FrameworkElement stop) => ReferenceEquals(stop, _statusRow) || ReferenceEquals(stop, _themeRow);

    private void FocusStop(int index)
    {
        var stops = Stops().Where(s => s.IsEnabled).ToList();
        if (stops.Count == 0) return;
        FocusStop(stops[index < 0 ? stops.Count - 1 : Math.Min(index, stops.Count - 1)]);
    }

    // A switch is one stop: focus lands on its checked option.
    private void FocusStop(FrameworkElement stop)
    {
        if (!IsGroup(stop)) { stop.Focus(); return; }
        var segments = Segments(stop);
        (segments.FirstOrDefault(s => s.IsChecked == true) ?? segments.FirstOrDefault())?.Focus();
    }

    private List<System.Windows.Controls.RadioButton> Segments(FrameworkElement group)
    {
        var panel = ReferenceEquals(group, _statusRow) ? _statusSegments : _themeSegments;
        return panel?.Children.OfType<System.Windows.Controls.RadioButton>().ToList() ?? [];
    }

    private void SurfaceGotFocus(object sender, KeyboardFocusChangedEventArgs e) => SetHighlight(StopOf(e.NewFocus as DependencyObject));

    // One highlight follows the pointer and the keyboard. It glides on the snappy spring between rows and fades in from nothing.
    private void SetHighlight(FrameworkElement? stop)
    {
        if (_highlight is null || _list is null) return;
        bool animate = Level != ArcMotionLevel.Off && _presented;
        if (stop is null || !stop.IsVisible)
        {
            _highlightShown = false;
            _highlightMotion.Double(_highlight, OpacityProperty, 0, .12, animate);
            return;
        }
        // Layout position only: a row's enter offset may still be running.
        double shift = stop.RenderTransform is TranslateTransform move ? move.Y : 0;
        double top = stop.TranslatePoint(new Point(0, 0), _list).Y - shift;
        bool glide = _highlightShown && Full && _presented;
        _highlightMotion.Double(_highlightMove, TranslateTransform.YProperty, top, Spring(.26), glide, spring: true);
        _highlightMotion.Double(_highlight, HeightProperty, stop.ActualHeight, Spring(.26), glide, spring: true);
        _highlightMotion.Double(_highlight, OpacityProperty, 1, .12, animate);
        _highlightShown = true;
    }

    // Focus follows a mouse or pen over rows, so the keyboard carries on from wherever the pointer left the highlight.
    // The switches are not rows: the pointer over one releases the highlight instead of dragging it along.
    private void SurfaceMouseMove(object sender, MouseEventArgs e)
    {
        if (!_presented || _closing || e.StylusDevice is not null) return;
        var stop = StopOf(e.OriginalSource as DependencyObject);
        if (stop is null) return;
        if (IsGroup(stop))
        {
            if (_highlightShown && !stop.IsKeyboardFocusWithin) { SetHighlight(null); _surface?.Focus(); }
            return;
        }
        if (stop.IsEnabled && !stop.IsKeyboardFocused) stop.Focus();
    }

    private void ListMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_presented || e.StylusDevice is not null) return;
        SetHighlight(null);
        _surface?.Focus();
    }

    private void SurfaceKeyDown(object sender, KeyEventArgs e)
    {
        var stops = Stops().Where(s => s.IsEnabled).ToList();
        var current = StopOf(Keyboard.FocusedElement as DependencyObject);
        int index = current is null ? -1 : stops.IndexOf(current);
        void Step(int to) { e.Handled = true; if (stops.Count > 0) FocusStop(stops[(to % stops.Count + stops.Count) % stops.Count]); }
        switch (e.Key)
        {
            case Key.Down: Step(index + 1); return;
            case Key.Up: Step(index <= 0 ? stops.Count - 1 : index - 1); return;
            case Key.Home: Step(0); return;
            case Key.End: Step(stops.Count - 1); return;
            case Key.Escape:
            case Key.Tab: e.Handled = true; Close(returnFocus: true); return;
            case Key.Left:
            case Key.Right:
                if (current is null || !IsGroup(current)) return;
                e.Handled = true;
                var segments = Segments(current);
                int at = Math.Max(0, segments.FindIndex(s => s.IsChecked == true));
                var next = segments[(at + (e.Key == Key.Right ? 1 : -1) + segments.Count) % segments.Count];
                next.Focus();
                next.IsChecked = true;
                return;
        }
        if (Typeahead(e.Key) is not { } letter || Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift)) return;
        // Typeahead: letters jump to the next row whose label starts with what was typed within 500ms.
        _typedTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Input, (_, _) => { _typed = ""; _typedTimer!.Stop(); }, Dispatcher);
        _typedTimer.Stop(); _typedTimer.Start();
        _typed += letter;
        var ordered = stops.Skip(index + 1).Concat(stops.Take(index + 1)).ToList();
        List<FrameworkElement> search = _typed.Length > 1 && current is not null && StopLabel(current).StartsWith(_typed, StringComparison.OrdinalIgnoreCase) ? [current] : ordered;
        if (search.FirstOrDefault(s => StopLabel(s).StartsWith(_typed, StringComparison.OrdinalIgnoreCase)) is { } match) { e.Handled = true; FocusStop(match); }
    }

    private static char? Typeahead(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (char)('a' + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        _ => null
    };

    private void SurfaceClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button button) return;
        if (ReferenceEquals(button, _signOut)) { e.Handled = true; SignOut(); }
        else if (button.DataContext is UserMenuItem item) { e.Handled = true; Invoke(item); }
    }

    // ── Switches ─────────────────────────────────────────────────────────────────────────────────────

    // Theme and status choices keep the menu open.
    private void SegmentChecked(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.RadioButton { Tag: { } value }) return;
        if (value is UserMenuStatus status) SetStatus(status);
        else if (value is UserMenuTheme theme) SetTheme(theme);
    }

    private void UpdateSegments(bool animate)
    {
        animate &= Full && _presented;
        Update(_statusSegments, _statusThumbMove, Status);
        Update(_themeSegments, _themeThumbMove, Theme);

        // The thumb slides under the icons on --ease-spring (580ms).
        void Update(Panel? panel, TranslateTransform thumb, object value)
        {
            if (panel is null) return;
            int index = 0, at = 0;
            foreach (var segment in panel.Children.OfType<System.Windows.Controls.RadioButton>())
            {
                bool selected = Equals(segment.Tag, value);
                if (selected) at = index;
                segment.SetCurrentValue(ToggleButton.IsCheckedProperty, selected);
                index++;
            }
            _chromeMotion.Double(thumb, TranslateTransform.XProperty, at * 28, .58, animate, spring: true);
        }
    }

    // ── Sign out ─────────────────────────────────────────────────────────────────────────────────────

    private async void SignOut()
    {
        if (IsSigningOut) return;
        if (SignOutCommand?.CanExecute(SignOutCommandParameter) != false) SignOutCommand?.Execute(SignOutCommandParameter);
        if (SignOutAsync is not { } signOut) { Close(returnFocus: true); return; }
        // Async sign out keeps the menu open with its progress on the item, then closes once the task settles.
        SetValue(IsSigningOutPropertyKey, true);
        try { await signOut(); }
        catch { /* A failed sign out still settles the menu; the app reports its own error. */ }
        finally { SetValue(IsSigningOutPropertyKey, false); Close(returnFocus: false); }
    }

    // "Sign out" ↔ "Signing out": the new text rises out of a 0.35em offset; the icon swaps to a spinner turning every 800ms.
    private void UpdateSignOut(bool animate)
    {
        if (_signOutLabel is not null)
        {
            _signOutLabel.Text = IsSigningOut ? "Signing out" : "Sign out";
            animate &= Level != ArcMotionLevel.Off && _presented;
            bool rise = animate && Full;
            _signOutLabel.Opacity = animate ? 0 : 1;
            _signOutLabelMove.Y = rise ? .35 * _signOutLabel.FontSize : 0;
            _chromeMotion.Double(_signOutLabel, OpacityProperty, 1, Full ? .24 : 0, animate, enter: true);
            _chromeMotion.Double(_signOutLabelMove, TranslateTransform.YProperty, 0, .24, rise, enter: true);
        }
        if (_signOut is not null) System.Windows.Automation.AutomationProperties.SetItemStatus(_signOut, IsSigningOut ? "Busy" : "");
        _spinnerRotation.BeginAnimation(RotateTransform.AngleProperty, IsSigningOut
            ? new DoubleAnimation(0, 360, TimeSpan.FromSeconds(Full ? .8 : 2.4)) { RepeatBehavior = RepeatBehavior.Forever }
            : null);
    }

    internal static IReadOnlyList<string> SplitKeys(string? keys) => keys?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private void ReleaseTemplate()
    {
        if (_trigger is not null)
        {
            _trigger.Click -= TriggerClick;
            _trigger.PreviewKeyDown -= TriggerKeyDown;
            _trigger.MouseEnter -= TriggerPointerChanged;
            _trigger.MouseLeave -= TriggerPointerChanged;
        }
        if (_pressWatch is not null) System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ButtonBase)).RemoveValueChanged(_pressWatch, TriggerPointerChanged);
        if (_popup is not null) { _popup.Opened -= PopupOpened; _popup.Closed -= PopupClosed; _popup.CustomPopupPlacementCallback = null; }
        if (_surface is not null)
        {
            _surface.PreviewKeyDown -= SurfaceKeyDown;
            _surface.GotKeyboardFocus -= SurfaceGotFocus;
            _surface.PreviewMouseMove -= SurfaceMouseMove;
            _surface.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(SurfaceClick));
            _surface.RemoveHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(SegmentChecked));
        }
        if (_list is not null) _list.MouseLeave -= ListMouseLeave;
        ReleaseWindow();
        ResetMotion();
        _chromeMotion.Stop();
        _pressWatch = null;
        _trigger = _signOut = null; _triggerHover = _triggerPress = null; _popup = null; _surface = _highlight = null; _list = _statusSegments = _themeSegments = null; _items = null; _signOutLabel = null;
        _chevron = _header = _statusRow = _themeRow = _statusThumb = _themeThumb = _spinner = null;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new UserMenuAutomationPeer(this);

    private sealed class UserMenuAutomationPeer(UserMenu owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(UserMenu);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;
        protected override string GetNameCore() => owner.User?.Name ?? "User menu";
    }
}

public sealed class UserMenuItemInvokedEventArgs(RoutedEvent routedEvent, object source, UserMenuItem item) : RoutedEventArgs(routedEvent, source)
{
    public UserMenuItem Item { get; } = item;
}

/// <summary>
/// Arc's PresenceDot: one mark, three shapes. Solid for available, a bar for busy, a ring for away and offline.
/// Color and shape change together, so status never rests on color alone.
/// </summary>
[TemplatePart(Name = "PART_Fill", Type = typeof(Ellipse))]
[TemplatePart(Name = "PART_Hole", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Bar", Type = typeof(FrameworkElement))]
public class UserMenuStatusDot : Control
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(UserMenuStatus), typeof(UserMenuStatusDot), new PropertyMetadata(UserMenuStatus.Available, (d, _) => ((UserMenuStatusDot)d).Update(true)));
    /// <summary>The surface behind the dot: it fills the hole and the bar, and draws the 2px ring when <see cref="ShowRing"/> is set.</summary>
    public static readonly DependencyProperty SurfaceProperty = DependencyProperty.Register(nameof(Surface), typeof(Brush), typeof(UserMenuStatusDot), new PropertyMetadata(null));
    public static readonly DependencyProperty ShowRingProperty = DependencyProperty.Register(nameof(ShowRing), typeof(bool), typeof(UserMenuStatusDot), new PropertyMetadata(false));
    // Follows the status tone through a resource reference, so a theme switch recolors the dot without a status change.
    private static readonly DependencyProperty ToneProperty = DependencyProperty.Register("Tone", typeof(Brush), typeof(UserMenuStatusDot), new PropertyMetadata(null, (d, _) => ((UserMenuStatusDot)d).UpdateColor()));

    public UserMenuStatus Status { get => (UserMenuStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public Brush? Surface { get => (Brush?)GetValue(SurfaceProperty); set => SetValue(SurfaceProperty, value); }
    public bool ShowRing { get => (bool)GetValue(ShowRingProperty); set => SetValue(ShowRingProperty, value); }

    private readonly SolidColorBrush _fill = new(Colors.Transparent);
    private readonly ScaleTransform _holeScale = new(0, 0), _barScale = new(0, 1);
    private readonly OwnedMotion _motion = new();
    private FrameworkElement? _hole, _bar;
    private bool _recolor;

    static UserMenuStatusDot()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(UserMenuStatusDot), new FrameworkPropertyMetadata(typeof(UserMenuStatusDot)));
        FocusableProperty.OverrideMetadata(typeof(UserMenuStatusDot), new FrameworkPropertyMetadata(false));
        IsHitTestVisibleProperty.OverrideMetadata(typeof(UserMenuStatusDot), new FrameworkPropertyMetadata(false));
    }

    public UserMenuStatusDot() => Update(false);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Fill") is Ellipse fill) fill.Fill = _fill;
        _hole = GetTemplateChild("PART_Hole") as FrameworkElement;
        _bar = GetTemplateChild("PART_Bar") as FrameworkElement;
        if (_hole is not null) { _hole.RenderTransformOrigin = new Point(.5, .5); _hole.RenderTransform = _holeScale; }
        if (_bar is not null) { _bar.RenderTransformOrigin = new Point(.5, .5); _bar.RenderTransform = _barScale; }
        Resize(RenderSize);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); Resize(sizeInfo.NewSize); }

    // .dot::before is a 46% hole; .dot::after a 58% × 22% bar, at least 1.5px tall.
    private void Resize(Size size)
    {
        double side = Math.Min(size.Width, size.Height);
        if (side <= 0) return;
        if (_hole is not null) _hole.Width = _hole.Height = side * .46;
        if (_bar is not null) { _bar.Width = side * .58; _bar.Height = Math.Max(side * .22, 1.5); }
    }

    // The color crossfades over 240ms while the hole opens and the bar slides in on --ease-spring (580ms).
    private void Update(bool animate)
    {
        animate &= IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        _recolor = animate;
        SetResourceReference(ToneProperty, Status switch
        {
            UserMenuStatus.Busy => "ArcWpf.Brush.Danger",
            UserMenuStatus.Away => "ArcWpf.Brush.Warning",
            UserMenuStatus.Offline => "ArcWpf.Brush.TextMuted",
            _ => "ArcWpf.Brush.Success"
        });
        _recolor = false;
        double hole = Status is UserMenuStatus.Away or UserMenuStatus.Offline ? 1 : 0;
        _motion.Double(_holeScale, ScaleTransform.ScaleXProperty, hole, .58, animate, spring: true);
        _motion.Double(_holeScale, ScaleTransform.ScaleYProperty, hole, .58, animate, spring: true);
        _motion.Double(_barScale, ScaleTransform.ScaleXProperty, Status == UserMenuStatus.Busy ? 1 : 0, .58, animate, spring: true);
    }

    private void UpdateColor()
    {
        if (GetValue(ToneProperty) is SolidColorBrush tone) _motion.Color(_fill, tone.Color, .24, _recolor);
    }
}
