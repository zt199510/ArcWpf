using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_MainButton", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Menu", Type = typeof(Menu))]
[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
public class SplitButton : Control
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(SplitButton), new FrameworkPropertyMetadata("Save"));
    public static readonly DependencyProperty MenuLabelProperty = DependencyProperty.Register(nameof(MenuLabel), typeof(string), typeof(SplitButton), new FrameworkPropertyMetadata("More actions"));
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(SplitButton), new PropertyMetadata(null));
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(SplitButton), new PropertyMetadata(null));
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<MenuItem>), typeof(SplitButton), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(SplitButton), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SplitButton));
    public static readonly RoutedEvent ItemInvokedEvent = EventManager.RegisterRoutedEvent(nameof(ItemInvoked), RoutingStrategy.Bubble, typeof(EventHandler<MenuItemInvokedEventArgs>), typeof(SplitButton));

    private static readonly DependencyPropertyDescriptor IsPressedDescriptor = DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ButtonBase));
    private static readonly DependencyPropertyDescriptor IsMouseOverDescriptor = DependencyPropertyDescriptor.FromProperty(IsMouseOverProperty, typeof(UIElement));
    private const double Fast = .16, Instant = .12, Spring = .58;

    private System.Windows.Controls.Button? _mainButton;
    private System.Windows.Controls.Button? _trigger;
    private Menu? _menu;
    private Border? _surface;
    private readonly ScaleTransform _press = new(1, 1);
    private readonly SolidColorBrush _mainFill = new(Colors.Transparent);
    private readonly SolidColorBrush _triggerFill = new(Colors.Transparent);
    private readonly OwnedMotion _motion = new();

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string MenuLabel { get => (string)GetValue(MenuLabelProperty); set => SetValue(MenuLabelProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public ObservableCollection<MenuItem> Items { get => (ObservableCollection<MenuItem>?)GetValue(ItemsProperty) ?? throw new InvalidOperationException("SplitButton items are not initialized."); set => SetValue(ItemsProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public event RoutedEventHandler Click { add => AddHandler(ClickEvent, value); remove => RemoveHandler(ClickEvent, value); }
    public event EventHandler<MenuItemInvokedEventArgs> ItemInvoked { add => AddHandler(ItemInvokedEvent, value); remove => RemoveHandler(ItemInvokedEvent, value); }

    static SplitButton() => DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton), new FrameworkPropertyMetadata(typeof(SplitButton)));

    public SplitButton() => SetCurrentValue(ItemsProperty, new ObservableCollection<MenuItem>());

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _mainButton = GetTemplateChild("PART_MainButton") as System.Windows.Controls.Button;
        _menu = GetTemplateChild("PART_Menu") as Menu;
        _surface = GetTemplateChild("PART_Surface") as Border;
        if (_surface is not null) _surface.RenderTransform = _press;
        if (_mainButton is not null)
        {
            _mainButton.Click += MainButtonClick;
            AttachFill(_mainButton, _mainFill, left: true);
        }
        if (_menu is not null)
        {
            _menu.ApplyTemplate();
            _trigger = _menu.Template?.FindName("PART_Trigger", _menu) as System.Windows.Controls.Button;
            if (_trigger is not null) AttachFill(_trigger, _triggerFill, left: false);
            _menu.OpenStateChanged += MenuOpenStateChanged;
            // The popup's end edge meets the split's outer border, 4 below it (split-button.tsx align="end" sideOffset={4}).
            _menu.AlignOffset = 1;
            _menu.SideOffset = 4;
            _menu.ItemInvoked += MenuItemInvoked;
            _menu.SetCurrentValue(Menu.IsOpenProperty, IsOpen);
        }
        UpdateStates(false);
    }

    // The fill sits inside the 1px pill border, so its outer corners are the control radius less one (split-button.module.css).
    private void AttachFill(System.Windows.Controls.Button button, SolidColorBrush fill, bool left)
    {
        button.ApplyTemplate();
        if (button.Template?.FindName("Fill", button) is Border border)
        {
            border.Background = fill;
            var r = TryFindResource("ArcWpf.Radius.Control") is CornerRadius radius ? Math.Max(0, radius.TopLeft - 1) : 0;
            border.CornerRadius = left ? new CornerRadius(r, 0, 0, r) : new CornerRadius(0, r, r, 0);
        }
        IsPressedDescriptor.AddValueChanged(button, ButtonStateChanged);
        IsMouseOverDescriptor.AddValueChanged(button, ButtonStateChanged);
    }

    private void DetachFill(System.Windows.Controls.Button button)
    {
        IsPressedDescriptor.RemoveValueChanged(button, ButtonStateChanged);
        IsMouseOverDescriptor.RemoveValueChanged(button, ButtonStateChanged);
    }

    private void ButtonStateChanged(object? sender, EventArgs e) => UpdateStates(true);

    // Hover: foreground 88%; pressed or open menu: 80%. The whole pill dips to .97 only for the main action,
    // because the trigger anchors the menu and answers in colour alone.
    private void UpdateStates(bool animate)
    {
        var level = ArcMotion.GetLevel(this);
        bool full = animate && level == ArcMotionLevel.Full && IsLoaded && IsVisible;
        bool enabled = IsEnabled;
        SetFill(_mainFill, _mainButton is { IsPressed: true } && enabled, _mainButton is { IsMouseOver: true } && enabled, full);
        SetFill(_triggerFill, (_trigger is { IsPressed: true } || IsOpen) && enabled, _trigger is { IsMouseOver: true } && enabled, full);
        bool pressed = _mainButton is { IsPressed: true } && enabled && level == ArcMotionLevel.Full;
        _motion.Double(_press, ScaleTransform.ScaleXProperty, pressed ? .97 : 1, pressed ? Instant : Spring, full, spring: !pressed);
        _motion.Double(_press, ScaleTransform.ScaleYProperty, pressed ? .97 : 1, pressed ? Instant : Spring, full, spring: !pressed);
    }

    private void SetFill(SolidColorBrush fill, bool pressed, bool hover, bool animate)
    {
        var key = pressed ? "ArcWpf.Brush.SplitButtonPressed" : "ArcWpf.Brush.SplitButtonHover";
        var color = TryFindResource(key) is SolidColorBrush brush ? brush.Color : Colors.Transparent;
        // Rest fades the same hue to transparent so the pill's own fill shows through, even after a theme switch.
        if (!pressed && !hover) color.A = 0;
        _motion.Color(fill, color, pressed ? Instant : Fast, animate);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsOpenProperty && _menu is not null && _menu.IsOpen != IsOpen) _menu.SetCurrentValue(Menu.IsOpenProperty, IsOpen);
        if (e.Property == IsOpenProperty || e.Property == IsEnabledProperty) UpdateStates(true);
    }

    private void MainButtonClick(object sender, RoutedEventArgs e)
    {
        if (!IsEnabled || Command?.CanExecute(CommandParameter) == false) return;
        Command?.Execute(CommandParameter);
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        e.Handled = true;
    }

    private void MenuItemInvoked(object? sender, MenuItemInvokedEventArgs e)
    {
        RaiseEvent(new MenuItemInvokedEventArgs(ItemInvokedEvent, this, e.Item));
    }

    private void MenuOpenStateChanged(object? sender, EventArgs e)
    {
        if (_menu is not null) SetCurrentValue(IsOpenProperty, _menu.IsOpen);
    }

    private void ReleaseTemplate()
    {
        if (_mainButton is not null) { _mainButton.Click -= MainButtonClick; DetachFill(_mainButton); }
        if (_trigger is not null) DetachFill(_trigger);
        if (_menu is not null) { _menu.ItemInvoked -= MenuItemInvoked; _menu.OpenStateChanged -= MenuOpenStateChanged; }
        _motion.Stop();
        _press.ScaleX = _press.ScaleY = 1;
        _mainButton = null; _trigger = null; _menu = null; _surface = null;
    }
}
