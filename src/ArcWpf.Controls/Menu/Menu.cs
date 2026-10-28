using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public sealed class MenuItem
{
    public string Label { get; set; } = string.Empty;
    public object? Icon { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsDestructive { get; set; }
    public bool SeparatorBefore { get; set; }
    public string? InputGestureText { get; set; }
    public Visibility SeparatorVisibility => SeparatorBefore ? Visibility.Visible : Visibility.Collapsed;
}

[TemplatePart(Name = "PART_Trigger", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_ItemsHost", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_Highlight", Type = typeof(Border))]
[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_TriggerLabel", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Chevron", Type = typeof(FrameworkElement))]
public class Menu : Control
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Menu), new FrameworkPropertyMetadata("Actions"));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(object), typeof(Menu), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Menu), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<MenuItem>), typeof(Menu), new FrameworkPropertyMetadata(null));
    public static readonly RoutedEvent OpenedEvent = EventManager.RegisterRoutedEvent(nameof(Opened), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Menu));
    public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent(nameof(Closed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Menu));
    public static readonly RoutedEvent ItemInvokedEvent = EventManager.RegisterRoutedEvent(nameof(ItemInvoked), RoutingStrategy.Bubble, typeof(EventHandler<MenuItemInvokedEventArgs>), typeof(Menu));

    // The template trigger is a plain WPF Button; the bare name would mean ArcWpf.Controls.Button and never match.
    private System.Windows.Controls.Button? _trigger;
    private Popup? _popup;
    private ListBox? _itemsHost;
    private Border? _highlight;
    private Border? _surface;
    private TranslateTransform? _highlightMove;
    private ScaleTransform? _surfaceScale;
    private TranslateTransform? _triggerLabelMove;
    private FrameworkElement? _chevron;
    private readonly RotateTransform _chevronRotation = new();
    private readonly OwnedMotion _motion = new();
    private readonly OwnedMotion _popupMotion = new();
    private readonly List<(ListBoxItem Item, OwnedMotion Motion)> _itemMotions = [];
    private bool _presented;
    private bool _closing;
    private Window? _ownerWindow;

    // Hosts that embed the trigger (ButtonGroup, SplitButton) align the popup to their own outer edge:
    // AlignOffset is the host border between the trigger and that edge, SideOffset the gap below it.
    internal bool AlignToStart { get; set; }
    internal double AlignOffset { get; set; }
    internal double SideOffset { get; set; } = 6;
    // IsOpen flips before the exit animation ends; hosts follow it here rather than waiting for Closed.
    internal event EventHandler? OpenStateChanged;

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public ObservableCollection<MenuItem> Items
    {
        get => (ObservableCollection<MenuItem>?)GetValue(ItemsProperty) ?? throw new InvalidOperationException("Menu items are not initialized.");
        set => SetValue(ItemsProperty, value);
    }
    public event RoutedEventHandler Opened { add => AddHandler(OpenedEvent, value); remove => RemoveHandler(OpenedEvent, value); }
    public event RoutedEventHandler Closed { add => AddHandler(ClosedEvent, value); remove => RemoveHandler(ClosedEvent, value); }
    public event EventHandler<MenuItemInvokedEventArgs> ItemInvoked { add => AddHandler(ItemInvokedEvent, value); remove => RemoveHandler(ItemInvokedEvent, value); }

    static Menu()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Menu), new FrameworkPropertyMetadata(typeof(Menu)));
    }

    public Menu() => SetCurrentValue(ItemsProperty, new ObservableCollection<MenuItem>());

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _trigger = GetTemplateChild("PART_Trigger") as System.Windows.Controls.Button;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _itemsHost = GetTemplateChild("PART_ItemsHost") as ListBox;
        _highlight = GetTemplateChild("PART_Highlight") as Border;
        _surface = GetTemplateChild("PART_Surface") as Border;
        var triggerLabel = GetTemplateChild("PART_TriggerLabel") as TextBlock;
        if (triggerLabel is not null) { _triggerLabelMove = new TranslateTransform(0, 0); triggerLabel.RenderTransform = _triggerLabelMove; }
        _chevron = GetTemplateChild("PART_Chevron") as FrameworkElement;
        if (_chevron is not null) { _chevron.RenderTransformOrigin = new Point(.5, .5); _chevron.RenderTransform = _chevronRotation; }
        _highlightMove = new TranslateTransform();
        if (_highlight is not null)
        {
            // The highlight is sized and moved to the active row; stretch alignment would re-center it.
            _highlight.HorizontalAlignment = HorizontalAlignment.Left;
            _highlight.VerticalAlignment = VerticalAlignment.Top;
            _highlight.RenderTransform = _highlightMove;
        }
        if (_surface is not null) { _surfaceScale = new ScaleTransform(1, 1); _surface.RenderTransformOrigin = new Point(.5, 0); _surface.RenderTransform = _surfaceScale; }
        if (_trigger is not null)
        {
            _trigger.SetCurrentValue(Control.FocusVisualStyleProperty, null);
            _trigger.Click += TriggerClick;
        }
        if (_popup is not null) { _popup.Opened += PopupOpened; _popup.Closed += PopupClosed; _popup.PlacementTarget = _trigger; _popup.CustomPopupPlacementCallback = PlacePopup; }
        if (_itemsHost is not null) { _itemsHost.PreviewMouseLeftButtonUp += ItemsMouseUp; _itemsHost.PreviewKeyDown += ItemsKeyDown; _itemsHost.SelectionChanged += ItemsSelectionChanged; _itemsHost.PreviewMouseMove += ItemsMouseMove; }
        UpdateItems();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ItemsProperty) UpdateItems();
        if (e.Property == LabelProperty) AnimateTriggerLabel();
        if (e.Property == IsOpenProperty) AnimateChevron((bool)e.NewValue);
        if (e.Property == ArcMotion.LevelProperty && !IsOpen) ResetMotion();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled) return;
        if (!IsOpen && ReferenceEquals(e.OriginalSource, _trigger) && e.Key is Key.Enter or Key.Space or Key.Down)
        {
            e.Handled = true; SetCurrentValue(IsOpenProperty, true); return;
        }
        if (IsOpen && e.Key == Key.Escape) { e.Handled = true; CloseAndFocus(); }
    }

    private void UpdateItems()
    {
        if (_itemsHost is not null) _itemsHost.ItemsSource = Items;
    }

    private void TriggerClick(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsOpenProperty, !IsOpen);
        e.Handled = true;
    }

    private void PopupOpened(object? sender, EventArgs e)
    {
        SetCurrentValue(IsOpenProperty, true);
        _presented = true;
        _ownerWindow = Window.GetWindow(this);
        _ownerWindow?.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OwnerPreviewMouseDown), true);
        SelectFirstEnabled();
        _itemsHost?.UpdateLayout();
        _itemsHost?.Focus();
        UpdateHighlight(false);
        AnimatePopupIn();
        RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
    }

    private void PopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
        if (!_presented) return;
        _presented = false;
        _ownerWindow?.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OwnerPreviewMouseDown));
        _ownerWindow = null;
        ResetMotion();
        RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
        if (!ReferenceEquals(Keyboard.FocusedElement, _trigger)) _trigger?.Focus();
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var menu = (Menu)d;
        menu.OpenStateChanged?.Invoke(menu, EventArgs.Empty);
        if (menu._popup is null) return;
        if ((bool)e.NewValue) menu.CancelExit();
        else if (menu.BeginExit()) { menu._trigger?.Focus(); return; }
        menu._popup.IsOpen = (bool)e.NewValue;
        if (!(bool)e.NewValue) menu._trigger?.Focus();
    }

    // .menu[data-state="closed"]: 130ms fade to 0 with scale .985, ease-standard, no pointer input; Reduced keeps only a short fade.
    // The Popup stays open until the fade ends, and a reopen meanwhile reverses from the current values.
    private bool BeginExit()
    {
        if (!_presented || _surface is null || _surfaceScale is null || _popup is not { IsOpen: true } || !IsLoaded) return false;
        var level = ArcMotion.GetLevel(this);
        if (level == ArcMotionLevel.Off) return false;
        bool full = level == ArcMotionLevel.Full;
        _closing = true;
        _surface.IsHitTestVisible = false;
        _popupMotion.Double(_surface, UIElement.OpacityProperty, 0, full ? .13 : .08, true, completed: FinishExit);
        if (full)
        {
            _popupMotion.Double(_surfaceScale, ScaleTransform.ScaleXProperty, .985, .13, true);
            _popupMotion.Double(_surfaceScale, ScaleTransform.ScaleYProperty, .985, .13, true);
        }
        return true;
    }

    private void FinishExit()
    {
        if (!_closing) return;
        _closing = false;
        if (_surface is not null) _surface.IsHitTestVisible = true;
        if (!IsOpen && _popup is not null) _popup.IsOpen = false;
    }

    private void CancelExit()
    {
        if (!_closing || _surface is null || _surfaceScale is null) return;
        _closing = false;
        _surface.IsHitTestVisible = true;
        bool full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        _popupMotion.Double(_surface, UIElement.OpacityProperty, 1, full ? .14 : .08, true, enter: true);
        _popupMotion.Double(_surfaceScale, ScaleTransform.ScaleXProperty, 1, .22, full, spring: full);
        _popupMotion.Double(_surfaceScale, ScaleTransform.ScaleYProperty, 1, .22, full, spring: full);
    }

    private void ItemsMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (FindItem(e.OriginalSource as DependencyObject) is { } item) { Invoke(item); e.Handled = true; }
    }

    private void ItemsSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateHighlight(true);
    private void ItemsMouseMove(object sender, MouseEventArgs e)
    {
        if (_itemsHost is null || FindItem(e.OriginalSource as DependencyObject) is not { } item || !item.IsEnabled) return;
        var index = Items.IndexOf(item);
        if (index >= 0 && _itemsHost.SelectedIndex != index) _itemsHost.SelectedIndex = index;
        UpdateHighlight(true);
    }

    private void UpdateHighlight(bool animate)
    {
        if (_itemsHost is null || _highlight is null || _highlightMove is null || _itemsHost.SelectedIndex < 0) return;
        if (_itemsHost.ItemContainerGenerator.ContainerFromIndex(_itemsHost.SelectedIndex) is not ListBoxItem item) return;
        // Cover the item row only, not the separator above it, in the coordinates of the highlight's own panel.
        var row = item.Template?.FindName("PART_Row", item) as FrameworkElement ?? item;
        // Layout position only: the container's enter offset (-4 → 0) is still animating when the popup opens.
        var panel = VisualTreeHelper.GetParent(_highlight) as UIElement ?? _itemsHost;
        var itemsPanel = VisualTreeHelper.GetParent(item) as UIElement ?? _itemsHost;
        var origin = itemsPanel.TranslatePoint(new Point(0, 0), panel) + VisualTreeHelper.GetOffset(item) + (Vector)row.TranslatePoint(new Point(0, 0), item);
        _highlight.Width = row.ActualWidth;
        _highlight.Height = row.ActualHeight;
        _highlightMove.X = origin.X;
        _highlight.SetResourceReference(Border.BackgroundProperty, Items[_itemsHost.SelectedIndex].IsDestructive ? "ArcWpf.Brush.MenuDangerHighlight" : "ArcWpf.Brush.SurfaceMuted");
        var top = origin.Y;
        _highlight.Visibility = Visibility.Visible;
        _motion.Double(_highlightMove, TranslateTransform.YProperty, top, .22, animate && ArcMotion.GetLevel(this) == ArcMotionLevel.Full, spring: true);
    }

    private void AnimatePopupIn()
    {
        if (_surface is null || _surfaceScale is null) return;
        bool full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        _surface.Opacity = full ? 0 : 1;
        _surfaceScale.ScaleX = full ? .97 : 1;
        _surfaceScale.ScaleY = full ? .97 : 1;
        _popupMotion.Double(_surface, UIElement.OpacityProperty, 1, full ? .14 : .08, true, enter: full);
        _popupMotion.Double(_surfaceScale, ScaleTransform.ScaleXProperty, 1, .22, full, spring: full, enter: true);
        _popupMotion.Double(_surfaceScale, ScaleTransform.ScaleYProperty, 1, .22, full, spring: full, enter: true);
        if (_itemsHost is null) return;
        _itemsHost.UpdateLayout();
        _itemMotions.Clear();
        int index = 0;
        foreach (var item in Items)
        {
            if (_itemsHost.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container) continue;
            var move = new TranslateTransform(0, -4);
            container.RenderTransform = move;
            // Disabled rows rest at .45, as the container style sets; the fade must not lift them to 1.
            double rest = item.IsEnabled ? 1 : .45;
            container.Opacity = ArcMotion.GetLevel(this) == ArcMotionLevel.Off ? rest : 0;
            var motion = new OwnedMotion(); _itemMotions.Add((container, motion));
            bool animate = ArcMotion.GetLevel(this) != ArcMotionLevel.Off;
            motion.Double(container, UIElement.OpacityProperty, rest, animate ? .16 : 0, true, delay: animate && full ? Math.Min(index, 4) * .035 : 0, enter: true);
            motion.Double(move, TranslateTransform.YProperty, 0, animate && full ? .18 : 0, full, enter: true);
            index++;
        }
    }

    private void AnimateTriggerLabel()
    {
        var label = GetTemplateChild("PART_TriggerLabel") as TextBlock;
        if (label is null || _triggerLabelMove is null) return;
        bool full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        label.Opacity = full ? 0 : 1;
        _triggerLabelMove.Y = full ? 4 : 0;
        _popupMotion.Double(label, UIElement.OpacityProperty, 1, full ? .16 : .08, true, enter: true);
        _popupMotion.Double(_triggerLabelMove, TranslateTransform.YProperty, 0, full ? .18 : 0, full, enter: true);
    }

    private void AnimateChevron(bool open)
    {
        if (_chevron is null) return;
        bool full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full && IsLoaded && IsVisible;
        _motion.Double(_chevronRotation, RotateTransform.AngleProperty, open ? 180 : 0, .22, full, spring: full);
    }

    private void ResetMotion()
    {
        // A reset mid-exit (motion level change, template swap) must still close the popup it was fading.
        bool finishing = _closing;
        _closing = false;
        if (_surface is not null) _surface.IsHitTestVisible = true;
        _motion.Stop(); _popupMotion.Stop();
        foreach (var (_, motion) in _itemMotions) motion.Stop();
        _itemMotions.Clear();
        if (_surface is not null) { _surface.Opacity = 1; if (_surfaceScale is not null) { _surfaceScale.ScaleX = 1; _surfaceScale.ScaleY = 1; } }
        if (finishing && !IsOpen && _popup is not null) _popup.IsOpen = false;
    }

    private void ItemsKeyDown(object sender, KeyEventArgs e)
    {
        if (_itemsHost is null) return;
        if (e.Key is Key.Enter or Key.Space && _itemsHost.SelectedItem is MenuItem item) { Invoke(item); e.Handled = true; return; }
        if (e.Key is Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        {
            int next = e.Key switch
            {
                Key.Home or Key.PageUp => NextEnabled(0, 1),
                Key.End or Key.PageDown => NextEnabled(Items.Count - 1, -1),
                Key.Up => NextEnabled(_itemsHost.SelectedIndex - 1, -1),
                _ => NextEnabled(_itemsHost.SelectedIndex + 1, 1)
            };
            if (next >= 0) _itemsHost.SelectedIndex = next;
            e.Handled = true;
            return;
        }
        if (e.Key >= Key.A && e.Key <= Key.Z)
        {
            string prefix = e.Key.ToString();
            int start = Math.Max(0, _itemsHost.SelectedIndex + 1);
            int found = Enumerable.Range(0, Items.Count).Select(offset => (start + offset) % Items.Count).FirstOrDefault(index => Items[index].IsEnabled && Items[index].Label.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase));
            if (Items.Count > 0 && Items[found].IsEnabled && Items[found].Label.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase)) _itemsHost.SelectedIndex = found;
            e.Handled = true;
        }
    }

    private void Invoke(MenuItem item)
    {
        if (_closing || !item.IsEnabled || item.Command?.CanExecute(item.CommandParameter) == false) return;
        item.Command?.Execute(item.CommandParameter);
        RaiseEvent(new MenuItemInvokedEventArgs(ItemInvokedEvent, this, item));
        CloseAndFocus();
    }

    private void CloseAndFocus() => SetCurrentValue(IsOpenProperty, false);

    private void OwnerPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_presented || e.OriginalSource is not DependencyObject source) return;
        bool inMenu = source is Visual visual && (visual.IsDescendantOf(this) || (_popup?.Child is Visual popupChild && visual.IsDescendantOf(popupChild)));
        if (!inMenu) CloseAndFocus();
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
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[index].IsEnabled) return index;
            index = (index + direction + Items.Count) % Items.Count;
        }
        return -1;
    }

    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
    {
        double x = AlignToStart ? -AlignOffset : targetSize.Width - popupSize.Width + AlignOffset;
        double gap = AlignOffset + SideOffset;
        // Offsets are relative to the trigger's top-left: below first, flip above when the screen runs out.
        return [new CustomPopupPlacement(new Point(x, targetSize.Height + gap), PopupPrimaryAxis.None), new CustomPopupPlacement(new Point(x, -popupSize.Height - gap), PopupPrimaryAxis.None)];
    }

    private MenuItem? FindItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListBoxItem container) return container.DataContext as MenuItem;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void ReleaseTemplate()
    {
        if (_trigger is not null) _trigger.Click -= TriggerClick;
        if (_popup is not null) { _popup.Opened -= PopupOpened; _popup.Closed -= PopupClosed; _popup.CustomPopupPlacementCallback = null; }
        _ownerWindow?.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OwnerPreviewMouseDown));
        _ownerWindow = null;
        if (_itemsHost is not null) { _itemsHost.PreviewMouseLeftButtonUp -= ItemsMouseUp; _itemsHost.PreviewKeyDown -= ItemsKeyDown; _itemsHost.SelectionChanged -= ItemsSelectionChanged; _itemsHost.PreviewMouseMove -= ItemsMouseMove; }
        ResetMotion();
        _highlight = null; _highlightMove = null;
        _surface = null; _surfaceScale = null; _triggerLabelMove = null;
        _chevron = null;
        _trigger = null; _popup = null; _itemsHost = null;
    }
}

public sealed class MenuItemInvokedEventArgs(RoutedEvent routedEvent, object source, MenuItem item) : RoutedEventArgs(routedEvent, source)
{
    public MenuItem Item { get; } = item;
}

