using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

/// <summary>One chip: <see cref="Value"/> is what selection stores, <see cref="Label"/> what the chip shows.</summary>
public sealed class ChipOption
{
    public ChipOption() { }
    public ChipOption(string value, string? label = null) { Value = value; Label = label ?? value; }
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public override string ToString() => Label;
}

// chip-group.tsx (frozen 2026-10-31): selectable filter chips. Selecting morphs the chip: a check draws in, the label slides over and
// the visible edge follows the new width on a spring while neighbours glide to their new places. Long sets fold behind "+N more".
[TemplatePart(Name = "PART_Frame", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Panel", Type = typeof(Panel))]
public class ChipGroup : Control
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ChipGroup), new FrameworkPropertyMetadata(null, (d, _) => ((ChipGroup)d).Sync(true)));
    public static readonly DependencyProperty SelectedValuesProperty = DependencyProperty.Register(nameof(SelectedValues), typeof(IList<string>), typeof(ChipGroup), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ChipGroup)d).Sync(true)));
    public static readonly DependencyProperty MultipleProperty = DependencyProperty.Register(nameof(Multiple), typeof(bool), typeof(ChipGroup), new PropertyMetadata(true));
    public static readonly DependencyProperty MaxVisibleProperty = DependencyProperty.Register(nameof(MaxVisible), typeof(int), typeof(ChipGroup), new FrameworkPropertyMetadata(int.MaxValue, (d, _) => ((ChipGroup)d).Sync(true)), v => (int)v >= 0);
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ChipGroup), new PropertyMetadata(""));
    public static readonly RoutedEvent SelectedValuesChangedEvent = EventManager.RegisterRoutedEvent(nameof(SelectedValuesChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChipGroup));
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(ChipGroup), new PropertyMetadata(null, (d, _) => ((ChipGroup)d).Recolor()));

    /// <summary>Strings or <see cref="ChipOption"/>s.</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    /// <summary>Selected option values in option order. The control replaces the list on every change.</summary>
    public IList<string>? SelectedValues { get => (IList<string>?)GetValue(SelectedValuesProperty); set => SetValue(SelectedValuesProperty, value); }
    /// <summary>Allow several chips at once. In single mode the selected chip can still be cleared.</summary>
    public bool Multiple { get => (bool)GetValue(MultipleProperty); set => SetValue(MultipleProperty, value); }
    /// <summary>Chips shown before the rest fold behind a "+N more" chip. Chips selected when it folds stay in view.</summary>
    public int MaxVisible { get => (int)GetValue(MaxVisibleProperty); set => SetValue(MaxVisibleProperty, value); }
    /// <summary>Accessible name of the group, such as "Topics".</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public event RoutedEventHandler SelectedValuesChanged { add => AddHandler(SelectedValuesChangedEvent, value); remove => RemoveHandler(SelectedValuesChangedEvent, value); }

    private const double ItemStagger = .035;
    private FrameworkElement? _frameElement;
    private Panel? _panel;
    private readonly Canvas _leaving = new() { IsHitTestVisible = false };
    private HeightMorph? _morph;
    private readonly Dictionary<string, ChipGroupChip> _chips = [];
    private ChipGroupChip? _more;
    private bool _expanded;
    private List<string> _pinned = [];
    private string? _active;

    static ChipGroup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ChipGroup), new FrameworkPropertyMetadata(typeof(ChipGroup)));
        FocusableProperty.OverrideMetadata(typeof(ChipGroup), new FrameworkPropertyMetadata(false));
    }

    public ChipGroup()
    {
        SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border");
        Unloaded += (_, _) => _morph?.Stop();
    }

    internal bool Animating => IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;
    internal bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    internal IReadOnlyList<ChipOption> Options => (ItemsSource?.Cast<object>() ?? []).Select(item => item as ChipOption ?? new ChipOption(item?.ToString() ?? "")).ToList();
    private IList<string> Selected => SelectedValues ?? [];
    internal bool IsExpanded => _expanded;

    public override void OnApplyTemplate()
    {
        _morph?.Stop();
        if (_panel is not null) _panel.PreviewKeyDown -= PanelKeyDown;
        (_leaving.Parent as Panel)?.Children.Remove(_leaving);
        base.OnApplyTemplate();
        _frameElement = GetTemplateChild("PART_Frame") as FrameworkElement;
        _panel = GetTemplateChild("PART_Panel") as Panel;
        _morph = _frameElement is not null && _panel is not null ? new HeightMorph(_frameElement, _panel) : null;
        if (_panel?.Parent is Panel host) host.Children.Add(_leaving);
        if (_panel is not null) _panel.PreviewKeyDown += PanelKeyDown;
        _chips.Clear();
        _more = null;
        Sync(false);
    }

    /// <summary>Toggles one option as a click would.</summary>
    public void Toggle(string value)
    {
        bool on = Selected.Contains(value);
        IList<string> next = !Multiple ? (on ? [] : [value]) : Options.Where(o => o.Value == value ? !on : Selected.Contains(o.Value)).Select(o => o.Value).ToList();
        SetCurrentValue(SelectedValuesProperty, next);
        RaiseEvent(new RoutedEventArgs(SelectedValuesChangedEvent, this));
    }

    internal void ToggleMore()
    {
        if (_expanded) _pinned = Selected.ToList();
        _expanded = !_expanded;
        Sync(true);
    }

    private void Sync(bool animate)
    {
        if (_panel is null) return;
        animate &= Animating;
        var options = Options;
        var selected = Selected;
        int max = MaxVisible;
        bool foldable = options.Count > max;
        var visible = !foldable || _expanded ? options : options.Where((o, i) => i < max || _pinned.Contains(o.Value) || selected.Contains(o.Value)).ToList();
        int hidden = options.Count - visible.Count;
        bool showMore = foldable && (_expanded || hidden > 0);
        var before = Positions();
        _morph?.Morph(() =>
        {
            var keep = visible.Select(o => o.Value).ToHashSet();
            foreach (var pair in _chips.Where(p => !keep.Contains(p.Key)).ToArray()) { _chips.Remove(pair.Key); Leave(pair.Value, before, animate); }
            _panel.Children.Clear();
            for (int index = 0; index < visible.Count; index++)
            {
                var option = visible[index];
                if (!_chips.TryGetValue(option.Value, out var chip))
                {
                    chip = new ChipGroupChip(this, option.Value, false);
                    _chips[option.Value] = chip;
                    // Chips revealed by the overflow grow in one after another, in reading order.
                    if (animate) chip.Enter(_expanded ? Math.Min(Math.Max(0, index - max) * ItemStagger, .3) : 0, Full);
                }
                chip.Text = option.Label;
                chip.SetSelected(selected.Contains(option.Value), animate);
                Panel.SetZIndex(chip, 1);
                chip.Margin = new Thickness(0, 0, 8, 8);
                _panel.Children.Add(chip);
            }
            if (showMore)
            {
                _more ??= new ChipGroupChip(this, null, true) { Margin = new Thickness(0, 0, 8, 8) };
                _more.SetMoreText(_expanded ? "Show less" : $"+{hidden} more", animate);
                _panel.Children.Add(_more);
            }
            else if (_more is not null) { Leave(_more, before, animate); _more = null; }
        }, animate && Full);
        UpdateTabStop();
        if (animate && Full) Glide(before);
    }

    private Dictionary<UIElement, Point> Positions()
    {
        var map = new Dictionary<UIElement, Point>();
        if (_panel is null || !_panel.IsLoaded) return map;
        foreach (UIElement child in _panel.Children) map[child] = child.TranslatePoint(new Point(), _panel);
        return map;
    }

    // layout="position": once the row re-flows, every chip that moved glides from where it was on the morph spring, even across lines.
    private void Glide(Dictionary<UIElement, Point> before)
    {
        if (_panel is null || before.Count == 0) return;
        _panel.UpdateLayout();
        foreach (UIElement child in _panel.Children)
        {
            if (child is not ChipGroupChip chip || !before.TryGetValue(child, out var was)) continue;
            var now = child.TranslatePoint(new Point(), _panel);
            chip.Glide(was.X - now.X + chip.Offset.X, was.Y - now.Y + chip.Offset.Y);
        }
    }

    // Leaving chips step out of the flow (popLayout) and fade with a shrink (.12 standard); reduced fades only.
    private void Leave(ChipGroupChip chip, Dictionary<UIElement, Point> before, bool animate)
    {
        _panel?.Children.Remove(chip);
        if (!animate || !before.TryGetValue(chip, out var at)) return;
        Canvas.SetLeft(chip, at.X);
        Canvas.SetTop(chip, at.Y);
        _leaving.Children.Add(chip);
        chip.Exit(Full, () => _leaving.Children.Remove(chip));
    }

    internal void Focused(string? key) { _active = key; UpdateTabStop(); }

    // One chip holds the tab stop: the last one focused, else the first selected, else the first.
    private void UpdateTabStop()
    {
        if (_panel is null) return;
        var chips = _panel.Children.OfType<ChipGroupChip>().ToList();
        var stop = chips.FirstOrDefault(c => _active is not null && c.Key == _active) ?? chips.FirstOrDefault(c => c.IsChecked == true && !c.IsMore) ?? chips.FirstOrDefault();
        foreach (var chip in chips) chip.IsTabStop = ReferenceEquals(chip, stop);
    }

    private void PanelKeyDown(object sender, KeyEventArgs e)
    {
        if (_panel is null) return;
        var chips = _panel.Children.OfType<ChipGroupChip>().ToList();
        int index = chips.FindIndex(c => c.IsKeyboardFocused);
        if (index < 0) return;
        int last = chips.Count - 1;
        int? target = e.Key switch
        {
            Key.Right or Key.Down => index == last ? 0 : index + 1,
            Key.Left or Key.Up => index == 0 ? last : index - 1,
            Key.Home => 0,
            Key.End => last,
            _ => null,
        };
        if (target is not int to) return;
        e.Handled = true;
        chips[to].Focus();
    }

    private void Recolor()
    {
        foreach (var chip in _chips.Values) chip.Recolor(false);
        _more?.Recolor(false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChipGroupPeer(this);

    private sealed class ChipGroupPeer(ChipGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(ChipGroup);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.Label;
    }
}

/// <summary>
/// The button is only the hit area and the layout box. What you see is the surface, which trails the box on a spring while the width
/// morphs: <c>lag</c> is how far the visible edge trails the layout edge; it jumps by the change and springs back to zero (morph).
/// </summary>
internal sealed class ChipGroupChip : ToggleButton
{
    private const double Slot = 18, CheckLength = 23.35;
    private readonly ChipGroup _owner;
    private readonly Grid _body = new() { Height = 36 };
    private readonly Border _surface = new() { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _slot = new() { Width = 0 };
    private readonly Grid _text = new();
    private readonly TranslateTransform _textMove = new();
    private readonly Canvas _checkHost = new() { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(12, 0, 0, 0), IsHitTestVisible = false };
    private readonly Path _check = new() { Data = Geometry.Parse("M4,12.5 L9.5,18 L20,6.5"), StrokeThickness = 2.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, StrokeDashCap = PenLineCap.Round };
    private readonly ScaleTransform _checkScale = new(), _press = new(1, 1), _enter = new(1, 1);
    private readonly TranslateTransform _offset = new();
    private readonly BlurEffect _checkBlur = new() { Radius = 0 };
    private readonly SolidColorBrush _ink = new(), _border = new(), _fill = new();
    private readonly SpringDriver _driver;
    private readonly ControlSpring _lag;
    private readonly SpringDriver _glide;
    private readonly ControlSpring _x, _y;
    private TextBlock? _line;
    private bool _selected;

    public ChipGroupChip(ChipGroup owner, string? key, bool more)
    {
        _owner = owner;
        Key = key;
        IsMore = more;
        FocusVisualStyle = null;
        Cursor = Cursors.Hand;
        Template = Bare;
        _driver = new SpringDriver(ApplyLag);
        _lag = _driver.Add(0, Tween.Morph);
        _glide = new SpringDriver(() => { _offset.X = _x!.Position; _offset.Y = _y!.Position; });
        _x = _glide.Add(0, Tween.Morph);
        _y = _glide.Add(0, Tween.Morph);
        RenderTransform = new TransformGroup { Children = { _enter, _offset } };
        RenderTransformOrigin = new Point(.5, .5);
        _body.RenderTransform = _press;
        _body.RenderTransformOrigin = new Point(.5, .5);
        _surface.Background = _fill;
        _surface.BorderBrush = _border;
        _body.Children.Add(_surface);
        _check.RenderTransform = new ScaleTransform(14d / 24, 14d / 24);
        _check.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.AccentStrong");
        _checkHost.Children.Add(_check);
        _checkHost.RenderTransform = _checkScale;
        _checkHost.RenderTransformOrigin = new Point(0, .5);
        _checkHost.Effect = _checkBlur;
        if (!more) _body.Children.Add(_checkHost);
        _text.RenderTransform = _textMove;
        _row.Children.Add(_slot);
        _row.Children.Add(_text);
        if (more) _row.HorizontalAlignment = HorizontalAlignment.Center;
        _body.Children.Add(_row);
        Content = _body;
        SetValue(TextElement.ForegroundProperty, _ink);
        FontSize = 14;
        FontWeight = FontWeights.Medium;
        SetValue(TextBlock.LineHeightProperty, 20d);
        SizeChanged += (_, _) => ApplyLag();
        Unloaded += (_, _) => { _driver.Finish(); _glide.Finish(); };
        GotKeyboardFocus += (_, _) => owner.Focused(IsMore ? "\0more" : Key);
        Grow(0);
        Recolor(false);
    }

    private static readonly ControlTemplate Bare = CreateBare();
    private static ControlTemplate CreateBare()
    {
        var template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        template.Seal();
        return template;
    }

    public string? Key { get; }
    public bool IsMore { get; }
    public Point Offset => new(_offset.X, _offset.Y);

    public string Text
    {
        get => _line?.Text ?? "";
        set { if (_line is null) { _line = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }; _text.Children.Add(_line); } _line.Text = value; }
    }

    protected override void OnClick()
    {
        if (IsMore) { _owner.ToggleMore(); return; }
        if (Key is not null) _owner.Toggle(Key);
    }

    // ToggleButton would flip IsChecked itself; selection is owned by the group.
    protected override void OnToggle() { }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_driver is null) return;
        if (e.Property == IsMouseOverProperty) Recolor(true);
        // Press answers on the body (.97 over 100 ms), released on the spring token, so it never fights the layout glide.
        else if (e.Property == IsPressedProperty) Tween.Press(_press, IsPressed, .97, _owner.Full, Tween.Instant);
    }

    public void SetSelected(bool selected, bool animate)
    {
        bool changed = selected != _selected;
        _selected = selected;
        IsChecked = selected;
        Recolor(animate && changed);
        if (!changed) return;
        double previous = _body.ActualWidth;
        _slot.Width = selected ? Slot : 0;
        Morph(previous, animate);
    }

    public void SetMoreText(string text, bool animate)
    {
        if (_line?.Text == text) return;
        double previous = _body.ActualWidth;
        var old = _line;
        _line = new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        _text.Children.Add(_line);
        if (old is not null)
        {
            if (animate && _owner.Full)
            {
                // A leaving line holds its own offset, so the new line's centring never drags it sideways.
                old.RenderTransform = new TranslateTransform(_textMove.X, 0);
                _text.Children.Remove(old);
                var holder = new Canvas();
                _text.Children.Add(holder);
                holder.Children.Add(old);
                Tween.Present(old, false, -.3 * 14, 2, Tween.Fast, ArcEasing.Standard, done: () => _text.Children.Remove(holder));
                Tween.Present(_line, true, .3 * 14, 4, Tween.Standard, ArcEasing.Enter);
            }
            else _text.Children.Remove(old);
        }
        Morph(previous, animate);
    }

    private void Morph(double previous, bool animate)
    {
        _body.Measure(new Size(double.PositiveInfinity, 36));
        double next = _body.DesiredSize.Width;
        if (previous <= 0 || !animate || !_owner.Full) { _driver.Snap(_lag, 0); return; }
        // Retarget from wherever the edge is now and keep its speed, so fast toggling never snaps.
        _lag.Snap(_lag.Position + previous - next);
        _driver.Animate(_lag, 0, Tween.Morph);
    }

    private void ApplyLag()
    {
        double lag = _lag.Position;
        _surface.Width = Math.Max(0, _body.ActualWidth + lag);
        if (IsMore) _textMove.X = lag / 2;
        else
        {
            _textMove.X = lag;
            // The check grows from zero width in exactly the gap the label opens, so the two never overlap.
            Grow(Math.Max(0, ((_selected ? Slot : 0) + lag) / Slot));
        }
    }

    private void Grow(double grown)
    {
        double scale = Math.Min(grown, 1.1);
        _checkScale.ScaleX = _checkScale.ScaleY = scale;
        _checkHost.Opacity = Math.Min(1, grown * 1.4);
        _checkBlur.Radius = grown >= 1 ? 0 : (1 - grown) * 2;
        // The stroke draws in step with the growth (pathLength), so the check writes itself as the label makes room.
        double draw = Math.Clamp(grown, .001, 1) * CheckLength / 2.5;
        _check.StrokeDashArray = [draw, CheckLength];
    }

    public void Recolor(bool animate)
    {
        animate &= _owner.Full;
        string ink, border, fill;
        if (IsMore) (ink, border, fill) = ("TextMuted", "Border", "SurfaceMuted");
        else if (_selected) (ink, border, fill) = ("Foreground", IsMouseOver ? "ChipSelectedHoverBorder" : "ChipSelectedBorder", IsMouseOver ? "ChipSelectedHoverSurface" : "ChipSelectedSurface");
        else (ink, border, fill) = (IsMouseOver ? "Foreground" : "TextSecondary", IsMouseOver ? "BorderStrong" : "Border", "Surface");
        Tween.Color(_ink, Tween.Resolve(_owner, $"ArcWpf.Brush.{ink}"), Tween.Fast, animate);
        Tween.Color(_border, Tween.Resolve(_owner, $"ArcWpf.Brush.{border}"), Tween.Fast, animate);
        Tween.Color(_fill, Tween.Resolve(_owner, $"ArcWpf.Brush.{fill}"), Tween.Fast, animate);
    }

    public void Glide(double x, double y)
    {
        if (Math.Abs(x) < .5 && Math.Abs(y) < .5) return;
        _x.Snap(x); _y.Snap(y);
        _offset.X = x; _offset.Y = y;
        _glide.Animate(_x, 0, Tween.Morph);
        _glide.Animate(_y, 0, Tween.Morph);
    }

    // initial opacity 0, scale .9 → snappy spring with the reveal stagger; opacity over .16 with the same delay. Reduced fades only.
    public void Enter(double delay, bool full)
    {
        Opacity = 0;
        Tween.To(this, OpacityProperty, 1, full ? Tween.Fast : .15, ArcEasing.Standard, full ? delay : 0);
        if (!full) return;
        _enter.ScaleX = _enter.ScaleY = .9;
        Tween.Spring(_enter, ScaleTransform.ScaleXProperty, 1, Tween.Snappy, delay);
        Tween.Spring(_enter, ScaleTransform.ScaleYProperty, 1, Tween.Snappy, delay);
    }

    public void Exit(bool full, Action done)
    {
        IsHitTestVisible = false;
        IsTabStop = false;
        Tween.To(this, OpacityProperty, 0, full ? Tween.Instant : .15, ArcEasing.Standard, done: done);
        if (!full) return;
        Tween.To(_enter, ScaleTransform.ScaleXProperty, .9, Tween.Instant, ArcEasing.Standard);
        Tween.To(_enter, ScaleTransform.ScaleYProperty, .9, Tween.Instant, ArcEasing.Standard);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChipPeer(this);

    // Chips are pressed toggles (aria-pressed); the overflow chip is an expander (aria-expanded).
    private sealed class ChipPeer(ChipGroupChip owner) : ToggleButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        protected override string GetNameCore() => owner.Text;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        public override object? GetPattern(PatternInterface pattern) => owner.IsMore
            ? pattern == PatternInterface.ExpandCollapse ? this : pattern == PatternInterface.Toggle ? null : base.GetPattern(pattern)
            : pattern == PatternInterface.ExpandCollapse ? null : base.GetPattern(pattern);
        public ExpandCollapseState ExpandCollapseState => owner._owner.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public void Expand() { if (!owner._owner.IsExpanded) owner._owner.ToggleMore(); }
        public void Collapse() { if (owner._owner.IsExpanded) owner._owner.ToggleMore(); }
    }
}
