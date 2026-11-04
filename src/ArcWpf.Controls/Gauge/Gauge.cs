using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum ArcTone { Accent, Success, Warning, Danger }

/// <summary>A band that starts at From: the highest band the value reaches sets the tone and names the state, so it never rests on color alone.</summary>
public sealed class GaugeThreshold
{
    public double From { get; set; }
    public ArcTone Tone { get; set; }
    public string Label { get; set; } = "";
}

public sealed class GaugeThresholdCollection : Collection<GaugeThreshold>;

internal static class ToneBrushes
{
    public static string Line(ArcTone tone) => "ArcWpf.Brush." + tone switch { ArcTone.Success => "ArcSuccess", ArcTone.Warning => "ArcWarning", ArcTone.Danger => "ArcDanger", _ => "Accent" };
    public static string Copy(ArcTone tone) => tone == ArcTone.Accent ? "ArcWpf.Brush.AccentStrong" : Line(tone);
}

// gauge.tsx (frozen 2026-11-03): a 270 degree activity ring with a percent readout and labelled bands.
public class Gauge : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(Gauge), new FrameworkPropertyMetadata(0d, (d, _) => ((Gauge)d).OnValue()));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(Gauge), new FrameworkPropertyMetadata(0d, (d, _) => ((Gauge)d).OnValue()));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(Gauge), new FrameworkPropertyMetadata(100d, (d, _) => ((Gauge)d).OnValue()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Gauge), new FrameworkPropertyMetadata("", (d, _) => ((Gauge)d).Sync()));
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(nameof(Detail), typeof(string), typeof(Gauge), new FrameworkPropertyMetadata(null, (d, _) => ((Gauge)d).Sync()));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(ArcTone), typeof(Gauge), new FrameworkPropertyMetadata(ArcTone.Accent, (d, _) => ((Gauge)d).Sync()));
    public static readonly DependencyProperty ThresholdsProperty = DependencyProperty.Register(nameof(Thresholds), typeof(IList<GaugeThreshold>), typeof(Gauge), new FrameworkPropertyMetadata(null, (d, _) => ((Gauge)d).Sync()));

    /// <summary>Current value within Minimum and Maximum.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>What is measured.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Secondary caption line.</summary>
    public string? Detail { get => (string?)GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    /// <summary>Ring color when no threshold applies.</summary>
    public ArcTone Tone { get => (ArcTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    /// <summary>Bands by starting value. The highest band reached sets the tone and shows its label.</summary>
    public IList<GaugeThreshold>? Thresholds { get => (IList<GaugeThreshold>?)GetValue(ThresholdsProperty); set => SetValue(ThresholdsProperty, value); }

    private sealed class Ring(Gauge owner) : FrameworkElement
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            double width = Math.Min(double.IsInfinity(availableSize.Width) ? MaxRing : availableSize.Width, MaxRing);
            return new Size(width, width * .9);
        }
        protected override void OnRender(DrawingContext dc) => owner.DrawRing(dc, ActualWidth / 100);
    }

    private const double MaxRing = 176, Radius = 42, Start = -135, Span = 270, StrokeWidth = 9, DisplayTracking = -1.08, BodyTracking = -.16;
    // The first fill is slower and never overshoots, like a ring closing when the view opens.
    private static readonly SpringTune Reveal = SpringTune.Physical(Tween.Considered * 1.6);

    private readonly FrameLoop _loop;
    private readonly GestureSpring _sweep = new(0, .0005, .005), _count = new(0, .01, .1), _digitsWidth = new(0, .05, .5);
    private readonly InViewOnce _inView;
    private readonly SolidColorBrush _line = new(), _lineSoft = new(), _copy = new(), _ink = new(), _secondary = new(), _muted = new();
    private readonly Ring _ring;
    private readonly Grid _readout = new() { IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
    private readonly Canvas _digitsBox = new() { HorizontalAlignment = HorizontalAlignment.Left, ClipToBounds = false };
    private TrackedText? _digits, _unit;
    private SwapText? _status, _label, _detail;
    private StackPanel? _caption;
    private Decorator? _host;
    private GaugeThreshold? _live;
    private bool _filled;
    private int _moved = 1, _displayed;
    private string _sizerText = "";
    private ArcTone? _appliedTone;

    static Gauge() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Gauge), new FrameworkPropertyMetadata(typeof(Gauge)));

    public Gauge()
    {
        _loop = new FrameLoop(Frame);
        _ring = new Ring(this);
        _inView = new InViewOnce(this, Drive);
        Loaded += (_, _) => { Paint(false); Drive(); };
        Unloaded += (_, _) => _loop.Stop();
        ThemeWatch.Add(this, () => { Inks(); _appliedTone = null; Paint(false); });
    }

    internal double SweepForTest => _sweep.Position;
    internal double CountForTest => _count.Position;
    internal string DigitsForTest => _digits?.Text ?? "";
    internal string StatusForTest => _status?.Text ?? "";
    internal ArcTone ActiveToneForTest => ActiveTone;
    internal bool FilledForTest => _filled;
    internal bool RunningForTest => _loop.IsRunning;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    internal string SpokenForTest => Spoken;

    protected override AutomationPeer OnCreateAutomationPeer() => new ArcRangePeer(this, AutomationControlType.ProgressBar, () => Label, () => Minimum, () => SafeMax, () => Math.Clamp(Value, Minimum, SafeMax), () => Spoken);

    private double SafeMax => Maximum > Minimum ? Maximum : Minimum + 1;
    private double Share => Math.Clamp((Value - Minimum) / (SafeMax - Minimum), 0, 1);
    private int Display => (int)Math.Round(Share * 100, MidpointRounding.AwayFromZero);
    private string Spoken => $"{Display}%" + (BandAt(Value) is { } band ? $", {band.Label}" : "");
    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;

    private GaugeThreshold? BandAt(double at)
    {
        GaugeThreshold? found = null;
        foreach (var band in (Thresholds ?? []).OrderBy(band => band.From)) { if (at >= band.From - 1e-6) { found = band; } }
        return found;
    }

    private GaugeThreshold? Shown => Reduced || !_filled ? BandAt(Value) : _live;
    private ArcTone ActiveTone => Shown?.Tone ?? Tone;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Build();
    }

    private void Build()
    {
        if (_host is null) { return; }
        Inks();
        var family = (FontFamily)FontFamily;
        _digits = CopyMotion.Text("0", 36, 1, DisplayTracking, FontWeights.Medium, _ink);
        _unit = CopyMotion.Text("%", 18, 1, 0, FontWeights.Medium, _secondary);
        _digitsBox.Children.Clear();
        _digitsBox.Children.Add(_digits);
        _digitsBox.Height = 36;
        var number = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        number.Children.Add(_digitsBox);
        _unit.Margin = new Thickness(1, BaselineGap(family), 0, 0);
        _unit.VerticalAlignment = VerticalAlignment.Top;
        number.Children.Add(_unit);
        _status = new SwapText(12, 1.4, BodyTracking, FontWeights.Medium, _copy) { MinWidth = 100, Height = 16.8, Margin = new Thickness(0, 4, 0, 0) };
        var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        center.Children.Add(number);
        center.Children.Add(_status);
        _readout.Children.Clear();
        _readout.Children.Add(center);
        var visual = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        visual.Children.Add(_ring);
        visual.Children.Add(_readout);
        // The readout is a square laid over the ring, so it reaches past the ring's .9 height without pushing the caption down.
        _ring.SizeChanged += (_, e) => { _readout.Height = e.NewSize.Width; _readout.Margin = new Thickness(0, 0, 0, e.NewSize.Height - e.NewSize.Width); };
        _label = new SwapText(14, 1.4, BodyTracking, FontWeights.Medium, _ink);
        _detail = new SwapText(12, 1.4, BodyTracking, FontWeights.Normal, _muted) { Margin = new Thickness(0, 3, 0, 0) };
        _caption = new StackPanel { Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        _caption.Children.Add(_label);
        _caption.Children.Add(_detail);
        var figure = new StackPanel();
        figure.Children.Add(visual);
        figure.Children.Add(_caption);
        _host.Child = figure;
        _displayed = -1;
        _digits.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Sync();
    }

    // The unit shares the number's baseline, as CSS align-items: baseline does.
    private double BaselineGap(FontFamily family)
    {
        double Base(double size) { var text = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(family, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal), size, Brushes.Black, 1); return (size - text.Height) / 2 + text.Baseline; }
        return Base(36) - Base(18);
    }

    private void Inks()
    {
        _ink.Color = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _secondary.Color = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
    }

    private void OnValue()
    {
        int display = Display;
        _moved = display < _shownDisplay ? -1 : 1;
        _shownDisplay = display;
        Sync();
        Drive();
        ArcRangePeer.Notify(this, Value, Value);
    }
    private int _shownDisplay;

    private void Sync()
    {
        if (_label is null) { return; }
        _label.Set(Label ?? "");
        _detail!.Set(Detail ?? "", _moved);
        _detail.Visibility = string.IsNullOrEmpty(Detail) ? Visibility.Collapsed : Visibility.Visible;
        _status!.Visibility = Thresholds is null ? Visibility.Collapsed : Visibility.Visible;
        string sizer = Display.ToString(CultureInfo.InvariantCulture);
        if (sizer != _sizerText)
        {
            bool first = _sizerText.Length == 0;
            _sizerText = sizer;
            double width = TextWidth(sizer);
            if (first || Reduced || !IsLoaded) { _digitsWidth.Jump(width); } else { _digitsWidth.To(width, SpringTune.Smooth); _loop.Start(); }
        }
        Paint(IsLoaded);
    }

    private double TextWidth(string text)
    {
        var probe = CopyMotion.Text(text, 36, 1, DisplayTracking, FontWeights.Medium, Brushes.Black, FontFamily);
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Width;
    }

    // The ring fills once it is seen, then springs to each new value; the count follows without overshooting.
    private void Drive()
    {
        if (!IsLoaded) { return; }
        double share = Share;
        if (Reduced)
        {
            _sweep.Jump(share);
            _count.Jump(share * 100);
            _filled = true;
            _loop.Stop();
            Paint(false);
            return;
        }
        if (!_inView.Seen) { Paint(false); return; }
        _sweep.To(share, _filled ? SpringTune.Morph : Reveal);
        _count.To(share * 100, _filled ? SpringTune.Smooth : Reveal);
        _loop.Start();
    }

    private bool Frame(double seconds)
    {
        _sweep.Step(seconds);
        _count.Step(seconds);
        _digitsWidth.Step(seconds);
        // The state arrives as the first fill lands. After that it follows the number on screen, so the color and label change exactly as the count crosses a threshold.
        var next = BandAt(Minimum + _count.Position / 100 * (SafeMax - Minimum));
        bool landing = !_filled && _inView.Seen && (Math.Abs(_count.Position - Share * 100) < 1 || !_count.Active);
        if (_filled || landing) { _live = next; }
        if (landing) { _filled = true; Drive(); }
        Paint(true);
        return _sweep.Active || _count.Active || _digitsWidth.Active;
    }

    private void Paint(bool animate)
    {
        if (_digits is null) { return; }
        var tone = ActiveTone;
        if (_appliedTone != tone)
        {
            bool cross = animate && _appliedTone is not null && !Reduced;
            _appliedTone = tone;
            var line = Tween.Resolve(this, ToneBrushes.Line(tone));
            var soft = line;
            soft.A = (byte)Math.Round(255 * .13);
            double seconds = cross ? Tween.Standard : 0;
            Tween.Color(_line, line, seconds, cross);
            Tween.Color(_lineSoft, soft, seconds, cross);
            Tween.Color(_copy, Tween.Resolve(this, ToneBrushes.Copy(tone)), seconds, cross);
        }
        int shown = (int)Math.Round(Math.Clamp(_count.Position, 0, 100), MidpointRounding.AwayFromZero);
        if (shown != _displayed)
        {
            _displayed = shown;
            _digits.Text = shown.ToString(CultureInfo.InvariantCulture);
            _digits.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        // Digits are right aligned in a box sized to the target number, so the ones column never jumps while counting and the group recentres on a spring.
        _digitsBox.Width = _digitsWidth.Position;
        Canvas.SetLeft(_digits, _digitsWidth.Position - _digits.DesiredSize.Width);
        _status?.Set(_filled ? Shown?.Label ?? "" : "", _moved);
        _ring.InvalidateVisual();
    }

    private static Point At(double angle) => new(50 + Radius * Math.Sin(angle * Math.PI / 180), 50 - Radius * Math.Cos(angle * Math.PI / 180));

    // A 270 degree ring open at the bottom, drawn clockwise from its lower left end. Redrawn from the sweep every frame so the round cap stays exact.
    internal static StreamGeometry ArcFor(double sweep, double scale)
    {
        double end = Start + Span * Math.Clamp(sweep, 0, 1);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            Point Scaled(Point p) => new(p.X * scale, p.Y * scale);
            context.BeginFigure(Scaled(At(Start)), false, false);
            context.ArcTo(Scaled(At(end)), new Size(Radius * scale, Radius * scale), 0, end - Start > 180, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private void DrawRing(DrawingContext dc, double scale)
    {
        dc.DrawGeometry(null, new Pen(_lineSoft, StrokeWidth * scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, ArcFor(1, scale));
        double sweep = _sweep.Position;
        if (sweep <= 0) { return; }
        // The cap fades in over the first sliver instead of popping in as a dot.
        dc.PushOpacity(Math.Clamp(sweep / .02, 0, 1));
        dc.DrawGeometry(null, new Pen(_line, StrokeWidth * scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, ArcFor(sweep, scale));
        dc.Pop();
    }
}
