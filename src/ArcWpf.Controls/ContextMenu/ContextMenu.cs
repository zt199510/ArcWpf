using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_ItemsHost", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_Highlight", Type = typeof(Border))]
[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
public class ContextMenu : Control
{
    public static readonly DependencyProperty MenuProperty = DependencyProperty.RegisterAttached("Menu", typeof(ContextMenu), typeof(ContextMenu), new PropertyMetadata(null, OnMenuChanged));
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<MenuItem>), typeof(ContextMenu), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ContextMenu), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ContextMenu), new FrameworkPropertyMetadata("Context menu"));
    public static readonly RoutedEvent OpenedEvent = EventManager.RegisterRoutedEvent(nameof(Opened), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ContextMenu));
    public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent(nameof(Closed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ContextMenu));
    public static readonly RoutedEvent ItemInvokedEvent = EventManager.RegisterRoutedEvent(nameof(ItemInvoked), RoutingStrategy.Bubble, typeof(EventHandler<MenuItemInvokedEventArgs>), typeof(ContextMenu));

    private Popup? _popup;
    private ListBox? _itemsHost;
    private Border? _highlight;
    private Border? _surface;
    private TranslateTransform? _highlightMove;
    private DependencyObject? _owner;
    private IInputElement? _focusReturn;
    private Window? _ownerWindow;
    private ScaleTransform? _surfaceScale;
    private readonly OwnedMotion _motion = new();

    public ObservableCollection<MenuItem> Items { get => (ObservableCollection<MenuItem>?)GetValue(ItemsProperty) ?? throw new InvalidOperationException("ContextMenu items are not initialized."); set => SetValue(ItemsProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public event RoutedEventHandler Opened { add => AddHandler(OpenedEvent, value); remove => RemoveHandler(OpenedEvent, value); }
    public event RoutedEventHandler Closed { add => AddHandler(ClosedEvent, value); remove => RemoveHandler(ClosedEvent, value); }
    public event EventHandler<MenuItemInvokedEventArgs> ItemInvoked { add => AddHandler(ItemInvokedEvent, value); remove => RemoveHandler(ItemInvokedEvent, value); }

    static ContextMenu() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ContextMenu), new FrameworkPropertyMetadata(typeof(ContextMenu)));

    public ContextMenu() => SetCurrentValue(ItemsProperty, new ObservableCollection<MenuItem>());

    public static void SetMenu(DependencyObject element, ContextMenu? menu) => element.SetValue(MenuProperty, menu);
    public static ContextMenu? GetMenu(DependencyObject element) => (ContextMenu?)element.GetValue(MenuProperty);

    public void OpenFor(FrameworkElement owner)
    {
        _owner = owner;
        _focusReturn = Keyboard.FocusedElement;
        if (_popup is not null)
        {
            _popup.Placement = PlacementMode.Relative;
            _popup.PlacementTarget = owner;
            _popup.HorizontalOffset = 0;
            _popup.VerticalOffset = owner.ActualHeight;
        }
        SetCurrentValue(IsOpenProperty, true);
    }

    public void OpenAtMouse()
    {
        _owner = null;
        _focusReturn = Keyboard.FocusedElement;
        if (_popup is not null)
        {
            _popup.Placement = PlacementMode.MousePoint;
            _popup.PlacementTarget = null;
            _popup.HorizontalOffset = 0;
            _popup.VerticalOffset = 0;
        }
        SetCurrentValue(IsOpenProperty, true);
    }

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _itemsHost = GetTemplateChild("PART_ItemsHost") as ListBox;
        _highlight = GetTemplateChild("PART_Highlight") as Border;
        _surface = GetTemplateChild("PART_Surface") as Border;
        if (_highlight is not null)
        {
            _highlight.HorizontalAlignment = HorizontalAlignment.Left;
            _highlight.VerticalAlignment = VerticalAlignment.Top;
            _highlightMove = new TranslateTransform();
            _highlight.RenderTransform = _highlightMove;
        }
        if (_popup is not null) { _popup.Opened += PopupOpened; _popup.Closed += PopupClosed; }
        if (_surface is not null) { _surface.RenderTransformOrigin = new Point(.5, 0); _surfaceScale = new ScaleTransform(1, 1); _surface.RenderTransform = _surfaceScale; }
        if (_itemsHost is not null)
        {
            _itemsHost.ItemsSource = Items;
            _itemsHost.PreviewMouseLeftButtonUp += ItemsMouseUp;
            _itemsHost.PreviewKeyDown += ItemsKeyDown;
            _itemsHost.SelectionChanged += ItemsSelectionChanged;
            _itemsHost.PreviewMouseMove += ItemsMouseMove;
        }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ItemsProperty && _itemsHost is not null) _itemsHost.ItemsSource = Items;
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var menu = (ContextMenu)d;
        if (menu._popup is not null) menu._popup.IsOpen = (bool)e.NewValue;
        if (!(bool)e.NewValue) menu._focusReturn?.Focus();
    }

    private static void OnMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;
        if (e.OldValue is ContextMenu oldMenu) oldMenu.Detach(element);
        if (e.NewValue is ContextMenu newMenu) newMenu.Attach(element);
    }

    private void Attach(UIElement element)
    {
        element.PreviewMouseRightButtonUp += HostRightButtonUp;
        element.PreviewKeyDown += HostPreviewKeyDown;
    }

    private void Detach(UIElement element)
    {
        element.PreviewMouseRightButtonUp -= HostRightButtonUp;
        element.PreviewKeyDown -= HostPreviewKeyDown;
    }

    private void HostRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement owner)
        {
            OpenAtMouse();
            _owner = owner;
            e.Handled = true;
        }
    }

    private void HostPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || e.Key == Key.Apps) && sender is FrameworkElement owner)
        {
            OpenFor(owner); e.Handled = true;
        }
    }

    private void PopupOpened(object? sender, EventArgs e)
    {
        SetCurrentValue(IsOpenProperty, true);
        _ownerWindow = Window.GetWindow(_owner as DependencyObject ?? this);
        _ownerWindow?.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OwnerPreviewMouseDown), true);
        _ownerWindow?.AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OwnerPreviewMouseWheel), true);
        if (_ownerWindow is not null) _ownerWindow.SizeChanged += OwnerWindowSizeChanged;
        SelectFirstEnabled();
        _itemsHost?.Focus();
        UpdateHighlight();
        AnimateOpen();
        RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
    }

    private void PopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
        _ownerWindow?.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OwnerPreviewMouseDown));
        _ownerWindow?.RemoveHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OwnerPreviewMouseWheel));
        if (_ownerWindow is not null) _ownerWindow.SizeChanged -= OwnerWindowSizeChanged;
        _ownerWindow = null;
        _motion.Stop();
        RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
    }

    private void OwnerPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        bool inside = source is Visual visual && (_popup?.Child is Visual popupChild && visual.IsDescendantOf(popupChild));
        if (!inside) SetCurrentValue(IsOpenProperty, false);
    }

    private void OwnerPreviewMouseWheel(object sender, MouseWheelEventArgs e) => SetCurrentValue(IsOpenProperty, false);
    private void OwnerWindowSizeChanged(object? sender, SizeChangedEventArgs e) => SetCurrentValue(IsOpenProperty, false);

    private void AnimateOpen()
    {
        if (_surface is null || _surfaceScale is null) return;
        var level = ArcMotion.GetLevel(this);
        bool full = IsLoaded && IsVisible && level == ArcMotionLevel.Full;
        _surface.Opacity = full ? 0 : 1;
        _surfaceScale.ScaleX = full ? .97 : 1;
        _surfaceScale.ScaleY = full ? .97 : 1;
        _motion.Double(_surface, UIElement.OpacityProperty, 1, full ? .14 : .08, true, enter: full);
        _motion.Double(_surfaceScale, ScaleTransform.ScaleXProperty, 1, .22, full, spring: full, enter: true);
        _motion.Double(_surfaceScale, ScaleTransform.ScaleYProperty, 1, .22, full, spring: full, enter: true);
    }

    private void ItemsMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (FindItem(e.OriginalSource as DependencyObject) is { } item) { Invoke(item); e.Handled = true; }
    }

    private void ItemsMouseMove(object sender, MouseEventArgs e)
    {
        if (_itemsHost is null || FindItem(e.OriginalSource as DependencyObject) is not { } item || !item.IsEnabled) return;
        var index = Items.IndexOf(item); if (index >= 0) _itemsHost.SelectedIndex = index;
        UpdateHighlight();
    }

    private void ItemsSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateHighlight();

    private void ItemsKeyDown(object sender, KeyEventArgs e)
    {
        if (_itemsHost is null) return;
        if (e.Key is Key.Enter or Key.Space && _itemsHost.SelectedItem is MenuItem item) { Invoke(item); e.Handled = true; return; }
        if (e.Key == Key.Escape) { SetCurrentValue(IsOpenProperty, false); e.Handled = true; return; }
        if (e.Key is Key.Up or Key.Down or Key.Home or Key.End)
        {
            var next = e.Key switch { Key.Home => NextEnabled(0, 1), Key.End => NextEnabled(Items.Count - 1, -1), Key.Up => NextEnabled(_itemsHost.SelectedIndex - 1, -1), _ => NextEnabled(_itemsHost.SelectedIndex + 1, 1) };
            if (next >= 0) _itemsHost.SelectedIndex = next; e.Handled = true;
        }
    }

    private void UpdateHighlight()
    {
        if (_itemsHost is null || _highlight is null || _itemsHost.SelectedIndex < 0) return;
        if (_itemsHost.ItemContainerGenerator.ContainerFromIndex(_itemsHost.SelectedIndex) is not ListBoxItem item) return;
        var row = item.Template?.FindName("PART_Row", item) as FrameworkElement ?? item;
        _highlight.Width = row.ActualWidth; _highlight.Height = row.ActualHeight;
        _highlight.Visibility = Visibility.Visible;
        var panel = VisualTreeHelper.GetParent(_highlight) as UIElement ?? _itemsHost;
        var itemsPanel = VisualTreeHelper.GetParent(item) as UIElement ?? _itemsHost;
        var origin = itemsPanel.TranslatePoint(new Point(), panel) + VisualTreeHelper.GetOffset(item) + (Vector)row.TranslatePoint(new Point(), item);
        if (_highlightMove is not null)
        {
            _highlightMove.X = origin.X;
            _highlightMove.Y = origin.Y;
        }
    }

    private void Invoke(MenuItem item)
    {
        if (!item.IsEnabled || item.Command?.CanExecute(item.CommandParameter) == false) return;
        item.Command?.Execute(item.CommandParameter);
        RaiseEvent(new MenuItemInvokedEventArgs(ItemInvokedEvent, this, item));
        SetCurrentValue(IsOpenProperty, false);
    }

    private void SelectFirstEnabled()
    {
        if (_itemsHost is null) return;
        var index = Items.Select((item, index) => (item, index)).FirstOrDefault(pair => pair.item.IsEnabled).index;
        if (index >= 0) _itemsHost.SelectedIndex = index;
    }

    private int NextEnabled(int start, int direction)
    {
        if (Items.Count == 0) return -1;
        int index = (start % Items.Count + Items.Count) % Items.Count;
        for (int i = 0; i < Items.Count; i++) { if (Items[index].IsEnabled) return index; index = (index + direction + Items.Count) % Items.Count; }
        return -1;
    }

    private MenuItem? FindItem(DependencyObject? source)
    {
        while (source is not null) { if (source is ListBoxItem container) return container.DataContext as MenuItem; source = VisualTreeHelper.GetParent(source); }
        return null;
    }

    private void ReleaseTemplate()
    {
        if (_popup is not null) { _popup.Opened -= PopupOpened; _popup.Closed -= PopupClosed; }
        _motion.Stop();
        if (_itemsHost is not null) { _itemsHost.PreviewMouseLeftButtonUp -= ItemsMouseUp; _itemsHost.PreviewKeyDown -= ItemsKeyDown; _itemsHost.SelectionChanged -= ItemsSelectionChanged; _itemsHost.PreviewMouseMove -= ItemsMouseMove; }
        _popup = null; _itemsHost = null; _highlight = null; _surface = null; _surfaceScale = null; _highlightMove = null;
    }
}
