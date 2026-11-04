using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

// text-shimmer.tsx (frozen 2026-11-03): a calm light sweep across a short status line. When IsActive turns false the band glides off and the text settles to solid; a new label rises in place while the line springs to its width.
public class TextShimmer : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextShimmer), new FrameworkPropertyMetadata("", (d, _) => ((TextShimmer)d).Swap()));
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(TextShimmer), new FrameworkPropertyMetadata(true, (d, _) => ((TextShimmer)d).OnActive()));
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(double), typeof(TextShimmer), new FrameworkPropertyMetadata(1.8, (d, _) => ((TextShimmer)d).Restart()));
    public static readonly DependencyProperty LineRatioProperty = DependencyProperty.Register(nameof(LineRatio), typeof(double), typeof(TextShimmer), new FrameworkPropertyMetadata(double.NaN, (d, _) => ((TextShimmer)d).Rebuild()));
    public static readonly DependencyProperty BaseColorProperty = DependencyProperty.Register(nameof(BaseColor), typeof(Color?), typeof(TextShimmer), new FrameworkPropertyMetadata(null, (d, _) => ((TextShimmer)d).Paint()));
    private static readonly DependencyProperty SweepProperty = DependencyProperty.Register("Sweep", typeof(double), typeof(TextShimmer), new FrameworkPropertyMetadata(0d, (d, _) => ((TextShimmer)d).Paint()));
    private static readonly DependencyProperty SettleProperty = DependencyProperty.Register("Settle", typeof(double), typeof(TextShimmer), new FrameworkPropertyMetadata(0d, (d, _) => ((TextShimmer)d).Paint()));

    /// <summary>The status text; keep it to one short line.</summary>
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    /// <summary>Sweep while work is ongoing.</summary>
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    /// <summary>Seconds for one sweep across the text.</summary>
    public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    /// <summary>Line box height as a multiple of the font size; NaN uses the font's own height.</summary>
    public double LineRatio { get => (double)GetValue(LineRatioProperty); set => SetValue(LineRatioProperty, value); }
    /// <summary>The resting color under the band; by default 12% of the text color over the muted text color.</summary>
    public Color? BaseColor { get => (Color?)GetValue(BaseColorProperty); set => SetValue(BaseColorProperty, value); }

    private const double Band = 2.2, FirstDelay = .12, RestShare = .32;
    private static readonly (double Offset, double Strength)[] Falloff = [(-1, 0), (-.5, .5), (-.2, .9), (0, 1), (.2, .9), (.5, .5), (1, 0)];

    private sealed record Label(TextBlock View, LinearGradientBrush Brush, double Width);

    private readonly SpringDriver _driver;
    private readonly ControlSpring _width;
    private Decorator? _host;
    private Grid? _stage;
    private Label? _label, _leaving;
    private int _pass;

    static TextShimmer() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TextShimmer), new FrameworkPropertyMetadata(typeof(TextShimmer)));

    public TextShimmer()
    {
        _driver = new SpringDriver(ApplyWidth);
        _width = _driver.Add(0, ArcSpring.Morph);
        Loaded += (_, _) => { SetValue(SettleProperty, IsActive ? 0d : 1d); Restart(); };
        Unloaded += (_, _) => { Halt(); _driver.Finish(); };
        IsVisibleChanged += (_, _) => Restart();
        ThemeWatch.Add(this, Paint);
    }

    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private bool Running => IsActive && Moving && IsVisible;

    internal double SweepForTest => (double)GetValue(SweepProperty);
    internal double SettleForTest => (double)GetValue(SettleProperty);
    internal double WidthForTest => _width.Position;
    internal double WidthTargetForTest => _width.Target;
    internal string? LeavingForTest => _leaving?.View.Text;
    internal Color BandColorAtForTest(double x) => Sample(_label!.Brush, x);
    internal Color BaseForTest => Resolve(SettleForTest).Base;
    internal void SetSweepForTest(double sweep) { Halt(); Tween.Set(this, SweepProperty, sweep); }
    internal void FinishMotionForTest()
    {
        Halt();
        Tween.Set(this, SettleProperty, IsActive ? 0 : 1);
        if (_leaving is not null) { _stage?.Children.Remove(_leaving.View); _leaving = null; }
        _driver.Finish();
        if (_label is not null) { Tween.Set(_label.View, OpacityProperty, 1); if (_label.View.RenderTransform is TranslateTransform move) { Tween.Set(move, TranslateTransform.YProperty, 0); } _label.View.Effect = null; }
    }

    private static Color Sample(LinearGradientBrush brush, double x)
    {
        double at = (x - brush.StartPoint.X) / (brush.EndPoint.X - brush.StartPoint.X);
        var stops = brush.GradientStops;
        for (int i = 1; i < stops.Count; i++)
        {
            if (at > stops[i].Offset) { continue; }
            double span = stops[i].Offset - stops[i - 1].Offset, t = span <= 0 ? 1 : (at - stops[i - 1].Offset) / span;
            var (a, b) = (stops[i - 1].Color, stops[i].Color);
            return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }
        return stops[^1].Color;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ArcTextPeer(this, () => Text);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Rebuild();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty || e.Property == FontFamilyProperty || e.Property == FontWeightProperty) { Rebuild(); }
        else if (e.Property == ForegroundProperty) { Paint(); }
    }

    private void Rebuild()
    {
        if (_host is null) { return; }
        _driver.Stop();
        _stage = new Grid { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Height = double.IsNaN(LineRatio) ? double.NaN : LineRatio * FontSize };
        _host.Child = _stage;
        _leaving = null;
        _label = Make(Text ?? "");
        _stage.Children.Add(_label.View);
        _width.Snap(_label.Width);
        ApplyWidth();
        Status();
        SetValue(SettleProperty, IsActive ? 0d : 1d);
        Paint();
    }

    private Label Make(string text)
    {
        var brush = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute };
        for (int i = 0; i < Falloff.Length + 2; i++) { brush.GradientStops.Add(new GradientStop()); }
        var view = new TextBlock { Text = text, Foreground = brush, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = new TranslateTransform(), IsHitTestVisible = false };
        view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        // A TextBlock outside the tree measures with defaults, so the width comes from this control's own font.
        double width = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace;
        return new Label(view, brush, width);
    }

    private void Swap()
    {
        if (_stage is null || _label is null) { return; }
        var old = _label;
        if (old.View.Text == (Text ?? "")) { return; }
        if (_leaving is not null) { _stage.Children.Remove(_leaving.View); _leaving = null; }
        _label = Make(Text ?? "");
        _stage.Children.Add(_label.View);
        Status();
        if (!Moving)
        {
            _stage.Children.Remove(old.View);
            _driver.Snap(_width, _label.Width);
            Paint();
            return;
        }
        double em = FontSize;
        _leaving = old;
        Tween.To(old.View, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, 0, () => { if (ReferenceEquals(_leaving, old)) { _stage?.Children.Remove(old.View); _leaving = null; } });
        Tween.To((TranslateTransform)old.View.RenderTransform, TranslateTransform.YProperty, -.24 * em, Tween.Fast, ArcEasing.Standard);
        TextMotion.Blur(old.View, 0, 2, Tween.Fast, ArcEasing.Standard);
        var view = _label.View;
        view.Opacity = 0;
        var move = (TranslateTransform)view.RenderTransform;
        move.Y = .3 * em;
        Tween.To(view, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
        Tween.To(move, TranslateTransform.YProperty, 0, Tween.Standard, ArcEasing.Enter);
        TextMotion.Blur(view, 4, 0, Tween.Standard, ArcEasing.Enter);
        if (Math.Abs(_label.Width - _width.Target) >= .5) { _driver.Animate(_width, _label.Width, ArcSpring.Morph); } else { _width.Snap(_label.Width); ApplyWidth(); }
        Paint();
    }

    private void ApplyWidth() { if (_stage is not null) { _stage.Width = _width.Position; } }

    private void Status() => AutomationProperties.SetItemStatus(this, IsActive ? "Busy" : "");

    private void Halt()
    {
        _pass++;
        double sweep = SweepForTest;
        BeginAnimation(SweepProperty, null);
        SetValue(SweepProperty, sweep);
    }

    // Loops only while work is ongoing, visible and motion is allowed; a paused band resumes where it stopped.
    private void Restart()
    {
        if (!IsLoaded) { return; }
        if (!Running)
        {
            if (Tween.Level(this) != ArcMotionLevel.Full) { Tween.Set(this, SweepProperty, 0); }
            else if (IsActive) { Halt(); }
            return;
        }
        Pass(SweepForTest > 0 ? 0 : FirstDelay);
    }

    private void Pass(double delay)
    {
        int pass = ++_pass;
        if (SweepForTest >= 1) { Tween.Set(this, SweepProperty, 0); }
        Tween.To(this, SweepProperty, 1, Duration * (1 - SweepForTest), null, delay, () => { if (pass == _pass) { Pass(Duration * RestShare); } });
    }

    private void OnActive()
    {
        Status();
        if (!IsLoaded) { SetValue(SettleProperty, IsActive ? 0d : 1d); return; }
        bool full = Tween.Level(this) == ArcMotionLevel.Full;
        if (IsActive)
        {
            Tween.To(this, SettleProperty, 0, full ? Tween.Standard : 0, ArcEasing.Standard);
            Restart();
            return;
        }
        // Finishing lets a band in flight glide off while the base resolves to solid, so the text never hard cuts.
        double t = SweepForTest;
        _pass++;
        if (full && t > 0 && t < 1) { Tween.To(this, SweepProperty, 1, Math.Min(.42, Duration * (1 - t) * .5), ArcEasing.Enter); }
        else { Halt(); }
        Tween.To(this, SettleProperty, 1, full ? .36 : Tween.Instant, ArcEasing.Standard);
    }

    private (Color Highlight, Color Base) Resolve(double settle)
    {
        var highlight = (Foreground as SolidColorBrush)?.Color ?? Colors.Black;
        var rest = BaseColor ?? OkMix.Mix(highlight, .12, Tween.Resolve(this, "ArcWpf.Brush.TextMuted"));
        return (highlight, OkMix.Mix(highlight, settle, rest));
    }

    private void Paint()
    {
        if (_label is null) { return; }
        double sweep = SweepForTest, settle = SettleForTest;
        var (highlight, rest) = Resolve(settle);
        PaintLabel(_label, sweep, highlight, rest);
        if (_leaving is not null) { PaintLabel(_leaving, sweep, highlight, rest); }
    }

    private void PaintLabel(Label label, double t, Color highlight, Color rest)
    {
        double em = FontSize, reach = Band * 2 * em;
        // The gradient spans past the text by the band's full travel, so every stop keeps an offset between 0 and 1.
        double start = -reach, end = label.Width + reach, span = end - start;
        var brush = label.Brush;
        brush.StartPoint = new Point(start, 0);
        brush.EndPoint = new Point(end, 0);
        var stops = brush.GradientStops;
        stops[0].Offset = 0;
        stops[0].Color = rest;
        for (int i = 0; i < Falloff.Length; i++)
        {
            var (offset, strength) = Falloff[i];
            double x = t * label.Width + (2 * t - 1 + offset) * Band * em;
            stops[i + 1].Offset = Math.Clamp((x - start) / span, 0, 1);
            stops[i + 1].Color = OkMix.Mix(highlight, strength, rest);
        }
        stops[^1].Offset = 1;
        stops[^1].Color = rest;
    }
}
