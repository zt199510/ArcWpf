using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum ButtonGroupVariant { Outline, Solid }

/// <summary>
/// Related actions joined into one surface: a shared border, hairline dividers and one soft highlight that glides between
/// segments under the pointer or keyboard focus. MenuItems adds a trailing chevron segment that opens a Menu.
/// </summary>
[TemplatePart(Name = "PART_Scroll", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_Track", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Highlight", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_HighlightFill", Type = typeof(Border))]
[TemplatePart(Name = "PART_HighlightPressed", Type = typeof(Border))]
[TemplatePart(Name = "PART_Menu", Type = typeof(Menu))]
[TemplatePart(Name = "PART_Announcer", Type = typeof(TextBlock))]
public class ButtonGroup : ItemsControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ButtonGroup), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(ButtonGroupVariant), typeof(ButtonGroup),
        new FrameworkPropertyMetadata(ButtonGroupVariant.Outline, OnLayoutChanged), value => value is ButtonGroupVariant variant && Enum.IsDefined(variant));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(ControlSize), typeof(ButtonGroup),
        new FrameworkPropertyMetadata(ControlSize.Medium, OnLayoutChanged), value => value is ControlSize size && Enum.IsDefined(size));
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ButtonGroup),
        new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutChanged));
    public static readonly DependencyProperty CollapseLabelsProperty = DependencyProperty.Register(nameof(CollapseLabels), typeof(bool), typeof(ButtonGroup),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty MenuItemsProperty = DependencyProperty.Register(nameof(MenuItems), typeof(ObservableCollection<MenuItem>), typeof(ButtonGroup), new FrameworkPropertyMetadata(null, OnMenuItemsChanged));
    public static readonly DependencyProperty MenuLabelProperty = DependencyProperty.Register(nameof(MenuLabel), typeof(string), typeof(ButtonGroup), new FrameworkPropertyMetadata("More actions"));
    private static readonly DependencyPropertyKey IsCompactPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsCompact), typeof(bool), typeof(ButtonGroup), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsCompactProperty = IsCompactPropertyKey.DependencyProperty;

    /// <summary>Accessible name of the group, such as "Document actions".</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ButtonGroupVariant Variant { get => (ButtonGroupVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Small or Medium, matching the button heights. Large uses the Medium metrics; Arc has no large group.</summary>
    public ControlSize Size { get => (ControlSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>When a horizontal row no longer fits, segments with an icon drop their label and keep the icon.</summary>
    public bool CollapseLabels { get => (bool)GetValue(CollapseLabelsProperty); set => SetValue(CollapseLabelsProperty, value); }
    /// <summary>Non-empty adds a trailing chevron segment opening these actions, aligned to the group's end edge (start when vertical).</summary>
    public ObservableCollection<MenuItem> MenuItems { get => (ObservableCollection<MenuItem>)GetValue(MenuItemsProperty); set => SetValue(MenuItemsProperty, value); }
    /// <summary>Accessible name and tooltip of the chevron segment.</summary>
    public string MenuLabel { get => (string)GetValue(MenuLabelProperty); set => SetValue(MenuLabelProperty, value); }
    public bool IsCompact { get => (bool)GetValue(IsCompactProperty); private set => SetValue(IsCompactPropertyKey, value); }
    public event EventHandler<MenuItemInvokedEventArgs> MenuItemInvoked { add => AddHandler(Menu.ItemInvokedEvent, value); remove => RemoveHandler(Menu.ItemInvokedEvent, value); }

    private const double Fast = .16, Instant = .12, ChevronSpring = .58;

    private ScrollViewer? _scroll;
    private FrameworkElement? _track;
    private FrameworkElement? _highlight;
    private Border? _highlightFill;
    private Border? _highlightPressed;
    private Menu? _menu;
    private System.Windows.Controls.Button? _trigger;
    private Border? _triggerDivider;
    private FrameworkElement? _chevron;
    private FrameworkElement? _chevronInk;
    private System.Windows.Shapes.Shape? _chevronMuted;
    private TextBlock? _announcer;
    private readonly TranslateTransform _highlightMove = new();
    private readonly RotateTransform _chevronTurn = new();
    private readonly ControlSpring _x = new(0, .26), _y = new(0, .26), _width = new(0, .26), _height = new(0, .26);
    private readonly OwnedMotion _motion = new();
    private ObservableCollection<MenuItem>? _observedMenuItems;
    private FrameworkElement? _hover, _pressed, _focused, _shown;
    private bool _rendering;
    private long _lastFrame;
    private double _fullWidth;

    static ButtonGroup() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonGroup), new FrameworkPropertyMetadata(typeof(ButtonGroup)));

    public ButtonGroup()
    {
        SetCurrentValue(MenuItemsProperty, new ObservableCollection<MenuItem>());
        Unloaded += (_, _) => StopMotion();
        IsVisibleChanged += (_, _) => { if (!IsVisible) { StopMotion(); } };
        IsEnabledChanged += (_, _) => { _hover = _pressed = _focused = null; UpdateHighlight(false); };
        ItemContainerGenerator.StatusChanged += (_, _) => ApplyLayout();
        AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(FocusChanged), true);
        AddHandler(Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(FocusLost), true);
        AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler((_, _) => Release()), true);
        AddHandler(Menu.OpenedEvent, new RoutedEventHandler((_, _) => MenuStateChanged()));
        AddHandler(Menu.ClosedEvent, new RoutedEventHandler((_, _) => MenuStateChanged()));
    }

    private bool Vertical => Orientation == Orientation.Vertical;
    private bool MenuVisible => MenuItems is { Count: > 0 };
    private ArcMotionLevel Level => IsLoaded && IsVisible ? ArcMotion.GetLevel(this) : ArcMotionLevel.Off;

    protected override bool IsItemItsOwnContainerOverride(object item) => item is ButtonGroupItem;
    protected override DependencyObject GetContainerForItemOverride() => new ButtonGroupItem();

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ButtonGroupItem segment && !ReferenceEquals(segment, item) && item is string label) { segment.SetCurrentValue(ButtonGroupItem.LabelProperty, label); }
        ApplyLayout();
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        _hover = _pressed = _focused = null;
        ApplyLayout();
        UpdateHighlight(false);
    }

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _scroll = GetTemplateChild("PART_Scroll") as ScrollViewer;
        _track = GetTemplateChild("PART_Track") as FrameworkElement;
        _highlight = GetTemplateChild("PART_Highlight") as FrameworkElement;
        _highlightFill = GetTemplateChild("PART_HighlightFill") as Border;
        _highlightPressed = GetTemplateChild("PART_HighlightPressed") as Border;
        _menu = GetTemplateChild("PART_Menu") as Menu;
        _announcer = GetTemplateChild("PART_Announcer") as TextBlock;
        if (_highlight is not null) { _highlight.RenderTransform = _highlightMove; _highlight.Opacity = 0; }
        if (_track is not null) { _track.LayoutUpdated += TrackLayoutUpdated; }
        if (_scroll is not null) { _scroll.PreviewMouseWheel += Wheel; }
        if (_menu is not null)
        {
            _menu.ApplyTemplate();
            _trigger = _menu.Template?.FindName("PART_Trigger", _menu) as System.Windows.Controls.Button;
            if (_trigger is not null)
            {
                _trigger.ApplyTemplate();
                _triggerDivider = _trigger.Template?.FindName("PART_Divider", _trigger) as Border;
                _chevron = _trigger.Template?.FindName("PART_Chevron", _trigger) as FrameworkElement;
                _chevronInk = _trigger.Template?.FindName("PART_ChevronInk", _trigger) as FrameworkElement;
                _chevronMuted = _trigger.Template?.FindName("PART_ChevronMuted", _trigger) as System.Windows.Shapes.Shape;
                if (_chevron is not null) { _chevron.RenderTransformOrigin = new Point(.5, .5); _chevron.RenderTransform = _chevronTurn; }
            }
        }
        ApplyLayout();
        UpdateHighlight(false);
    }

    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        ReleaseTemplate();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    private void ReleaseTemplate()
    {
        StopMotion();
        if (_track is not null) { _track.LayoutUpdated -= TrackLayoutUpdated; }
        if (_scroll is not null) { _scroll.PreviewMouseWheel -= Wheel; }
        _scroll = null; _track = null; _highlight = null; _highlightFill = null; _highlightPressed = null;
        _menu = null; _trigger = null; _triggerDivider = null; _chevron = null; _chevronInk = null; _chevronMuted = null; _announcer = null;
        _shown = null;
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var group = (ButtonGroup)d;
        if (e.Property == OrientationProperty) { group._fullWidth = 0; group.IsCompact = false; }
        group.ApplyLayout();
        group.UpdateHighlight(false);
    }

    private static void OnMenuItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var group = (ButtonGroup)d;
        if (group._observedMenuItems is not null) { group._observedMenuItems.CollectionChanged -= group.MenuItemsCollectionChanged; }
        group._observedMenuItems = e.NewValue as ObservableCollection<MenuItem>;
        if (group._observedMenuItems is not null) { group._observedMenuItems.CollectionChanged += group.MenuItemsCollectionChanged; }
        group.ApplyLayout();
    }

    private void MenuItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ApplyLayout();

    internal IEnumerable<ButtonGroupItem> Segments() => Items.OfType<ButtonGroupItem>().Concat(
        Enumerable.Range(0, Items.Count).Select(i => ItemContainerGenerator.ContainerFromIndex(i)).OfType<ButtonGroupItem>()).Distinct();

    // Segments in visual order, the chevron last; only visible ones count.
    private List<FrameworkElement> Targets()
    {
        var list = new List<FrameworkElement>();
        for (int i = 0; i < Items.Count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is ButtonGroupItem { Visibility: Visibility.Visible } segment) { list.Add(segment); }
        }
        if (MenuVisible && _trigger is { IsVisible: true }) { list.Add(_trigger); }
        return list;
    }

    private void ApplyLayout()
    {
        bool small = Size == ControlSize.Small;
        // Arc: segments are the control height minus the border; padding 14/12, gap 8/6, divider inset 11/9 (10 vertical).
        double outer = small ? 36 : 44;
        var layout = new SegmentLayout(outer - 2, small ? 12 : 14, small ? 6 : 8, Vertical ? 10 : small ? 9 : 11, Vertical, IsCompact, true, Variant == ButtonGroupVariant.Solid);
        int index = 0;
        for (int i = 0; i < Items.Count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is not ButtonGroupItem segment) { continue; }
            segment.ApplyLayout(layout with { IsFirst = index == 0 });
            if (segment.Visibility == Visibility.Visible) { index++; }
        }
        if (_menu is not null)
        {
            _menu.Visibility = MenuVisible ? Visibility.Visible : Visibility.Collapsed;
            _menu.AlignToStart = Vertical;
            _menu.AlignOffset = 1;
        }
        if (_trigger is not null)
        {
            _trigger.Height = layout.Height;
            _trigger.Width = Vertical ? double.NaN : outer - 6;
        }
        if (_triggerDivider is not null)
        {
            _triggerDivider.Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
            _triggerDivider.SetResourceReference(Border.BackgroundProperty, layout.Solid ? "ArcWpf.Brush.ButtonGroupSolidDivider" : "ArcWpf.Brush.Border");
            _triggerDivider.Width = Vertical ? double.NaN : 1;
            _triggerDivider.Height = Vertical ? 1 : double.NaN;
            _triggerDivider.HorizontalAlignment = Vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            _triggerDivider.VerticalAlignment = Vertical ? VerticalAlignment.Top : VerticalAlignment.Stretch;
            _triggerDivider.Margin = Vertical ? new Thickness(layout.DividerInset, 0, layout.DividerInset, 0) : new Thickness(0, layout.DividerInset, 0, layout.DividerInset);
        }
        _chevronMuted?.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, layout.Solid ? "ArcWpf.Brush.ButtonGroupSolidMutedInk" : "ArcWpf.Brush.TextSecondary");
        _highlightFill?.SetResourceReference(Border.BackgroundProperty, layout.Solid ? "ArcWpf.Brush.ButtonGroupSolidHighlight" : "ArcWpf.Brush.SurfaceMuted");
        _highlightPressed?.SetResourceReference(Border.BackgroundProperty, layout.Solid ? "ArcWpf.Brush.ButtonGroupSolidHighlightPressed" : "ArcWpf.Brush.ButtonGroupHighlightPressed");
    }

    internal void InvalidateCollapse()
    {
        _fullWidth = 0;
        InvalidateMeasure();
    }

    // Labels collapse to icons when the row overflows, and come back once the container fits the full row again.
    protected override Size MeasureOverride(Size constraint)
    {
        bool collapsible = CollapseLabels && !Vertical && Items.OfType<ButtonGroupItem>().Any(item => item.IsCollapsible);
        if (!collapsible)
        {
            SetCompact(false);
            return base.MeasureOverride(constraint);
        }
        if (IsCompact && constraint.Width >= _fullWidth) { SetCompact(false); }
        if (!IsCompact && !double.IsInfinity(constraint.Width))
        {
            var full = base.MeasureOverride(new Size(double.PositiveInfinity, constraint.Height));
            if (full.Width > constraint.Width + 1)
            {
                _fullWidth = full.Width;
                SetCompact(true);
            }
        }
        return base.MeasureOverride(constraint);
    }

    private void SetCompact(bool compact)
    {
        if (IsCompact == compact) { return; }
        IsCompact = compact;
        ApplyLayout();
    }

    internal void Announce(string text)
    {
        if (_announcer is null) { return; }
        _announcer.Text = text;
        var peer = UIElementAutomationPeer.FromElement(_announcer) ?? UIElementAutomationPeer.CreatePeerForElement(_announcer);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    // The highlight sits on the pointer's segment, else the pressed one, else the open menu's chevron, else keyboard focus.
    private void UpdateHighlight(bool animate)
    {
        if (_highlight is null || _track is null) { return; }
        var level = Level;
        bool menuOpen = _menu?.IsOpen == true;
        var active = _hover ?? _pressed ?? (menuOpen ? _trigger : null) ?? _focused;
        var targets = Targets();
        if (active is not null && (!targets.Contains(active) || active.ActualWidth <= 0 || !active.IsDescendantOf(_track) || (!active.IsEnabled && !ReferenceEquals(active, _focused)))) { active = null; }
        if (active is null)
        {
            if (_shown is not null) { _motion.Double(_highlight, OpacityProperty, 0, level == ArcMotionLevel.Full ? Fast : Instant, animate && level != ArcMotionLevel.Off); }
            _shown = null;
            UpdateQuiet(targets, -1, animate);
            UpdatePressed(false, animate);
            return;
        }
        var box = active.TransformToAncestor(_track).TransformBounds(new Rect(active.RenderSize));
        if (_shown is null || !animate || level != ArcMotionLevel.Full)
        {
            // Arriving from nowhere, it appears in place; only moves between segments travel.
            StopFrames();
            _x.Snap(box.X); _y.Snap(box.Y); _width.Snap(box.Width); _height.Snap(box.Height);
            Draw();
        }
        else
        {
            _x.Target = box.X; _y.Target = box.Y; _width.Target = box.Width; _height.Target = box.Height;
            StartFrames();
        }
        _motion.Double(_highlight, OpacityProperty, 1, level == ArcMotionLevel.Full ? Fast : Instant, animate && level != ArcMotionLevel.Off);
        _shown = active;
        UpdateQuiet(targets, targets.IndexOf(active), animate);
        UpdatePressed(_pressed is not null && ReferenceEquals(_pressed, active), animate);
    }

    private void UpdatePressed(bool deep, bool animate)
    {
        if (_highlightPressed is null) { return; }
        _motion.Double(_highlightPressed, OpacityProperty, deep ? 1 : 0, deep ? Instant : Fast, animate && Level != ArcMotionLevel.Off);
    }

    private void UpdateQuiet(List<FrameworkElement> targets, int active, bool animate)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            bool quiet = active >= 0 && (i == active || i == active + 1);
            if (targets[i] is ButtonGroupItem segment) { segment.SetQuiet(quiet, animate); }
            else if (_triggerDivider is not null) { _motion.Double(_triggerDivider, OpacityProperty, quiet ? 0 : 1, Fast, animate && Level != ArcMotionLevel.Off); }
        }
    }

    // Segments resize when a label morphs or labels collapse; the highlight stays locked to its segment.
    private void TrackLayoutUpdated(object? sender, EventArgs e)
    {
        if (_shown is null || _track is null || !_shown.IsVisible || !_shown.IsDescendantOf(_track)) { return; }
        var box = _shown.TransformToAncestor(_track).TransformBounds(new Rect(_shown.RenderSize));
        if (Math.Abs(_x.Target - box.X) < .01 && Math.Abs(_y.Target - box.Y) < .01 && Math.Abs(_width.Target - box.Width) < .01 && Math.Abs(_height.Target - box.Height) < .01) { return; }
        if (_rendering) { _x.Target = box.X; _y.Target = box.Y; _width.Target = box.Width; _height.Target = box.Height; }
        else { _x.Snap(box.X); _y.Snap(box.Y); _width.Snap(box.Width); _height.Snap(box.Height); Draw(); }
    }

    private void Draw()
    {
        if (_highlight is null) { return; }
        _highlightMove.X = _x.Position;
        _highlightMove.Y = _y.Position;
        _highlight.Width = Math.Max(0, _width.Position);
        _highlight.Height = Math.Max(0, _height.Position);
    }

    private void StartFrames()
    {
        if (_rendering) { return; }
        _rendering = true;
        _lastFrame = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += Frame;
    }

    private void Frame(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double dt = (now - _lastFrame) / (double)Stopwatch.Frequency;
        _lastFrame = now;
        bool x = _x.Step(dt), y = _y.Step(dt), w = _width.Step(dt), h = _height.Step(dt);
        Draw();
        if (x && y && w && h) { StopFrames(); }
    }

    private void StopFrames()
    {
        if (!_rendering) { return; }
        CompositionTarget.Rendering -= Frame;
        _rendering = false;
    }

    private void StopMotion()
    {
        StopFrames();
        _motion.Stop();
        _hover = _pressed = _focused = _shown = null;
        if (_highlight is not null) { _highlight.Opacity = 0; }
        if (_highlightPressed is not null) { _highlightPressed.Opacity = 0; }
        if (_triggerDivider is not null) { _triggerDivider.Opacity = 1; }
        _chevronTurn.Angle = _menu?.IsOpen == true ? 180 : 0;
        if (_chevronInk is not null) { _chevronInk.Opacity = _menu?.IsOpen == true ? 1 : 0; }
        foreach (var segment in Segments()) { segment.StopMotion(); }
    }

    private FrameworkElement? Hit(MouseEventArgs e)
    {
        foreach (var target in Targets())
        {
            var point = e.GetPosition(target);
            if (point.X >= 0 && point.Y >= 0 && point.X < target.ActualWidth && point.Y < target.ActualHeight) { return target.IsEnabled ? target : null; }
        }
        return null;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        // Touch gets the highlight only while pressed.
        if (e.StylusDevice is not null) { return; }
        var hit = Hit(e);
        if (ReferenceEquals(hit, _hover)) { return; }
        _hover = hit;
        UpdateHighlight(true);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover is null) { return; }
        _hover = null;
        UpdateHighlight(true);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        _pressed = Hit(e);
        UpdateHighlight(true);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        Release();
    }

    private void Release()
    {
        if (_pressed is null) { return; }
        _pressed = null;
        UpdateHighlight(true);
    }

    // Keyboard focus shows the highlight; a mouse click focuses without it, so nothing stays lit after the pointer leaves.
    private void FocusChanged(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is not FrameworkElement target || !Targets().Contains(target)) { return; }
        var focused = InputManager.Current.MostRecentInputDevice is KeyboardDevice ? target : null;
        if (ReferenceEquals(focused, _focused)) { return; }
        _focused = focused;
        UpdateHighlight(true);
    }

    private void FocusLost(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_focused is null || (e.NewFocus is FrameworkElement next && Targets().Contains(next))) { return; }
        _focused = null;
        UpdateHighlight(true);
    }

    // Arrow keys along the orientation move between segments and wrap; Home and End jump. Tab visits every segment.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None) { return; }
        var list = Targets().Where(target => target.IsEnabled).ToList();
        int index = list.IndexOf(Keyboard.FocusedElement as FrameworkElement ?? this);
        if (index < 0) { return; }
        // ArrowDown on the chevron opens its menu, as the dropdown trigger does.
        if (ReferenceEquals(list[index], _trigger) && e.Key == Key.Down) { return; }
        bool rtl = !Vertical && FlowDirection == FlowDirection.RightToLeft;
        Key forward = Vertical ? Key.Down : rtl ? Key.Left : Key.Right;
        Key backward = Vertical ? Key.Up : rtl ? Key.Right : Key.Left;
        int last = list.Count - 1;
        int target = e.Key == forward ? (index == last ? 0 : index + 1)
            : e.Key == backward ? (index == 0 ? last : index - 1)
            : e.Key == Key.Home ? 0 : e.Key == Key.End ? last : -1;
        if (target < 0) { return; }
        e.Handled = true;
        list[target].Focus();
    }

    private void Wheel(object sender, MouseWheelEventArgs e)
    {
        if (_scroll is null || _scroll.ScrollableWidth <= 0) { return; }
        _scroll.ScrollToHorizontalOffset(_scroll.HorizontalOffset - e.Delta / 3d);
        e.Handled = true;
    }

    // The chevron turns over and takes the full ink while its menu is open.
    private void MenuStateChanged()
    {
        bool open = _menu?.IsOpen == true;
        var level = Level;
        _motion.Double(_chevronTurn, RotateTransform.AngleProperty, open ? 180 : 0, ChevronSpring, level == ArcMotionLevel.Full, spring: true);
        if (_chevronInk is not null) { _motion.Double(_chevronInk, OpacityProperty, open ? 1 : 0, Fast, level != ArcMotionLevel.Off); }
        UpdateHighlight(true);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty) { StopMotion(); }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonGroupAutomationPeer(this);

    private sealed class ButtonGroupAutomationPeer(ButtonGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(ButtonGroup);
        protected override string GetNameCore() => string.IsNullOrEmpty(base.GetNameCore()) ? owner.Label : base.GetNameCore();
    }
}
