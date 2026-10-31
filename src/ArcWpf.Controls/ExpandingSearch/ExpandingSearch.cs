using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum ExpandingSearchAnchor { Start, End }

public sealed class ExpandingSearchItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>One short line under the title, such as the type, owner, or last edit.</summary>
    public string? Meta { get; set; }
    /// <summary>Results gather under this heading, in the order groups first appear in Items.</summary>
    public string? Group { get; set; }
    /// <summary>A stroked icon on the 24-unit grid, drawn at 16 DIP.</summary>
    public Geometry? Icon { get; set; }
    /// <summary>Extra words that should also find this item.</summary>
    public IList<string> Keywords { get; set; } = [];
}

public sealed class ExpandingSearchItemEventArgs(RoutedEvent routedEvent, object source, ExpandingSearchItem item) : RoutedEventArgs(routedEvent, source)
{
    public ExpandingSearchItem Item { get; } = item;
}

[TemplatePart(Name = "PART_Shell", Type = typeof(Border))]
[TemplatePart(Name = "PART_Trigger", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Icon", Type = typeof(Path))]
[TemplatePart(Name = "PART_Input", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Placeholder", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Clear", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_Panel", Type = typeof(Border))]
[TemplatePart(Name = "PART_Clip", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Status", Type = typeof(TextBlock))]
public class ExpandingSearch : Control
{
    // expanding-search.tsx: one shell is both the button and the field; its width rides a spring and its radius follows the width.
    private const double FallbackSize = 44;
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ExpandingSearch), new FrameworkPropertyMetadata("Search", OnTextChanged));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ExpandingSearch), new FrameworkPropertyMetadata(null, OnTextChanged));
    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(nameof(Query), typeof(string), typeof(ExpandingSearch), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnQueryChanged, (_, v) => v ?? string.Empty));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ExpandingSearch), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsExpandedChanged));
    public static readonly DependencyProperty ExpandedWidthProperty = DependencyProperty.Register(nameof(ExpandedWidth), typeof(double), typeof(ExpandingSearch), new FrameworkPropertyMetadata(360d, OnSizeChanged), v => v is double d && d > 0 && double.IsFinite(d));
    public static readonly DependencyProperty MaxResultsProperty = DependencyProperty.Register(nameof(MaxResults), typeof(int), typeof(ExpandingSearch), new FrameworkPropertyMetadata(6, OnContentChanged), v => v is int n && n > 0);
    public static readonly DependencyProperty AnchorProperty = DependencyProperty.Register(nameof(Anchor), typeof(ExpandingSearchAnchor), typeof(ExpandingSearch), new FrameworkPropertyMetadata(ExpandingSearchAnchor.End, OnSizeChanged));
    public static readonly DependencyProperty SuggestionsLabelProperty = DependencyProperty.Register(nameof(SuggestionsLabel), typeof(string), typeof(ExpandingSearch), new FrameworkPropertyMetadata("Recent", OnContentChanged));
    public static readonly DependencyProperty EmptyHintProperty = DependencyProperty.Register(nameof(EmptyHint), typeof(string), typeof(ExpandingSearch), new FrameworkPropertyMetadata("Try a shorter word or check the spelling.", OnContentChanged));
    public static readonly RoutedEvent ItemSelectedEvent = EventManager.RegisterRoutedEvent(nameof(ItemSelected), RoutingStrategy.Bubble, typeof(EventHandler<ExpandingSearchItemEventArgs>), typeof(ExpandingSearch));
    public static readonly RoutedEvent ExpandedEvent = EventManager.RegisterRoutedEvent(nameof(Expanded), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ExpandingSearch));
    public static readonly RoutedEvent CollapsedEvent = EventManager.RegisterRoutedEvent(nameof(Collapsed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ExpandingSearch));
    private static readonly string[] ColorKeys = ["Border", "BorderStrong", "Foreground", "Surface", "SurfaceMuted", "TextSecondary", "TextMuted"];
    private static readonly DependencyProperty[] ColorProbes = ColorKeys.Select(key => DependencyProperty.Register("Probe" + key, typeof(Brush), typeof(ExpandingSearch), new PropertyMetadata(null, (d, _) => ((ExpandingSearch)d).UpdateColors(false)))).ToArray();

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Placeholder { get => (string?)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string Query { get => (string)GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    /// <summary>The widest the field grows. It never grows past the control's own width.</summary>
    public double ExpandedWidth { get => (double)GetValue(ExpandedWidthProperty); set => SetValue(ExpandedWidthProperty, value); }
    public int MaxResults { get => (int)GetValue(MaxResultsProperty); set => SetValue(MaxResultsProperty, value); }
    /// <summary>The edge the button sits on; the field grows away from it.</summary>
    public ExpandingSearchAnchor Anchor { get => (ExpandingSearchAnchor)GetValue(AnchorProperty); set => SetValue(AnchorProperty, value); }
    public string SuggestionsLabel { get => (string)GetValue(SuggestionsLabelProperty); set => SetValue(SuggestionsLabelProperty, value); }
    public string EmptyHint { get => (string)GetValue(EmptyHintProperty); set => SetValue(EmptyHintProperty, value); }
    public ObservableCollection<ExpandingSearchItem> Items { get; } = [];
    /// <summary>Shown before anything is typed, such as recent searches.</summary>
    public ObservableCollection<ExpandingSearchItem> Suggestions { get; } = [];
    public event EventHandler<ExpandingSearchItemEventArgs> ItemSelected { add => AddHandler(ItemSelectedEvent, value); remove => RemoveHandler(ItemSelectedEvent, value); }
    public event RoutedEventHandler Expanded { add => AddHandler(ExpandedEvent, value); remove => RemoveHandler(ExpandedEvent, value); }
    public event RoutedEventHandler Collapsed { add => AddHandler(CollapsedEvent, value); remove => RemoveHandler(CollapsedEvent, value); }

    private enum Mode { None, Suggestions, Results, Empty }
    private sealed record Group(string Name, List<ExpandingSearchItem> Items);
    private sealed record Row(ExpandingSearchItem Item, Border Element, Path? Icon);

    private Border? _shell, _panel, _clearSurface;
    private System.Windows.Controls.Button? _trigger, _clear;
    private Path? _icon, _clearIcon;
    private TextBox? _input;
    private TextBlock? _placeholder, _status;
    private Popup? _popup;
    private Grid? _clip;
    private Window? _window;
    private readonly SolidColorBrush _shellBorder = new(), _shellBackground = new(), _iconBrush = new(), _clearBackground = new(), _clearForeground = new();
    private readonly ScaleTransform _press = new(), _clearScale = new(.6, .6);
    private readonly OwnedMotion _motion = new(), _colors = new(), _clearMotion = new(), _panelMotion = new();
    private readonly SpringDriver _springs;
    private readonly ControlSpring _width, _height, _travel;
    private double _collapsedSize = FallbackSize, _fullSize = 360;
    private bool _open, _focused, _pressing, _keyboard, _syncingQuery, _panelShown, _clearShown;
    private int _active = -1, _panelVersion;
    private Mode _mode = Mode.None;
    private string _layoutKey = "", _activeId = "";
    private List<Row> _rows = [];
    private FrameworkElement? _view, _highlight;
    private ScrollArea? _scroller;
    private Grid? _list;

    static ExpandingSearch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ExpandingSearch), new FrameworkPropertyMetadata(typeof(ExpandingSearch)));
        FocusableProperty.OverrideMetadata(typeof(ExpandingSearch), new FrameworkPropertyMetadata(false));
        IsEnabledProperty.OverrideMetadata(typeof(ExpandingSearch), new FrameworkPropertyMetadata((d, e) => { var s = (ExpandingSearch)d; if (!(bool)e.NewValue) s.Collapse(false); s.UpdateColors(false); }));
    }

    public ExpandingSearch()
    {
        _springs = new SpringDriver(ApplySprings);
        _width = _springs.Add(FallbackSize, .42); _height = _springs.Add(0, .4); _travel = _springs.Add(0, .26);
        for (int i = 0; i < ColorKeys.Length; i++) SetResourceReference(ColorProbes[i], "ArcWpf.Brush." + ColorKeys[i]);
        Items.CollectionChanged += CollectionChanged; Suggestions.CollectionChanged += CollectionChanged;
        Loaded += (_, _) => { AttachWindow(); UpdateSizes(); };
        Unloaded += (_, _) => { Collapse(false); DetachWindow(); _springs.Finish(); _motion.Stop(); _colors.Stop(); _clearMotion.Stop(); _panelMotion.Stop(); ShowPanel(false, false); };
    }

    public override void OnApplyTemplate()
    {
        Release();
        base.OnApplyTemplate();
        _shell = GetTemplateChild("PART_Shell") as Border;
        _trigger = GetTemplateChild("PART_Trigger") as System.Windows.Controls.Button;
        _icon = GetTemplateChild("PART_Icon") as Path;
        _input = GetTemplateChild("PART_Input") as TextBox;
        _placeholder = GetTemplateChild("PART_Placeholder") as TextBlock;
        _clear = GetTemplateChild("PART_Clear") as System.Windows.Controls.Button;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _panel = GetTemplateChild("PART_Panel") as Border;
        _clip = GetTemplateChild("PART_Clip") as Grid;
        _status = GetTemplateChild("PART_Status") as TextBlock;
        if (_shell is not null)
        {
            _shell.BorderBrush = _shellBorder; _shell.Background = _shellBackground;
            _shell.RenderTransformOrigin = new Point(.5, .5); _shell.RenderTransform = _press;
            _shell.MouseEnter += ShellHover; _shell.MouseLeave += ShellHover;
            _shell.SizeChanged += (_, _) => UpdateSizes();
        }
        if (_icon is not null) _icon.Stroke = _iconBrush;
        if (_trigger is not null)
        {
            _trigger.Click += TriggerClick;
            _trigger.PreviewMouseLeftButtonDown += TriggerPress; _trigger.PreviewMouseLeftButtonUp += TriggerRelease; _trigger.LostMouseCapture += TriggerRelease;
            _trigger.GotKeyboardFocus += FocusMoved; _trigger.LostKeyboardFocus += FocusMoved;
        }
        if (_input is not null)
        {
            _input.Text = Query;
            _input.TextChanged += InputChanged; _input.PreviewKeyDown += InputKeyDown;
            _input.GotKeyboardFocus += InputFocused; _input.LostKeyboardFocus += InputBlurred;
        }
        if (_clear is not null)
        {
            _clear.Click += ClearClick;
            _clear.MouseEnter += (_, _) => UpdateClearColors(true); _clear.MouseLeave += (_, _) => UpdateClearColors(true);
            _clear.RenderTransformOrigin = new Point(.5, .5); _clear.RenderTransform = _clearScale;
            _clearSurface = _clear.Template?.FindName("Surface", _clear) as Border;
            _clear.ApplyTemplate();
            _clearSurface ??= _clear.Template?.FindName("Surface", _clear) as Border;
            _clearIcon = _clear.Template?.FindName("Glyph", _clear) as Path;
            if (_clearSurface is not null) _clearSurface.Background = _clearBackground;
            if (_clearIcon is not null) _clearIcon.Stroke = _clearForeground;
        }
        if (_clip is not null) _clip.SizeChanged += ClipSized;
        if (_popup is not null) _popup.Closed += PopupClosed;
        ApplyText(); ApplyAnchor();
        _clearShown = false; ShowClear(false);
        ApplyInert(false);
        UpdateSizes();
        UpdateColors(false);
        if (IsLoaded) AttachWindow();
    }

    private void Release()
    {
        if (_trigger is not null) { _trigger.Click -= TriggerClick; _trigger.PreviewMouseLeftButtonDown -= TriggerPress; _trigger.PreviewMouseLeftButtonUp -= TriggerRelease; _trigger.LostMouseCapture -= TriggerRelease; _trigger.GotKeyboardFocus -= FocusMoved; _trigger.LostKeyboardFocus -= FocusMoved; }
        if (_input is not null) { _input.TextChanged -= InputChanged; _input.PreviewKeyDown -= InputKeyDown; _input.GotKeyboardFocus -= InputFocused; _input.LostKeyboardFocus -= InputBlurred; }
        if (_clear is not null) _clear.Click -= ClearClick;
        if (_shell is not null) { _shell.MouseEnter -= ShellHover; _shell.MouseLeave -= ShellHover; }
        if (_clip is not null) _clip.SizeChanged -= ClipSized;
        if (_popup is not null) { _popup.Closed -= PopupClosed; _popup.IsOpen = false; }
        _springs.Stop(); _motion.Stop(); _clearMotion.Stop(); _panelMotion.Stop(); _colors.Stop();
        _panelShown = false; _view = null; _highlight = null; _scroller = null; _list = null; _rows = []; _mode = Mode.None; _layoutKey = ""; _activeId = "";
        _shell = _panel = _clearSurface = null; _trigger = _clear = null; _icon = _clearIcon = null; _input = null; _placeholder = _status = null; _popup = null; _clip = null;
    }

    private ArcMotionLevel Level => ArcMotion.GetLevel(this);
    private bool Animating => IsLoaded && IsVisible && Level != ArcMotionLevel.Off;
    private bool Reduced => Level != ArcMotionLevel.Full;

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ExpandingSearch)d).ApplyText();
    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var s = (ExpandingSearch)d; s.ApplyAnchor(); s.UpdateSizes(); }
    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ExpandingSearch)d).Refresh();
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
    private static void OnQueryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var s = (ExpandingSearch)d;
        if (s._input is not null && s._input.Text != (string)e.NewValue) { s._syncingQuery = true; s._input.Text = (string)e.NewValue; s._syncingQuery = false; }
        s.ApplyText();
        s.ShowClear(true);
        s.Refresh();
    }
    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var s = (ExpandingSearch)d;
        if ((bool)e.NewValue == s._open) return;
        if ((bool)e.NewValue) s.Expand(); else s.Collapse(false);
    }

    private void ApplyText()
    {
        if (_placeholder is not null)
        {
            _placeholder.Text = Placeholder ?? Label;
            _placeholder.Visibility = string.IsNullOrEmpty(Query) ? Visibility.Visible : Visibility.Hidden;
        }
        if (_trigger is not null) AutomationProperties.SetName(_trigger, Label);
        if (_input is not null) AutomationProperties.SetName(_input, Label);
    }

    private void ApplyAnchor()
    {
        var side = Anchor == ExpandingSearchAnchor.End ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        if (_shell is not null) _shell.HorizontalAlignment = side;
    }

    // The field grows to its own limit or the control's width, whichever is smaller; container resizes are followed at once.
    private void UpdateSizes()
    {
        if (_shell is null) return;
        _collapsedSize = _shell.ActualHeight > 0 ? _shell.ActualHeight : FallbackSize;
        double lane = ActualWidth > 0 ? ActualWidth : ExpandedWidth;
        _fullSize = Math.Max(_collapsedSize, Math.Min(ExpandedWidth, lane));
        double goal = _open ? _fullSize : _collapsedSize;
        if (_springs.IsRunning) _width.Target = goal; else _springs.Snap(_width, goal);
        if (_scroller is not null) _scroller.Width = _fullSize - 2;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); UpdateSizes(); }

    private void ApplySprings()
    {
        if (_shell is null) return;
        double width = _width.Position;
        _shell.Width = Math.Max(0, width);
        // The corner radius follows the width: a circle while collapsed, the control radius once open.
        double open = Math.Clamp((width - _collapsedSize) / Math.Max(1, _fullSize - _collapsedSize), 0, 1);
        double radius = _collapsedSize / 2 - (_collapsedSize / 2 - 18) * open;
        _shell.CornerRadius = new CornerRadius(radius);
        if (_panel is not null) _panel.Width = Math.Max(0, width);
        if (_clip is not null && !double.IsNaN(_clip.Height) && _panelShown) _clip.Height = Math.Max(0, _height.Position);
        if (_highlight?.RenderTransform is TranslateTransform move) move.Y = _travel.Position;
        PlacePopup();
    }

    // Opening grows on the morph spring; folding back settles on the smooth spring so the circle never dips below its size.
    private void MorphTo(double goal)
    {
        if (!Animating || Reduced) { _springs.Snap(_width, goal); return; }
        _springs.Animate(_width, goal, goal >= _width.Position ? .42 : .4);
    }

    private void Expand()
    {
        if (_open || !IsEnabled) { if (!IsEnabled) SetCurrentValue(IsExpandedProperty, false); return; }
        _open = true; _focused = true;
        SetCurrentValue(IsExpandedProperty, true);
        ApplyInert(true);
        _input?.Focus();
        MorphTo(_fullSize);
        UpdateColors(true);
        Refresh();
        RaiseEvent(new RoutedEventArgs(ExpandedEvent, this));
    }

    private void Collapse(bool returnFocus)
    {
        if (!_open) return;
        _open = false; _focused = false; _active = -1;
        SetCurrentValue(IsExpandedProperty, false);
        SetCurrentValue(QueryProperty, string.Empty);
        ApplyInert(true);
        if (returnFocus) _trigger?.Focus();
        else if (_input?.IsKeyboardFocusWithin == true) Keyboard.ClearFocus();
        MorphTo(_collapsedSize);
        UpdateColors(true);
        Refresh();
        RaiseEvent(new RoutedEventArgs(CollapsedEvent, this));
    }

    // The placeholder waits for the shape to start moving, then fades in; on close it leaves first.
    private void ApplyInert(bool animate)
    {
        if (_trigger is not null) _trigger.Visibility = _open ? Visibility.Collapsed : Visibility.Visible;
        if (_input is null) return;
        _input.IsHitTestVisible = _open; _input.Focusable = _open; _input.IsTabStop = _open;
        var host = (UIElement?)_input.Parent ?? _input;
        bool motion = animate && Animating;
        if (_open) _motion.Double(host, OpacityProperty, 1, Reduced ? .15 : .2, motion, delay: Reduced ? 0 : .08, enter: true);
        else _motion.Double(host, OpacityProperty, 0, Reduced ? .1 : .08, motion);
        ShowClear(animate);
    }

    private void TriggerClick(object sender, RoutedEventArgs e) { e.Handled = true; Expand(); }
    // A compact press while it is still a button; the morph takes over on release.
    private void TriggerPress(object sender, MouseButtonEventArgs e) { _pressing = true; UpdatePress(); }
    private void TriggerRelease(object sender, EventArgs e) { if (!_pressing) return; _pressing = false; UpdatePress(); }
    private void UpdatePress()
    {
        bool motion = Animating && !Reduced;
        double scale = _pressing && !_open && motion ? .97 : 1;
        double seconds = scale < 1 ? .12 : .58;
        _motion.Double(_press, ScaleTransform.ScaleXProperty, scale, seconds, motion, spring: true);
        _motion.Double(_press, ScaleTransform.ScaleYProperty, scale, seconds, motion, spring: true);
    }

    private void FocusMoved(object sender, KeyboardFocusChangedEventArgs e) => UpdateColors(true);
    private void ShellHover(object sender, MouseEventArgs e) => UpdateColors(true);

    private Color Probe(int index) => (GetValue(ColorProbes[index]) as SolidColorBrush)?.Color ?? Colors.Transparent;
    private void UpdateColors(bool animate)
    {
        bool motion = animate && Animating && !Reduced;
        bool hover = _shell?.IsMouseOver == true && IsEnabled;
        bool focus = _input?.IsKeyboardFocused == true || _trigger?.IsKeyboardFocused == true && InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        // Border, BorderStrong, Foreground, Surface, SurfaceMuted, TextSecondary.
        Color border = focus ? Probe(2) : _open ? (hover ? Probe(2) : Probe(1)) : Probe(0);
        Color background = !_open && hover ? Probe(4) : Probe(3);
        Color ink = _open || hover || focus ? Probe(2) : Probe(5);
        _colors.Color(_shellBorder, border, .16, motion);
        _colors.Color(_shellBackground, background, .16, motion);
        _colors.Color(_iconBrush, ink, .16, motion);
        UpdateClearColors(animate);
    }
    private void UpdateClearColors(bool animate)
    {
        bool hover = _clear?.IsMouseOver == true;
        bool motion = animate && Animating && !Reduced;
        var transparent = Probe(4); transparent.A = 0;
        _colors.Color(_clearBackground, hover ? Probe(4) : transparent, .16, motion);
        _colors.Color(_clearForeground, hover ? Probe(2) : Probe(6), .16, motion);
    }

    private void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingQuery || _input is null) return;
        string next = _input.Text;
        _keyboard = false;
        _active = next.Trim().Length > 0 ? 0 : -1;
        SetCurrentValue(QueryProperty, next);
    }

    private void InputFocused(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_open) return;
        _focused = true;
        UpdateColors(true);
        Refresh();
    }

    private void InputBlurred(object sender, KeyboardFocusChangedEventArgs e)
    {
        UpdateColors(true);
        // Switching windows keeps the search as it was; the field refocuses on return.
        if (e.NewFocus is null || _window?.IsActive == false) return;
        Blur();
    }

    private void Blur()
    {
        _focused = false;
        if (_open && string.IsNullOrWhiteSpace(_input?.Text)) { Collapse(false); return; }
        // The typed query stays; focus leaves the field so the next click back reopens the panel.
        if (_input?.IsKeyboardFocusWithin == true) Keyboard.ClearFocus();
        Refresh();
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _active = -1;
        SetCurrentValue(QueryProperty, string.Empty);
        _input?.Focus();
    }

    private void InputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; Collapse(true); return; }
        if (!PanelOpen || _rows.Count == 0) return;
        if (e.Key is Key.Down or Key.Up)
        {
            e.Handled = true;
            int step = e.Key == Key.Down ? 1 : -1;
            _keyboard = true;
            int index = Math.Min(_active, _rows.Count - 1);
            SetActive(index < 0 ? (step > 0 ? 0 : _rows.Count - 1) : (index + step + _rows.Count) % _rows.Count);
        }
        else if (e.Key == Key.Enter && _active >= 0 && _active < _rows.Count)
        {
            e.Handled = true;
            Choose(_rows[_active].Item);
        }
    }

    private void Choose(ExpandingSearchItem item)
    {
        Collapse(true);
        RaiseEvent(new ExpandingSearchItemEventArgs(ItemSelectedEvent, this, item));
    }

    // The clear button pops in on the snappy spring with a soft blur, and leaves on a short fade.
    private void ShowClear(bool animate)
    {
        if (_clear is null) return;
        bool show = _open && !string.IsNullOrEmpty(Query);
        if (show == _clearShown && animate) return;
        _clearShown = show;
        bool motion = animate && Animating;
        if (show)
        {
            _clear.Visibility = Visibility.Visible; _clear.IsHitTestVisible = true;
            if (!motion) { _clearMotion.Stop(); _clear.Opacity = 1; _clearScale.ScaleX = _clearScale.ScaleY = 1; _clear.Effect = null; return; }
            if (Reduced) { _clearScale.ScaleX = _clearScale.ScaleY = 1; _clearMotion.Double(_clear, OpacityProperty, 1, .15, true); return; }
            var blur = new BlurEffect { Radius = _clear.Opacity < .05 ? 2 : 0 }; _clear.Effect = blur;
            _clearMotion.Double(_clear, OpacityProperty, 1, .16, true);
            _clearMotion.Double(_clearScale, ScaleTransform.ScaleXProperty, 1, .26, true, spring: true);
            _clearMotion.Double(_clearScale, ScaleTransform.ScaleYProperty, 1, .26, true, spring: true);
            _clearMotion.Double(blur, BlurEffect.RadiusProperty, 0, .16, true, completed: () => { if (ReferenceEquals(_clear?.Effect, blur)) _clear!.Effect = null; });
            return;
        }
        _clear.IsHitTestVisible = false;
        if (!motion) { _clearMotion.Stop(); _clear.Opacity = 0; _clear.Visibility = Visibility.Hidden; _clearScale.ScaleX = _clearScale.ScaleY = .6; _clear.Effect = null; return; }
        double seconds = Reduced ? .1 : .12;
        if (!Reduced)
        {
            var blur = new BlurEffect { Radius = 0 }; _clear.Effect = blur;
            _clearMotion.Double(blur, BlurEffect.RadiusProperty, 2, seconds, true);
            _clearMotion.Double(_clearScale, ScaleTransform.ScaleXProperty, .6, seconds, true);
            _clearMotion.Double(_clearScale, ScaleTransform.ScaleYProperty, .6, seconds, true);
        }
        _clearMotion.Double(_clear, OpacityProperty, 0, seconds, true, completed: () => { if (!_clearShown && _clear is not null) { _clear.Visibility = Visibility.Hidden; _clear.Effect = null; } });
    }

    // Title starts beat word starts, which beat matches inside a word, which beat keyword and meta matches. Ties keep order.
    internal static List<ExpandingSearchItem> Rank(IEnumerable<ExpandingSearchItem> items, string query, int limit)
    {
        string q = query.ToLowerInvariant();
        var scored = new List<(ExpandingSearchItem Item, int Score, int Order)>();
        int order = 0;
        foreach (var item in items)
        {
            string title = item.Title.ToLowerInvariant();
            int at = title.IndexOf(q, StringComparison.Ordinal);
            bool extra = new[] { item.Meta ?? "" }.Concat(item.Keywords).Any(word => word.ToLowerInvariant().Contains(q, StringComparison.Ordinal));
            int score = at == 0 ? 0 : at > 0 && (char.IsWhiteSpace(title[at - 1]) || title[at - 1] is '-' or '/') ? 1 : at > 0 ? 2 : extra ? 3 : -1;
            if (score >= 0) scored.Add((item, score, order));
            order++;
        }
        return scored.OrderBy(entry => entry.Score).ThenBy(entry => entry.Order).Take(limit).Select(entry => entry.Item).ToList();
    }

    private static List<Group> GroupBy(List<ExpandingSearchItem> list, IList<ExpandingSearchItem> source)
    {
        var order = new Dictionary<string, int>();
        for (int i = 0; i < source.Count; i++) order.TryAdd(source[i].Group ?? "", i);
        return list.GroupBy(item => item.Group ?? "").OrderBy(group => order.GetValueOrDefault(group.Key)).Select(group => new Group(group.Key, group.ToList())).ToList();
    }

    private bool PanelOpen => _open && _focused && _mode != Mode.None;

    // Rebuilds the view whenever the mode or the visible set changes, then shows or folds the panel.
    private void Refresh()
    {
        string trimmed = Query.Trim();
        var results = trimmed.Length > 0 ? Rank(Items, trimmed, MaxResults) : [];
        var mode = trimmed.Length == 0 ? (Suggestions.Count > 0 ? Mode.Suggestions : Mode.None) : results.Count > 0 ? Mode.Results : Mode.Empty;
        var groups = mode == Mode.Suggestions ? [new Group(SuggestionsLabel, Suggestions.ToList())] : mode == Mode.Results ? GroupBy(results, Items) : new List<Group>();
        bool panelOpen = _open && _focused && mode != Mode.None;
        if (!panelOpen) { _mode = mode; ShowPanel(false, true); Announce(""); return; }
        string key = mode + "|" + string.Join("|", groups.SelectMany(g => g.Items).Select(i => i.Id)) + "|" + (mode == Mode.Empty ? trimmed : mode == Mode.Results ? trimmed : "");
        bool modeChanged = mode != _mode || _view is null;
        _mode = mode;
        if (modeChanged || key != _layoutKey) BuildView(mode, groups, trimmed, modeChanged);
        _layoutKey = key;
        ShowPanel(true, true);
        int count = _rows.Count;
        Announce(mode == Mode.Empty ? $"No results for {trimmed}" : mode == Mode.Results ? $"{count} {(count == 1 ? "result" : "results")}" : $"{count} {SuggestionsLabel.ToLowerInvariant()} {(count == 1 ? "item" : "items")}");
    }

    private void Announce(string text)
    {
        if (_status is null || _status.Text == text) return;
        _status.Text = text;
        if (text.Length > 0 && UIElementAutomationPeer.FromElement(_status) is { } peer) peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void BuildView(Mode mode, List<Group> groups, string trimmed, bool modeChanged)
    {
        if (_clip is null) return;
        var previousIds = _rows.Select(row => row.Item.Id).ToHashSet();
        var view = new Grid { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        _rows = [];
        _highlight = null; _list = null;
        if (mode == Mode.Empty)
        {
            var empty = new StackPanel { Margin = new Thickness(12, 12, 12, 14) };
            var title = new TextBlock { Text = $"No results for “{trimmed}”", FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap };
            title.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
            var hint = new TextBlock { Text = EmptyHint, FontSize = 12, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            empty.Children.Add(title); empty.Children.Add(hint);
            view.Children.Add(empty);
        }
        else
        {
            // A flex gap, not margins: 4 DIP between groups and between rows.
            var list = new StackPanel();
            var highlight = new Border { CornerRadius = new CornerRadius(20), VerticalAlignment = VerticalAlignment.Top, Opacity = 0, IsHitTestVisible = false, RenderTransform = new TranslateTransform() };
            highlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.ExpandingSearchHighlight");
            string query = mode == Mode.Results ? trimmed : "";
            bool firstGroup = true;
            foreach (var group in groups)
            {
                var groupPanel = new StackPanel { Margin = new Thickness(0, firstGroup ? 0 : 4, 0, 0) };
                firstGroup = false;
                if (group.Name.Length > 0)
                {
                    var label = new TextBlock { Text = group.Name, FontSize = 12, FontWeight = FontWeights.Medium, Margin = new Thickness(12, 8, 12, 4) };
                    label.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
                    groupPanel.Children.Add(label);
                }
                bool firstRow = group.Name.Length > 0;
                foreach (var item in group.Items)
                {
                    var row = BuildRow(item, query, _rows.Count);
                    if (!firstRow) row.Element.Margin = new Thickness(0, groupPanel.Children.Count > 0 ? 4 : 0, 0, 0);
                    else row.Element.Margin = new Thickness(0, 0, 0, 0);
                    firstRow = false;
                    groupPanel.Children.Add(row.Element);
                    _rows.Add(row);
                    // Rows that were not on screen fade in; rows that stay hold still.
                    if (!modeChanged && !previousIds.Contains(item.Id) && Animating) { row.Element.Opacity = 0; _panelMotion.Double(row.Element, OpacityProperty, 1, Reduced ? .15 : .16, true, enter: true); }
                }
                list.Children.Add(groupPanel);
            }
            var listHost = new Grid();
            listHost.Children.Add(highlight); listHost.Children.Add(list);
            _list = listHost;
            _highlight = highlight;
            view.Children.Add(listHost);
        }
        // The body keeps the final width and rides the leading edge; the panel clips it while it unfolds.
        var scroller = new ScrollArea { Fade = 0, Focusable = false, Width = _fullSize - 2, MaxHeight = BodyMaxHeight(), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = new Border { Padding = new Thickness(6), Child = view } };
        var old = _view;
        _scroller = scroller;
        _view = scroller;
        _clip.Children.Add(scroller);
        _activeId = "";
        if (old is not null)
        {
            if (modeChanged && Animating)
            {
                old.IsHitTestVisible = false;
                _panelMotion.Double(old, OpacityProperty, 0, .1, true, completed: () => _clip?.Children.Remove(old));
                scroller.Opacity = 0;
                if (!Reduced) { var blur = new BlurEffect { Radius = 2 }; scroller.Effect = blur; _panelMotion.Double(blur, BlurEffect.RadiusProperty, 0, .16, true, enter: true, completed: () => { if (ReferenceEquals(scroller.Effect, blur)) scroller.Effect = null; }); }
                _panelMotion.Double(scroller, OpacityProperty, 1, Reduced ? .15 : .16, true, enter: true);
            }
            else _clip.Children.Remove(old);
        }
        scroller.Loaded += (_, _) => { PlaceHighlight(); FitHeight(); };
        if (_active >= _rows.Count) _active = _rows.Count - 1;
    }

    private double BodyMaxHeight()
    {
        double screen = _window?.ActualHeight > 0 ? _window.ActualHeight : SystemParameters.WorkArea.Height;
        return Math.Min(352, screen * .6);
    }

    private Row BuildRow(ExpandingSearchItem item, string query, int position)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Path? icon = null;
        if (item.Icon is not null)
        {
            icon = new Path { Data = item.Icon, Width = 24, Height = 24, StrokeThickness = 1.75, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
            icon.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.TextMuted");
            grid.Children.Add(new Border { Width = 18, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Child = new Viewbox { Width = 16, Height = 16, Child = icon } });
        }
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1);
        var title = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        title.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        int at = query.Length > 0 ? item.Title.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) : -1;
        if (at < 0) title.Inlines.Add(new Run(item.Title));
        else
        {
            title.Inlines.Add(new Run(item.Title[..at]));
            title.Inlines.Add(new Run(item.Title.Substring(at, query.Length)) { FontWeight = FontWeights.Medium });
            title.Inlines.Add(new Run(item.Title[(at + query.Length)..]));
        }
        text.Children.Add(title);
        if (!string.IsNullOrEmpty(item.Meta))
        {
            var meta = new TextBlock { Text = item.Meta, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            meta.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            text.Children.Add(meta);
        }
        grid.Children.Add(text);
        var element = new Border { MinHeight = 48, Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(20), Background = Brushes.Transparent, Cursor = Cursors.Hand, Child = grid };
        AutomationProperties.SetName(element, item.Title);
        element.MouseMove += (_, _) => { if (_active != position) { _keyboard = false; SetActive(position); } };
        element.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true; // Pointer presses never take focus from the field.
        element.MouseLeftButtonUp += (_, e) => { e.Handled = true; Choose(item); };
        return new Row(item, element, icon);
    }

    private void SetActive(int index)
    {
        _active = index;
        PlaceHighlight();
        if (_keyboard && index >= 0 && index < _rows.Count) _rows[index].Element.BringIntoView();
    }

    // The highlight travels from the old row on the snappy spring; on a new set it fades in where it lands.
    private void PlaceHighlight()
    {
        if (_highlight is null || _list is null) return;
        for (int i = 0; i < _rows.Count; i++)
        {
            var color = i == _active ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextMuted";
            _rows[i].Icon?.SetResourceReference(Shape.StrokeProperty, color);
        }
        if (_active < 0 || _active >= _rows.Count) { _activeId = ""; _panelMotion.Double(_highlight, OpacityProperty, 0, .08, Animating); return; }
        var row = _rows[_active].Element;
        if (!row.IsArrangeValid || row.ActualHeight <= 0) return;
        double top = row.TranslatePoint(new Point(), _list).Y;
        _highlight.Height = row.ActualHeight;
        string id = _rows[_active].Item.Id;
        string previous = _activeId;
        _activeId = id;
        if (previous == id) return;
        if (previous.Length > 0 && Animating && !Reduced)
        {
            _highlight.Margin = new Thickness(0, top, 0, 0);
            _travel.Snap(_travel.Position + (_highlightTop - top));
            _springs.Animate(_travel, 0, .26);
        }
        else
        {
            _springs.Snap(_travel, 0);
            _highlight.Margin = new Thickness(0, top, 0, 0);
            if (Animating && !Reduced) { _highlight.Opacity = 0; _panelMotion.Double(_highlight, OpacityProperty, 1, .12, true, enter: true); }
            else _panelMotion.Double(_highlight, OpacityProperty, 1, 0, false);
        }
        _highlightTop = top;
    }
    private double _highlightTop;

    // The panel shares the field's width, so on first open it unfolds out of the field; afterwards only its height follows content.
    private void ShowPanel(bool show, bool animate)
    {
        if (_popup is null || _panel is null || _clip is null) return;
        bool motion = animate && Animating;
        int version = ++_panelVersion;
        if (show)
        {
            if (_panelShown) { FitHeight(); return; }
            _panelShown = true;
            _panelMotion.Double(_panel, OpacityProperty, motion ? 0 : 1, 0, false);
            _panel.IsHitTestVisible = true;
            if (motion && !Reduced) { _springs.Snap(_height, 0); _clip.Height = 0; }
            else _clip.Height = double.NaN;
            _popup.IsOpen = _window?.IsActive != false;
            PlacePopup();
            if (motion) _panelMotion.Double(_panel, OpacityProperty, 1, Reduced ? .15 : .16, true, enter: true);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(FitHeight));
            return;
        }
        if (!_panelShown) return;
        _panelShown = false;
        if (!motion || !_popup.IsOpen) { ClosePanel(); return; }
        _panel.IsHitTestVisible = false;
        _panelMotion.Double(_panel, OpacityProperty, 0, Reduced ? .1 : .12, true, completed: () => { if (version == _panelVersion) ClosePanel(); });
    }

    private void ClosePanel()
    {
        if (_popup is not null) _popup.IsOpen = false;
        if (_panel is not null) _panel.IsHitTestVisible = true;
        if (_clip is not null) { _clip.Children.Clear(); _clip.Height = double.NaN; }
        _view = null; _scroller = null; _highlight = null; _list = null; _rows = []; _layoutKey = ""; _activeId = "";
    }

    private void FitHeight()
    {
        if (!_panelShown || _scroller is null || _clip is null) return;
        // Measured at its natural height, then pinned there so the unfolding clip reveals it rather than squeezing it.
        _scroller.Height = double.NaN;
        _scroller.Measure(new Size(_scroller.Width, double.PositiveInfinity));
        double goal = Math.Min(_scroller.DesiredSize.Height, _scroller.MaxHeight);
        if (double.IsNaN(_clip.Height)) { PlaceHighlight(); return; }
        _scroller.Height = goal;
        if (!Animating || Reduced) { _clip.Height = double.NaN; _scroller.Height = double.NaN; return; }
        if (Math.Abs(_height.Target - goal) > .5 || !_springs.IsRunning) _springs.Animate(_height, goal, .4);
        PlaceHighlight();
    }

    private void ClipSized(object sender, SizeChangedEventArgs e)
    {
        if (_clip is not null) _clip.Clip = new RectangleGeometry(new Rect(e.NewSize), 25, 25);
    }

    private void PopupClosed(object? sender, EventArgs e)
    {
        if (_panelShown && _window?.IsActive != false) { _panelShown = false; ClosePanel(); }
    }

    // The popup hangs off the control's top-left; its offset follows the shell's leading edge each frame.
    private void PlacePopup()
    {
        if (_popup is null || !_popup.IsOpen || _shell is null) return;
        double left = Anchor == ExpandingSearchAnchor.End ? ActualWidth - _shell.Width : 0;
        _popup.HorizontalOffset = left - 24 + (_popup.HorizontalOffset == left - 24 ? .001 : 0);
        _popup.VerticalOffset = ActualHeight + 8;
    }

    private void AttachWindow()
    {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(window, _window)) return;
        DetachWindow();
        _window = window;
        if (_window is null) return;
        _window.PreviewMouseDown += WindowPointerDown;
        _window.LocationChanged += WindowMoved;
        _window.Activated += WindowActivation; _window.Deactivated += WindowActivation;
    }

    private void DetachWindow()
    {
        if (_window is null) return;
        _window.PreviewMouseDown -= WindowPointerDown;
        _window.LocationChanged -= WindowMoved;
        _window.Activated -= WindowActivation; _window.Deactivated -= WindowActivation;
        _window = null;
    }

    private void WindowMoved(object? sender, EventArgs e) => PlacePopup();
    // An inactive window keeps the search as it was but hides the floating panel, which would otherwise sit above other apps.
    private void WindowActivation(object? sender, EventArgs e)
    {
        if (_popup is null) return;
        _popup.IsOpen = _panelShown && _window?.IsActive == true;
        PlacePopup();
    }

    // A click outside the shell and panel blurs the field, as it does in a browser.
    private void WindowPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (!_open || e.OriginalSource is not DependencyObject source) return;
        // Logical parents first, so popup content walks back through its Popup into this control.
        for (DependencyObject? node = source; node is not null; node = LogicalTreeHelper.GetParent(node) ?? (node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : null))
            if (ReferenceEquals(node, this)) return;
        if (_input?.IsKeyboardFocusWithin == true) Blur();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpandingSearchPeer(this);
    private sealed class ExpandingSearchPeer(ExpandingSearch owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(ExpandingSearch);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.Label;
    }
}
