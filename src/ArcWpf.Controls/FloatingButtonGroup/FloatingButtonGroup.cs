using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum FloatingButtonGroupVariant { Muted, Floating }
public enum FloatingButtonGroupSize { Small, Medium }

public sealed class FloatingButtonGroupItem
{
    private string _label = "", _icon = "";
    private bool _disabled;
    private bool? _pressed;
    internal event Action? Changed;

    public string Id { get; init; } = "";
    public bool IsSeparator { get; init; }
    public bool? IconOnly { get; init; }
    public string? Shortcut { get; init; }
    public IReadOnlyList<string> ReserveLabels { get; init; } = [];
    public string Label { get => _label; set { _label = value; Changed?.Invoke(); } }
    public string Icon { get => _icon; set { _icon = value; Changed?.Invoke(); } }
    public bool IsDisabled { get => _disabled; set { _disabled = value; Changed?.Invoke(); } }
    /// <summary>Marks a mode switch such as Present: it keeps a quiet tint while on. Null means it is not a toggle.</summary>
    public bool? IsPressed { get => _pressed; set { _pressed = value; Changed?.Invoke(); } }
}

/// <summary>Separate soft buttons in a quiet tray. One shared highlight morphs to whichever button the pointer or keyboard focus is on.</summary>
public class FloatingButtonGroup : Control
{
    public static readonly DependencyProperty ItemsProperty = Prop(nameof(Items), (IReadOnlyList<FloatingButtonGroupItem>)[]);
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(FloatingButtonGroup), new PropertyMetadata("", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));
    public static readonly DependencyProperty VariantProperty = Prop(nameof(Variant), FloatingButtonGroupVariant.Muted);
    public static readonly DependencyProperty SizeProperty = Prop(nameof(Size), FloatingButtonGroupSize.Medium);
    public static readonly DependencyProperty OrientationProperty = Prop(nameof(Orientation), Orientation.Horizontal);
    public static readonly DependencyProperty IconOnlyProperty = Prop(nameof(IconOnly), false);
    public static readonly DependencyProperty TooltipSideProperty = Prop<PopoverSide?>(nameof(TooltipSide), null);

    private static DependencyProperty Prop<T>(string name, T fallback) => DependencyProperty.Register(name, typeof(T), typeof(FloatingButtonGroup), new PropertyMetadata(fallback, (d, _) => ((FloatingButtonGroup)d).Build()));

    public IReadOnlyList<FloatingButtonGroupItem> Items { get => (IReadOnlyList<FloatingButtonGroupItem>)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public FloatingButtonGroupVariant Variant { get => (FloatingButtonGroupVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public FloatingButtonGroupSize Size { get => (FloatingButtonGroupSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public bool IconOnly { get => (bool)GetValue(IconOnlyProperty); set => SetValue(IconOnlyProperty, value); }
    public PopoverSide? TooltipSide { get => (PopoverSide?)GetValue(TooltipSideProperty); set => SetValue(TooltipSideProperty, value); }

    /// <summary>Raised with the item id after a click on an enabled item.</summary>
    public event EventHandler<string>? ItemInvoked;

    private readonly OwnedMotion _motion = new();
    private readonly SpringDriver _driver;
    private readonly ControlSpring _x, _y, _w, _h, _scale;
    private readonly ThemedBrush _highlightFill;
    private readonly List<ItemView> _views = [];
    private readonly ScaleTransform _highlightScale = new();
    private readonly TranslateTransform _highlightMove = new();
    private Border? _highlight;
    private Grid? _inner;
    private Decorator? _host;
    private string? _hoverId, _focusId, _tabStop;
    private bool _pressing, _traveling;

    static FloatingButtonGroup() => DefaultStyleKeyProperty.OverrideMetadata(typeof(FloatingButtonGroup), new FrameworkPropertyMetadata(typeof(FloatingButtonGroup)));

    public FloatingButtonGroup()
    {
        _driver = new SpringDriver(Apply) { Settled = () => _traveling = false };
        _x = _driver.Add(0, ArcSpring.Morph);
        _y = _driver.Add(0, ArcSpring.Morph);
        _w = _driver.Add(0, ArcSpring.Morph);
        _h = _driver.Add(0, ArcSpring.Morph);
        _scale = _driver.Add(1, ArcSpring.Snappy);
        _highlightFill = new ThemedBrush(this, _motion);
        Unloaded += (_, _) => { _driver.Stop(); _motion.Stop(); };
        ThemeWatch.Add(this, () => { _highlightFill.To(_pressing && ActiveId is not null ? "FloatHighlightPress" : "FloatHighlight", false); foreach (var view in _views) { view.Button.Refresh(false); } });
    }

    internal string? ActiveId => Enabled(_hoverId) ? _hoverId : _focusId is not null && _views.Any(v => v.Item.Id == _focusId) ? _focusId : null;
    internal string? TabStopId => Enabled(_tabStop) ? _tabStop : _views.FirstOrDefault(v => !v.Item.IsDisabled)?.Item.Id;
    internal Rect HighlightTargetForTest => new(_x.Target, _y.Target, _w.Target, _h.Target);
    internal double HighlightOpacityForTest => _highlight?.Opacity ?? 0;
    internal bool PressingForTest => _pressing;
    internal ToolbarButton? ButtonForTest(string id) => _views.FirstOrDefault(v => v.Item.Id == id)?.Button;
    internal void HoverForTest(string? id) { if (id is null) { LeaveTray(); } else { SetHover(id); } }
    internal void FinishMotionForTest() => _driver.Finish();

    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private bool Vertical => Orientation == Orientation.Vertical;
    private bool Enabled(string? id) => id is not null && _views.Any(v => v.Item.Id == id && !v.Item.IsDisabled);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Build();
    }

    private void Build()
    {
        if (_host is null) { return; }
        foreach (var view in _views) { view.Detach(); }
        _views.Clear();
        bool small = Size == FloatingButtonGroupSize.Small, floating = Variant == FloatingButtonGroupVariant.Floating;
        double height = small ? 30 : 36, pad = small ? 3 : 4, gap = small ? 3 : 4, outer = small ? 15 : 18, radius = outer - pad - 1;

        var stack = new StackPanel { Orientation = Orientation };
        bool first = true;
        foreach (var item in Items ?? [])
        {
            double before = first ? 0 : gap;
            FrameworkElement part;
            if (item.IsSeparator)
            {
                var rule = new Border { Width = Vertical ? height * .5 : 1, Height = Vertical ? 1 : height * .5, CornerRadius = new CornerRadius(1), Margin = Vertical ? new Thickness(0, before + 2, 0, 2) : new Thickness(before + 2, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                rule.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.FloatRule");
                part = rule;
            }
            else
            {
                var view = new ItemView(this, item, height, radius, small ? 10 : 12, small ? 12 : 14);
                _views.Add(view);
                part = view.Root;
                part.Margin = Vertical ? new Thickness(0, before, 0, 0) : new Thickness(before, 0, 0, 0);
            }
            stack.Children.Add(part);
            first = false;
        }

        _highlight = new Border { CornerRadius = new CornerRadius(radius), Background = _highlightFill.Brush, Opacity = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new TransformGroup { Children = { _highlightScale, _highlightMove } } };
        _highlightFill.To("FloatHighlight", false);
        _inner = new Grid { Children = { _highlight, stack } };
        _inner.SizeChanged += (_, _) => { if (ActiveId is { } id) { Place(id, false); } };
        var tray = new Border { CornerRadius = new CornerRadius(outer), BorderThickness = new Thickness(1), Padding = new Thickness(pad), Child = _inner };
        tray.SetResourceReference(Border.BackgroundProperty, floating ? "ArcWpf.Brush.SurfaceRaised" : "ArcWpf.Brush.FloatTray");
        tray.SetResourceReference(Border.BorderBrushProperty, floating ? "ArcWpf.Brush.Border" : "ArcWpf.Brush.FloatTrayEdge");
        tray.MouseLeave += (_, _) => LeaveTray();
        tray.PreviewKeyDown += OnTrayKey;
        tray.LostKeyboardFocus += (_, _) => { if (!tray.IsKeyboardFocusWithin) { _focusId = null; SetPressing(false); Sync(); } };
        _host.Child = floating ? new Grid { Children = { FloatingShadow.Plate(outer), tray } } : tray;
        Sync();
    }

    private void SetHover(string id) { if (_hoverId != id) { _hoverId = id; Sync(); } }
    private void LeaveTray() { _hoverId = null; SetPressing(false); Sync(); }

    private void SetPressing(bool pressing)
    {
        if (_pressing == pressing) { return; }
        _pressing = pressing;
        Sync();
    }

    private string? _lastActive;
    private bool _lastPressed;

    private void Sync()
    {
        string? active = ActiveId, stop = TabStopId;
        foreach (var view in _views) { view.Button.IsTabStop = view.Item.Id == stop; view.Button.Refresh(IsLoaded); }
        if (active != _lastActive) { _lastActive = active; Place(active, true); }
        bool pressed = _pressing && active is not null;
        if (pressed != _lastPressed)
        {
            _lastPressed = pressed;
            _highlightFill.To(pressed ? "FloatHighlightPress" : "FloatHighlight", IsLoaded, pressed ? Tween.Instant : Tween.Fast);
            if (Moving) { _driver.Animate(_scale, pressed ? .965 : 1, ArcSpring.Snappy); } else { _driver.Snap(_scale, 1); }
        }
    }

    private void Place(string? id, bool move)
    {
        var node = _views.FirstOrDefault(v => v.Item.Id == id)?.Button;
        if (_highlight is null || _inner is null) { return; }
        if (node is null || !node.IsLoaded || !_inner.IsAncestorOf(node))
        {
            if (id is null) { Tween.To(_highlight, OpacityProperty, 0, Moving ? Tween.Standard : Tween.Instant, ArcEasing.Standard); }
            return;
        }
        var at = node.TransformToAncestor(_inner).Transform(new Point());
        bool visible = _highlight.Opacity > .15;
        if (Moving && visible && (move || _traveling))
        {
            _traveling = true;
            _driver.Animate(_x, at.X, ArcSpring.Morph);
            _driver.Animate(_y, at.Y, ArcSpring.Morph);
            _driver.Animate(_w, node.ActualWidth, ArcSpring.Morph);
            _driver.Animate(_h, node.ActualHeight, ArcSpring.Morph);
        }
        else
        {
            _traveling = false;
            _x.Snap(at.X); _y.Snap(at.Y); _w.Snap(node.ActualWidth); _h.Snap(node.ActualHeight);
            if (!visible && Moving)
            {
                _scale.Snap(.94);
                _driver.Animate(_scale, _pressing ? .965 : 1, ArcSpring.Snappy);
            }
            Apply();
        }
        if (_highlight.Opacity != 1) { Tween.To(_highlight, OpacityProperty, 1, Moving ? Tween.Fast : Tween.Instant, ArcEasing.Standard); }
    }

    private void Apply()
    {
        if (_highlight is null) { return; }
        _highlightMove.X = _x.Position; _highlightMove.Y = _y.Position;
        _highlight.Width = Math.Max(0, _w.Position); _highlight.Height = Math.Max(0, _h.Position);
        _highlightScale.ScaleX = _highlightScale.ScaleY = _scale.Position;
    }

    private void OnTrayKey(object sender, KeyEventArgs e)
    {
        int index = _views.FindIndex(v => v.Button.IsKeyboardFocused);
        if (index < 0) { return; }
        var usable = _views.Select(v => !v.Item.IsDisabled).ToList();
        bool moves = e.Key is Key.Home or Key.End || (Vertical ? e.Key is Key.Up or Key.Down : e.Key is Key.Left or Key.Right);
        if (!moves) { return; }
        e.Handled = true;
        int target = ToolbarKeys.Next(usable, index, e.Key, Vertical);
        if (target < 0) { return; }
        // The keyboard takes the highlight back until the pointer moves again.
        _hoverId = null;
        _views[target].Button.Focus();
    }

    private sealed class ItemView
    {
        private readonly FloatingButtonGroup _owner;
        private readonly OwnedMotion _motion = new();
        private readonly Grid _slot = new();
        private readonly double _fontSize;
        private readonly bool _iconOnly;
        private StackPanel? _phase;
        private string _shownLabel = "", _shownIcon = "";
        public FloatingButtonGroupItem Item { get; }
        public ToolbarButton Button { get; }
        public FrameworkElement Root { get; }

        public ItemView(FloatingButtonGroup owner, FloatingButtonGroupItem item, double height, double radius, double padX, double fontSize)
        {
            _owner = owner;
            Item = item;
            _fontSize = fontSize;
            bool hasIcon = !string.IsNullOrEmpty(item.Icon);
            _iconOnly = hasIcon && (item.IconOnly ?? owner.IconOnly);
            Button = new ToolbarButton(radius) { Height = height, MinWidth = height, Roles = Roles, Focusable = !item.IsDisabled };
            if (_iconOnly) { Button.Width = height; Button.Padding = new Thickness(0); }
            else { Button.Padding = new Thickness(hasIcon ? padX - 2 : padX, 0, padX, 0); }
            if (_iconOnly && owner.Vertical) { Button.HorizontalAlignment = HorizontalAlignment.Center; }
            if (owner.Vertical && !_iconOnly) { Button.HorizontalContentAlignment = HorizontalAlignment.Left; }
            Button.Content = _slot;
            Button.IsInert = item.IsDisabled;
            Button.Opacity = item.IsDisabled ? .5 : 1;
            AutomationProperties.SetName(Button, item.Label);
            foreach (var text in _iconOnly ? [] : new[] { item.Label }.Concat(item.ReserveLabels).Distinct())
            {
                var ghost = Line(text, hasIcon ? "x" : "");
                ghost.Visibility = Visibility.Hidden;
                _slot.Children.Add(ghost);
            }
            _shownLabel = item.Label; _shownIcon = item.Icon;
            _phase = Line(item.Label, item.Icon);
            _slot.Children.Add(_phase);

            Button.MouseMove += (_, e) => { if (e.StylusDevice is null) { _owner.SetHover(Item.Id); } };
            Button.PreviewMouseLeftButtonDown += (_, _) => { if (!Item.IsDisabled) { _owner._hoverId = Item.Id; _owner.SetPressing(true); } };
            Button.PreviewMouseLeftButtonUp += (_, _) => _owner.SetPressing(false);
            Button.MouseLeave += (_, _) => _owner.SetPressing(false);
            Button.PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space && !e.IsRepeat && !Item.IsDisabled) { _owner.SetPressing(true); } };
            Button.PreviewKeyUp += (_, e) => { if (e.Key is Key.Enter or Key.Space) { _owner.SetPressing(false); } };
            Button.GotKeyboardFocus += (_, _) => { _owner._tabStop = Item.Id; _owner._focusId = ThreadUi.FocusVisible(Button) ? Item.Id : null; _owner.Sync(); };
            Button.Click += (_, _) => _owner.ItemInvoked?.Invoke(_owner, Item.Id);
            Item.Changed += OnChanged;

            var tip = TipFor();
            if (tip is null) { Root = Button; }
            else
            {
                var side = owner.TooltipSide ?? (owner.Vertical ? PopoverSide.Right : PopoverSide.Top);
                var wrapper = new Tooltip { Tip = tip, Side = side, Content = Button };
                if (_iconOnly && owner.Vertical) { wrapper.HorizontalAlignment = HorizontalAlignment.Center; }
                Root = wrapper;
            }
        }

        public void Detach() { Item.Changed -= OnChanged; _motion.Stop(); }

        private object? TipFor()
        {
            if (!_iconOnly && string.IsNullOrEmpty(Item.Shortcut)) { return null; }
            return string.IsNullOrEmpty(Item.Shortcut) ? Item.Label : Item.Label + "   " + Item.Shortcut;
        }

        private (string?, string?, string?) Roles(ArcChromeButton _)
        {
            bool on = Item.IsPressed == true, active = _owner.ActiveId == Item.Id;
            return (on ? "FloatOn" : null, null, Item.IsDisabled ? "TextMuted" : active || on ? "Foreground" : "TextSecondary");
        }

        private StackPanel Line(string label, string icon)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = _owner.Vertical ? HorizontalAlignment.Left : HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            bool hasIcon = !string.IsNullOrEmpty(icon);
            if (hasIcon) { line.Children.Add(ArcText.Icon(icon, 16, Button.Ink)); }
            if (!_iconOnly)
            {
                var text = ArcText.Block(label, _fontSize, double.NaN, medium: true);
                text.Foreground = Button.Ink;
                if (hasIcon) { text.Margin = new Thickness(6, 0, 0, 0); }
                line.Children.Add(text);
            }
            return line;
        }

        private void OnChanged()
        {
            Button.IsInert = Item.IsDisabled;
            Button.Focusable = !Item.IsDisabled;
            Button.Opacity = Item.IsDisabled ? .5 : 1;
            AutomationProperties.SetName(Button, Item.Label);
            if (Root is Tooltip wrapper) { wrapper.Tip = TipFor(); }
            if (Item.Label != _shownLabel || Item.Icon != _shownIcon)
            {
                _shownLabel = Item.Label; _shownIcon = Item.Icon;
                Swap();
            }
            _owner.Sync();
        }

        private void Swap()
        {
            var old = _phase;
            _phase = Line(Item.Label, Item.Icon);
            _slot.Children.Add(_phase);
            var level = Tween.Level(Button);
            if (old is null || !Button.IsLoaded || level == ArcMotionLevel.Off) { if (old is not null) { _slot.Children.Remove(old); } return; }
            bool full = level == ArcMotionLevel.Full;
            var next = _phase;
            Tween.Present(next, true, full ? 4 : 0, full ? 4 : 0, full ? Tween.Standard : Tween.Instant, ArcEasing.Enter);
            Tween.Present(old, false, full ? -3 : 0, full ? 4 : 0, full ? Tween.Fast : Tween.Instant, ArcEasing.Standard, done: () => _slot.Children.Remove(old));
        }
    }
}
