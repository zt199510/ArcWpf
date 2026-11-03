using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class ReactionOption
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>A Lucide icon name, or any element.</summary>
    public object? Icon { get; set; }
}

public sealed class Reaction : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string? _label;
    private object? _icon;
    private int _count;
    private bool _reacted;
    public string Id { get => _id; set => Set(ref _id, value); }
    public string? Label { get => _label; set => Set(ref _label, value); }
    public object? Icon { get => _icon; set => Set(ref _icon, value); }
    public int Count { get => _count; set => Set(ref _count, value); }
    public bool Reacted { get => _reacted; set => Set(ref _reacted, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) { return; }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class ReactionChangedEventArgs(string id, bool reacted, IReadOnlyList<Reaction> reactions) : EventArgs
{
    public string Id { get; } = id;
    public bool Reacted { get; } = reacted;
    public IReadOnlyList<Reaction> Reactions { get; } = reactions;
}

/// <summary>A reaction bar for comments, posts and release notes. Tapping a pill adds or removes the viewer's reaction, and the add button opens a compact picker that grows from it.</summary>
[TemplatePart(Name = "PART_Bar", Type = typeof(ReactionsBar))]
public class Reactions : Control
{
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(ObservableCollection<ReactionOption>), typeof(Reactions), new FrameworkPropertyMetadata(null, OnCollectionSwapped));
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(ObservableCollection<Reaction>), typeof(Reactions), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCollectionSwapped));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Reactions), new FrameworkPropertyMetadata("Reactions", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));
    public ObservableCollection<ReactionOption> Options { get => (ObservableCollection<ReactionOption>)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public ObservableCollection<Reaction> Values { get => (ObservableCollection<Reaction>)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public event EventHandler<ReactionChangedEventArgs>? ValueChanged;

    private ReactionsBar? _bar;
    private readonly ReactionAddButton _add;
    private readonly TextBlock _status = new() { Width = 0, Height = 0 };
    private readonly Dictionary<string, ReactionPill> _pills = [];
    private readonly HashSet<Reaction> _watched = [];
    private readonly SpringDriver _heightDriver;
    private readonly ControlSpring _height;
    private double _natural = -1;
    private long _armedUntil;
    private ReactionPicker? _picker;
    private readonly Popup _popup = new() { AllowsTransparency = true, Placement = PlacementMode.Custom, StaysOpen = true, PopupAnimation = PopupAnimation.None };
    private Window? _window;
    private string? _refocus;

    static Reactions() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Reactions), new FrameworkPropertyMetadata(typeof(Reactions)));

    public Reactions()
    {
        _heightDriver = new SpringDriver(InvalidateMeasure);
        _height = _heightDriver.Add(0, ArcSpring.Smooth);
        _add = new ReactionAddButton(this);
        _add.Click += (_, _) => { if (IsPickerOpen) { ClosePicker(false); } else { OpenPicker(); } };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(this, Label);
        _popup.PlacementTarget = _add;
        _popup.CustomPopupPlacementCallback = PlacePopup;
        Options = [];
        Values = [];
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    internal bool IsPickerOpen { get; private set; }
    internal ReactionPicker? Picker => _picker;
    internal IEnumerable<ReactionPill> Pills => _pills.Values;
    internal ReactionAddButton AddButton => _add;
    internal string Announcement => _status.Text;
    private bool Reduced => ArcMotion.GetLevel(this) != ArcMotionLevel.Full;

    private CustomPopupPlacement[] PlacePopup(Size popup, Size target, Point offset) =>
        [new CustomPopupPlacement(new Point((target.Width - popup.Width) / 2, -popup.Height - 8 + ReactionPicker.Bleed), PopupPrimaryAxis.Horizontal)];

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window is not null)
        {
            _window.PreviewMouseDown += OnWindowPress;
            _window.Deactivated += OnWindowDeactivated;
            _window.LocationChanged += OnWindowMoved;
        }
        Sync(false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.PreviewMouseDown -= OnWindowPress;
            _window.Deactivated -= OnWindowDeactivated;
            _window.LocationChanged -= OnWindowMoved;
        }
        _window = null;
        foreach (var reaction in _watched) { reaction.PropertyChanged -= OnReactionChanged; }
        _watched.Clear();
        CloseNow();
        _heightDriver.Finish();
    }

    private void OnWindowPress(object sender, MouseButtonEventArgs e)
    {
        if (IsPickerOpen && e.OriginalSource is DependencyObject source && !_add.IsAncestorOf(source) && !ReferenceEquals(source, _add)) { ClosePicker(false); }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) { if (IsPickerOpen && !_popup.IsKeyboardFocusWithin) { ClosePicker(false); } }
    private void OnWindowMoved(object? sender, EventArgs e) { if (IsPickerOpen) { _popup.HorizontalOffset += 1; _popup.HorizontalOffset -= 1; } }

    private static void OnCollectionSwapped(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var reactions = (Reactions)d;
        if (e.OldValue is INotifyCollectionChanged old) { old.CollectionChanged -= reactions.OnCollection; }
        if (e.NewValue is INotifyCollectionChanged next) { next.CollectionChanged += reactions.OnCollection; }
        reactions.Sync(true);
    }

    private void OnCollection(object? sender, NotifyCollectionChangedEventArgs e) => Sync(true);
    private void OnReactionChanged(object? sender, PropertyChangedEventArgs e) => Sync(true);

    public override void OnApplyTemplate()
    {
        _bar?.Children.Clear();
        base.OnApplyTemplate();
        _bar = GetTemplateChild("PART_Bar") as ReactionsBar;
        if (_bar is null) { return; }
        _bar.Children.Add(_status);
        _bar.Children.Add(_add);
        foreach (var pill in _pills.Values) { _bar.Children.Insert(_bar.Children.Count - 1, pill); }
        Sync(false);
    }

    private ReactionOption? Option(string id) => Options?.FirstOrDefault(option => option.Id == id);

    /// <summary>Pills keep the order of Values. A reaction whose count reaches zero leaves the bar.</summary>
    private void Sync(bool animate)
    {
        if (Values is null) { return; }
        foreach (var reaction in Values) { if (_watched.Add(reaction)) { reaction.PropertyChanged += OnReactionChanged; } }
        foreach (var gone in _watched.Where(reaction => !Values.Contains(reaction)).ToList()) { gone.PropertyChanged -= OnReactionChanged; _watched.Remove(gone); }
        if (_bar is null) { return; }
        animate = animate && IsLoaded && IsVisible;
        var shown = Values.Where(reaction => reaction.Count > 0 && (Option(reaction.Id) is not null || reaction.Label is not null)).ToList();
        foreach (var id in _pills.Keys.Where(id => shown.All(reaction => reaction.Id != id)).ToList())
        {
            var pill = _pills[id];
            _pills.Remove(id);
            pill.Leave(animate && !Reduced, () => _bar.Children.Remove(pill));
        }
        int at = 0;
        foreach (var reaction in shown)
        {
            if (!_pills.TryGetValue(reaction.Id, out var pill))
            {
                var option = Option(reaction.Id);
                pill = new ReactionPill(this, reaction.Id, reaction.Label ?? option?.Label ?? reaction.Id, reaction.Icon ?? option?.Icon);
                _pills[reaction.Id] = pill;
                _bar.Children.Insert(IndexFor(at), pill);
                pill.Update(reaction, false);
                if (animate) { pill.Enter(Reduced); }
            }
            else { pill.Update(reaction, animate); }
            at++;
        }
        _bar.InvalidateMeasure();
        _picker?.Refresh(ReactedIds);
        if (_refocus is not null)
        {
            var target = _refocus;
            _refocus = null;
            UIElement next = target == "add" || !_pills.TryGetValue(target, out var neighbour) ? _add : neighbour;
            next.Focus();
        }
    }

    // Pills that are leaving hold their old slots out of flow, so the index counts live pills only.
    private int IndexFor(int live)
    {
        for (int i = 0, seen = 0; i < _bar!.Children.Count; i++)
        {
            if (_bar.Children[i] is ReactionAddButton) { return i; }
            if (_bar.Children[i] is ReactionPill { Leaving: false }) { if (seen == live) { return i; } seen++; }
        }
        return _bar.Children.Count;
    }

    internal HashSet<string> ReactedIds => Values.Where(reaction => reaction.Reacted && reaction.Count > 0).Select(reaction => reaction.Id).ToHashSet();

    internal void Toggle(string id)
    {
        var option = Option(id);
        var current = Values.FirstOrDefault(reaction => reaction.Id == id && reaction.Count > 0);
        string label = option?.Label ?? current?.Label ?? id;
        if (option is null && current is null) { return; }
        bool add = current?.Reacted != true;
        int count = Math.Max(0, (current?.Count ?? 0) + (add ? 1 : -1));
        _armedUntil = Environment.TickCount64 + 700;
        if (count == 0 && _pills.TryGetValue(id, out var leaving) && leaving.IsKeyboardFocused)
        {
            var order = Values.Where(reaction => reaction.Count > 0).Select(reaction => reaction.Id).ToList();
            int index = order.IndexOf(id);
            _refocus = index + 1 < order.Count ? order[index + 1] : index > 0 ? order[index - 1] : "add";
        }
        if (add && _pills.TryGetValue(id, out var pill)) { pill.Kick(Reduced); }
        if (current is null)
        {
            foreach (var stale in Values.Where(reaction => reaction.Id == id).ToList()) { Values.Remove(stale); }
            _nextPop = id;
            Values.Add(new Reaction { Id = id, Label = option?.Label, Icon = option?.Icon, Count = 1, Reacted = true });
        }
        else if (count == 0) { Values.Remove(current); }
        else { current.Count = count; current.Reacted = add; }
        if (_nextPop == id && _pills.TryGetValue(id, out var fresh)) { fresh.Kick(Reduced); }
        _nextPop = null;
        string noun = count == 1 ? "reaction" : "reactions";
        ArcEffects.Announce(_status, add ? $"Added {label}. {count} {noun}." : count > 0 ? $"Removed {label}. {count} {noun}." : $"Removed {label}.");
        ValueChanged?.Invoke(this, new ReactionChangedEventArgs(id, add, Values.ToList()));
    }

    private string? _nextPop;
    public void ToggleForTest(string id) => Toggle(id);

    // ---- Picker ----
    internal void OpenPicker()
    {
        if (IsPickerOpen || Options.Count == 0) { return; }
        IsPickerOpen = true;
        _picker = new ReactionPicker(this, Options.ToList(), ReactedIds, Reduced);
        _picker.Picked += id => { ClosePicker(true); Toggle(id); };
        _picker.Dismissed += () => ClosePicker(true);
        _popup.Child = _picker;
        _popup.IsOpen = true;
        _add.SetOpen(true, IsLoaded);
        _picker.PlayOpen();
    }

    internal void ClosePicker(bool focus)
    {
        if (!IsPickerOpen) { return; }
        IsPickerOpen = false;
        _add.SetOpen(false, IsLoaded);
        var picker = _picker;
        _picker = null;
        if (focus) { _add.Focus(); }
        picker?.PlayClose(Reduced || !IsLoaded, () => { if (ReferenceEquals(_popup.Child, picker)) { _popup.IsOpen = false; _popup.Child = null; } });
    }

    private void CloseNow()
    {
        IsPickerOpen = false;
        _picker = null;
        _popup.IsOpen = false;
        _popup.Child = null;
    }

    // ---- Height: wrapping onto a new row springs instead of shoving the content below. ----
    protected override Size MeasureOverride(Size constraint)
    {
        var size = base.MeasureOverride(constraint);
        if (size.Height != _natural)
        {
            bool first = _natural < 0;
            _natural = size.Height;
            if (first || Reduced || !IsLoaded || Environment.TickCount64 > _armedUntil) { _heightDriver.Stop(); _height.Snap(_natural); }
            else { _heightDriver.Animate(_height, _natural, ArcSpring.Smooth); }
        }
        return new Size(size.Width, _height.Position);
    }
}

/// <summary>A wrapping row whose items glide to new positions. Items leaving hold their last slot and drop out of flow.</summary>
public sealed class ReactionsBar : Panel
{
    private const double ColumnGap = 6, RowGap = 8;
    private readonly List<(ReactionBarButton Item, Rect Slot)> _slots = [];
    private double _height;

    protected override Size MeasureOverride(Size availableSize)
    {
        _slots.Clear();
        double limit = availableSize.Width, x = 0, y = 0, row = 0, widest = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (child is not ReactionBarButton item || item.Leaving) { continue; }
            var size = item.DesiredSize;
            if (x > 0 && x + size.Width > limit) { x = 0; y += row + RowGap; row = 0; }
            _slots.Add((item, new Rect(x, y, size.Width, size.Height)));
            widest = Math.Max(widest, x + size.Width);
            x += size.Width + ColumnGap;
            row = Math.Max(row, size.Height);
        }
        _height = y + row;
        return new Size(double.IsInfinity(limit) ? widest : limit, _height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (item, slot) in _slots) { item.ArrangeSlot(slot); }
        foreach (UIElement child in InternalChildren)
        {
            if (child is ReactionBarButton { Leaving: true } gone) { gone.Arrange(gone.LastSlot); }
            else if (child is not ReactionBarButton) { child.Arrange(new Rect()); }
        }
        return finalSize;
    }

    // The root follows the wrapped height on a spring, so this panel is briefly taller than its slot and must not be cut.
    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => null;
}

/// <summary>A bar item that keeps its screen position across layout changes and springs home.</summary>
internal abstract class ReactionBarButton : ArcChromeButton
{
    private readonly SpringDriver _layoutDriver;
    private readonly ControlSpring _dx, _dy;
    private readonly TranslateTransform _layoutMove = new();
    protected readonly ScaleTransform Press = new(), Entry = new();
    protected readonly OwnedMotion Motion = new();
    private bool _hasSlot;
    internal Rect LastSlot { get; private set; }
    internal bool Leaving { get; private protected set; }
    internal double LayoutOffsetX => _dx.Position;

    protected ReactionBarButton() : base(16, 1)
    {
        Height = 32;
        _layoutDriver = new SpringDriver(() => { _layoutMove.X = _dx!.Position; _layoutMove.Y = _dy!.Position; });
        _dx = _layoutDriver.Add(0, ArcSpring.Morph);
        _dy = _layoutDriver.Add(0, ArcSpring.Morph);
        RenderTransformOrigin = new Point(.5, .5);
        var transforms = new TransformGroup();
        transforms.Children.Add(Press);
        transforms.Children.Add(Entry);
        transforms.Children.Add(_layoutMove);
        RenderTransform = transforms;
        Unloaded += (_, _) => _layoutDriver.Finish();
    }

    internal void ArrangeSlot(Rect slot)
    {
        if (_hasSlot && slot.Location != LastSlot.Location && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
        {
            double dx = LastSlot.X + _dx.Position - slot.X, dy = LastSlot.Y + _dy.Position - slot.Y;
            _dx.Launch(_dx.Velocity); _dy.Launch(_dy.Velocity);
            _dx.Snap(dx); _dy.Snap(dy);
            _layoutMove.X = dx; _layoutMove.Y = dy;
            _layoutDriver.Animate(_dx, 0, ArcSpring.Morph);
            _layoutDriver.Animate(_dy, 0, ArcSpring.Morph);
        }
        LastSlot = slot;
        _hasSlot = true;
        Arrange(slot);
    }

}

internal sealed class ReactionPill : ReactionBarButton
{
    private readonly Reactions _owner;
    private readonly RollingCount _count;
    private readonly FrameworkElement _glyph;
    private readonly ArcDataGridIcon? _icon;
    private readonly ReactionTip _tip;
    private readonly SpringDriver _pressDriver;
    private readonly ControlSpring _pressSpring;
    private readonly ReactionKick _kick;
    public string Id { get; }
    public bool Reacted { get; private set; }

    public ReactionPill(Reactions owner, string id, string label, object? icon)
    {
        _owner = owner;
        Id = id;
        Padding = new Thickness(9, 0, 11, 0);
        FontSize = 14;
        FontWeight = FontWeights.Medium;
        Roles = _ => Reacted
            ? (IsPressed ? "ReactionPressedActive" : IsMouseOver ? "ReactionPressedHover" : "AccentSubtle", IsMouseOver ? "ReactionPressedHoverEdge" : "ReactionPressedEdge", "AccentStrong")
            : (IsPressed || IsMouseOver ? "SurfaceMuted" : "Surface", IsMouseOver ? "BorderStrong" : "Border", IsMouseOver ? "Foreground" : "TextSecondary");
        if (icon is UIElement custom) { _glyph = new ContentPresenter { Content = custom, Width = 16, Height = 16 }; }
        else { _icon = ArcText.Icon(icon as string ?? "smile-plus", 16, Ink); _glyph = _icon; }
        _kick = new ReactionKick(_glyph);
        _count = new RollingCount(Ink);
        _tip = new ReactionTip(label);
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_glyph);
        _count.Margin = new Thickness(6, 0, 0, 0);
        row.Children.Add(_count);
        Content = new Grid { Children = { row, _tip.Anchor } };
        Click += (_, _) => _owner.Toggle(Id);
        _pressDriver = new SpringDriver(() => { Press.ScaleX = Press.ScaleY = _pressSpring!.Position; });
        _pressSpring = _pressDriver.Add(1, ArcSpring.Snappy);
        MouseEnter += (_, _) => { _tip.Hover(IsMouseOver); Panel.SetZIndex(this, IsMouseOver || IsKeyboardFocused ? 2 : 0); };
        MouseLeave += (_, _) => { _tip.Hover(IsMouseOver); Panel.SetZIndex(this, IsMouseOver || IsKeyboardFocused ? 2 : 0); };
        GotKeyboardFocus += (_, _) => { if (InputManager.Current.MostRecentInputDevice is KeyboardDevice) { _tip.Focused(true); } Panel.SetZIndex(this, 2); };
        LostKeyboardFocus += (_, _) => { _tip.Focused(false); Panel.SetZIndex(this, IsMouseOver ? 2 : 0); };
        Unloaded += (_, _) => { _pressDriver.Finish(); _kick.Stop(); };
    }

    public void Update(Reaction reaction, bool animate)
    {
        Reacted = reaction.Reacted;
        if (_icon is not null) { if (Reacted) { _icon.SetResourceReference(ArcDataGridIcon.FillProperty, "ArcWpf.Brush.ReactionIconFill"); } else { _icon.ClearValue(ArcDataGridIcon.FillProperty); } }
        _count.Set(reaction.Count, animate && ArcMotion.GetLevel(this) == ArcMotionLevel.Full);
        string noun = reaction.Count == 1 ? "reaction" : "reactions";
        AutomationProperties.SetName(this, $"{_tip.Label}, {reaction.Count} {noun}{(Reacted ? ", including yours" : "")}");
        Refresh(animate);
    }

    public void Kick(bool reduced) { if (!reduced) { _kick.Impulse(); } }
    internal bool IsTipShown => _tip.IsShown;
    internal string CountText => _count.Text;

    protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsPressedChanged(e);
        if (ArcMotion.GetLevel(this) == ArcMotionLevel.Full) { _pressDriver.Animate(_pressSpring, IsPressed ? .97 : 1, ArcSpring.Snappy); }
    }

    public void Enter(bool reduced)
    {
        Opacity = 0;
        if (reduced) { Motion.Double(this, OpacityProperty, 1, .16, true); return; }
        Entry.ScaleX = Entry.ScaleY = .5;
        Motion.Double(this, OpacityProperty, 1, .16, true, enter: true);
        Motion.Double(Entry, ScaleTransform.ScaleXProperty, 1, .36, true, spring: true);
        Motion.Double(Entry, ScaleTransform.ScaleYProperty, 1, .36, true, spring: true);
        ArcEffects.BlurIn(Motion, this, 4, .16);
    }

    public void Leave(bool animate, Action done)
    {
        Leaving = true;
        IsHitTestVisible = false;
        Focusable = false;
        if (!animate) { done(); return; }
        Motion.Double(this, OpacityProperty, 0, .16, true, completed: done);
        Motion.Double(Entry, ScaleTransform.ScaleXProperty, .6, .16, true);
        Motion.Double(Entry, ScaleTransform.ScaleYProperty, .6, .16, true);
        ArcEffects.BlurOut(Motion, this, 2, .16);
    }
}

internal sealed class ReactionAddButton : ReactionBarButton
{
    private readonly Reactions _owner;
    private readonly Grid _glyphs = new() { Width = 16, Height = 16 };
    private readonly ReactionTip _tip = new("Add reaction");
    private bool _open;

    internal ReactionAddButton(Reactions owner)
    {
        _owner = owner;
        Width = 38;
        Roles = _ => _open
            ? ("SurfaceMuted", "BorderStrong", "Foreground")
            : (IsPressed || IsMouseOver ? "SurfaceMuted" : "Surface", IsMouseOver ? "BorderStrong" : "Border", IsMouseOver || IsPressed ? "Foreground" : "TextMuted");
        AutomationProperties.SetName(this, "Add reaction");
        _glyphs.Children.Add(ArcText.Icon("smile-plus", 16, Ink));
        Content = new Grid { Children = { _glyphs, _tip.Anchor } };
        MouseEnter += (_, _) => { _tip.Hover(IsMouseOver && !_open); Panel.SetZIndex(this, IsMouseOver ? 2 : 0); };
        MouseLeave += (_, _) => { _tip.Hover(IsMouseOver && !_open); Panel.SetZIndex(this, IsMouseOver ? 2 : 0); };
    }

    internal bool IsOpen => _open;
    internal string GlyphName => _glyphs.Children.OfType<ArcDataGridIcon>().Last().Icon;

    internal void SetOpen(bool open, bool animate)
    {
        _open = open;
        if (open) { _tip.Hover(false); }
        animate = animate && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        Refresh(animate);
        foreach (var old in _glyphs.Children.OfType<ArcDataGridIcon>().ToList())
        {
            if (!animate) { _glyphs.Children.Remove(old); continue; }
            var scale = new ScaleTransform(1, 1, 8, 8);
            old.RenderTransform = scale;
            Motion.Double(old, OpacityProperty, 0, .16, true, completed: () => _glyphs.Children.Remove(old));
            Motion.Double(scale, ScaleTransform.ScaleXProperty, .6, .16, true);
            Motion.Double(scale, ScaleTransform.ScaleYProperty, .6, .16, true);
        }
        var next = ArcText.Icon(open ? "x" : "smile-plus", 16, Ink);
        _glyphs.Children.Add(next);
        if (!animate) { return; }
        var rise = new ScaleTransform(.6, .6, 8, 8);
        next.RenderTransform = rise;
        next.Opacity = 0;
        Motion.Double(next, OpacityProperty, 1, .16, true, enter: true);
        Motion.Double(rise, ScaleTransform.ScaleXProperty, 1, .36, true, spring: true);
        Motion.Double(rise, ScaleTransform.ScaleYProperty, 1, .36, true, spring: true);
    }
}

/// <summary>The label that appears after a short hover or on keyboard focus. It floats over neighbours and takes no input.</summary>
internal sealed class ReactionTip
{
    private readonly Border _box;
    private readonly TranslateTransform _move = new(0, 3);
    private readonly OwnedMotion _motion = new();
    private readonly DispatcherTimer _delay = new();
    private bool _shouldShow;
    public Canvas Anchor { get; } = new() { Width = 0, Height = 0, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
    public string Label { get; }
    public bool IsShown => _shouldShow;

    public ReactionTip(string label)
    {
        Label = label;
        var text = ArcText.Block(label, 12, 14.4);
        text.FontWeight = FontWeights.Normal;
        _box = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(999), Padding = new Thickness(9, 5, 9, 5), Child = text, Opacity = 0, RenderTransform = _move };
        _box.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        _box.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _box.SetResourceReference(UIElement.EffectProperty, "ArcWpf.Shadow.Raised");
        Anchor.Children.Add(_box);
        _box.SizeChanged += (_, _) => { Canvas.SetLeft(_box, -_box.ActualWidth / 2); Canvas.SetTop(_box, -_box.ActualHeight - 8); };
        _delay.Tick += (_, _) => { _delay.Stop(); Show(true); };
    }

    public void Hover(bool on) => Request(on, .45);
    public void Focused(bool on) => Request(on, .2);

    private void Request(bool on, double delay)
    {
        _delay.Stop();
        if (!on) { Show(false); return; }
        _delay.Interval = TimeSpan.FromSeconds(delay);
        _delay.Start();
    }

    private void Show(bool on)
    {
        _shouldShow = on;
        _motion.Double(_box, UIElement.OpacityProperty, on ? 1 : 0, .12, true);
        _motion.Double(_move, TranslateTransform.YProperty, on ? 0 : 3, .16, true);
    }
}

/// <summary>An impulse, not a keyframe: the icon starts at rest with velocity, overshoots and settles. A quick second tap adds to the motion.</summary>
internal sealed class ReactionKick(FrameworkElement glyph)
{
    private const double Stiffness = 420, Damping = 39, Step = 1 / 480d;
    private readonly ScaleTransform _scale = new();
    private readonly RotateTransform _rotate = new();
    private double _s, _sv, _r, _rv;
    private long _last;
    private bool _running;

    internal double ScaleOffset => _s;

    public void Impulse()
    {
        if (glyph.RenderTransform is not TransformGroup)
        {
            glyph.RenderTransformOrigin = new Point(.5, .5);
            var group = new TransformGroup();
            group.Children.Add(_scale);
            group.Children.Add(_rotate);
            glyph.RenderTransform = group;
        }
        _sv += 11;
        _rv -= 420;
        if (_running) { return; }
        _running = true;
        _last = System.Diagnostics.Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += Frame;
    }

    private void Frame(object? sender, EventArgs e)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        Advance(Math.Min(System.Diagnostics.Stopwatch.GetElapsedTime(_last, now).TotalSeconds, .05));
        _last = now;
    }

    internal void Advance(double seconds)
    {
        for (double t = 0; t < seconds; t += Step)
        {
            _sv += (-Stiffness * _s - Damping * _sv) * Step;
            _s += _sv * Step;
            _rv += (-Stiffness * _r - Damping * _rv) * Step;
            _r += _rv * Step;
        }
        _scale.ScaleX = _scale.ScaleY = 1 + _s;
        _rotate.Angle = _r;
        if (Math.Abs(_s) < .0005 && Math.Abs(_sv) < .01 && Math.Abs(_r) < .02 && Math.Abs(_rv) < .5) { Stop(); }
    }

    public void Stop()
    {
        if (_running) { CompositionTarget.Rendering -= Frame; }
        _running = false;
        _s = _sv = _r = _rv = 0;
        _scale.ScaleX = _scale.ScaleY = 1;
        _rotate.Angle = 0;
    }
}

/// <summary>Digits roll in the direction the count moved. Only the digits that change move, and the window springs when a digit is added.</summary>
internal sealed class RollingCount : Canvas
{
    private readonly Brush _ink;
    private readonly OwnedMotion _motion = new();
    private readonly SpringDriver _widthDriver;
    private readonly ControlSpring _width;
    private readonly Dictionary<string, TextBlock> _digits = [];
    private int _value = -1;
    public string Text => _value.ToString();

    public RollingCount(Brush ink)
    {
        _ink = ink;
        Height = 14;
        VerticalAlignment = VerticalAlignment.Center;
        _widthDriver = new SpringDriver(() => Width = _width!.Position);
        _width = _widthDriver.Add(0, ArcSpring.Morph);
        Unloaded += (_, _) => _widthDriver.Finish();
    }

    public void Set(int value, bool animate)
    {
        if (value == _value) { return; }
        double direction = value > _value ? 1 : -1;
        bool first = _value < 0;
        animate = animate && !first;
        _value = value;
        string text = value.ToString();
        var keep = new HashSet<string>();
        double x = 0;
        var placed = new List<(TextBlock Block, bool New)>();
        for (int i = 0; i < text.Length; i++)
        {
            string key = $"{text.Length - i}:{text[i]}";
            keep.Add(key);
            bool isNew = !_digits.TryGetValue(key, out var block);
            if (isNew)
            {
                block = new TextBlock { Text = text[i].ToString(), Foreground = _ink, FontSize = 14, FontWeight = FontWeights.Medium, LineHeight = 14, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, RenderTransform = new TranslateTransform() };
                _digits[key] = block;
                Children.Add(block);
            }
            block!.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            SetLeft(block, x);
            x += block.DesiredSize.Width;
            placed.Add((block, isNew));
        }
        foreach (var key in _digits.Keys.Where(key => !keep.Contains(key)).ToList())
        {
            var gone = _digits[key];
            _digits.Remove(key);
            if (!animate) { Children.Remove(gone); continue; }
            _motion.Double(gone, OpacityProperty, 0, .16, true, completed: () => Children.Remove(gone));
            _motion.Double(gone.RenderTransform, TranslateTransform.YProperty, direction * -.55 * 14, .16, true);
            ArcEffects.BlurOut(_motion, gone, 2, .16);
        }
        foreach (var (block, isNew) in placed)
        {
            if (!isNew || !animate) { continue; }
            block.Opacity = 0;
            ((TranslateTransform)block.RenderTransform).Y = direction * .55 * 14;
            _motion.Double(block, OpacityProperty, 1, .16, true, enter: true);
            _motion.Double(block.RenderTransform, TranslateTransform.YProperty, 0, .36, true, spring: true);
            ArcEffects.BlurIn(_motion, block, 2, .16);
        }
        if (animate) { _widthDriver.Animate(_width, x, ArcSpring.Morph); }
        else { _widthDriver.Snap(_width, x); Width = x; }
    }
}

/// <summary>The tray of every option. A shared highlight glides to the hovered or focused option, and a caption names it.</summary>
internal sealed class ReactionPicker : Grid
{
    internal const double Bleed = 28;
    private readonly List<(ReactionOption Option, ArcChromeButton Button, ArcDataGridIcon Icon, ScaleTransform Lit, TranslateTransform Lift, SpringDriver Driver, ControlSpring Scale, ControlSpring Y)> _items = [];
    private readonly Border _tray, _highlight;
    private readonly TranslateTransform _highlightMove = new();
    private readonly SpringDriver _highlightDriver;
    private readonly ControlSpring _highlightX;
    private readonly Grid _caption = new() { Height = 22 };
    private readonly ScaleTransform _trayScale = new();
    private readonly OwnedMotion _motion = new();
    private HashSet<string> _reacted;
    private int _focusIndex;
    private int? _hovered;
    private int _active = -1;
    private readonly bool _reduced;
    private string? _captionKey;
    public event Action<string>? Picked;
    public event Action? Dismissed;
    internal double HighlightX => _highlightX.Position;
    internal double HighlightTarget => _highlightX.Target;
    internal string CaptionText => _caption.Children.OfType<StackPanel>().LastOrDefault()?.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? string.Empty;
    internal void HoverForTest(int index) => Hover(index);
    internal Border Tray => _tray;

    public ReactionPicker(Reactions owner, IReadOnlyList<ReactionOption> options, HashSet<string> reacted, bool reduced)
    {
        _reacted = reacted;
        _reduced = reduced;
        Margin = new Thickness(Bleed);
        _highlightDriver = new SpringDriver(() => _highlightMove.X = _highlightX!.Position);
        _highlightX = _highlightDriver.Add(0, ArcSpring.Morph);
        _highlight = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(20), RenderTransform = _highlightMove, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        _highlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        AutomationProperties.SetName(row, "Reactions");
        for (int i = 0; i < options.Count; i++) { row.Children.Add(BuildOption(options[i], i)); }
        row.MouseLeave += (_, _) => { _hovered = null; UpdateActive(); };
        row.PreviewKeyDown += OnKey;
        var stack = new Grid();
        stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stack.Children.Add(_highlight);
        stack.Children.Add(row);
        _caption.Margin = new Thickness(0, 2, 0, 0);
        Grid.SetRow(_caption, 1);
        stack.Children.Add(_caption);
        _tray = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(26), Padding = new Thickness(5), Child = stack, RenderTransform = _trayScale, RenderTransformOrigin = new Point(.5, 1) };
        _tray.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        _tray.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        var shadow = new Border { CornerRadius = new CornerRadius(26) };
        shadow.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        shadow.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        shadow.RenderTransform = _trayScale;
        shadow.RenderTransformOrigin = new Point(.5, 1);
        Children.Add(shadow);
        Children.Add(_tray);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Dismissed?.Invoke(); } };
        UpdateActive();
        Refresh(reacted);
    }

    private ArcChromeButton BuildOption(ReactionOption option, int index)
    {
        var button = new ArcChromeButton(20, 0) { Width = 40, Height = 40, IsTabStop = index == 0, Margin = new Thickness(index == 0 ? 0 : 2, 0, 0, 0) };
        var lit = new ScaleTransform();
        var lift = new TranslateTransform();
        var icon = ArcText.Icon(option.Icon as string ?? "smile-plus", 20, button.Ink);
        var group = new TransformGroup();
        group.Children.Add(lit);
        group.Children.Add(lift);
        icon.RenderTransformOrigin = new Point(.5, .5);
        icon.RenderTransform = group;
        button.Content = icon;
        AutomationProperties.SetName(button, option.Label);
        button.Roles = _ =>
        {
            bool chosen = _reacted.Contains(option.Id);
            return (chosen ? "AccentSubtle" : null, chosen ? "ReactionOptionRing" : null, chosen ? "AccentStrong" : button.IsMouseOver ? "Foreground" : "TextSecondary");
        };
        button.MouseMove += (_, _) => Hover(index);
        button.GotKeyboardFocus += (_, _) => { _focusIndex = index; UpdateActive(); };
        button.Click += (_, _) => Picked?.Invoke(option.Id);
        ControlSpring? scale = null, y = null;
        var driver = new SpringDriver(() => { lit.ScaleX = lit.ScaleY = scale!.Position; lift.Y = y!.Position; });
        scale = driver.Add(1, ArcSpring.Snappy);
        y = driver.Add(0, ArcSpring.Snappy);
        _items.Add((option, button, icon, lit, lift, driver, scale, y));
        return button;
    }

    private void Hover(int index)
    {
        if (_hovered == index) { return; }
        _hovered = index;
        UpdateActive();
    }

    private void UpdateActive()
    {
        int active = Math.Min(_hovered ?? _focusIndex, _items.Count - 1);
        for (int i = 0; i < _items.Count; i++) { _items[i].Button.IsTabStop = i == _focusIndex; }
        if (active == _active) { UpdateCaption(); return; }
        bool first = _active < 0;
        int previous = _active;
        _active = active;
        double target = active * 42;
        if (first || _reduced) { _highlightDriver.Snap(_highlightX, target); } else { _highlightDriver.Animate(_highlightX, target, ArcSpring.Morph); }
        if (!_reduced)
        {
            if (previous >= 0) { var old = _items[previous]; old.Driver.Animate(old.Scale, 1, ArcSpring.Snappy); old.Driver.Animate(old.Y, 0, ArcSpring.Snappy); }
            var now = _items[active];
            now.Driver.Animate(now.Scale, 1.14, ArcSpring.Snappy);
            now.Driver.Animate(now.Y, -1, ArcSpring.Snappy);
        }
        UpdateCaption();
    }

    private void UpdateCaption()
    {
        if (_active < 0) { return; }
        var option = _items[_active].Option;
        bool chosen = _reacted.Contains(option.Id);
        string key = $"{option.Id}:{chosen}";
        if (key == _captionKey) { return; }
        bool animate = _captionKey is not null && !_reduced;
        _captionKey = key;
        foreach (var old in _caption.Children.OfType<StackPanel>().ToList())
        {
            if (!animate) { _caption.Children.Remove(old); continue; }
            _motion.Double(old, OpacityProperty, 0, .16, true, completed: () => _caption.Children.Remove(old));
            _motion.Double(old.RenderTransform, TranslateTransform.YProperty, -3.6, .16, true);
            ArcEffects.BlurOut(_motion, old, 2, .16);
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransform = new TranslateTransform() };
        var label = ArcText.Block(option.Label, 12, 12, "TextSecondary");
        if (chosen)
        {
            var check = new ArcDataGridIcon { Icon = "check", Size = 12, StrokeThickness = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            check.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextSecondary");
            row.Children.Add(check);
        }
        row.Children.Add(label);
        _caption.Children.Add(row);
        if (!animate) { return; }
        row.Opacity = 0;
        ((TranslateTransform)row.RenderTransform).Y = 3.6;
        _motion.Double(row, OpacityProperty, 1, .24, true, enter: true);
        _motion.Double(row.RenderTransform, TranslateTransform.YProperty, 0, .24, true, enter: true);
        ArcEffects.BlurIn(_motion, row, 4, .24);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        int count = _items.Count;
        int next = e.Key switch { Key.Right or Key.Down => (_focusIndex + 1) % count, Key.Left or Key.Up => (_focusIndex - 1 + count) % count, Key.Home => 0, Key.End => count - 1, _ => -1 };
        if (next < 0) { return; }
        e.Handled = true;
        _hovered = null;
        _focusIndex = next;
        UpdateActive();
        _items[next].Button.Focus();
    }

    public void Refresh(HashSet<string> reacted)
    {
        _reacted = reacted;
        foreach (var item in _items)
        {
            item.Button.BorderThickness = new Thickness(_reacted.Contains(item.Option.Id) ? 1 : 0);
            if (_reacted.Contains(item.Option.Id)) { item.Icon.SetResourceReference(ArcDataGridIcon.FillProperty, "ArcWpf.Brush.ReactionIconFill"); } else { item.Icon.ClearValue(ArcDataGridIcon.FillProperty); }
            item.Button.Refresh(false);
        }
        UpdateCaption();
    }

    public void PlayOpen()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { if (_items.Count > 0) { _items[_focusIndex].Button.Focus(); } });
        if (_reduced) { Opacity = 0; _motion.Double(this, OpacityProperty, 1, .16, true); return; }
        Opacity = 0;
        _trayScale.ScaleX = _trayScale.ScaleY = .5;
        _motion.Double(this, OpacityProperty, 1, .16, true);
        _motion.Double(_trayScale, ScaleTransform.ScaleXProperty, 1, .36, true, spring: true);
        _motion.Double(_trayScale, ScaleTransform.ScaleYProperty, 1, .36, true, spring: true);
        ArcEffects.BlurIn(_motion, _tray, 4, .16);
        for (int i = 0; i < _items.Count; i++)
        {
            double delay = .04 + i * .035;
            var button = _items[i].Button;
            var rise = new ScaleTransform(.6, .6, 20, 20);
            button.RenderTransform = rise;
            button.Opacity = 0;
            _motion.Double(button, OpacityProperty, 1, .16, true, delay: delay, enter: true);
            _motion.Double(rise, ScaleTransform.ScaleXProperty, 1, .36, true, spring: true, delay: delay);
            _motion.Double(rise, ScaleTransform.ScaleYProperty, 1, .36, true, spring: true, delay: delay);
        }
    }

    public void PlayClose(bool immediate, Action done)
    {
        IsHitTestVisible = false;
        if (immediate) { done(); return; }
        _motion.Double(this, OpacityProperty, 0, .16, true, completed: done);
        _motion.Double(_trayScale, ScaleTransform.ScaleXProperty, .9, .16, true);
        _motion.Double(_trayScale, ScaleTransform.ScaleYProperty, .9, .16, true);
        ArcEffects.BlurOut(_motion, _tray, 2, .16);
    }
}
