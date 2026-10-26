using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public sealed class BreadcrumbItem : INotifyPropertyChanged
{
    private string _label = string.Empty;
    private ICommand? _command;
    private object? _commandParameter;
    private bool _isEnabled = true;
    private bool _isCurrent;
    private bool _showSeparator;

    public string Label { get => _label; set { if (_label == value) return; _label = value; OnPropertyChanged(nameof(Label)); } }
    public ICommand? Command { get => _command; set { if (ReferenceEquals(_command, value)) return; _command = value; OnPropertyChanged(nameof(Command)); OnPropertyChanged(nameof(IsInteractive)); } }
    public object? CommandParameter { get => _commandParameter; set { if (Equals(_commandParameter, value)) return; _commandParameter = value; OnPropertyChanged(nameof(CommandParameter)); } }
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); } }
    public bool IsCurrent { get => _isCurrent; internal set { if (_isCurrent == value) return; _isCurrent = value; OnPropertyChanged(nameof(IsCurrent)); } }
    public bool ShowSeparator { get => _showSeparator; internal set { if (_showSeparator == value) return; _showSeparator = value; OnPropertyChanged(nameof(ShowSeparator)); } }
    public bool IsInteractive => Command is not null;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

[TemplatePart(Name = "PART_ItemsHost", Type = typeof(ItemsControl))]
public class Breadcrumb : Control
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<BreadcrumbItem>), typeof(Breadcrumb), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Breadcrumb), new FrameworkPropertyMetadata("Breadcrumb"));
    public static readonly RoutedEvent ItemInvokedEvent = EventManager.RegisterRoutedEvent(nameof(ItemInvoked), RoutingStrategy.Bubble, typeof(EventHandler<BreadcrumbItemInvokedEventArgs>), typeof(Breadcrumb));

    private ItemsControl? _itemsHost;
    private readonly OwnedMotion _motion = new();

    public ObservableCollection<BreadcrumbItem> Items
    {
        get => (ObservableCollection<BreadcrumbItem>?)GetValue(ItemsProperty) ?? throw new InvalidOperationException("Breadcrumb items are not initialized.");
        set => SetValue(ItemsProperty, value);
    }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public event EventHandler<BreadcrumbItemInvokedEventArgs> ItemInvoked { add => AddHandler(ItemInvokedEvent, value); remove => RemoveHandler(ItemInvokedEvent, value); }

    static Breadcrumb() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Breadcrumb), new FrameworkPropertyMetadata(typeof(Breadcrumb)));

    public Breadcrumb()
    {
        SetCurrentValue(ItemsProperty, new ObservableCollection<BreadcrumbItem>());
        AutomationProperties.SetName(this, Label);
    }

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _itemsHost = GetTemplateChild("PART_ItemsHost") as ItemsControl;
        if (_itemsHost is not null)
        {
            _itemsHost.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(ItemClick), true);
            _itemsHost.ItemsSource = Items;
        }
        UpdateCurrent();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ItemsProperty) UpdateItemsSubscription(e.OldValue as ObservableCollection<BreadcrumbItem>, e.NewValue as ObservableCollection<BreadcrumbItem>);
        if (e.Property == LabelProperty) AutomationProperties.SetName(this, Label);
    }

    private void UpdateItemsSubscription(ObservableCollection<BreadcrumbItem>? oldItems, ObservableCollection<BreadcrumbItem>? newItems)
    {
        if (oldItems is not null) oldItems.CollectionChanged -= ItemsChanged;
        if (newItems is not null) newItems.CollectionChanged += ItemsChanged;
        if (_itemsHost is not null) _itemsHost.ItemsSource = newItems;
        UpdateCurrent();
    }

    private void ItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateCurrent();

    private void UpdateCurrent()
    {
        var items = Items;
        for (var index = 0; index < items.Count; index++) { items[index].IsCurrent = index == items.Count - 1; items[index].ShowSeparator = index > 0; }
        if (_itemsHost is not null) _itemsHost.ItemsSource = items;
    }

    private void ItemClick(object sender, RoutedEventArgs e)
    {
        if (FindItem(e.OriginalSource as DependencyObject) is not { } item || item.IsCurrent || !item.IsEnabled || item.Command?.CanExecute(item.CommandParameter) == false) return;
        item.Command?.Execute(item.CommandParameter);
        RaiseEvent(new BreadcrumbItemInvokedEventArgs(ItemInvokedEvent, this, item));
        e.Handled = true;
    }

    private static BreadcrumbItem? FindItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement element && element.DataContext is BreadcrumbItem item) return item;
            source = source is Visual visual ? System.Windows.Media.VisualTreeHelper.GetParent(visual) : null;
        }
        return null;
    }

    private void ReleaseTemplate()
    {
        if (_itemsHost is not null) _itemsHost.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(ItemClick));
        _motion.Stop();
        _itemsHost = null;
    }
}

public sealed class BreadcrumbItemInvokedEventArgs(RoutedEvent routedEvent, object source, BreadcrumbItem item) : RoutedEventArgs(routedEvent, source)
{
    public BreadcrumbItem Item { get; } = item;
}
