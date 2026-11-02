using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum ToggleGroupType { Single, Multiple }
public enum ToggleGroupSize { Small, Medium }

/// <summary>
/// toggle-group.tsx: a flat row on the surface. Single mode glides one fill between options on the morph spring and behaves like a radio
/// group; multiple mode gives every option its own fill, and neighbours that are both on join across the gap.
/// </summary>
[TemplatePart(Name = "PART_Track", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Fill", Type = typeof(FrameworkElement))]
public class ToggleGroup : ListBox
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ToggleGroup), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(nameof(Type), typeof(ToggleGroupType), typeof(ToggleGroup), new FrameworkPropertyMetadata(ToggleGroupType.Single, OnTypeChanged));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(ToggleGroupSize), typeof(ToggleGroup), new FrameworkPropertyMetadata(ToggleGroupSize.Medium, (d, _) => ((ToggleGroup)d).Queue(false)));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(ToggleGroup), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ToggleGroup)d).ApplyValue((string?)e.NewValue)));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ToggleGroupType Type { get => (ToggleGroupType)GetValue(TypeProperty); set => SetValue(TypeProperty, value); }
    public ToggleGroupSize Size { get => (ToggleGroupSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The pressed option in single mode; the first pressed one in multiple mode, where <see cref="Values"/> is the whole set.</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public IReadOnlyList<string> Values => Containers().Where(c => c.IsSelected).Select(c => c.Value).ToArray();
    public event EventHandler? ValueChanged;

    private FrameworkElement? _track, _fill;
    private TranslateTransform? _fillMove;
    private readonly SpringDriver _driver;
    private readonly ControlSpring _x;
    private bool _fillPlaced, _syncing;
    private ToggleGroupItem? _tabStop;
    private DispatcherOperation? _pending;

    static ToggleGroup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleGroup), new FrameworkPropertyMetadata(typeof(ToggleGroup)));
        SelectionModeProperty.OverrideMetadata(typeof(ToggleGroup), new FrameworkPropertyMetadata(SelectionMode.Single));
    }
    public ToggleGroup()
    {
        _driver = new SpringDriver(() => { if (_fillMove is not null) _fillMove.X = _x!.Position; });
        _x = _driver.Add(0, Tween.Morph);
        SetCurrentValue(KeyboardNavigation.TabNavigationProperty, KeyboardNavigationMode.Local);
        Loaded += (_, _) => { if (Value is { } value && !Containers().Any(c => c.IsSelected)) ApplyValue(value); Queue(false); };
        Unloaded += (_, _) => _driver.Finish();
        SizeChanged += (_, _) => Queue(false);
        ItemContainerGenerator.StatusChanged += (_, _) => Queue(false);
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is ToggleGroupItem;
    protected override DependencyObject GetContainerForItemOverride() => new ToggleGroupItem();
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is not ToggleGroupItem container) return;
        container.SetBinding(ToggleGroupItem.SizeProperty, new Binding(nameof(Size)) { Source = this });
        if (string.IsNullOrEmpty(container.Value) && item is string text) container.Value = text;
    }
    internal IEnumerable<ToggleGroupItem> Containers() => Enumerable.Range(0, Items.Count).Select(i => ItemContainerGenerator.ContainerFromIndex(i)).OfType<ToggleGroupItem>();

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _track = GetTemplateChild("PART_Track") as FrameworkElement;
        _fill = GetTemplateChild("PART_Fill") as FrameworkElement;
        _fillMove = new TranslateTransform();
        if (_fill is not null) _fill.RenderTransform = _fillMove;
        _fillPlaced = false;
        Queue(false);
    }
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        Queue(false);
    }
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (!_syncing)
        {
            _syncing = true;
            SetCurrentValue(ValueProperty, Values.FirstOrDefault());
            _syncing = false;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        Queue(true);
    }

    private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var group = (ToggleGroup)d;
        group.SelectionMode = (ToggleGroupType)e.NewValue == ToggleGroupType.Multiple ? SelectionMode.Multiple : SelectionMode.Single;
        group.Queue(false);
    }

    private void ApplyValue(string? value)
    {
        if (_syncing) return;
        _syncing = true;
        foreach (var item in Containers()) item.IsSelected = value is not null && item.Value == value;
        _syncing = false;
        Queue(true);
    }

    internal void Press(ToggleGroupItem item)
    {
        if (!item.IsEnabled || !IsEnabled) return;
        // Single choice behaves like a radio group: choosing the pressed option again keeps it.
        if (Type == ToggleGroupType.Single) item.IsSelected = true;
        else item.IsSelected = !item.IsSelected;
    }

    internal void NoteFocus(ToggleGroupItem item)
    {
        _tabStop = item;
        UpdateTabStops();
    }

    private void UpdateTabStops()
    {
        var items = Containers().ToArray();
        // Roving tab stop: the last focused item, else the first pressed one, else the first item.
        var stop = _tabStop is { IsEnabled: true } && items.Contains(_tabStop) ? _tabStop : items.FirstOrDefault(i => i.IsSelected && i.IsEnabled) ?? items.FirstOrDefault(i => i.IsEnabled);
        foreach (var item in items) item.IsTabStop = ReferenceEquals(item, stop);
    }

    private void Queue(bool animate)
    {
        // Selection can change several times in one input; the last request wins, but an animated request is never downgraded.
        _queuedAnimate |= animate;
        _pending?.Abort();
        _pending = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            bool run = _queuedAnimate;
            _queuedAnimate = false;
            Update(run);
        }));
    }
    private bool _queuedAnimate;

    private void Update(bool animate)
    {
        UpdateTabStops();
        var items = Containers().ToArray();
        bool motion = animate && IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        bool multiple = Type == ToggleGroupType.Multiple;
        for (int i = 0; i < items.Length; i++)
        {
            bool next = multiple && items[i].IsSelected && i + 1 < items.Length && items[i + 1].IsSelected;
            bool previous = multiple && items[i].IsSelected && i > 0 && items[i - 1].IsSelected;
            items[i].UpdateFill(multiple && items[i].IsSelected, previous, next, motion);
        }
        UpdateGlidingFill(multiple ? null : items.FirstOrDefault(i => i.IsSelected), motion);
    }

    private void UpdateGlidingFill(ToggleGroupItem? on, bool motion)
    {
        if (_fill is null || _track is null) return;
        if (on is null || on.ActualWidth <= 0 || !_track.IsAncestorOf(on))
        {
            _fill.Visibility = Visibility.Collapsed;
            _fillPlaced = false;
            return;
        }
        double x = on.TransformToAncestor(_track).Transform(new Point()).X;
        _fill.Width = on.ActualWidth;
        _fill.Height = on.ActualHeight;
        _fill.Visibility = Visibility.Visible;
        if (motion && _fillPlaced) _driver.Animate(_x, x, Tween.Morph);
        else _driver.Snap(_x, x);
        _fillPlaced = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None) { base.OnPreviewKeyDown(e); return; }
        var valid = Containers().Where(i => i.IsEnabled).ToArray();
        var focused = Keyboard.FocusedElement as ToggleGroupItem;
        int current = focused is null ? -1 : Array.IndexOf(valid, focused);
        if (current >= 0 && e.Key is Key.Left or Key.Up or Key.Right or Key.Down or Key.Home or Key.End)
        {
            int next = e.Key == Key.Home ? 0 : e.Key == Key.End ? valid.Length - 1 : (current + (e.Key is Key.Right or Key.Down ? 1 : -1) + valid.Length) % valid.Length;
            valid[next].Focus();
            e.Handled = true;
            return;
        }
        if (current >= 0 && e.Key is Key.Space or Key.Enter)
        {
            Press(focused!);
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ToggleGroupPeer(this);
    private sealed class ToggleGroupPeer(ToggleGroup owner) : ListBoxAutomationPeer(owner)
    {
        protected override string GetNameCore() => string.IsNullOrEmpty(owner.Label) ? base.GetNameCore() : owner.Label;
    }
}

/// <summary>The row's layout: inline-flex with the source's 2 DIP gap.</summary>
public sealed class ToggleGroupPanel : Panel
{
    private const double Gap = 2;
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width + Math.Max(0, InternalChildren.Count - 1) * Gap, height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width + Gap;
        }
        return finalSize;
    }
}

[TemplatePart(Name = "PART_ItemFill", Type = typeof(Border))]
[TemplatePart(Name = "PART_Content", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Icon", Type = typeof(Decorator))]
public class ToggleGroupItem : ListBoxItem
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(ToggleGroupItem), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ToggleGroupItem), new FrameworkPropertyMetadata(string.Empty, OnLabelChanged));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ToggleGroupItem), new FrameworkPropertyMetadata(null, (d, _) => ((ToggleGroupItem)d).UpdateIcon()));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(ToggleGroupSize), typeof(ToggleGroupItem), new FrameworkPropertyMetadata(ToggleGroupSize.Medium, (d, _) => ((ToggleGroupItem)d).UpdateIcon()));
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>Accessible name and tooltip, required when the item shows only an icon.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Lucide-style path data on the 24-unit grid, drawn at 16 DIP (14 when small) with a 1.75 stroke.</summary>
    public string? Icon { get => (string?)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public ToggleGroupSize Size { get => (ToggleGroupSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private const double FillRadius = 15, Gap = 2;
    private static readonly DependencyProperty FillRightProperty = DependencyProperty.Register("FillRight", typeof(double), typeof(ToggleGroupItem), new PropertyMetadata(0d, OnFillShape));
    private static readonly DependencyProperty FillLeftRadiusProperty = DependencyProperty.Register("FillLeftRadius", typeof(double), typeof(ToggleGroupItem), new PropertyMetadata(FillRadius, OnFillShape));
    private static readonly DependencyProperty FillRightRadiusProperty = DependencyProperty.Register("FillRightRadius", typeof(double), typeof(ToggleGroupItem), new PropertyMetadata(FillRadius, OnFillShape));
    private Border? _fill;
    private FrameworkElement? _content;
    private Decorator? _icon;
    private readonly ScaleTransform _fillScale = new(.86, .86), _press = new(1, 1);

    static ToggleGroupItem() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleGroupItem), new FrameworkPropertyMetadata(typeof(ToggleGroupItem)));

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _fill = GetTemplateChild("PART_ItemFill") as Border;
        _content = GetTemplateChild("PART_Content") as FrameworkElement;
        _icon = GetTemplateChild("PART_Icon") as Decorator;
        if (_fill is not null) { _fill.RenderTransform = _fillScale; _fill.Opacity = 0; }
        if (_content is not null) _content.RenderTransform = _press;
        UpdateIcon();
        ApplyFillShape();
    }

    private void UpdateIcon()
    {
        if (_icon is null) return;
        _icon.Child = string.IsNullOrEmpty(Icon) ? null : Lucide.Glyph(Icon, Size == ToggleGroupSize.Small ? 14 : 16);
        _icon.Visibility = _icon.Child is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var item = (ToggleGroupItem)d;
        var label = (string)e.NewValue;
        AutomationProperties.SetName(item, label);
        item.ToolTip = string.IsNullOrEmpty(label) ? null : label;
    }

    private static void OnFillShape(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ToggleGroupItem)d).ApplyFillShape();
    private void ApplyFillShape()
    {
        if (_fill is null) return;
        _fill.Margin = new Thickness(0, 0, -(double)GetValue(FillRightProperty), 0);
        double left = (double)GetValue(FillLeftRadiusProperty), right = (double)GetValue(FillRightRadiusProperty);
        _fill.CornerRadius = new CornerRadius(left, right, right, left);
    }

    /// <summary>Multiple mode: the fill fades and settles in place, and a neighbour that is also on joins it across the gap.</summary>
    internal void UpdateFill(bool on, bool joinsPrevious, bool joinsNext, bool animate)
    {
        if (_fill is null) return;
        double scale = on ? 1 : .86, right = joinsNext ? Gap : 0, leftRadius = joinsPrevious ? 0 : FillRadius, rightRadius = joinsNext ? 0 : FillRadius;
        if (!animate)
        {
            Tween.Set(_fill, OpacityProperty, on ? 1 : 0);
            Tween.Set(_fillScale, ScaleTransform.ScaleXProperty, scale);
            Tween.Set(_fillScale, ScaleTransform.ScaleYProperty, scale);
            Tween.Set(this, FillRightProperty, right);
            Tween.Set(this, FillLeftRadiusProperty, leftRadius);
            Tween.Set(this, FillRightRadiusProperty, rightRadius);
            return;
        }
        Tween.To(_fill, OpacityProperty, on ? 1 : 0, Tween.Fast, ArcEasing.Standard);
        Tween.To(_fillScale, ScaleTransform.ScaleXProperty, scale, .36, Tween.SpringToken);
        Tween.To(_fillScale, ScaleTransform.ScaleYProperty, scale, .36, Tween.SpringToken);
        Tween.To(this, FillRightProperty, right, Tween.Standard, ArcEasing.Standard);
        Tween.To(this, FillLeftRadiusProperty, leftRadius, Tween.Standard, ArcEasing.Standard);
        Tween.To(this, FillRightRadiusProperty, rightRadius, Tween.Standard, ArcEasing.Standard);
    }

    private ToggleGroup? Group => ItemsControl.ItemsControlFromItemContainer(this) as ToggleGroup;

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Group?.NoteFocus(this);
    }

    // The label and icon give a small press; the item box never scales, so neighbours stay put.
    private void PressContent(bool pressed)
    {
        bool animate = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        double to = pressed ? .92 : 1;
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            if (!animate) Tween.Set(_press, property, to);
            else if (pressed) Tween.To(_press, property, to, .09, ArcEasing.Standard);
            else Tween.To(_press, property, to, .32, Tween.SpringToken);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsEnabled) return;
        Focus();
        CaptureMouse();
        PressContent(true);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured) return;
        bool inside = new Rect(RenderSize).Contains(e.GetPosition(this));
        ReleaseMouseCapture();
        if (inside) Group?.Press(this);
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        PressContent(false);
    }
}
