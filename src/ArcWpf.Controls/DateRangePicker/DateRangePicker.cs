using System.Collections.ObjectModel;
using System.Globalization;
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

/// <summary>An inclusive range of whole days.</summary>
public readonly record struct DateRange(DateTime Start, DateTime End);

/// <summary>A shortcut in the preset rail. Range receives the local today.</summary>
public sealed class DateRangePreset(string label, Func<DateTime, DateRange> range)
{
    public string Label { get; } = label;
    public Func<DateTime, DateRange> Range { get; } = range;
}

public enum DateRangePickerMonths { Auto, One, Two }

public sealed class DateRangeChangedEventArgs(RoutedEvent routedEvent, object source, DateRange range) : RoutedEventArgs(routedEvent, source)
{
    public DateRange Range { get; } = range;
}

[TemplatePart(Name = "PART_Root", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Rest", Type = typeof(Border))]
[TemplatePart(Name = "PART_Trigger", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_TriggerContent", Type = typeof(StackPanel))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_Stage", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Surface", Type = typeof(Grid))]
public class DateRangePicker : Control
{
    // date-range-picker.tsx: one material from trigger to panel. The surface springs from the trigger's box to the panel's and back.
    private const double TriggerRadius = 18, PanelRadius = 26, WideCell = 36, WideMin = 712, Edge = 8, Gutter = 24;
    private static readonly double Grow = ControlSpring.Physical(.48), Shrink = ControlSpring.Physical(.38), Stretch = ControlSpring.Physical(.3), Glide = ControlSpring.Physical(.3), Slide = ControlSpring.Physical(.4);

    public static readonly DependencyProperty StartDateProperty = DependencyProperty.Register(nameof(StartDate), typeof(DateTime?), typeof(DateRangePicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCommittedChanged));
    public static readonly DependencyProperty EndDateProperty = DependencyProperty.Register(nameof(EndDate), typeof(DateTime?), typeof(DateRangePicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCommittedChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(DateRangePicker), new FrameworkPropertyMetadata("Date range", OnCommittedChanged));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(DateRangePicker), new FrameworkPropertyMetadata("Select dates", OnCommittedChanged));
    public static readonly DependencyProperty MinimumDateProperty = DependencyProperty.Register(nameof(MinimumDate), typeof(DateTime?), typeof(DateRangePicker), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty MaximumDateProperty = DependencyProperty.Register(nameof(MaximumDate), typeof(DateTime?), typeof(DateRangePicker), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty FirstDayOfWeekProperty = DependencyProperty.Register(nameof(FirstDayOfWeek), typeof(DayOfWeek), typeof(DateRangePicker), new FrameworkPropertyMetadata(DayOfWeek.Sunday), v => v is DayOfWeek.Sunday or DayOfWeek.Monday);
    public static readonly DependencyProperty MonthsProperty = DependencyProperty.Register(nameof(Months), typeof(DateRangePickerMonths), typeof(DateRangePicker), new FrameworkPropertyMetadata(DateRangePickerMonths.Auto));
    public static readonly DependencyProperty BoundaryProperty = DependencyProperty.Register(nameof(Boundary), typeof(FrameworkElement), typeof(DateRangePicker), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(DateRangePicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));
    public static readonly RoutedEvent RangeChangedEvent = EventManager.RegisterRoutedEvent(nameof(RangeChanged), RoutingStrategy.Bubble, typeof(EventHandler<DateRangeChangedEventArgs>), typeof(DateRangePicker));
    private static readonly string[] ColorKeys = ["SurfaceRaised", "RangeTriggerHover", "Border", "BorderStrong"];
    private static readonly DependencyProperty[] ColorProbes = ColorKeys.Select(key => DependencyProperty.Register("Probe" + key, typeof(Brush), typeof(DateRangePicker), new PropertyMetadata(null, (d, _) => ((DateRangePicker)d).UpdateRest(false)))).ToArray();

    public DateTime? StartDate { get => (DateTime?)GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }
    public DateTime? EndDate { get => (DateTime?)GetValue(EndDateProperty); set => SetValue(EndDateProperty, value); }
    /// <summary>Accessible name of the trigger and the dialog.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public DateTime? MinimumDate { get => (DateTime?)GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }
    public DateTime? MaximumDate { get => (DateTime?)GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }
    /// <summary>Sunday or Monday.</summary>
    public DayOfWeek FirstDayOfWeek { get => (DayOfWeek)GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }
    /// <summary>Auto shows two months when the boundary is at least 712 DIP wide.</summary>
    public DateRangePickerMonths Months { get => (DateRangePickerMonths)GetValue(MonthsProperty); set => SetValue(MonthsProperty, value); }
    /// <summary>The element the panel should stay inside. Defaults to the window content.</summary>
    public FrameworkElement? Boundary { get => (FrameworkElement?)GetValue(BoundaryProperty); set => SetValue(BoundaryProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public ObservableCollection<DateRangePreset> Presets { get; } = new(DefaultPresets);
    /// <summary>Raised with the applied range.</summary>
    public event EventHandler<DateRangeChangedEventArgs> RangeChanged { add => AddHandler(RangeChangedEvent, value); remove => RemoveHandler(RangeChangedEvent, value); }

    public static IReadOnlyList<DateRangePreset> DefaultPresets { get; } =
    [
        new("Today", t => new(t, t)),
        new("Yesterday", t => new(t.AddDays(-1), t.AddDays(-1))),
        new("Last 7 days", t => new(t.AddDays(-6), t)),
        new("Last 30 days", t => new(t.AddDays(-29), t)),
        new("This month", t => new(MonthStart(t), t)),
        new("Last month", t => new(AddMonths(t, -1), AddMonths(t, 0).AddDays(-1))),
        new("This quarter", t => new(new DateTime(t.Year, (t.Month - 1) / 3 * 3 + 1, 1), t)),
        new("Year to date", t => new(new DateTime(t.Year, 1, 1), t)),
    ];

    internal static DateTime MonthStart(DateTime date) => new(date.Year, date.Month, 1);
    internal static DateTime AddMonths(DateTime date, int amount) => MonthStart(date).AddMonths(amount);
    internal static int DayDiff(DateTime a, DateTime b) => (a.Date - b.Date).Days;
    internal static int MonthDiff(DateTime a, DateTime b) => (a.Year - b.Year) * 12 + a.Month - b.Month;
    internal static DateRange Ordered(DateTime a, DateTime b) => DayDiff(a, b) <= 0 ? new(a.Date, b.Date) : new(b.Date, a.Date);

    private Grid? _root, _stage, _surface;
    private Border? _rest;
    private System.Windows.Controls.Button? _trigger;
    private StackPanel? _triggerContent;
    private Popup? _popup;
    private Window? _window;
    private readonly RollingText _triggerValue = new();
    private readonly SolidColorBrush _restBackground = new(), _restBorder = new();
    private readonly OwnedMotion _colors = new(), _motion = new();
    private readonly SpringDriver _morph, _rail;
    private readonly ControlSpring _width, _height, _radius, _x, _rootWidth, _gx, _gy, _gw, _gh;
    private int _committedDirection = 1, _openVersion;
    private long _committedTime = long.MinValue;
    private bool _ready;

    // Open-panel state.
    private DateRange? _draft;
    private DateTime? _anchor, _hover;
    private DateTime _view, _today;
    private string _focusKey = "";
    private bool _compact;
    private double _panelWidth, _cell = WideCell;
    private int _shownDirection = 1;
    private long _shownTime = long.MinValue;
    private Grid? _panel, _viewport, _railHost, _face;
    private StackPanel? _railList;
    private Border? _railHighlight, _outline, _background;
    private System.Windows.Controls.Button? _prev, _next, _apply;
    private RollingText? _summary, _count, _faceValue;
    private TextBlock? _status;
    private FrameworkElement? _monthsElement;
    private readonly List<RangeMonth> _months = [];
    private bool _pendingDayFocus;

    static DateRangePicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DateRangePicker), new FrameworkPropertyMetadata(typeof(DateRangePicker)));
        FocusableProperty.OverrideMetadata(typeof(DateRangePicker), new FrameworkPropertyMetadata(false));
    }

    public DateRangePicker()
    {
        _morph = new SpringDriver(ApplyMorph) { Settled = () => { if (!IsOpen && _popup?.IsOpen == true) FinishClose(_openVersion); } };
        _width = _morph.Add(0, Grow); _height = _morph.Add(0, Grow); _radius = _morph.Add(TriggerRadius, Grow); _x = _morph.Add(0, Grow); _rootWidth = _morph.Add(0, .42);
        _rail = new SpringDriver(ApplyRail);
        _gx = _rail.Add(0, Glide); _gy = _rail.Add(0, Glide); _gw = _rail.Add(0, Glide); _gh = _rail.Add(0, Glide);
        for (int i = 0; i < ColorKeys.Length; i++) SetResourceReference(ColorProbes[i], "ArcWpf.Brush." + ColorKeys[i]);
        AddHandler(KeyDownEvent, new KeyEventHandler(RootKey), true);
        Loaded += (_, _) => { AttachWindow(); _ready = false; SyncTrigger(false); };
        Unloaded += (_, _) => { SetCurrentValue(IsOpenProperty, false); FinishClose(_openVersion); DetachWindow(); _morph.Stop(); _rail.Stop(); _motion.Stop(); _colors.Stop(); };
    }

    public override void OnApplyTemplate()
    {
        Release();
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Grid;
        _rest = GetTemplateChild("PART_Rest") as Border;
        _trigger = GetTemplateChild("PART_Trigger") as System.Windows.Controls.Button;
        _triggerContent = GetTemplateChild("PART_TriggerContent") as StackPanel;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _stage = GetTemplateChild("PART_Stage") as Grid;
        _surface = GetTemplateChild("PART_Surface") as Grid;
        if (_rest is not null) { _rest.Background = _restBackground; _rest.BorderBrush = _restBorder; }
        if (_triggerContent is not null && _triggerContent.Children.Count >= 2 && _triggerContent.Children[1] is Border host) host.Child = _triggerValue;
        if (_trigger is not null)
        {
            _trigger.Click += TriggerClick;
            _trigger.MouseEnter += TriggerHover; _trigger.MouseLeave += TriggerHover;
            _trigger.SizeChanged += TriggerSized;
        }
        if (_popup is not null) _popup.Closed += PopupClosed;
        _ready = false;
        SyncTrigger(false);
        UpdateRest(false);
    }

    private void Release()
    {
        if (_trigger is not null) { _trigger.Click -= TriggerClick; _trigger.MouseEnter -= TriggerHover; _trigger.MouseLeave -= TriggerHover; _trigger.SizeChanged -= TriggerSized; }
        if (_popup is not null) { _popup.Closed -= PopupClosed; _popup.IsOpen = false; }
        if (_triggerContent is not null && _triggerContent.Children.Count >= 2 && _triggerContent.Children[1] is Border host) host.Child = null;
        _morph.Stop(); _rail.Stop(); _motion.Stop(); _colors.Stop();
        ClearPanel();
        _root = _stage = _surface = null; _rest = null; _trigger = null; _triggerContent = null; _popup = null;
    }

    private ArcMotionLevel Level => ArcMotion.GetLevel(this);
    private bool Animating => IsLoaded && IsVisible && Level != ArcMotionLevel.Off;
    private bool Reduced => Level != ArcMotionLevel.Full;
    private CultureInfo Culture { get { try { return Language.GetSpecificCulture(); } catch (InvalidOperationException) { return CultureInfo.GetCultureInfo("en-US"); } } }

    private DateRange? Committed => StartDate is { } start && EndDate is { } end ? Ordered(start, end) : null;
    private DateRange? Shown => _anchor is { } anchor ? Ordered(anchor, _hover ?? anchor) : _draft;

    internal string FormatDay(DateTime date) => date.ToString(Culture.TwoLetterISOLanguageName == "en" ? "MMM d, yyyy" : "d", Culture);
    internal string Format(DateRange? range)
    {
        if (range is not { } r) return Placeholder;
        if (DayDiff(r.Start, r.End) == 0) return FormatDay(r.Start);
        if (Culture.TwoLetterISOLanguageName != "en") return $"{FormatDay(r.Start)} – {FormatDay(r.End)}";
        // Intl.DateTimeFormat.formatRange in en-US: the shared month or year is written once.
        var c = Culture;
        if (r.Start.Year == r.End.Year && r.Start.Month == r.End.Month) return $"{r.Start.ToString("MMM d", c)} – {r.End.Day}, {r.End.Year}";
        if (r.Start.Year == r.End.Year) return $"{r.Start.ToString("MMM d", c)} – {r.End.ToString("MMM d", c)}, {r.End.Year}";
        return $"{FormatDay(r.Start)} – {FormatDay(r.End)}";
    }

    private static long TimeOf(DateRange? range) => range is { } r ? r.Start.Ticks * 2 + r.End.Ticks : long.MinValue;

    private static void OnCommittedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((DateRangePicker)d).SyncTrigger(true);

    // Remembers which way the committed range moved so the trigger label rolls in time's direction.
    private void SyncTrigger(bool animate)
    {
        var committed = Committed;
        long time = TimeOf(committed);
        if (time != _committedTime) { _committedDirection = time == long.MinValue || _committedTime == long.MinValue || time >= _committedTime ? 1 : -1; _committedTime = time; }
        _triggerValue.Set(Format(committed), _committedDirection, Level, animate && !IsOpen);
        _triggerValue.SetResourceReference(TextElementForeground, committed is null ? "ArcWpf.Brush.TextSecondary" : "ArcWpf.Brush.Foreground");
        System.Windows.Documents.TextElement.SetFontWeight(_triggerValue, committed is null ? FontWeights.Normal : FontWeights.Medium);
        if (_trigger is not null) AutomationProperties.SetName(_trigger, $"{Label}: {Format(committed)}");
    }
    private static readonly DependencyProperty TextElementForeground = System.Windows.Documents.TextElement.ForegroundProperty;

    private Color Probe(int index) => (GetValue(ColorProbes[index]) as SolidColorBrush)?.Color ?? Colors.Transparent;
    private void TriggerHover(object sender, MouseEventArgs e) => UpdateRest(true);
    private void UpdateRest(bool animate)
    {
        bool hover = !IsOpen && IsEnabled && _trigger?.IsMouseOver == true;
        bool motion = animate && Animating && !Reduced;
        _colors.Color(_restBackground, hover ? Probe(1) : Probe(0), .16, motion);
        _colors.Color(_restBorder, hover ? Probe(3) : Probe(2), .16, motion);
    }

    // The root holds the trigger's place; its width follows the trigger on the morph spring, so a new label never jumps.
    private void TriggerSized(object sender, SizeChangedEventArgs e)
    {
        if (_root is null || _trigger is null) return;
        double width = _trigger.ActualWidth;
        if (!_ready || !Animating || Reduced) { _ready = true; _morph.Snap(_rootWidth, width); }
        else _morph.Animate(_rootWidth, width, .42);
        if (!IsOpen && _popup?.IsOpen == true) { _width.Target = width; }
    }

    private void ApplyMorph()
    {
        if (_root is not null && _rootWidth.Position > 0) _root.Width = _rootWidth.Position;
        if (_surface is null || _popup is null) return;
        double w = Math.Max(0, _width.Position), h = Math.Max(0, _height.Position);
        _surface.Width = w; _surface.Height = h;
        var radius = new CornerRadius(Math.Max(0, _radius.Position));
        if (_background is not null) _background.CornerRadius = radius;
        if (_outline is not null) _outline.CornerRadius = radius;
        if (_panel?.Parent is Grid clip) clip.Clip = new RectangleGeometry(new Rect(0, 0, w, h), radius.TopLeft, radius.TopLeft);
        _popup.HorizontalOffset = _x.Position - Gutter;
        _popup.VerticalOffset = -Gutter;
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (DateRangePicker)d;
        if ((bool)e.NewValue) picker.OpenPanel(); else picker.ClosePanel(false);
    }

    private void TriggerClick(object sender, RoutedEventArgs e) { e.Handled = true; SetCurrentValue(IsOpenProperty, true); }

    private (bool Single, double Width) MeasureLayout()
    {
        double available = BoundaryWidth() - Edge * 2;
        bool single = Months == DateRangePickerMonths.One || Months == DateRangePickerMonths.Auto && available < WideMin;
        return (single, Math.Min(352, Math.Max(0, available)));
    }

    private double BoundaryWidth()
    {
        var element = Boundary ?? _window?.Content as FrameworkElement ?? _window;
        return element?.ActualWidth > 0 ? element.ActualWidth : SystemParameters.WorkArea.Width;
    }

    private static DateTime ViewFor(DateRange? range, bool single, DateTime fallback)
    {
        var end = MonthStart(range?.End ?? fallback);
        return single ? end : end.AddMonths(-1);
    }

    private void OpenPanel()
    {
        if (!IsLoaded || !IsEnabled || _popup is null || _surface is null || _trigger is null) { SetCurrentValue(IsOpenProperty, false); return; }
        int version = ++_openVersion;
        _today = DateTime.Today;
        var layout = MeasureLayout();
        _compact = layout.Single; _panelWidth = layout.Width;
        _cell = _compact && _panelWidth > 0 ? Math.Max(32, Math.Min(42, Math.Floor((_panelWidth - 24) / 7))) : WideCell;
        _draft = Committed; _anchor = null; _hover = null;
        _view = ViewFor(_draft, _compact, _today);
        _focusKey = KeyOf(_draft?.Start ?? _today);
        _shownTime = TimeOf(Shown); _shownDirection = 1;
        BuildPanel();
        UpdateRest(true);
        double tw = _trigger.ActualWidth, th = _trigger.ActualHeight;
        bool motion = Animating && !Reduced;
        if (!_popup.IsOpen || !motion) { _morph.Snap(_width, tw); _morph.Snap(_height, th); _morph.Snap(_radius, TriggerRadius); _morph.Snap(_x, 0); }
        if (_triggerContent is not null) _triggerContent.Opacity = 0;
        // Attach first so the panel measures with the picker's inherited font, then grow toward that size.
        _popup.IsOpen = true;
        _panel!.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var panel = _panel.DesiredSize;
        // Clamp inside the boundary with an 8 DIP edge, like the source's viewport clamp.
        double offset = 0;
        var box = Boundary ?? _window?.Content as FrameworkElement;
        if (box is not null && box.IsAncestorOf(this))
        {
            double left = TranslatePoint(new Point(), box).X;
            offset = Math.Min(0, box.ActualWidth - Edge - (left + panel.Width));
            offset = Math.Max(offset, Edge - left);
        }
        if (_stage is not null) { _stage.Width = Math.Max(panel.Width, tw); _stage.Height = Math.Max(panel.Height, th); }
        if (motion)
        {
            _morph.Animate(_width, panel.Width, Grow); _morph.Animate(_height, panel.Height, Grow);
            _morph.Animate(_radius, PanelRadius, Grow); _morph.Animate(_x, offset, Grow);
        }
        else { _morph.Snap(_width, panel.Width); _morph.Snap(_height, panel.Height); _morph.Snap(_radius, PanelRadius); _morph.Snap(_x, offset); }
        // The trigger face quiets while the panel content arrives just behind the growing surface.
        if (_face is not null) Face(_face, false, motion, Reduced);
        if (_panel is not null) Face(_panel, true, Animating, Reduced, delay: true);
        _pendingDayFocus = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { if (version == _openVersion) { FocusDay(); PlaceRailHighlight(false); } }));
    }

    // faceIn / gone for panel content (blur 4, lift 4 in / 2 out); quiet for the trigger face (blur 2, .12 out, .22 in after .1).
    private void Face(FrameworkElement element, bool show, bool motion, bool reduced, bool delay = false)
    {
        bool face = ReferenceEquals(element, _face);
        if (!motion) { _motion.Double(element, OpacityProperty, show ? 1 : 0, 0, false); element.Effect = null; return; }
        double seconds = face ? (show ? .22 : .12) : show ? (reduced ? .12 : .26) : (reduced ? .08 : .12);
        double wait = reduced ? 0 : face && show ? .1 : delay && show ? .07 : 0;
        if (!reduced)
        {
            double far = face ? 2 : 4;
            var blur = new BlurEffect { Radius = show ? far : element.Effect is BlurEffect current ? current.Radius : 0 };
            element.Effect = blur;
            _motion.Double(blur, BlurEffect.RadiusProperty, show ? 0 : far, seconds, true, delay: wait, enter: show, completed: () => { if (show && ReferenceEquals(element.Effect, blur)) element.Effect = null; });
            if (!face)
            {
                if (element.RenderTransform is not TranslateTransform move) element.RenderTransform = move = new TranslateTransform();
                if (show) _motion.Double(move, TranslateTransform.YProperty, -4, 0, false);
                _motion.Double(move, TranslateTransform.YProperty, show ? 0 : -2, seconds, true, delay: wait, enter: show);
            }
        }
        _motion.Double(element, OpacityProperty, show ? 1 : 0, seconds, true, delay: wait, enter: show);
    }

    private void ClosePanel(bool focusTrigger)
    {
        int version = ++_openVersion;
        _anchor = null; _hover = null;
        UpdateRest(true);
        if (_popup is null || !_popup.IsOpen) { FinishClose(version); if (focusTrigger) _trigger?.Focus(); return; }
        if (focusTrigger) _trigger?.Focus();
        if (!Animating) { FinishClose(version); return; }
        if (_panel is not null) { _panel.IsHitTestVisible = false; Face(_panel, false, true, Reduced); }
        if (_face is not null) { _faceValue?.Set(Format(Committed), 1, Level, false); Face(_face, true, true, Reduced); }
        double tw = _trigger?.ActualWidth ?? _width.Position, th = _trigger?.ActualHeight ?? _height.Position;
        if (Reduced) { _morph.Snap(_width, tw); _morph.Snap(_height, th); _morph.Snap(_radius, TriggerRadius); _morph.Snap(_x, 0); FinishClose(version); return; }
        _morph.Animate(_width, tw, Shrink); _morph.Animate(_height, th, Shrink); _morph.Animate(_radius, TriggerRadius, Shrink); _morph.Animate(_x, 0, Shrink);
    }

    private void FinishClose(int version)
    {
        if (version != _openVersion || IsOpen) return;
        if (_popup is not null) _popup.IsOpen = false;
        ClearPanel();
        if (_triggerContent is not null) _motion.Double(_triggerContent, OpacityProperty, 1, 0, false);
        UpdateRest(false);
    }

    private void PopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
    }

    private void Apply()
    {
        DateRange? next = _anchor is { } anchor ? new DateRange(anchor, anchor) : _draft;
        if (next is not { } range) return;
        SetCurrentValue(StartDateProperty, range.Start);
        SetCurrentValue(EndDateProperty, range.End);
        RaiseEvent(new DateRangeChangedEventArgs(RangeChangedEvent, this, range));
        Close(true);
    }

    private void Close(bool focusTrigger)
    {
        if (!IsOpen) return;
        SetCurrentValue(IsOpenProperty, false);
        if (focusTrigger) _trigger?.Focus();
    }

    private void RootKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsOpen) { e.Handled = true; Close(true); }
    }

    // ---------- Panel construction ----------

    private void ClearPanel()
    {
        foreach (var month in _months) month.Dispose();
        _months.Clear();
        _rail.Stop();
        if (_surface is not null) _surface.Children.Clear();
        _panel = _viewport = _railHost = _face = null; _railList = null; _railHighlight = _outline = _background = null;
        _prev = _next = _apply = null; _summary = _count = _faceValue = null; _status = null; _monthsElement = null;
    }

    private static T Ink<T>(T element, string key) where T : FrameworkElement
    {
        element.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, key);
        return element;
    }

    private static TextBlock Text(string text, string brush, double size = 14, bool medium = false)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = medium ? FontWeights.Medium : FontWeights.Normal };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }

    internal static Viewbox Icon(string data, string brush, double size = 16, double stroke = 1.75)
    {
        var path = new Path { Data = Geometry.Parse(data), Width = 24, Height = 24, StrokeThickness = stroke, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        path.SetResourceReference(Shape.StrokeProperty, brush);
        return new Viewbox { Width = size, Height = size, Child = path };
    }
    internal const string CalendarDaysIcon = "M8,2 v3 M16,2 v3 M5,3 h14 a2,2 0 0 1 2,2 v14 a2,2 0 0 1 -2,2 h-14 a2,2 0 0 1 -2,-2 v-14 a2,2 0 0 1 2,-2 z M3,9 h18 M8,13 h0.01 M12,13 h0.01 M16,13 h0.01 M8,17 h0.01 M12,17 h0.01 M16,17 h0.01";
    internal const string ChevronDownIcon = "M6,9 l6,6 l6,-6";
    private const string ChevronLeftIcon = "M15,18 l-6,-6 l6,-6", ChevronRightIcon = "M9,18 l6,-6 l-6,-6";

    private void BuildPanel()
    {
        ClearPanel();
        if (_surface is null) return;
        _background = new Border { Effect = (Effect?)TryFindResource("ArcWpf.Shadow.Floating") };
        _background.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        _outline = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false };
        _outline.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        var clip = new Grid();

        // The trigger face inside the surface: it carries the label while the surface shrinks back over the real trigger.
        _faceValue = new RollingText();
        _faceValue.Set(Format(Committed), 1, Level, false);
        Ink(_faceValue, Committed is null ? "ArcWpf.Brush.TextSecondary" : "ArcWpf.Brush.Foreground");
        System.Windows.Documents.TextElement.SetFontWeight(_faceValue, Committed is null ? FontWeights.Normal : FontWeights.Medium);
        _face = new Grid { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Height = _trigger?.ActualHeight ?? 44, IsHitTestVisible = false };
        var faceRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        faceRow.Children.Add(Icon(CalendarDaysIcon, "ArcWpf.Brush.TextSecondary"));
        faceRow.Children.Add(new Border { Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Child = _faceValue });
        faceRow.Children.Add(Icon(ChevronDownIcon, "ArcWpf.Brush.TextMuted"));
        _face.Children.Add(faceRow);

        _panel = new Grid { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Opacity = 0 };
        _panel.PreviewKeyDown += RootKey;
        AutomationProperties.SetName(_panel, Label);
        var panelBody = new StackPanel { Margin = new Thickness(_compact ? 8 : 10) };
        if (_compact && _panelWidth > 0) panelBody.Width = _panelWidth - 16;
        var body = new StackPanel { Orientation = _compact ? Orientation.Vertical : Orientation.Horizontal };
        body.Children.Add(BuildRail());
        body.Children.Add(BuildCalendars());
        panelBody.Children.Add(body);
        panelBody.Children.Add(BuildFooter());
        _status = new TextBlock { Width = 0, Height = 0 };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        _panel.Children.Add(panelBody);
        _panel.Children.Add(_status);

        clip.Children.Add(_face);
        clip.Children.Add(_panel);
        _surface.Children.Add(_background);
        _surface.Children.Add(clip);
        _surface.Children.Add(_outline);
        UpdateShown(false);
    }

    private FrameworkElement BuildRail()
    {
        _railList = new StackPanel { Orientation = _compact ? Orientation.Horizontal : Orientation.Vertical };
        _railHighlight = new Border { CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Opacity = 0, IsHitTestVisible = false, RenderTransform = new TranslateTransform() };
        _railHighlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.RangeRailHighlight");
        _railHost = new Grid { Children = { _railHighlight, _railList } };
        AutomationProperties.SetName(_railHost, "Presets");
        for (int i = 0; i < Presets.Count; i++)
        {
            var preset = Presets[i];
            var button = new System.Windows.Controls.Button { Content = preset.Label, Height = 32, Padding = new Thickness(_compact ? 11 : 12, 0, _compact ? 11 : 12, 0), Margin = new Thickness(0, 0, _compact ? 2 : 0, _compact ? 0 : 2), Tag = i, Style = PresetStyle(), Cursor = Cursors.Hand };
            button.Click += (_, _) => ChoosePreset(preset);
            button.PreviewKeyDown += RailKey;
            _railList.Children.Add(button);
        }
        if (_compact)
        {
            var scroller = new ScrollArea { Orientation = ScrollAreaOrientation.Horizontal, Fade = 0, Focusable = false, Margin = new Thickness(-8, 0, -8, 0), Padding = new Thickness(8, 0, 8, 8), Content = _railHost };
            return new Border { BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 0, 0, 8), Child = scroller }.Also(b => b.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle"));
        }
        var rail = new Border { Width = 148, Padding = new Thickness(0, 0, 10, 0), Margin = new Thickness(0, 0, 10, 0), BorderThickness = new Thickness(0, 0, 1, 0), Child = _railHost };
        rail.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        return rail;
    }

    // Styles live in the theme dictionary; string keys there are invisible to FindResource, component keys are not.
    private Style PresetStyle() => (Style)FindResource(new ComponentResourceKey(typeof(DateRangePicker), "Preset"));

    private FrameworkElement BuildCalendars()
    {
        var calendars = new Grid { Margin = new Thickness(2, 0, 2, 0), HorizontalAlignment = _compact ? HorizontalAlignment.Center : HorizontalAlignment.Left };
        calendars.MouseLeave += (_, _) => { if (_anchor is not null) { _hover = null; UpdateShown(true); } };
        _viewport = new Grid { Height = 60 + _cell * 6, ClipToBounds = true };
        _prev = NavButton(ChevronLeftIcon, "Previous month", HorizontalAlignment.Left, -1);
        _next = NavButton(ChevronRightIcon, "Next month", HorizontalAlignment.Right, 1);
        calendars.Children.Add(_viewport);
        calendars.Children.Add(_prev);
        calendars.Children.Add(_next);
        ShowMonths(0);
        return calendars;
    }

    private System.Windows.Controls.Button NavButton(string icon, string name, HorizontalAlignment side, int step)
    {
        var button = new System.Windows.Controls.Button { Width = 32, Height = 32, HorizontalAlignment = side, VerticalAlignment = VerticalAlignment.Top, Content = Icon(icon, "ArcWpf.Brush.TextSecondary"), Style = (Style)FindResource(new ComponentResourceKey(typeof(DateRangePicker), "Nav")), Cursor = Cursors.Hand };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => GoTo(_view.AddMonths(step));
        return button;
    }

    private FrameworkElement BuildFooter()
    {
        var footer = new DockPanel { Margin = new Thickness(0, _compact ? 6 : 10, 0, 0) };
        var border = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(_compact ? 11 : 12, 10, 2, 0), Margin = footer.Margin, Child = footer };
        footer.Margin = new Thickness();
        border.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        var cancel = new System.Windows.Controls.Button { Content = "Cancel", Style = (Style)FindResource(new ComponentResourceKey(typeof(DateRangePicker), "Ghost")), Margin = new Thickness(0, 0, 6, 0) };
        cancel.Click += (_, _) => Close(true);
        _apply = new System.Windows.Controls.Button { Content = "Apply", Style = (Style)FindResource(new ComponentResourceKey(typeof(DateRangePicker), "Primary")) };
        _apply.Click += (_, _) => Apply();
        actions.Children.Add(cancel); actions.Children.Add(_apply);
        DockPanel.SetDock(actions, Dock.Right);
        footer.Children.Add(actions);
        _summary = new RollingText();
        Ink(_summary, "ArcWpf.Brush.Foreground");
        System.Windows.Documents.TextElement.SetFontWeight(_summary, FontWeights.Medium);
        Typography.SetNumeralAlignment(_summary, FontNumeralAlignment.Tabular);
        _count = new RollingText { MinHeight = 17 };
        Ink(_count, "ArcWpf.Brush.TextMuted");
        System.Windows.Documents.TextElement.SetFontSize(_count, 12);
        var summary = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _summary, _count } };
        footer.Children.Add(summary);
        return border;
    }

    private static string KeyOf(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private int Count => _compact ? 1 : 2;

    // Months slide horizontally in the direction of travel inside a fixed six-week window.
    private void ShowMonths(int direction)
    {
        if (_viewport is null) return;
        foreach (var stale in _months) stale.Dispose();
        _months.Clear();
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = new TranslateTransform() };
        for (int i = 0; i < Count; i++)
        {
            var month = new RangeMonth(this, _view.AddMonths(i), _cell);
            if (i > 0) month.Element.Margin = new Thickness(24, 0, 0, 0);
            row.Children.Add(month.Element);
            _months.Add(month);
        }
        var old = _monthsElement;
        _monthsElement = row;
        _viewport.Children.Add(row);
        if (old is not null)
        {
            old.IsHitTestVisible = false;
            foreach (var button in FindButtons(old)) button.IsEnabled = false;
            if (direction != 0 && Animating)
            {
                if (!Reduced) SlideMonths((TranslateTransform)old.RenderTransform, (TranslateTransform)row.RenderTransform, direction);
                row.Opacity = 0;
                _motion.Double(row, OpacityProperty, 1, Reduced ? .12 : .22, true, enter: true);
                _motion.Double(old, OpacityProperty, 0, Reduced ? .08 : .14, true, completed: () => _viewport?.Children.Remove(old));
            }
            else _viewport.Children.Remove(old);
        }
        UpdateNav();
    }

    private void SlideMonths(TranslateTransform leaving, TranslateTransform arriving, int direction)
    {
        ControlSpring? outSpring = null, inSpring = null;
        var driver = new SpringDriver(() => { leaving.X = outSpring!.Position; arriving.X = inSpring!.Position; });
        outSpring = driver.Add(leaving.X, Slide); inSpring = driver.Add(direction * 48, Slide);
        arriving.X = direction * 48;
        driver.Animate(outSpring, direction * -36, Slide);
        driver.Animate(inSpring, 0, Slide);
    }

    private static IEnumerable<System.Windows.Controls.Button> FindButtons(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is System.Windows.Controls.Button button) yield return button;
            foreach (var nested in FindButtons(child)) yield return nested;
        }
    }

    private void UpdateNav()
    {
        if (_prev is not null) _prev.IsEnabled = !(MinimumDate is { } min && MonthDiff(_view, min) <= 0);
        if (_next is not null) _next.IsEnabled = !(MaximumDate is { } max && MonthDiff(_view.AddMonths(Count - 1), max) >= 0);
    }

    private void GoTo(DateTime next)
    {
        next = MonthStart(next);
        int delta = MonthDiff(next, _view);
        if (delta == 0) return;
        _view = next;
        ShowMonths(Math.Sign(delta));
        UpdateShown(true);
    }

    // Keeps a range on screen: nothing moves when it already fits, otherwise its last month lands on the right.
    private void Reveal(DateRange range)
    {
        var last = _view.AddMonths(Count - 1);
        if (MonthDiff(range.Start, _view) >= 0 && MonthDiff(range.End, last) <= 0) return;
        GoTo(ViewFor(range, _compact, range.End));
    }

    internal bool IsDisabled(DateTime date) => MinimumDate is { } min && DayDiff(date, min) < 0 || MaximumDate is { } max && DayDiff(date, max) > 0;

    internal void Pick(DateTime date)
    {
        _focusKey = KeyOf(date);
        if (_anchor is null) { _anchor = date; _hover = date; }
        else { _draft = Ordered(_anchor.Value, date); _anchor = null; _hover = null; }
        UpdateShown(true);
    }

    internal void HoverDay(DateTime date) { if (_anchor is not null && _hover != date) { _hover = date; UpdateShown(true); } }
    internal void FocusedDay(DateTime date) { _focusKey = KeyOf(date); UpdateTabStops(); }

    private void ChoosePreset(DateRangePreset preset)
    {
        var range = preset.Range(_today);
        range = Ordered(range.Start, range.End);
        _draft = range; _anchor = null; _hover = null;
        _focusKey = KeyOf(range.Start);
        Reveal(range);
        UpdateShown(true);
    }

    internal void DayKey(KeyEventArgs e, DateTime date)
    {
        int weekday = ((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        DateTime? next = e.Key switch
        {
            Key.Left => date.AddDays(-1), Key.Right => date.AddDays(1), Key.Up => date.AddDays(-7), Key.Down => date.AddDays(7),
            Key.Home => date.AddDays(-weekday), Key.End => date.AddDays(6 - weekday),
            Key.PageUp => date.AddMonths(shift ? -12 : -1), Key.PageDown => date.AddMonths(shift ? 12 : 1),
            _ => null,
        };
        if (next is not { } target) return;
        e.Handled = true;
        if (MinimumDate is { } min && DayDiff(target, min) < 0) target = min.Date;
        if (MaximumDate is { } max && DayDiff(target, max) > 0) target = max.Date;
        _focusKey = KeyOf(target);
        if (_anchor is not null) _hover = target;
        _pendingDayFocus = true;
        var last = _view.AddMonths(Count - 1);
        if (MonthDiff(target, _view) < 0) GoTo(MonthStart(target));
        else if (MonthDiff(target, last) > 0) GoTo(MonthStart(target).AddMonths(-(Count - 1)));
        UpdateShown(true);
        FocusDay();
    }

    private void RailKey(object sender, KeyEventArgs e)
    {
        if (_railList is null) return;
        var (back, forward) = _compact ? (Key.Left, Key.Right) : (Key.Up, Key.Down);
        if (e.Key != back && e.Key != forward) return;
        var buttons = _railList.Children.OfType<System.Windows.Controls.Button>().ToList();
        int at = buttons.IndexOf((System.Windows.Controls.Button)sender);
        if (at < 0) return;
        e.Handled = true;
        buttons[(at + (e.Key == forward ? 1 : -1) + buttons.Count) % buttons.Count].Focus();
    }

    /// <summary>The day that owns the tab stop: the focused day when on screen, else the first visible day of the range, else the 1st.</summary>
    private string Tabbable()
    {
        bool OnScreen(DateTime date) => _months.Any(month => MonthDiff(date, month.Month) == 0);
        if (_focusKey.Length > 0 && OnScreen(DateTime.ParseExact(_focusKey, "yyyy-MM-dd", CultureInfo.InvariantCulture))) return _focusKey;
        if (Shown is { } shown && OnScreen(shown.Start)) return KeyOf(shown.Start);
        return KeyOf(_view);
    }

    private void UpdateTabStops()
    {
        string key = Tabbable();
        foreach (var month in _months) month.SetTabStop(key);
    }

    private void FocusDay()
    {
        if (!_pendingDayFocus) return;
        foreach (var month in _months)
            if (month.Find(_focusKey) is { } day) { _pendingDayFocus = false; day.Focus(); return; }
    }

    private void UpdateShown(bool animate)
    {
        var shown = Shown;
        long time = TimeOf(shown);
        if (time != _shownTime) { _shownDirection = time == long.MinValue || _shownTime == long.MinValue || time >= _shownTime ? 1 : -1; _shownTime = time; }
        bool motion = animate && Animating;
        foreach (var month in _months) month.Update(shown, _today, motion && !Reduced);
        UpdateTabStops();
        int days = shown is { } r ? DayDiff(r.End, r.Start) + 1 : 0;
        string shownText = shown is null ? "No dates" : Format(shown);
        string countText = shown is null ? "" : _anchor is { } a && _hover is { } h && DayDiff(a, h) == 0 ? "Pick an end date" : $"{days} {(days == 1 ? "day" : "days")}";
        _summary?.Set(shownText, _shownDirection, Level, motion);
        _count?.Set(countText, _shownDirection, Level, motion);
        if (_apply is not null) _apply.IsEnabled = shown is not null;
        if (_status is not null)
        {
            string status = _anchor is { } start ? $"Start {FormatDay(start)}. Choose an end date." : shown is null ? "" : $"{shownText}, {countText}";
            if (_status.Text != status)
            {
                _status.Text = status;
                if (status.Length > 0 && UIElementAutomationPeer.FromElement(_status) is { } peer) peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }
        PlaceRailHighlight(motion);
    }

    private int ActivePreset()
    {
        if (_anchor is not null || _draft is not { } draft) return -1;
        for (int i = 0; i < Presets.Count; i++)
        {
            var range = Presets[i].Range(_today);
            var ordered = Ordered(range.Start, range.End);
            if (DayDiff(ordered.Start, draft.Start) == 0 && DayDiff(ordered.End, draft.End) == 0) return i;
        }
        return -1;
    }

    // The preset highlight glides between presets; it appears in place when nothing was highlighted.
    private void PlaceRailHighlight(bool animate)
    {
        if (_railList is null || _railHighlight is null || _railHost is null) return;
        int active = ActivePreset();
        var buttons = _railList.Children.OfType<System.Windows.Controls.Button>().ToList();
        for (int i = 0; i < buttons.Count; i++) buttons[i].Tag = i == active ? "pressed" : i;
        bool motion = animate && Animating && !Reduced;
        if (active < 0 || active >= buttons.Count || buttons[active].ActualWidth <= 0)
        {
            _motion.Double(_railHighlight, OpacityProperty, 0, .14, motion && active < 0);
            return;
        }
        var node = buttons[active];
        var place = node.TranslatePoint(new Point(), _railHost);
        if (_railHighlight.Opacity < .05 || !motion)
        {
            _rail.Snap(_gx, place.X); _rail.Snap(_gy, place.Y); _rail.Snap(_gw, node.ActualWidth); _rail.Snap(_gh, node.ActualHeight);
        }
        else
        {
            _rail.Animate(_gx, place.X, Glide); _rail.Animate(_gy, place.Y, Glide); _rail.Animate(_gw, node.ActualWidth, Glide); _rail.Animate(_gh, node.ActualHeight, Glide);
        }
        _motion.Double(_railHighlight, OpacityProperty, 1, .16, motion);
        if (_compact) node.BringIntoView();
    }

    private void ApplyRail()
    {
        if (_railHighlight?.RenderTransform is not TranslateTransform move) return;
        move.X = _gx.Position; move.Y = _gy.Position;
        _railHighlight.Width = Math.Max(0, _gw.Position); _railHighlight.Height = Math.Max(0, _gh.Position);
    }

    // ---------- Window wiring: outside clicks and deactivation close without applying ----------

    private void AttachWindow()
    {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(window, _window)) return;
        DetachWindow();
        _window = window;
        if (_window is null) return;
        _window.PreviewMouseDown += WindowPointerDown;
        _window.Deactivated += WindowDeactivated;
        _window.LocationChanged += WindowMoved;
    }

    private void DetachWindow()
    {
        if (_window is null) return;
        _window.PreviewMouseDown -= WindowPointerDown;
        _window.Deactivated -= WindowDeactivated;
        _window.LocationChanged -= WindowMoved;
        _window = null;
    }

    private void WindowDeactivated(object? sender, EventArgs e) => Close(false);
    private void WindowMoved(object? sender, EventArgs e) { if (_popup?.IsOpen == true) { _popup.HorizontalOffset += .001; _popup.HorizontalOffset -= .001; } }

    private void WindowPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsOpen || e.OriginalSource is not DependencyObject source || Owns(source)) return;
        Close(false);
    }

    // Logical parents first, so popup content walks back through its Popup into this control.
    private bool Owns(DependencyObject source)
    {
        for (DependencyObject? node = source; node is not null; node = LogicalTreeHelper.GetParent(node) ?? (node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : null))
            if (ReferenceEquals(node, this)) return true;
        return false;
    }

    protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusWithinChanged(e);
        // Focus leaving the picker (Tab out) closes it without applying.
        if (!(bool)e.NewValue && IsOpen && _window?.IsActive == true)
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (IsOpen && Keyboard.FocusedElement is DependencyObject focused && !Owns(focused)) Close(false);
            }));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DateRangePickerPeer(this);
    private sealed class DateRangePickerPeer(DateRangePicker owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(DateRangePicker);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.Label;
    }
}

internal static class ElementExtensions
{
    public static T Also<T>(this T value, Action<T> action) { action(value); return value; }
}
