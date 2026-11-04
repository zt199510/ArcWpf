using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum ScrubInputSource { Scrub, Type, Key, Reset }

public sealed class ScrubInputChangedEventArgs(double value, ScrubInputSource source, string unit) : EventArgs
{
    public double Value { get; } = value;
    /// <summary>What produced the value: a label drag, a typed entry, an arrow key, or a double click reset.</summary>
    public ScrubInputSource Source { get; } = source;
    /// <summary>The unit in effect after the change.</summary>
    public string Unit { get; } = unit;
}

public readonly record struct ScrubExpressionResult(double Value, string? Unit);

public static partial class ScrubExpression
{
    [GeneratedRegex(@"\d*\.?\d+(?:e[+-]?\d+)?|[a-z%°]+|[-+*/()]|\S", RegexOptions.IgnoreCase)] private static partial Regex Tokens();
    [GeneratedRegex(@"[×x](?=\s*[\d(.])", RegexOptions.IgnoreCase)] private static partial Regex Times();
    [GeneratedRegex(@"^\d*\.?\d+")] private static partial Regex Number();
    [GeneratedRegex(@"^[a-z%°]+$", RegexOptions.IgnoreCase)] private static partial Regex Word();

    /// <summary>Evaluates + - * / and parentheses over numbers that may carry a unit. Returns null for anything else, or for an unknown unit.</summary>
    public static ScrubExpressionResult? Evaluate(string text, IReadOnlyList<string> units)
    {
        string source = Times().Replace(text.Trim().Replace(',', '.'), "*").Replace('÷', '/').Replace('−', '-');
        if (source.Length == 0) { return null; }
        string[] tokens = [.. Tokens().Matches(source).Select(match => match.Value)];
        if (tokens.Length == 0) { return null; }
        int index = 0;
        string? unit = null;
        bool bad = false;
        string[] known = [.. units.Select(entry => entry.ToLowerInvariant())];
        string? Peek() => index < tokens.Length ? tokens[index] : null;
        double Primary()
        {
            string? token = Peek();
            index++;
            if (token == "(") { double inner = Sum(); if (Peek() != ")") { bad = true; } index++; return inner; }
            if (token == "-") { return -Primary(); }
            if (token == "+") { return Primary(); }
            if (token is null || !Number().IsMatch(token) || !double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) { bad = true; return 0; }
            if (Peek() is { } next && Word().IsMatch(next))
            {
                index++;
                string word = next.ToLowerInvariant(), alias = word == "°" ? "deg" : word == "deg" ? "°" : word;
                int found = Array.IndexOf(known, word) is var direct && direct >= 0 ? direct : Array.IndexOf(known, alias);
                if (found < 0) { bad = true; } else { unit = units[found]; }
            }
            return number;
        }
        double Product()
        {
            double left = Primary();
            while (Peek() is "*" or "/") { string op = tokens[index++]; double right = Primary(); left = op == "*" ? left * right : left / right; }
            return left;
        }
        double Sum()
        {
            double left = Product();
            while (Peek() is "+" or "-") { string op = tokens[index++]; double right = Product(); left = op == "+" ? left + right : left - right; }
            return left;
        }
        double value = Sum();
        return bad || index != tokens.Length || !double.IsFinite(value) ? null : new ScrubExpressionResult(value, unit);
    }
}

// The editable number. It speaks as a spin button with the value and its unit.
internal sealed class ScrubField : TextBox
{
    private static readonly ControlTemplate Plain = MakeTemplate();
    public ScrubInput? Owner { get; set; }

    public ScrubField()
    {
        OverridesDefaultStyle = true;
        Template = Plain;
        FocusVisualStyle = null;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(4, 0, 0, 0);
        VerticalContentAlignment = VerticalAlignment.Center;
        SelectionOpacity = 1;
    }

    private static ControlTemplate MakeTemplate()
    {
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(FocusableProperty, false);
        host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        host.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        host.SetValue(PaddingProperty, new Thickness(4, 0, 0, 0));
        var template = new ControlTemplate(typeof(TextBox)) { VisualTree = host };
        template.Seal();
        return template;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(ScrubField box) : TextBoxAutomationPeer(box), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;
        protected override string GetNameCore() => box.Owner?.SpokenName ?? base.GetNameCore();
        protected override string GetItemStatusCore() => box.Owner?.ValueText ?? "";
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);
        public double Value => box.Owner?.Value ?? 0;
        public double Minimum => box.Owner?.Minimum ?? 0;
        public double Maximum => box.Owner?.Maximum ?? 0;
        public double SmallChange => box.Owner?.Step ?? 1;
        public double LargeChange => box.Owner?.CoarseAmount ?? 10;
        bool IRangeValueProvider.IsReadOnly => box.Owner is not { IsEnabled: true };
        public void SetValue(double value)
        {
            if (box.Owner is not { IsEnabled: true } owner) { throw new ElementNotEnabledException(); }
            owner.SetFromAutomation(value);
        }
    }
}

// The unit swaps with a small rise and blur rather than the copy transition.
internal sealed class ScrubUnitSwap(double size, Brush ink) : SwapText(size, 1.4, -.14, FontWeights.Normal, ink)
{
    protected override void Enter(TrackedText view, int direction)
    {
        var level = Tween.Level(this);
        if (level == ArcMotionLevel.Off || !IsLoaded) { return; }
        view.Opacity = 0;
        if (level == ArcMotionLevel.Reduced) { Tween.To(view, OpacityProperty, 1, Tween.Instant, null); return; }
        var move = (TranslateTransform)view.RenderTransform;
        move.Y = 6;
        Tween.Spring(view, OpacityProperty, 1, Tween.Snappy);
        Tween.Spring(move, TranslateTransform.YProperty, 0, Tween.Snappy);
        TextMotion.Blur(view, 2, 0, Tween.Snappy, ArcEasing.Spring(Tween.Snappy, Tween.Snappy * 2));
    }

    protected override void Leave(TrackedText view, int direction, Action done)
    {
        var level = Tween.Level(this);
        if (level == ArcMotionLevel.Off || !IsLoaded) { done(); return; }
        Tween.To(view, OpacityProperty, 0, Tween.Instant, null, 0, done);
        if (level == ArcMotionLevel.Reduced) { return; }
        Tween.To((TranslateTransform)view.RenderTransform, TranslateTransform.YProperty, -6, Tween.Instant, null);
        TextMotion.Blur(view, 0, 2, Tween.Instant, null);
    }
}

// A virtual cursor stands in for the locked pointer and wraps at the window edges, the way long scrubs do in design tools.
internal sealed class ScrubCursorAdorner : System.Windows.Documents.Adorner
{
    private static readonly Geometry Glyph = Geometry.Parse("M1.5 8 L7.5 2.5 V6 H18.5 V2.5 L24.5 8 L18.5 13.5 V10 H7.5 V13.5 Z");
    private readonly StackPanel _face = new() { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
    private readonly Border _pill = new() { CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(4, 0, 0, 0), Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(.5, .5) };
    private readonly TextBlock _pillText = new() { FontSize = 12, LineHeight = 16, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Foreground = Brushes.White };
    private readonly TranslateTransform _at = new();

    public ScrubCursorAdorner(UIElement adorned) : base(adorned)
    {
        IsHitTestVisible = false;
        var ink = Color.FromRgb(0x11, 0x11, 0x11);
        var path = new System.Windows.Shapes.Path { Data = Glyph, Fill = new SolidColorBrush(ink), Stroke = Brushes.White, StrokeThickness = 1.4, StrokeLineJoin = PenLineJoin.Round, Width = 26, Height = 16, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Direction = 270, Opacity = .3 } };
        _pill.Background = new SolidColorBrush(ink);
        _pill.Child = _pillText;
        _face.Children.Add(path);
        _face.Children.Add(_pill);
        _face.RenderTransform = _at;
        AddVisualChild(_face);
        Opacity = 0;
        Tween.To(this, OpacityProperty, 1, Tween.Instant, ArcEasing.Standard);
    }

    public void Move(double x, double y) { _at.X = x - 13; _at.Y = y - 8; }
    public void Tag(string? text)
    {
        _pill.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
        _pillText.Text = text ?? "";
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _face;
    protected override Size MeasureOverride(Size constraint) { _face.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); return constraint; }
    protected override Size ArrangeOverride(Size finalSize) { _face.Arrange(new Rect(_face.DesiredSize)); return finalSize; }
}

// scrub-input.tsx (frozen 2026-11-03): a compact number field. Drag the label to scrub, or click in and type arithmetic and units.
public class ScrubInput : Control
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(object), typeof(ScrubInput), new FrameworkPropertyMetadata(null, (d, _) => ((ScrubInput)d).BuildLabel()));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(ScrubInput), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ScrubInput)d).OnValue()));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ScrubInput), new FrameworkPropertyMetadata(double.NegativeInfinity, (d, _) => ((ScrubInput)d).Limits()));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ScrubInput), new FrameworkPropertyMetadata(double.PositiveInfinity, (d, _) => ((ScrubInput)d).Limits()));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(ScrubInput), new FrameworkPropertyMetadata(1d, (d, _) => ((ScrubInput)d).Sync()));
    public static readonly DependencyProperty FineStepProperty = DependencyProperty.Register(nameof(FineStep), typeof(double?), typeof(ScrubInput), new FrameworkPropertyMetadata(null, (d, _) => ((ScrubInput)d).Sync()));
    public static readonly DependencyProperty CoarseStepProperty = DependencyProperty.Register(nameof(CoarseStep), typeof(double?), typeof(ScrubInput), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty PrecisionProperty = DependencyProperty.Register(nameof(Precision), typeof(int?), typeof(ScrubInput), new FrameworkPropertyMetadata(null, (d, _) => ((ScrubInput)d).Sync()));
    public static readonly DependencyProperty PixelsPerStepProperty = DependencyProperty.Register(nameof(PixelsPerStep), typeof(double), typeof(ScrubInput), new FrameworkPropertyMetadata(2d));
    public static readonly DependencyProperty WrapProperty = DependencyProperty.Register(nameof(Wrap), typeof(bool), typeof(ScrubInput), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ScrubInput), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ScrubInput)d).BuildUnit()));
    public static readonly DependencyProperty UnitsProperty = DependencyProperty.Register(nameof(Units), typeof(IReadOnlyList<string>), typeof(ScrubInput), new FrameworkPropertyMetadata(null, (d, _) => ((ScrubInput)d).BuildUnit()));
    public static readonly DependencyProperty ResetValueProperty = DependencyProperty.Register(nameof(ResetValue), typeof(double?), typeof(ScrubInput), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty ShowTrackProperty = DependencyProperty.Register(nameof(ShowTrack), typeof(bool?), typeof(ScrubInput), new FrameworkPropertyMetadata(null, (d, _) => ((ScrubInput)d).Limits()));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(ControlSize), typeof(ScrubInput), new FrameworkPropertyMetadata(ControlSize.Medium, (d, _) => ((ScrubInput)d).Sizes()));
    public static readonly DependencyProperty UsePointerLockProperty = DependencyProperty.Register(nameof(UsePointerLock), typeof(bool), typeof(ScrubInput), new FrameworkPropertyMetadata(true));

    /// <summary>Text or a small icon. Icon labels need AutomationProperties.Name so the field keeps an accessible name.</summary>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>One step for arrows and scrubbing.</summary>
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    /// <summary>Step with Alt held. Defaults to a tenth of Step.</summary>
    public double? FineStep { get => (double?)GetValue(FineStepProperty); set => SetValue(FineStepProperty, value); }
    /// <summary>Step with Shift held. Defaults to ten times Step.</summary>
    public double? CoarseStep { get => (double?)GetValue(CoarseStepProperty); set => SetValue(CoarseStepProperty, value); }
    /// <summary>Decimal places kept. Defaults to the precision of FineStep.</summary>
    public int? Precision { get => (int?)GetValue(PrecisionProperty); set => SetValue(PrecisionProperty, value); }
    /// <summary>Pointer travel for one step while scrubbing.</summary>
    public double PixelsPerStep { get => (double)GetValue(PixelsPerStepProperty); set => SetValue(PixelsPerStepProperty, value); }
    /// <summary>Values wrap around from Maximum to Minimum instead of stopping, as rotation does. Needs both limits.</summary>
    public bool Wrap { get => (bool)GetValue(WrapProperty); set => SetValue(WrapProperty, value); }
    /// <summary>The unit shown after the number. With more than one entry in Units, it becomes a button that cycles them.</summary>
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    /// <summary>Units a typed value may carry. Defaults to the current unit.</summary>
    public IReadOnlyList<string>? Units { get => (IReadOnlyList<string>?)GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }
    /// <summary>Double click the label to return here.</summary>
    public double? ResetValue { get => (double?)GetValue(ResetValueProperty); set => SetValue(ResetValueProperty, value); }
    /// <summary>Shows how far the value sits between the limits as a quiet fill. On by default when both limits are set.</summary>
    public bool? ShowTrack { get => (bool?)GetValue(ShowTrackProperty); set => SetValue(ShowTrackProperty, value); }
    public ControlSize Size { get => (ControlSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Hides and recentres the pointer while a mouse scrubs, so a long drag never runs out of screen.</summary>
    public bool UsePointerLock { get => (bool)GetValue(UsePointerLockProperty); set => SetValue(UsePointerLockProperty, value); }

    /// <summary>Fires for every change, including each step of a scrub.</summary>
    public event EventHandler<ScrubInputChangedEventArgs>? ValueChanged;
    /// <summary>Fires once when a change settles: a scrub ends, Enter or blur commits typing, or a key step lands.</summary>
    public event EventHandler<ScrubInputChangedEventArgs>? ValueCommitted;
    public event EventHandler<string>? UnitChanged;

    private sealed class Drag { public double LastX; public double Travel; public double Raw; public double Start; public bool Moved; public bool Locked; public bool Mouse; public POINT Anchor; public double CursorX; public double CursorY; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetCursorPos(int x, int y);

    // Movement before a press on the label becomes a scrub, and the most pixels the field strains past a limit.
    private const double Slop = 3, Strain = 12, OvershootCap = 90;
    private static readonly SpringTune Settle = SpringTune.Physical(.34), Shake = SpringTune.Physical(.3);

    private readonly FrameLoop _loop;
    private readonly OwnedMotion _motion = new();
    private readonly ThemedBrush _shellFill, _shellEdge, _handleInk, _washInk;
    private readonly GestureSpring _strain = new(0, .01, .5), _fill = new(0, .0005, .005);
    private readonly SolidColorBrush _unitInk = new(), _tagInk = new();
    private readonly Border _shell = new() { BorderThickness = new Thickness(1), IsHitTestVisible = false };
    private readonly Rectangle _wash = new();
    private readonly ScaleTransform _shellScale = new(), _washScale = new(), _unitPress = new(), _tagScale = new(1, 1);
    private readonly TranslateTransform _lean = new(), _tagLift = new();
    private readonly Border _handle = new() { MinWidth = 30, Padding = new Thickness(10, 0, 4, 0), Background = Brushes.Transparent, Cursor = Cursors.SizeWE };
    private readonly ScrubField _field = new();
    private readonly Grid _unitHost = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _tag = new() { CornerRadius = new CornerRadius(10), Padding = new Thickness(7, 2, 7, 2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6, -26, 0, 0), IsHitTestVisible = false, Opacity = 0, Visibility = Visibility.Hidden };
    private readonly TextBlock _tagText = new() { FontSize = 12, LineHeight = 16, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
    private readonly DispatcherTimer _invalidTimer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private Grid? _root;
    private ArcChromeButton? _cycle;
    private ScrubUnitSwap? _swap;
    private TrackedText? _plainUnit;
    private Drag? _drag;
    private ScrubCursorAdorner? _cursor;
    private System.Windows.Documents.AdornerLayer? _layer;
    private Window? _window;
    private string? _draft, _modifier;
    private string _unitNow = "";
    private double _latest;
    private bool _scrubbing, _invalid, _syncing, _emitting, _hover, _suppressClick, _built;

    static ScrubInput() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ScrubInput), new FrameworkPropertyMetadata(typeof(ScrubInput)));

    public ScrubInput()
    {
        _loop = new FrameLoop(Frame);
        _shellFill = new ThemedBrush(this, _motion);
        _shellEdge = new ThemedBrush(this, _motion);
        _handleInk = new ThemedBrush(this, _motion);
        _washInk = new ThemedBrush(this, _motion);
        _field.Owner = this;
        _invalidTimer.Tick += (_, _) => { _invalidTimer.Stop(); _invalid = false; States(true); };
        _handle.MouseLeftButtonDown += OnHandleDown;
        _handle.MouseMove += OnHandleMove;
        _handle.MouseLeftButtonUp += OnHandleUp;
        _handle.LostMouseCapture += (_, _) => EndScrub(false);
        _handle.MouseEnter += (_, _) => States(true);
        _handle.MouseLeave += (_, _) => States(true);
        MouseEnter += (_, _) => { _hover = true; States(true); };
        MouseLeave += (_, _) => { _hover = false; States(true); };
        IsKeyboardFocusWithinChanged += (_, _) => States(true);
        IsEnabledChanged += (_, _) => { Opacity = IsEnabled ? 1 : .5; States(false); };
        _field.PreviewKeyDown += OnFieldKey;
        _field.TextChanged += (_, _) => { if (!_syncing) { _draft = _field.Text; } };
        _field.LostKeyboardFocus += (_, _) => CommitDraft();
        _field.GotKeyboardFocus += (_, _) => _field.SelectAll();
        // A first click selects the whole value, ready to retype, like focusing by keyboard.
        _field.PreviewMouseLeftButtonDown += (_, e) => { if (!_field.IsKeyboardFocusWithin) { _field.Focus(); e.Handled = true; } };
        Loaded += (_, _) => { States(false); Inks(); };
        Unloaded += (_, _) => { EndScrub(false); _loop.Stop(); _motion.Stop(); _invalidTimer.Stop(); };
        ThemeWatch.Add(this, () => { States(false); Inks(); });
    }

    internal double StrainForTest => _strain.Position;
    internal double FillForTest => _fill.Position;
    internal double FillTargetForTest => _fill.Target;
    internal bool ScrubbingForTest => _scrubbing;
    internal bool InvalidForTest => _invalid;
    internal string TextForTest => _field.Text;
    internal string? TagForTest => _tag.Visibility == Visibility.Visible ? _tagText.Text : null;
    internal string? DraftForTest => _draft;
    internal string EffectiveUnitForTest => _unitNow;
    internal ScrubField FieldForTest => _field;
    internal bool CyclesForTest => _cycle is not null;
    internal double LeanForTest => _lean.X;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    internal void SettleForTest() { for (int index = 0; index < 300; index++) { _loop.Advance(1d / 60); } }
    internal void TypeForTest(string text) { _draft = text; _syncing = true; _field.Text = text; _syncing = false; }
    internal void CommitForTest() => CommitDraft();
    internal void StepForTest(int direction, bool shift = false, bool alt = false) => StepBy(direction, shift, alt);
    internal void BeginForTest(double x = 0) => BeginScrub(x, true, default);
    internal void ScrubForTest(double dx, bool shift = false, bool alt = false) { if (_drag is { } state) { Advance(state, dx, shift, alt); } }
    internal void EndForTest(bool cancel = false) => EndScrub(cancel);
    internal void CycleForTest() => CycleUnit();
    internal void ResetForTest() => DoReset();
    internal void EscapeForTest() => Escape();

    internal string SpokenName => AutomationProperties.GetName(this) is { Length: > 0 } name ? name : Label as string ?? "";
    internal string ValueText => FormatNumber(Value, Places) + (_unitNow.Length == 0 ? "" : " " + (_unitNow == "%" ? "percent" : _unitNow is "deg" or "°" ? "degrees" : _unitNow));
    internal double CoarseAmount => Coarse;

    private double Fine => FineStep ?? Step / 10;
    private double Coarse => CoarseStep ?? Step * 10;
    private int Places => Precision ?? Math.Max(GestureMath.DecimalsOf(Step), GestureMath.DecimalsOf(Fine));
    private bool Bounded => double.IsFinite(Minimum) && double.IsFinite(Maximum) && Maximum > Minimum;
    private bool CanWrap => Wrap && Bounded;
    private bool HasTrack => (ShowTrack ?? true) && Bounded;
    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;
    private double Inner => (Size == ControlSize.Small ? 9 : 12) - 1;
    private IReadOnlyList<string> UnitList => Units ?? (_unitNow.Length > 0 ? [_unitNow] : []);

    private static string FormatNumber(double value, int places)
    {
        string text = GestureMath.RoundTo(value, places).ToString("R", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host) { return; }
        _built = true;
        _latest = Value;
        _root = new Grid();
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });

        _wash.Fill = _washInk.Brush;
        _wash.RenderTransform = _washScale;
        _washScale.ScaleX = 0;
        _shell.Background = _shellFill.Brush;
        _shell.BorderBrush = _shellEdge.Brush;
        // overflow: hidden on the rounded shell.
        var washClip = new Grid();
        washClip.Children.Add(_wash);
        washClip.SizeChanged += (_, e) => washClip.Clip = new RectangleGeometry(new Rect(e.NewSize), Inner, Inner);
        _shell.Child = washClip;
        _shell.RenderTransform = _shellScale;
        // The shell is a separate layer behind the row, so it can strain past a limit without stretching the text.
        Grid.SetColumnSpan(_shell, 2);
        _root.Children.Add(_shell);

        _handle.VerticalAlignment = VerticalAlignment.Stretch;
        _root.Children.Add(_handle);

        _field.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
        _field.SetResourceReference(TextBoxBase.CaretBrushProperty, "ArcWpf.Brush.Foreground");
        var body = new Grid { Margin = new Thickness(0, 0, 10, 0), RenderTransform = _lean };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _unitHost.Margin = new Thickness(2, 0, 0, 0);
        Grid.SetColumn(_unitHost, 1);
        body.Children.Add(_field);
        body.Children.Add(_unitHost);
        Grid.SetColumn(body, 1);
        _root.Children.Add(body);

        _tag.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Foreground");
        _tagText.Foreground = _tagInk;
        _tag.Child = _tagText;
        _tag.RenderTransform = new TransformGroup { Children = { _tagScale, _tagLift } };
        _tag.RenderTransformOrigin = new Point(.5, .5);
        Grid.SetColumnSpan(_tag, 2);
        _root.Children.Add(_tag);

        host.Child = _root;
        Sizes();
        BuildLabel();
        BuildUnit();
        Limits();
        _fill.Jump(Fraction);
        Paint();
        Sync();
        States(false);
        Inks();
    }

    private void Sizes()
    {
        bool small = Size == ControlSize.Small;
        Height = small ? 30 : 36;
        FontSize = small ? 12 : 14;
        _field.FontSize = FontSize;
        _shell.CornerRadius = new CornerRadius(small ? 9 : 12);
        if (Label is string) { BuildLabel(); }
    }

    private void Inks()
    {
        _unitInk.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        _tagInk.Color = Tween.Resolve(this, "ArcWpf.Brush.Background");
        var selection = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        selection.A = (byte)Math.Round(255 * .16);
        _field.SelectionBrush = new SolidColorBrush(selection);
        _cycle?.Refresh(false);
    }

    private void BuildLabel()
    {
        if (!_built) { return; }
        object? label = Label;
        if (label is ArcDataGridIcon icon)
        {
            // Icon labels take the handle's ink so a scrub darkens them too.
            icon.Size = 14;
            icon.Stroke = _handleInk.Brush;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            _handle.Child = icon;
            return;
        }
        if (label is UIElement element) { _handle.Child = element; return; }
        var text = CopyMotion.Text(label?.ToString() ?? "", FontSize, 1.4, -.14, FontWeights.Medium, _handleInk.Brush);
        text.VerticalAlignment = VerticalAlignment.Center;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        _handle.Child = text;
    }

    private void BuildUnit()
    {
        if (!_built) { return; }
        string unit = !string.IsNullOrEmpty(Unit) ? Unit : Units?.FirstOrDefault() ?? "";
        bool same = _unitNow == unit;
        _unitNow = unit;
        if (unit.Length == 0) { _unitHost.Children.Clear(); _cycle = null; _swap = null; _plainUnit = null; return; }
        if ((Units?.Count ?? 0) > 1)
        {
            if (_cycle is null)
            {
                _unitHost.Children.Clear();
                _plainUnit = null;
                _swap = new ScrubUnitSwap(12, _unitInk);
                _cycle = new ArcChromeButton(6) { MinWidth = 18, Height = 22, Padding = new Thickness(4, 0, 4, 0), RenderTransform = _unitPress, RenderTransformOrigin = new Point(.5, .5) };
                _cycle.Roles = button => (button.IsMouseOver && button.IsEnabled ? "ScrubUnitHover" : null, null, button.IsMouseOver && button.IsEnabled ? "Foreground" : "TextMuted");
                _cycle.Content = _swap;
                _cycle.Click += (_, _) => CycleUnit();
                _cycle.PreviewMouseLeftButtonDown += (_, _) => PressUnit(.94);
                _cycle.PreviewMouseLeftButtonUp += (_, _) => PressUnit(1);
                _cycle.MouseLeave += (_, _) => PressUnit(1);
                _unitHost.Children.Add(_cycle);
            }
            _swap!.Set(unit);
            AutomationProperties.SetName(_cycle, $"Unit: {unit}. Change unit");
        }
        else
        {
            _unitHost.Children.Clear();
            _cycle = null;
            _swap = null;
            _plainUnit = CopyMotion.Text(unit, 12, 1.4, -.14, FontWeights.Normal, _unitInk);
            _plainUnit.HorizontalAlignment = HorizontalAlignment.Center;
            _plainUnit.VerticalAlignment = VerticalAlignment.Center;
            _unitHost.Children.Add(new Border { MinWidth = 18, Height = 22, Padding = new Thickness(4, 0, 4, 0), Child = _plainUnit, IsHitTestVisible = false });
        }
        if (!same && UIElementAutomationPeer.FromElement(_field) is { } peer) { peer.InvalidatePeer(); }
    }

    private void PressUnit(double scale)
    {
        if (Reduced) { return; }
        Tween.To(_unitPress, ScaleTransform.ScaleXProperty, scale, Tween.Fast, ArcEasing.Standard);
        Tween.To(_unitPress, ScaleTransform.ScaleYProperty, scale, Tween.Fast, ArcEasing.Standard);
    }

    private double Fraction => HasTrack ? Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1) : 0;

    private void Limits()
    {
        if (!_built) { return; }
        _wash.Visibility = HasTrack ? Visibility.Visible : Visibility.Collapsed;
        AimFill();
    }

    // The fill follows a drag 1:1 and glides to typed or stepped values.
    private void AimFill()
    {
        if (Reduced || !IsLoaded || _drag is { Moved: true }) { _fill.Jump(Fraction); Paint(); return; }
        _fill.To(Fraction, SpringTune.Smooth);
        _loop.Start();
    }

    private void OnValue()
    {
        if (!_built) { return; }
        if (!_emitting) { _latest = Value; }
        Sync();
        AimFill();
        if (UIElementAutomationPeer.FromElement(_field) is { } peer) { peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, Value, Value); }
    }

    private void Sync()
    {
        if (!_built) { return; }
        string text = _draft ?? FormatNumber(Value, Places);
        if (_field.Text == text) { return; }
        _syncing = true;
        _field.Text = text;
        _syncing = false;
        if (_field.IsKeyboardFocusWithin && _draft is null) { _field.SelectAll(); }
    }

    private void States(bool animate)
    {
        if (!_built) { return; }
        bool focused = IsKeyboardFocusWithin;
        _shellFill.To(focused ? "Surface" : "SurfaceMuted", animate);
        _shellEdge.To(_invalid ? "ArcDanger" : focused || _scrubbing ? "BorderStrong" : _hover && IsEnabled ? "Border" : null, animate);
        _handleInk.To(_scrubbing || _handle.IsMouseOver ? "Foreground" : "TextMuted", animate);
        // color-mix(foreground, transparent), stronger while held.
        _washInk.To("ScrubWash" + (_scrubbing ? "Held" : ""), animate);
    }

    private bool Frame(double seconds)
    {
        _strain.Step(seconds);
        _fill.Step(seconds);
        Paint();
        return _strain.Active || _fill.Active;
    }

    private void Paint()
    {
        if (!_built) { return; }
        double strain = _strain.Position, width = Math.Max(1, ActualWidth);
        // Strain past a limit: positive past max, negative past min. It drives the shell stretch and the value's lean.
        _shellScale.CenterX = strain >= 0 ? 0 : width;
        _shellScale.ScaleX = 1 + Math.Abs(strain) / width;
        _lean.X = strain * .5;
        _washScale.ScaleX = Math.Clamp(_fill.Position, 0, 1);
    }

    private void Emit(double next, ScrubInputSource source, bool commit, string? unit = null)
    {
        string nextUnit = unit ?? _unitNow;
        if (next != _latest)
        {
            _latest = next;
            _emitting = true;
            Value = next;
            _emitting = false;
            ValueChanged?.Invoke(this, new ScrubInputChangedEventArgs(next, source, nextUnit));
        }
        if (commit) { ValueCommitted?.Invoke(this, new ScrubInputChangedEventArgs(next, source, nextUnit)); }
    }

    private double Constrain(double next)
    {
        double rounded = GestureMath.RoundTo(next, Places);
        if (CanWrap)
        {
            double span = Maximum - Minimum;
            return GestureMath.RoundTo(((rounded - Minimum) % span + span) % span + Minimum, Places);
        }
        return Math.Min(Maximum, Math.Max(Minimum, rounded));
    }

    // A velocity kick on the strain: the field leans toward the push and springs home.
    private void Bump(int direction, SpringTune? kind = null)
    {
        if (Reduced) { return; }
        _strain.To(0, kind ?? Settle, direction * 260);
        _loop.Start();
    }

    private void FlagInvalid()
    {
        _invalid = true;
        States(true);
        _invalidTimer.Stop();
        _invalidTimer.Start();
        Bump(-1, Shake);
    }

    private bool CommitDraft()
    {
        string? text = _draft;
        _draft = null;
        if (text is null) { return true; }
        if (text.Trim() == FormatNumber(Value, Places)) { Sync(); return true; }
        var parsed = ScrubExpression.Evaluate(text, UnitList);
        if (parsed is null)
        {
            Sync();
            FlagInvalid();
            return false;
        }
        string nextUnit = _unitNow;
        if (parsed.Value.Unit is { } typed && typed != nextUnit)
        {
            nextUnit = typed;
            SetCurrentValue(UnitProperty, nextUnit);
            UnitChanged?.Invoke(this, nextUnit);
        }
        double next = Constrain(parsed.Value.Value);
        if (!CanWrap && (parsed.Value.Value > Maximum || parsed.Value.Value < Minimum)) { Bump(parsed.Value.Value > Maximum ? 1 : -1); }
        Emit(next, ScrubInputSource.Type, true, nextUnit);
        Sync();
        return true;
    }

    private void StepBy(int direction, bool shift, bool alt)
    {
        double amount = shift ? Coarse : alt ? Fine : Step, value = _latest;
        if (!CanWrap && ((direction > 0 && value >= Maximum) || (direction < 0 && value <= Minimum))) { Bump(direction); return; }
        // Coarse steps land on the coarse grid, so 13 with Shift goes to 20, not 23.
        double snapped = shift ? (direction > 0 ? Math.Floor(value / amount + 1e-9) + 1 : Math.Ceiling(value / amount - 1e-9) - 1) * amount : value + direction * amount;
        Emit(Constrain(snapped), ScrubInputSource.Key, true);
    }

    private void OnHandleDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEnabled || _drag is not null) { return; }
        e.Handled = true;
        if (e.ClickCount >= 2) { DoReset(); return; }
        _suppressClick = false;
        if (_draft is not null) { CommitDraft(); }
        _handle.CaptureMouse();
        GetCursorPos(out POINT anchor);
        BeginScrub(e.GetPosition(_handle).X, e.StylusDevice is null, anchor);
        var root = Window.GetWindow(this)?.Content as UIElement;
        if (_drag is { } state && root is not null) { var at = e.GetPosition(root); (state.CursorX, state.CursorY) = (at.X, at.Y); }
    }

    private void BeginScrub(double x, bool mouse, POINT anchor) => _drag = new Drag { LastX = x, Raw = _latest, Start = _latest, Mouse = mouse, Anchor = anchor };

    private void OnHandleMove(object sender, MouseEventArgs e)
    {
        if (_drag is not { } state) { return; }
        double dx;
        if (state.Locked)
        {
            GetCursorPos(out POINT now);
            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            dx = (now.X - state.Anchor.X) / dip;
            if (now.X != state.Anchor.X || now.Y != state.Anchor.Y) { SetCursorPos(state.Anchor.X, state.Anchor.Y); }
            if (dx == 0) { return; }
        }
        else
        {
            double x = e.GetPosition(_handle).X;
            dx = x - state.LastX;
            state.LastX = x;
        }
        var modifiers = Keyboard.Modifiers;
        Advance(state, dx, modifiers.HasFlag(ModifierKeys.Shift), modifiers.HasFlag(ModifierKeys.Alt));
    }

    private void Advance(Drag state, double dx, bool shift, bool alt)
    {
        state.Travel += dx;
        if (!state.Moved)
        {
            if (Math.Abs(state.Travel) < Slop) { return; }
            state.Moved = true;
            Keyboard.ClearFocus();
            Mouse.OverrideCursor = Cursors.SizeWE;
            _scrubbing = true;
            States(true);
            HookWindow();
            if (UsePointerLock && state.Mouse && _handle.IsMouseCaptured) { Lock(state); }
        }
        if (state.Locked && _cursor is not null)
        {
            double width = Math.Max(1, _layer?.ActualWidth ?? 1);
            state.CursorX = ((state.CursorX + dx) % width + width) % width;
            _cursor.Move(state.CursorX, state.CursorY);
        }
        double amount = shift ? Coarse : alt ? Fine : Step;
        SetModifier(shift ? "coarse" : alt ? "fine" : null);
        state.Raw += dx / PixelsPerStep * amount;
        if (!CanWrap)
        {
            double perPx = amount / PixelsPerStep;
            double over = state.Raw > Maximum ? (state.Raw - Maximum) / perPx : state.Raw < Minimum ? (state.Raw - Minimum) / perPx : 0;
            // Cap the stored overshoot so reversing starts moving the value again after a short relax, not a long walk back.
            if (Math.Abs(over) > OvershootCap) { state.Raw = (over > 0 ? Maximum : Minimum) + Math.Sign(over) * OvershootCap * perPx; }
            _strain.Jump(Reduced ? 0 : GestureMath.Rubber(Math.Clamp(over, -OvershootCap, OvershootCap), Strain));
            Paint();
        }
        double next = Constrain(Math.Floor(state.Raw / amount + .5) * amount);
        if (next != _latest) { Emit(next, ScrubInputSource.Scrub, false); }
    }

    // The pointer is hidden and held in place while its movement drives the value. A drawn cursor stands in for it; without somewhere to draw one the real pointer stays.
    private void Lock(Drag state)
    {
        if (Window.GetWindow(this)?.Content is not UIElement root || System.Windows.Documents.AdornerLayer.GetAdornerLayer(root) is not { } layer) { return; }
        _layer = layer;
        _cursor = new ScrubCursorAdorner(root);
        _cursor.Move(state.CursorX, state.CursorY);
        layer.Add(_cursor);
        Mouse.OverrideCursor = Cursors.None;
        state.Locked = true;
        Tag();
    }

    private void SetModifier(string? mode)
    {
        if (_modifier == mode) { return; }
        _modifier = mode;
        Tag();
    }

    private string? Multiplier => _modifier == "coarse" ? $"×{FormatNumber(Coarse / Step, 2)}" : _modifier == "fine" ? $"×{FormatNumber(Fine / Step, 3)}" : null;

    private void Tag()
    {
        string? text = Multiplier;
        _cursor?.Tag(text);
        bool show = text is not null && _drag is not { Locked: true };
        if (show)
        {
            _tagText.Text = text;
            bool arriving = _tag.Visibility != Visibility.Visible;
            _tag.Visibility = Visibility.Visible;
            if (Reduced || !IsLoaded) { Tween.To(_tag, OpacityProperty, 1, Tween.Instant, null); return; }
            if (arriving)
            {
                _tagScale.ScaleX = _tagScale.ScaleY = .96;
                _tagLift.Y = 4;
            }
            Tween.Spring(_tag, OpacityProperty, 1, Tween.Snappy);
            Tween.Spring(_tagScale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
            Tween.Spring(_tagScale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
            Tween.Spring(_tagLift, TranslateTransform.YProperty, 0, Tween.Snappy);
        }
        else if (_tag.Visibility == Visibility.Visible)
        {
            _tagScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _tagScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _tagLift.BeginAnimation(TranslateTransform.YProperty, null);
            Tween.To(_tag, OpacityProperty, 0, Tween.Instant, null, 0, () => { if (Multiplier is null || _drag is { Locked: true }) { _tag.Visibility = Visibility.Hidden; } });
        }
    }

    private void HookWindow()
    {
        _window = Window.GetWindow(this);
        if (_window is null) { return; }
        _window.PreviewKeyDown += OnWindowKey;
        _window.PreviewKeyUp += OnWindowKey;
    }

    private void UnhookWindow()
    {
        if (_window is null) { return; }
        _window.PreviewKeyDown -= OnWindowKey;
        _window.PreviewKeyUp -= OnWindowKey;
        _window = null;
    }

    private void OnWindowKey(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape && e.IsDown) { e.Handled = true; EndScrub(true); return; }
        if (key is Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt)
        {
            e.Handled = true;
            var modifiers = Keyboard.Modifiers;
            SetModifier(modifiers.HasFlag(ModifierKeys.Shift) ? "coarse" : modifiers.HasFlag(ModifierKeys.Alt) ? "fine" : null);
        }
    }

    private void OnHandleUp(object sender, MouseButtonEventArgs e)
    {
        if (_drag is not { } state) { return; }
        bool moved = state.Moved;
        EndScrub(false);
        _handle.ReleaseMouseCapture();
        e.Handled = true;
        if (moved || !IsEnabled) { return; }
        _field.Focus();
        _field.SelectAll();
    }

    private void EndScrub(bool cancel)
    {
        if (_drag is not { } state) { return; }
        _drag = null;
        Mouse.OverrideCursor = null;
        if (_cursor is not null) { _layer?.Remove(_cursor); _cursor = null; }
        UnhookWindow();
        _scrubbing = false;
        _modifier = null;
        Tag();
        States(true);
        if (_handle.IsMouseCaptured) { _handle.ReleaseMouseCapture(); }
        if (!state.Moved) { return; }
        _suppressClick = true;
        if (cancel) { Emit(state.Start, ScrubInputSource.Scrub, true); }
        else { ValueCommitted?.Invoke(this, new ScrubInputChangedEventArgs(_latest, ScrubInputSource.Scrub, _unitNow)); }
        if (Reduced) { _strain.Jump(0); Paint(); }
        else { _strain.To(0, Settle); _loop.Start(); }
    }

    private void DoReset()
    {
        if (!IsEnabled || ResetValue is not { } reset) { return; }
        Emit(Constrain(reset), ScrubInputSource.Reset, true);
    }

    private void OnFieldKey(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Up or Key.Down)
        {
            e.Handled = true;
            if (_draft is not null && !CommitDraft()) { return; }
            var modifiers = Keyboard.Modifiers;
            StepBy(key == Key.Up ? 1 : -1, modifiers.HasFlag(ModifierKeys.Shift), modifiers.HasFlag(ModifierKeys.Alt));
            Reselect();
        }
        else if (key == Key.Enter)
        {
            e.Handled = true;
            CommitDraft();
            Reselect();
        }
        else if (key == Key.Escape && _draft is not null)
        {
            e.Handled = true;
            Escape();
        }
    }

    private void Escape()
    {
        _draft = null;
        Sync();
        Reselect();
    }

    private void Reselect() => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(_field.SelectAll));

    private void CycleUnit()
    {
        var list = UnitList;
        if (list.Count < 2) { return; }
        int at = -1;
        for (int index = 0; index < list.Count; index++) { if (list[index] == _unitNow) { at = index; break; } }
        string next = list[(at + 1) % list.Count];
        SetCurrentValue(UnitProperty, next);
        UnitChanged?.Invoke(this, next);
    }

    internal void SetFromAutomation(double value) => Emit(Constrain(value), ScrubInputSource.Type, true);
}
