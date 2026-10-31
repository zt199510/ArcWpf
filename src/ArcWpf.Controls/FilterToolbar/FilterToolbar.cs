using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ArcWpf.Controls;

// filter-toolbar.tsx FilterToolbar (frozen 2026-10-31): removable chips, Clear all while any filter is applied, an
// optional Add filter menu and extra actions (Content). Each chip sits in a slot whose width springs open or shut, so
// neighbours and the toolbar follow; a chip that wraps glides from where it was, and the frame springs to new lines.
[TemplatePart(Name = "PART_Frame", Type = typeof(Border))]
[TemplatePart(Name = "PART_Chips", Type = typeof(WrapPanel))]
[TemplatePart(Name = "PART_Empty", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_Menu", Type = typeof(FilterMenu))]
[TemplatePart(Name = "PART_ClearSlot", Type = typeof(Border))]
[TemplatePart(Name = "PART_Clear", Type = typeof(System.Windows.Controls.Primitives.ButtonBase))]
[TemplatePart(Name = "PART_Status", Type = typeof(TextBlock))]
public class FilterToolbar : ContentControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(FilterToolbar), new FrameworkPropertyMetadata("Active filters"));
    public static readonly DependencyProperty AddFilterLabelProperty = DependencyProperty.Register(nameof(AddFilterLabel), typeof(string), typeof(FilterToolbar), new FrameworkPropertyMetadata("Add filter"));
    public static readonly DependencyProperty AddFilterAlignmentProperty = DependencyProperty.Register(nameof(AddFilterAlignment), typeof(FilterMenuAlignment), typeof(FilterToolbar), new FrameworkPropertyMetadata(FilterMenuAlignment.End));
    public static readonly RoutedEvent FilterRemovedEvent = EventManager.RegisterRoutedEvent(nameof(FilterRemoved), RoutingStrategy.Bubble, typeof(EventHandler<FilterChipEventArgs>), typeof(FilterToolbar));
    public static readonly RoutedEvent FilterAddedEvent = EventManager.RegisterRoutedEvent(nameof(FilterAdded), RoutingStrategy.Bubble, typeof(EventHandler<FilterChipEventArgs>), typeof(FilterToolbar));
    public static readonly RoutedEvent FiltersClearedEvent = EventManager.RegisterRoutedEvent(nameof(FiltersCleared), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilterToolbar));

    /// <summary>Accessible name of the toolbar group.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string AddFilterLabel { get => (string)GetValue(AddFilterLabelProperty); set => SetValue(AddFilterLabelProperty, value); }
    public FilterMenuAlignment AddFilterAlignment { get => (FilterMenuAlignment)GetValue(AddFilterAlignmentProperty); set => SetValue(AddFilterAlignmentProperty, value); }
    /// <summary>Applied filters, shown as chips. The toolbar edits this collection when a chip is removed, cleared or added.</summary>
    public ObservableCollection<FilterChip> Filters { get; } = [];
    /// <summary>Fields for the Add filter menu; the trigger shows while any field exists.</summary>
    public ObservableCollection<FilterField> Fields => _menu.Fields;
    public event EventHandler<FilterChipEventArgs> FilterRemoved { add => AddHandler(FilterRemovedEvent, value); remove => RemoveHandler(FilterRemovedEvent, value); }
    public event EventHandler<FilterChipEventArgs> FilterAdded { add => AddHandler(FilterAddedEvent, value); remove => RemoveHandler(FilterAddedEvent, value); }
    public event RoutedEventHandler FiltersCleared { add => AddHandler(FiltersClearedEvent, value); remove => RemoveHandler(FiltersClearedEvent, value); }

    private const double SMOOTH = .4, SNAPPY = .26, CHIP_GAP = 8, CLEAR_GAP = 12, ARMED = .9;
    private readonly FilterMenu _menu = new();
    private readonly Dictionary<string, Chip> _chips = [];
    private readonly SpringDriver _frameDriver;
    private readonly ControlSpring _frameHeight;
    private List<FilterChip> _seen = [];
    private Border? _frame, _clearSlot, _menuHost;
    private WrapPanel? _panel;
    private Canvas? _empty;
    private TextBlock? _emptyText, _status;
    private System.Windows.Controls.Primitives.ButtonBase? _clear;
    private Slot? _clearMotion;
    private DateTime _armedUntil;
    private int? _pendingFocus;

    static FilterToolbar() { DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterToolbar), new FrameworkPropertyMetadata(typeof(FilterToolbar))); }

    public FilterToolbar()
    {
        _frameDriver = new SpringDriver(() => { if (_frame is not null) _frame.Height = Math.Max(0, _frameHeight!.Position); }) { Settled = () => { if (_frame is not null) _frame.Height = double.NaN; } };
        _frameHeight = _frameDriver.Add(0, SMOOTH);
        Filters.CollectionChanged += FiltersChanged;
        _menu.FilterSelected += MenuSelected;
        _menu.Active = Filters;
        Fields.CollectionChanged += (_, _) => _menu.Visibility = Fields.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _menu.Visibility = Visibility.Collapsed;
        Unloaded += (_, _) => { _frameDriver.Finish(); foreach (var chip in _chips.Values) chip.Slot.Finish(); };
    }

    private ArcMotionLevel Level => ArcMotion.GetLevel(this);
    private bool Armed => DateTime.UtcNow < _armedUntil;

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == AddFilterLabelProperty) _menu.Label = (string)e.NewValue;
        else if (e.Property == AddFilterAlignmentProperty) _menu.Alignment = (FilterMenuAlignment)e.NewValue;
    }

    public override void OnApplyTemplate()
    {
        if (_panel is not null) { _panel.SizeChanged -= PanelResized; _panel.LayoutUpdated -= Reflow; _panel.Children.Clear(); }
        if (_clear is not null) _clear.Click -= ClearClick;
        if (_menuHost is not null) _menuHost.Child = null;
        base.OnApplyTemplate();
        _frame = GetTemplateChild("PART_Frame") as Border;
        _panel = GetTemplateChild("PART_Chips") as WrapPanel;
        _empty = GetTemplateChild("PART_Empty") as Canvas;
        _menuHost = GetTemplateChild("PART_MenuHost") as Border;
        _clearSlot = GetTemplateChild("PART_ClearSlot") as Border;
        _clear = GetTemplateChild("PART_Clear") as System.Windows.Controls.Primitives.ButtonBase;
        _status = GetTemplateChild("PART_Status") as TextBlock;
        if (_menuHost is not null) _menuHost.Child = _menu;
        if (_panel is not null) { _panel.SizeChanged += PanelResized; _panel.LayoutUpdated += Reflow; }
        if (_clear is not null) _clear.Click += ClearClick;
        if (_clear?.Content is TextBlock clearText) clearText.RenderTransform = new TranslateTransform();
        _clearMotion = _clearSlot is not null && _clear is not null ? new Slot(_clearSlot, _clear, CLEAR_GAP, true) : null;
        _chips.Clear();
        _emptyText = null;
        Sync(false);
    }

    private void FiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var before = _seen;
        _seen = [.. Filters];
        if (Describe(before, _seen) is { Length: > 0 } message) Announce(message);
        // Layout motion runs only for a moment after the filters change, so resizes settle at once.
        _armedUntil = DateTime.UtcNow.AddSeconds(ARMED);
        Sync(IsLoaded);
        FocusAfterRemoval();
    }

    private void Sync(bool animate)
    {
        if (_panel is null) { _seen = [.. Filters]; return; }
        if (!animate) _seen = [.. Filters];
        bool motion = animate && Level != ArcMotionLevel.Off;
        bool full = motion && Level == ArcMotionLevel.Full;
        var ids = Filters.Select(filter => filter.Id).ToHashSet();
        foreach (var (id, chip) in _chips.Where(pair => !ids.Contains(pair.Key)).ToList())
        {
            _chips.Remove(id);
            chip.Leave(full, () => _panel.Children.Remove(chip.Slot.Host));
        }
        var live = new List<UIElement>();
        foreach (var filter in Filters)
        {
            if (_chips.TryGetValue(filter.Id, out var chip)) chip.Update(filter, motion, full);
            else
            {
                chip = new Chip(this, filter);
                _chips[filter.Id] = chip;
                chip.Enter(full);
            }
            live.Add(chip.Slot.Host);
        }
        // Leaving slots stay where they were while they collapse; live slots follow the filter order.
        var leaving = _panel.Children.Cast<UIElement>().Select((element, index) => (element, index)).Where(pair => !live.Contains(pair.element)).ToList();
        var order = new List<UIElement>(live);
        foreach (var (element, index) in leaving) order.Insert(Math.Min(index, order.Count), element);
        if (!_panel.Children.Cast<UIElement>().SequenceEqual(order))
        {
            var focused = Keyboard.FocusedElement as IInputElement;
            _panel.Children.Clear();
            foreach (var element in order) _panel.Children.Add(element);
            if (focused is UIElement { IsVisible: true } element2) element2.Focus();
        }
        _clearMotion?.Show(Filters.Count > 0, full, motion);
        if (Filters.Count > 0 && _clear?.Content is FrameworkElement clearText && full && !_clearMotion!.WasShown) FilterMenu.TextIn(clearText, 14, true);
        UpdateEmpty(Filters.Count == 0, motion, full);
    }

    // The empty note sits over the leading edge, out of flow; it waits for leaving chips to fade before it rises in.
    private void UpdateEmpty(bool show, bool motion, bool full)
    {
        if (_empty is null) return;
        if (show == (_emptyText is not null)) return;
        if (show)
        {
            _emptyText = new TextBlock { Text = "No filters applied", FontSize = 14, RenderTransform = new TranslateTransform(), IsHitTestVisible = false };
            _emptyText.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            _emptyText.Loaded += (s, _) => { if (s is TextBlock text) Canvas.SetTop(text, Math.Max(0, (36 - text.ActualHeight) / 2)); };
            _empty.Children.Add(_emptyText);
            if (motion) FilterMenu.TextIn(_emptyText, 14, full, full ? .12 * .75 : 0);
        }
        else
        {
            var old = _emptyText!;
            _emptyText = null;
            if (motion) FilterMenu.TextOut(old, 14, full, () => _empty.Children.Remove(old));
            else _empty.Children.Remove(old);
        }
    }

    // The frame follows the chip list height: a spring while the filters just changed, at once otherwise.
    private void PanelResized(object sender, SizeChangedEventArgs e)
    {
        if (_frame is null || !e.HeightChanged) return;
        double next = Math.Max(0, e.NewSize.Height - CHIP_GAP);
        if (!Armed || Level != ArcMotionLevel.Full || !IsLoaded || e.PreviousSize.Height == 0)
        {
            if (!_frameDriver.IsRunning) _frame.Height = double.NaN;
            return;
        }
        if (!_frameDriver.IsRunning) _frameHeight.Snap(Math.Max(0, e.PreviousSize.Height - CHIP_GAP));
        _frame.Height = _frameHeight.Position;
        _frameDriver.Animate(_frameHeight, next, SMOOTH);
    }

    // Chips glide with their slot widths, but one that wraps to another line would teleport: each jump is offset in the
    // same frame and springs back to zero from where it was seen.
    private void Reflow(object? sender, EventArgs e)
    {
        if (_panel is null) return;
        bool armed = Armed && Level == ArcMotionLevel.Full;
        foreach (UIElement child in _panel.Children)
        {
            if (child is not FrameworkElement host || host.Tag is not Slot slot) continue;
            var offset = VisualTreeHelper.GetOffset(host);
            var at = new Point(offset.X, offset.Y);
            if (slot.Seen is { } seen)
            {
                double jx = seen.X - at.X, jy = seen.Y - at.Y;
                if (armed && (Math.Abs(jy) >= 1 || Math.Abs(jx) >= 40)) slot.Glide(jx, jy);
            }
            slot.Seen = at;
        }
    }

    private void MenuSelected(object? sender, FilterChipEventArgs e)
    {
        e.Handled = true;
        // A second pick for a field replaces the first; a new field takes its place in the fields' order.
        int existing = Filters.ToList().FindIndex(filter => filter.Id == e.Filter.Id);
        if (existing >= 0) Filters[existing] = e.Filter;
        else
        {
            int rank = Rank(e.Filter.Id);
            int index = Filters.Select((filter, i) => (filter, i)).FirstOrDefault(pair => Rank(pair.filter.Id) > rank, (null!, Filters.Count)).Item2;
            Filters.Insert(index, e.Filter);
        }
        RaiseEvent(new FilterChipEventArgs(FilterAddedEvent, this, e.Filter, e.Field));
    }

    private int Rank(string id) { int index = Fields.ToList().FindIndex(field => field.Id == id); return index < 0 ? int.MaxValue : index; }

    internal void Remove(FilterChip filter, bool focused)
    {
        int index = Filters.IndexOf(filter);
        if (index < 0) return;
        if (focused) _pendingFocus = index;
        Filters.RemoveAt(index);
        RaiseEvent(new FilterChipEventArgs(FilterRemovedEvent, this, filter, null));
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        _pendingFocus = -1;
        Filters.Clear();
        RaiseEvent(new RoutedEventArgs(FiltersClearedEvent, this));
    }

    // A removed chip hands focus to its neighbour, or to the Add filter trigger once the list is empty.
    private void FocusAfterRemoval()
    {
        if (_pendingFocus is not { } index) return;
        _pendingFocus = null;
        if (Filters.Count > 0 && index >= 0 && _chips.TryGetValue(Filters[Math.Min(index, Filters.Count - 1)].Id, out var chip)) chip.Remove.Focus();
        else if (_menu.Template?.FindName("PART_Trigger", _menu) is UIElement trigger && _menu.IsVisible) trigger.Focus();
    }

    private void Announce(string message)
    {
        if (_status is null) return;
        _status.Text = message;
        UIElementAutomationPeer.FromElement(_status)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    /// <summary>The live-region text for a change: additions, value changes and removals, or "All filters cleared".</summary>
    internal static string Describe(IReadOnlyList<FilterChip> before, IReadOnlyList<FilterChip> after)
    {
        static string Text(FilterChip filter) => filter.Value is { Length: > 0 } ? $"{filter.Label}: {filter.Value}" : filter.Label;
        var removed = before.Where(old => after.All(filter => filter.Id != old.Id)).ToList();
        if (after.Count == 0 && removed.Count > 1) return "All filters cleared";
        var added = after.Where(filter => before.All(old => old.Id != filter.Id));
        var changed = after.Where(filter => before.Any(old => old.Id == filter.Id && old.Value != filter.Value));
        return string.Join(". ", added.Select(filter => $"Added {Text(filter)}").Concat(changed.Select(filter => $"{filter.Label} changed to {filter.Value ?? "any"}")).Concat(removed.Select(filter => $"Removed {Text(filter)}")));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FilterToolbarPeer(this);

    private sealed class FilterToolbarPeer(FilterToolbar owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.Label;
    }

    // A slot whose width grows from zero (with its gap) or collapses on the smooth spring. While it moves it clips and
    // the content keeps its natural width; at rest it returns to auto width.
    internal sealed class Slot
    {
        public FrameworkElement Host { get; }
        public Point? Seen { get; set; }
        public bool WasShown { get; private set; }
        private readonly FrameworkElement _content;
        private readonly double _gap;
        private readonly bool _leadingGap;
        private readonly SpringDriver _driver;
        private readonly ControlSpring _open, _x, _y;
        private readonly TranslateTransform _move = new();
        private Action? _closed;
        private double _width;

        public Slot(FrameworkElement host, FrameworkElement content, double gap, bool leadingGap)
        {
            Host = host;
            _content = content;
            _gap = gap;
            _leadingGap = leadingGap;
            host.Tag = this;
            host.RenderTransform = _move;
            _driver = new SpringDriver(Apply) { Settled = Settle };
            _open = _driver.Add(1, SMOOTH);
            _x = _driver.Add(0, SMOOTH);
            _y = _driver.Add(0, SMOOTH);
            Apply();
        }

        public void Show(bool show, bool full, bool motion)
        {
            WasShown = Host.Visibility == Visibility.Visible && _open.Target > 0;
            if (show == WasShown) return;
            if (show) { Host.Visibility = Visibility.Visible; Open(full); }
            else if (motion) Close(full, () => Host.Visibility = Visibility.Collapsed);
            else { _driver.Snap(_open, 0); Host.Visibility = Visibility.Collapsed; }
        }

        public void Open(bool animate)
        {
            _closed = null;
            _content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _width = _content.DesiredSize.Width;
            if (!animate) { _driver.Snap(_open, 1); Settle(); return; }
            if (!_driver.IsRunning || _open.Target == 1) _open.Snap(0);
            Host.ClipToBounds = true;
            _driver.Animate(_open, 1, SMOOTH);
        }

        public void Close(bool animate, Action done)
        {
            _width = _content.ActualWidth;
            Host.IsHitTestVisible = false;
            if (!animate) { _driver.Snap(_open, 0); done(); return; }
            _closed = done;
            Host.ClipToBounds = true;
            _driver.Animate(_open, 0, SMOOTH);
        }

        public void Glide(double dx, double dy)
        {
            _x.Snap(_x.Position + dx);
            _y.Snap(_y.Position + dy);
            _driver.Animate(_x, 0, SMOOTH);
            _driver.Animate(_y, 0, SMOOTH);
        }

        public void Finish() { _driver.Finish(); Settle(); }

        private void Apply()
        {
            double open = Math.Clamp(_open.Position, 0, 1);
            bool moving = _driver.IsRunning && (_open.Target != _open.Position || _open.Position < 1);
            Host.Width = moving || _open.Target == 0 ? Math.Max(0, open * _width) : double.NaN;
            var margin = Host.Margin;
            Host.Margin = _leadingGap ? new Thickness(_gap * open, margin.Top, 0, margin.Bottom) : new Thickness(0, margin.Top, _gap * open, margin.Bottom);
            _move.X = _x.Position;
            _move.Y = _y.Position;
        }

        private void Settle()
        {
            if (_open.Target == 0)
            {
                var done = _closed;
                _closed = null;
                done?.Invoke();
                return;
            }
            Host.ClipToBounds = false;
            Host.IsHitTestVisible = true;
            Host.Width = double.NaN;
        }
    }

    // A chip: label, " · ", the value (MorphText), and a 24 remove button. It pops in from scale .9 with a soft blur.
    private sealed class Chip
    {
        public Slot Slot { get; }
        public System.Windows.Controls.Button Remove { get; }
        private readonly FilterToolbar _owner;
        private readonly Border _chip;
        private readonly ScaleTransform _scale = new(1, 1);
        private readonly TextBlock _label;
        private readonly TextBlock _separator;
        private readonly MorphText _value;
        private FilterChip _filter;

        public Chip(FilterToolbar owner, FilterChip filter)
        {
            _owner = owner;
            _filter = filter;
            _label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            _separator = new TextBlock { Text = " · ", VerticalAlignment = VerticalAlignment.Center };
            _separator.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
            _value = new MorphText() { VerticalAlignment = VerticalAlignment.Center };
            _value.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.Foreground");
            TextElement.SetFontWeight(_value, FontWeights.Medium);
            Remove = new System.Windows.Controls.Button { Width = 24, Height = 24, Margin = new Thickness(8, 0, 0, 0), Content = FilterMenu.Glyph(FilterMenu.XIcon, 14, 1.8, null), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1) };
            Remove.SetResourceReference(StyleProperty, new ComponentResourceKey(typeof(FilterMenu), "ChipRemove"));
            Remove.Click += (_, _) => _owner.Remove(_filter, Remove.IsKeyboardFocused);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { _label, _separator, _value, Remove } };
            _chip = new Border { MinHeight = 36, Padding = new Thickness(12, 0, 8, 0), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = row, RenderTransformOrigin = new Point(.5, .5), RenderTransform = _scale };
            _chip.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
            _chip.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
            _chip.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
            TextElement.SetFontSize(_chip, 12);
            var host = new Border { Child = _chip, Margin = new Thickness(0, 0, CHIP_GAP, CHIP_GAP), CornerRadius = new CornerRadius(18), VerticalAlignment = VerticalAlignment.Center };
            Slot = new Slot(host, _chip, CHIP_GAP, false);
            Update(filter, false, false);
        }

        public void Update(FilterChip filter, bool motion, bool full)
        {
            _filter = filter;
            _label.Text = filter.Label;
            bool hasValue = !string.IsNullOrEmpty(filter.Value);
            _separator.Visibility = _value.Visibility = hasValue ? Visibility.Visible : Visibility.Collapsed;
            if (hasValue) _value.Set(filter.Value!, motion, full);
            AutomationProperties.SetName(Remove, $"Remove {filter.Label}{(hasValue ? $": {filter.Value}" : "")}");
        }

        // Enter: opacity .24 enter, scale .9 → 1 on the snappy spring, soft blur clearing; the slot opens on smooth.
        public void Enter(bool full)
        {
            Slot.Open(full);
            if (!full) return;
            TimePicker.Fly(_chip, 0, 1, 0, 0, 4, 0, .24, ArcEasing.Enter, null);
            var ease = ArcEasing.Spring(SNAPPY, .6);
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.9, 1, TimeSpan.FromSeconds(.6)) { EasingFunction = ease });
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.9, 1, TimeSpan.FromSeconds(.6)) { EasingFunction = ease });
        }

        // Exit: fade with a subtle blur and scale .9 (.12 standard) while the slot collapses.
        public void Leave(bool full, Action done)
        {
            Slot.Close(full, done);
            if (!full) return;
            TimePicker.Fly(_chip, _chip.Opacity, 0, 0, 0, 0, 2, .12, ArcEasing.Standard, null);
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.9, TimeSpan.FromSeconds(.12)) { EasingFunction = ArcEasing.Standard });
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.9, TimeSpan.FromSeconds(.12)) { EasingFunction = ArcEasing.Standard });
        }
    }

    // Text that swaps with a rise and blur while its box springs (morph) to the new width, so nothing beside it snaps.
    private sealed class MorphText : Grid
    {
        private const double MORPH = .42;
        private readonly Canvas _leaving = new() { IsHitTestVisible = false };
        private readonly SpringDriver _driver;
        private readonly ControlSpring _width;
        private TextBlock? _text;

        public MorphText()
        {
            ClipToBounds = false;
            Children.Add(_leaving);
            _driver = new SpringDriver(() => Width = Math.Max(0, _width!.Position)) { Settled = () => Width = double.NaN };
            _width = _driver.Add(0, MORPH);
        }

        public void Set(string text, bool motion, bool full)
        {
            if (_text?.Text == text) return;
            var old = _text;
            double from = ActualWidth;
            var fresh = new TextBlock { Text = text, RenderTransform = new TranslateTransform() };
            _text = fresh;
            if (old is not null)
            {
                Children.Remove(old);
                if (motion) _leaving.Children.Add(old);
            }
            Children.Add(fresh);
            if (old is null || !motion) { _driver.Stop(); Width = double.NaN; return; }
            double em = TextElement.GetFontSize(this);
            FilterMenu.TextIn(fresh, em, full);
            FilterMenu.TextOut(old, em, full, () => _leaving.Children.Remove(old));
            if (!full) return;
            // Only a new value morphs the width; the first measure settles at once.
            fresh.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double to = Math.Ceiling(fresh.DesiredSize.Width);
            if (!_driver.IsRunning) _width.Snap(from);
            Width = _width.Position;
            _driver.Animate(_width, to, MORPH);
        }
    }
}
