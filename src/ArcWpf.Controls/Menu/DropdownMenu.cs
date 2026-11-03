using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

/// <summary>
/// Arc's dropdown menu on top of <see cref="Menu"/>'s popup, keyboard and focus logic. One highlight glides between items for the pointer and
/// jumps for the keyboard, nothing is highlighted until an item is reached, and the trigger label swaps while its width springs.
/// </summary>
public class DropdownMenu : Menu
{
    private const double MenuOffset = -5;
    private readonly DispatcherTimer _clear = new() { Interval = TimeSpan.FromMilliseconds(70) };
    private readonly TranslateTransform _surfaceMove = new();
    private readonly OwnedMotion _brushMotion = new();
    private ThemedBrush? _fill, _edge;
    private Grid? _labelHost;
    private TextBlock? _label;
    private TextBlock? _leaving;
    private SpringDriver? _widthDriver;
    private ControlSpring? _width;
    private string _shownLabel = string.Empty;
    private bool _keyboard;
    private bool _pointer;

    static DropdownMenu() => DefaultStyleKeyProperty.OverrideMetadata(typeof(DropdownMenu), new FrameworkPropertyMetadata(typeof(DropdownMenu)));

    public DropdownMenu()
    {
        _clear.Tick += (_, _) => { _clear.Stop(); if (_itemsHost is not null) { _itemsHost.SelectedIndex = -1; } };
        Unloaded += (_, _) => { _clear.Stop(); _widthDriver?.Finish(); _brushMotion.Stop(); };
        ThemeWatch.Add(this, () => RefreshTrigger(false));
    }

    internal bool HighlightVisibleForTest => _highlight is { Opacity: > 0 };
    internal int HighlightedIndexForTest => _itemsHost?.SelectedIndex ?? -1;
    internal double TriggerWidthForTest => _trigger?.ActualWidth ?? 0;
    internal void HoverItemForTest(int index) { _pointer = true; _clear.Stop(); if (_itemsHost is not null) { _itemsHost.SelectedIndex = index; } }
    internal void LeaveItemsForTest() => _clear.Start();

    // The popup carries room for the shadow (40 at the sides, 6 above, 56 below), so the surface itself sits 5.5 from the trigger with their end edges aligned.
    private protected override CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
    {
        const double side = 40, top = 6, bottom = 56, gap = 5.5;
        double x = targetSize.Width - popupSize.Width + side;
        return [new CustomPopupPlacement(new Point(x, targetSize.Height + gap - top), PopupPrimaryAxis.None), new CustomPopupPlacement(new Point(x, bottom - popupSize.Height - gap), PopupPrimaryAxis.None)];
    }

    public override void OnApplyTemplate()
    {
        if (_trigger is not null) { _trigger.MouseEnter -= OnTriggerState; _trigger.MouseLeave -= OnTriggerState; _trigger.PreviewMouseLeftButtonDown -= OnTriggerPress; _trigger.PreviewMouseLeftButtonUp -= OnTriggerState; }
        if (_itemsHost is not null) { _itemsHost.MouseLeave -= OnItemsLeave; }
        base.OnApplyTemplate();
        _labelHost = GetTemplateChild("PART_LabelHost") as Grid;
        _label = GetTemplateChild("PART_TriggerLabel") as TextBlock;
        if (_label is not null) { _shownLabel = Label; }
        if (GetTemplateChild("PART_TriggerIcon") is FrameworkElement icon) { icon.Visibility = Icon is null ? Visibility.Collapsed : Visibility.Visible; }
        if (_surface is not null && _surfaceScale is not null)
        {
            // Radix's origin for align="end": the trigger's corner.
            _surface.RenderTransformOrigin = new Point(1, 0);
            var transform = new TransformGroup();
            transform.Children.Add(_surfaceScale);
            transform.Children.Add(_surfaceMove);
            _surface.RenderTransform = transform;
        }
        if (_highlight is not null) { _highlight.Opacity = 0; }
        if (_itemsHost is not null) { _itemsHost.MouseLeave += OnItemsLeave; }
        if (_trigger is not null)
        {
            var motion = _brushMotion;
            _fill = new ThemedBrush(_trigger, motion);
            _edge = new ThemedBrush(_trigger, motion);
            _trigger.Background = _fill.Brush;
            _trigger.BorderBrush = _edge.Brush;
            _trigger.MouseEnter += OnTriggerState;
            _trigger.MouseLeave += OnTriggerState;
            _trigger.PreviewMouseLeftButtonDown += OnTriggerPress;
            _trigger.PreviewMouseLeftButtonUp += OnTriggerState;
            _trigger.FocusVisualStyle = ArcChromeButton.FocusFor(18);
            RefreshTrigger(false);
        }
    }

    private void OnTriggerState(object sender, EventArgs e) => RefreshTrigger(true);
    private void OnTriggerPress(object sender, MouseButtonEventArgs e) { _keyboard = false; RefreshTrigger(true, true); }
    private void OnItemsLeave(object sender, MouseEventArgs e) { if (_pointer) { _clear.Start(); } }

    private void RefreshTrigger(bool animate, bool pressed = false)
    {
        if (_trigger is null || _fill is null || _edge is null) { return; }
        bool open = IsOpen, hover = _trigger.IsMouseOver;
        if (pressed || _trigger.IsPressed) { _fill.To("DropdownTriggerPressed", animate, Tween.Instant); _edge.To("BorderStrong", animate); }
        else { _fill.To(open || hover ? "SurfaceMuted" : "Surface", animate, Tween.Instant); _edge.To(open || hover ? "BorderStrong" : "Border", animate); }
        if (hover && !open && !pressed && !_trigger.IsPressed) { _trigger.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Resting"); } else { _trigger.ClearValue(EffectProperty); }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!IsOpen && e.Key is Key.Enter or Key.Space or Key.Down) { _keyboard = true; }
        if (IsOpen) { _pointer = false; _clear.Stop(); }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsOpenProperty)
        {
            RefreshTrigger(IsLoaded);
            if ((bool)e.NewValue) { _clear.Stop(); _motion.Double(_surfaceMove, TranslateTransform.YProperty, 0, Tween.SpringTokenSeconds, IsLoaded && Tween.Level(this) == ArcMotionLevel.Full, spring: true); }
        }
        if (e.Property == IconProperty && GetTemplateChild("PART_TriggerIcon") is FrameworkElement icon) { icon.Visibility = e.NewValue is null ? Visibility.Collapsed : Visibility.Visible; }
    }

    // Radix focuses the first item for the keyboard and the content for the pointer: a pointer open starts with no highlight.
    private protected override void SelectFirstEnabled()
    {
        _pointer = !_keyboard;
        if (_keyboard) { base.SelectFirstEnabled(); }
        else if (_itemsHost is not null) { _itemsHost.SelectedIndex = -1; }
        _keyboard = false;
    }

    private protected override void ItemsMouseMove(object sender, MouseEventArgs e)
    {
        _pointer = true;
        if (_itemsHost is null) { return; }
        var item = FindItem(e.OriginalSource as DependencyObject);
        var container = item is null ? null : _itemsHost.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
        // The separator lives in the next item's container, but it is a gap: the grace period keeps the highlight gliding across it.
        var row = container?.Template?.FindName("PART_Row", container) as FrameworkElement;
        if (item is null || row is not { IsMouseOver: true } || !item.IsEnabled) { if (_itemsHost.SelectedIndex >= 0 && !_clear.IsEnabled) { _clear.Start(); } return; }
        _clear.Stop();
        int index = Items.IndexOf(item);
        if (index >= 0 && _itemsHost.SelectedIndex != index) { _itemsHost.SelectedIndex = index; }
    }

    private protected override void UpdateHighlight(bool animate)
    {
        if (_itemsHost is null || _highlight is null || _highlightMove is null) { return; }
        bool full = Tween.Level(this) == ArcMotionLevel.Full;
        if (_itemsHost.SelectedIndex < 0)
        {
            _motion.Double(_highlight, OpacityProperty, 0, full ? .08 : 0, animate && full);
            return;
        }
        if (_itemsHost.ItemContainerGenerator.ContainerFromIndex(_itemsHost.SelectedIndex) is not ListBoxItem item) { return; }
        var row = item.Template?.FindName("PART_Row", item) as FrameworkElement ?? item;
        var panel = VisualTreeHelper.GetParent(_highlight) as UIElement ?? _itemsHost;
        var itemsPanel = VisualTreeHelper.GetParent(item) as UIElement ?? _itemsHost;
        var origin = itemsPanel.TranslatePoint(new Point(0, 0), panel) + VisualTreeHelper.GetOffset(item) + (Vector)row.TranslatePoint(new Point(0, 0), item);
        bool wasShown = _highlight.Opacity > 0;
        _highlight.Width = row.ActualWidth;
        _highlight.Height = row.ActualHeight;
        _highlightMove.X = origin.X;
        _highlight.SetResourceReference(Border.BackgroundProperty, Items[_itemsHost.SelectedIndex].IsDestructive ? "ArcWpf.Brush.DropdownDangerHighlight" : "ArcWpf.Brush.SurfaceMuted");
        bool glide = animate && full && _pointer && wasShown;
        if (glide) { Tween.Spring(_highlightMove, TranslateTransform.YProperty, origin.Y, Tween.Snappy); } else { Tween.Set(_highlightMove, TranslateTransform.YProperty, origin.Y); }
        _motion.Double(_highlight, OpacityProperty, 1, full ? .08 : 0, animate && full);
    }

    // .menu @starting-style: offset toward the trigger, scale from the origin, fade fast; items follow with a capped stagger.
    private protected override void AnimatePopupIn()
    {
        if (_surface is null || _surfaceScale is null) { return; }
        bool full = Tween.Level(this) == ArcMotionLevel.Full;
        bool any = Tween.Level(this) != ArcMotionLevel.Off;
        _surface.Opacity = any ? 0 : 1;
        _surfaceMove.Y = full ? MenuOffset : 0;
        _surfaceScale.ScaleX = _surfaceScale.ScaleY = full ? .97 : 1;
        _popupMotion.Double(_surface, OpacityProperty, 1, Tween.Fast, any, enter: true);
        foreach (var (target, property, value) in new (DependencyObject, DependencyProperty, double)[] { (_surfaceScale, ScaleTransform.ScaleXProperty, 1), (_surfaceScale, ScaleTransform.ScaleYProperty, 1), (_surfaceMove, TranslateTransform.YProperty, 0) })
        {
            _popupMotion.Double(target, property, value, Tween.SpringTokenSeconds, full, spring: true);
        }
        if (_itemsHost is null) { return; }
        _itemsHost.UpdateLayout();
        foreach (var (_, old) in _itemMotions) { old.Stop(); }
        _itemMotions.Clear();
        int index = 0;
        foreach (var item in Items)
        {
            if (_itemsHost.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container) { continue; }
            var move = new TranslateTransform(0, full ? MenuOffset * .4 : 0);
            container.RenderTransform = move;
            double rest = item.IsEnabled ? 1 : .45;
            container.Opacity = full ? 0 : rest;
            var motion = new OwnedMotion();
            _itemMotions.Add((container, motion));
            double delay = Math.Min(index, 4) * .035;
            motion.Double(container, OpacityProperty, rest, Tween.Standard, full, delay: delay, enter: true);
            motion.Double(move, TranslateTransform.YProperty, 0, Tween.Standard, full, delay: delay, enter: true);
            index++;
        }
    }

    private protected override bool BeginExit()
    {
        _clear.Stop();
        bool started = base.BeginExit();
        if (started && Tween.Level(this) == ArcMotionLevel.Full) { _popupMotion.Double(_surfaceMove, TranslateTransform.YProperty, MenuOffset * .5, .13, true); }
        return started;
    }

    private protected override void AnimateChevron(bool open)
    {
        bool full = Tween.Level(this) == ArcMotionLevel.Full && IsLoaded && IsVisible;
        _motion.Double(_chevronRotation, RotateTransform.AngleProperty, open ? 180 : 0, Tween.SpringTokenSeconds, full, spring: full);
    }

    // TriggerLabel: the new label rises in while the old one leaves, and the width springs to the measured text.
    private protected override void AnimateTriggerLabel()
    {
        if (_label is null || _labelHost is null) { return; }
        string previous = _shownLabel;
        _shownLabel = Label;
        if (previous == Label) { return; }
        bool full = IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
        double from = _labelHost.ActualWidth;
        _label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double to = Math.Ceiling(_label.DesiredSize.Width);
        if (!full || from <= 0) { _widthDriver?.Stop(); _labelHost.ClearValue(WidthProperty); return; }
        if (_leaving is not null) { _labelHost.Children.Remove(_leaving); }
        _leaving = new TextBlock { Text = previous, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        _labelHost.Children.Add(_leaving);
        var leaving = _leaving;
        double em = FontSize * .3;
        Tween.Present(leaving, false, -em, 2, Tween.Fast, ArcEasing.Standard, done: () => { _labelHost?.Children.Remove(leaving); if (ReferenceEquals(_leaving, leaving)) { _leaving = null; } });
        Tween.Present(_label, true, em, 4, Tween.Standard, ArcEasing.Enter);
        _widthDriver ??= new SpringDriver(() => { if (_labelHost is not null && _width is not null) { _labelHost.Width = Math.Max(0, _width.Position); } }) { Settled = () => _labelHost?.ClearValue(WidthProperty) };
        _width ??= _widthDriver.Add(from, Tween.Morph);
        if (!_widthDriver.IsRunning) { _width.Snap(from); }
        _widthDriver.Animate(_width, to, Tween.Morph);
    }
}
