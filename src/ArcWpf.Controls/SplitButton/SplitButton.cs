using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_MainButton", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Menu", Type = typeof(Menu))]
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

    private System.Windows.Controls.Button? _mainButton;
    private Menu? _menu;

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
        if (_mainButton is not null) _mainButton.Click += MainButtonClick;
        if (_menu is not null)
        {
            _menu.ItemInvoked += MenuItemInvoked;
            _menu.Opened += MenuOpened;
            _menu.Closed += MenuClosed;
            _menu.SetCurrentValue(Menu.IsOpenProperty, IsOpen);
        }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsOpenProperty && _menu is not null && _menu.IsOpen != IsOpen) _menu.SetCurrentValue(Menu.IsOpenProperty, IsOpen);
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

    private void MenuOpened(object? sender, RoutedEventArgs e) => SetCurrentValue(IsOpenProperty, true);
    private void MenuClosed(object? sender, RoutedEventArgs e) => SetCurrentValue(IsOpenProperty, false);

    private void ReleaseTemplate()
    {
        if (_mainButton is not null) _mainButton.Click -= MainButtonClick;
        if (_menu is not null) { _menu.ItemInvoked -= MenuItemInvoked; _menu.Opened -= MenuOpened; _menu.Closed -= MenuClosed; }
        _mainButton = null; _menu = null;
    }
}
