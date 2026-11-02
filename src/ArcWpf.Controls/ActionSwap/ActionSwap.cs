using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class ActionSwapState
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Path data on the lucide 24-unit grid, drawn at 16 DIP.</summary>
    public string? Icon { get; set; }
    public double IconStroke { get; set; } = 2;
    public ActionSwapTone Tone { get; set; } = ActionSwapTone.Neutral;
    /// <summary>Shown while the action runs on the way into this state. Without it the pill folds into a round spinner.</summary>
    public string? PendingLabel { get; set; }
    /// <summary>Milliseconds to hold this state before moving on by itself.</summary>
    public int HoldFor { get; set; }
}
public enum ActionSwapTone { Neutral, Accent, Success, Warning, Quiet, Danger }

/// <summary>
/// action-swap.tsx: a compact pill that morphs through related states. Width, padding, tone, icon and letters move together on one
/// spring, every change can be interrupted, and a failed action shows the failure label before returning to the current state.
/// </summary>
[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_Clip", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Row", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Icon", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Morph", Type = typeof(Canvas))]
public class ActionSwap : ButtonBase
{
    public static readonly DependencyProperty StatesProperty = DependencyProperty.Register(nameof(States), typeof(ObservableCollection<ActionSwapState>), typeof(ActionSwap), new FrameworkPropertyMetadata(null, OnStatesChanged));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(ActionSwap), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    public static readonly DependencyProperty FailureLabelProperty = DependencyProperty.Register(nameof(FailureLabel), typeof(string), typeof(ActionSwap), new FrameworkPropertyMetadata("Try again"));
    public static readonly DependencyProperty OnActionProperty = DependencyProperty.Register(nameof(OnAction), typeof(Func<ActionSwapState, Task>), typeof(ActionSwap), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty IsWorkingProperty = DependencyProperty.Register(nameof(IsWorking), typeof(bool), typeof(ActionSwap), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(ActionSwapTone), typeof(ActionSwap), new FrameworkPropertyMetadata(ActionSwapTone.Neutral));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(ControlSize), typeof(ActionSwap), new FrameworkPropertyMetadata(ControlSize.Medium, (d, _) => ((ActionSwap)d).Refresh(false)));
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(ActionSwap), new PropertyMetadata(null, (d, _) => ((ActionSwap)d).UpdateTone(false)));
    public ObservableCollection<ActionSwapState> States { get => (ObservableCollection<ActionSwapState>)GetValue(StatesProperty); set => SetValue(StatesProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string FailureLabel { get => (string)GetValue(FailureLabelProperty); set => SetValue(FailureLabelProperty, value); }
    /// <summary>Runs on press before the control moves on. A running task shows the working phase; a failure shows <see cref="FailureLabel"/>.</summary>
    public Func<ActionSwapState, Task>? OnAction { get => (Func<ActionSwapState, Task>?)GetValue(OnActionProperty); set => SetValue(OnActionProperty, value); }
    public bool IsWorking { get => (bool)GetValue(IsWorkingProperty); private set => SetValue(IsWorkingProperty, value); }
    public ActionSwapTone Tone { get => (ActionSwapTone)GetValue(ToneProperty); private set => SetValue(ToneProperty, value); }
    public ControlSize Size { get => (ControlSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public event EventHandler? ValueChanged;

    private const double FailureHold = 1800, MinWorking = 450, IconSize = 16, LabelGap = 8, BlurSoft = 4;
    private enum Phase { Idle, Working, Failed }
    private Phase _phase;
    private ActionSwapState? _target;
    private Border? _surface;
    private FrameworkElement? _clip, _row;
    private Grid? _icon;
    private Canvas? _morph;
    private GlyphRow? _glyphs;
    private FrameworkElement? _iconPhase;
    private string _iconKey = string.Empty;
    private int _run;
    private DispatcherTimer? _holdTimer, _failTimer;
    private readonly SolidColorBrush _fill = new(Colors.Transparent), _stroke = new(Colors.Transparent), _ink = new(Colors.Transparent);
    private readonly ScaleTransform _press = new(1, 1);
    private readonly SpringDriver _driver;
    private readonly ControlSpring _width, _iconWidth, _fold;

    static ActionSwap() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ActionSwap), new FrameworkPropertyMetadata(typeof(ActionSwap)));
    public ActionSwap()
    {
        _driver = new SpringDriver(ApplySprings);
        _width = _driver.Add(0, Tween.Morph);
        _iconWidth = _driver.Add(0, Tween.Smooth);
        _fold = _driver.Add(0, Tween.Morph);
        SetValue(StatesProperty, new ObservableCollection<ActionSwapState>());
        SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Foreground");
        Click += (_, _) => Advance();
        Loaded += (_, _) => { Refresh(false); ArmHold(); };
        Unloaded += (_, _) => { _run++; StopTimers(); _driver.Finish(); if (_phase != Phase.Idle) { _phase = Phase.Idle; IsWorking = false; } };
        MouseEnter += (_, _) => UpdateTone(true);
        MouseLeave += (_, _) => UpdateTone(true);
    }

    private ActionSwapState? Active => States.FirstOrDefault(s => s.Id == Value) ?? States.FirstOrDefault();
    private ActionSwapState? Next => States.Count == 0 ? null : States[(Math.Max(0, States.IndexOf(Active!)) + 1) % States.Count];
    protected override bool IsEnabledCore => base.IsEnabledCore && States is { Count: >= 2 };

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _surface = GetTemplateChild("PART_Surface") as Border;
        _clip = GetTemplateChild("PART_Clip") as FrameworkElement;
        _row = GetTemplateChild("PART_Row") as FrameworkElement;
        _icon = GetTemplateChild("PART_Icon") as Grid;
        _morph = GetTemplateChild("PART_Morph") as Canvas;
        _glyphs = _morph is null ? null : new GlyphRow(_morph);
        _iconPhase = null;
        _iconKey = string.Empty;
        if (_surface is not null)
        {
            _surface.Background = _fill;
            _surface.BorderBrush = _stroke;
            _surface.RenderTransform = _press;
            TextElement.SetForeground(_surface, _ink);
        }
        if (_clip is not null) _clip.SizeChanged += (_, _) => ClipPill();
        Refresh(false);
    }

    // The pill clips its content inside the border, so letters that run ahead of the width are revealed by the growing shape.
    private void ClipPill()
    {
        if (_clip is null) return;
        double radius = _clip.ActualHeight / 2;
        _clip.Clip = new RectangleGeometry(new Rect(_clip.RenderSize), radius, radius);
    }

    private static void OnStatesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ActionSwap)d;
        if (e.OldValue is ObservableCollection<ActionSwapState> oldStates) oldStates.CollectionChanged -= control.StatesCollectionChanged;
        if (e.NewValue is ObservableCollection<ActionSwapState> newStates) newStates.CollectionChanged += control.StatesCollectionChanged;
        control.StatesCollectionChanged(null, null);
    }
    private void StatesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs? e)
    {
        CoerceValue(IsEnabledProperty);
        Refresh(false);
    }
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ActionSwap)d;
        control.Refresh(control.IsLoaded);
        control.ArmHold();
    }

    public async void Advance()
    {
        if (!IsEnabled || _phase == Phase.Working || Active is null || Next is not { } next) return;
        int run = ++_run;
        StopTimers();
        Task? task;
        try { task = OnAction?.Invoke(next); }
        catch { Fail(); return; }
        if (task is null || task.IsCompletedSuccessfully) { Commit(next); return; }
        if (task.IsFaulted || task.IsCanceled) { Fail(); return; }
        _phase = Phase.Working;
        _target = next;
        IsWorking = true;
        Refresh(true);
        Announcer.Say(this, next.PendingLabel ?? "Working");
        long started = Stopwatch.GetTimestamp();
        bool ok = true;
        try { await task; }
        catch { ok = false; }
        // A fast response still shows the working shape long enough to read as a step, never as a flash.
        double left = MinWorking - Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (left > 0) await Task.Delay(TimeSpan.FromMilliseconds(left));
        if (_run != run) return;
        IsWorking = false;
        if (ok) Commit(next);
        else Fail();
    }

    private void Commit(ActionSwapState next)
    {
        _phase = Phase.Idle;
        _target = null;
        bool changed = Value != next.Id;
        SetCurrentValue(ValueProperty, next.Id);
        if (!changed) { Refresh(IsLoaded); ArmHold(); }
        Announcer.Say(this, next.Label);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Fail()
    {
        _phase = Phase.Failed;
        _target = null;
        Refresh(IsLoaded);
        Announcer.Say(this, FailureLabel);
        _failTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FailureHold) };
        _failTimer.Tick += (_, _) => { StopTimers(); _phase = Phase.Idle; Refresh(true); ArmHold(); };
        _failTimer.Start();
    }

    private void ArmHold()
    {
        _holdTimer?.Stop();
        _holdTimer = null;
        if (_phase != Phase.Idle || !IsLoaded || Active is not { HoldFor: > 0 } state) return;
        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(state.HoldFor) };
        _holdTimer.Tick += (_, _) => { _holdTimer?.Stop(); if (Next is { } next) Commit(next); };
        _holdTimer.Start();
    }

    private void StopTimers()
    {
        _holdTimer?.Stop();
        _failTimer?.Stop();
        _holdTimer = _failTimer = null;
    }

    private (string Key, string Label, string? Icon, double Stroke, ActionSwapTone Tone) View()
    {
        var active = Active;
        return _phase switch
        {
            Phase.Working => ("working", _target?.PendingLabel ?? string.Empty, "spinner", 0, active?.Tone ?? ActionSwapTone.Neutral),
            Phase.Failed => ("failed", FailureLabel, Lucide.CircleAlert, 1.75, ActionSwapTone.Danger),
            _ => ($"state:{active?.Id}", active?.Label ?? string.Empty, active?.Icon, active?.IconStroke ?? 2, active?.Tone ?? ActionSwapTone.Neutral)
        };
    }

    private void Refresh(bool animate)
    {
        var view = View();
        SetCurrentValue(ToneProperty, view.Tone);
        SetCurrentValue(ContentProperty, view.Label);
        AutomationProperties.SetName(this, view.Label.Length > 0 ? view.Label : Active?.Label ?? string.Empty);
        var level = ArcMotion.GetLevel(this);
        bool motion = animate && level != ArcMotionLevel.Off;
        bool spring = motion && level == ArcMotionLevel.Full;
        UpdateMetrics();
        UpdateTone(motion);
        SwapIcon(view.Key, view.Icon, view.Stroke, view.Tone, motion, spring);
        if (_glyphs is null) return;
        double width = _glyphs.Set(view.Label, view.Icon is not null && view.Label.Length > 0 ? LabelGap : 0, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, VisualTreeHelper.GetDpi(this).PixelsPerDip, motion, spring);
        double iconWidth = view.Icon is null ? 0 : IconSize, fold = _phase == Phase.Working && view.Label.Length == 0 ? 1 : 0;
        if (spring)
        {
            _driver.Animate(_width, width, Tween.Morph);
            _driver.Animate(_iconWidth, iconWidth, Tween.Smooth);
            _driver.Animate(_fold, fold, Tween.Morph);
        }
        else
        {
            _driver.Stop();
            _driver.Snap(_width, width);
            _driver.Snap(_iconWidth, iconWidth);
            _driver.Snap(_fold, fold);
        }
    }

    private (double Height, double Pad) Metrics => Size switch
    {
        ControlSize.Small => (36, 16),
        ControlSize.Large => (50, 24),
        _ => (44, 20)
    };

    private void UpdateMetrics()
    {
        double height = Metrics.Height;
        MinHeight = height;
        MinWidth = height;
        if (_surface is not null) _surface.CornerRadius = new CornerRadius(height / 2);
    }

    // One value drives the fold: the padding eases from the pill's to the circle's on the same spring as the label width.
    private void ApplySprings()
    {
        if (_morph is not null) _morph.Width = Math.Max(0, _width.Position);
        if (_icon is not null) _icon.Width = Math.Max(0, _iconWidth.Position);
        if (_row is null) return;
        var (height, pad) = Metrics;
        double inline = pad + ((height - 18) / 2 - pad) * _fold.Position;
        _row.Margin = new Thickness(inline, 0, inline, 0);
    }

    private void UpdateTone(bool animate)
    {
        var tone = Tone;
        bool hover = IsMouseOver && IsEnabled && (_phase != Phase.Working || tone is ActionSwapTone.Success or ActionSwapTone.Warning or ActionSwapTone.Danger);
        var (fill, stroke, ink) = tone switch
        {
            ActionSwapTone.Accent => (hover ? "AccentStrong" : "Accent", hover ? "AccentStrong" : "Accent", "Background"),
            ActionSwapTone.Quiet => (hover ? "SurfaceMuted" : "Surface", hover ? "BorderStrong" : "Border", "Foreground"),
            ActionSwapTone.Success => ("SwapSuccessFill", hover ? "SwapSuccessHoverBorder" : "SwapSuccessBorder", "Foreground"),
            ActionSwapTone.Warning => ("SwapWarningFill", hover ? "SwapWarningHoverBorder" : "SwapWarningBorder", "Foreground"),
            ActionSwapTone.Danger => ("SwapDangerFill", hover ? "SwapDangerHoverBorder" : "SwapDangerBorder", "Foreground"),
            _ => (hover ? "TextSecondary" : "Foreground", hover ? "TextSecondary" : "Foreground", "Background")
        };
        animate &= IsLoaded;
        Tween.Color(_fill, Tween.Resolve(this, $"ArcWpf.Brush.{fill}"), Tween.Standard, animate);
        Tween.Color(_stroke, Tween.Resolve(this, $"ArcWpf.Brush.{stroke}"), Tween.Standard, animate);
        Tween.Color(_ink, Tween.Resolve(this, $"ArcWpf.Brush.{ink}"), Tween.Standard, animate);
    }

    private void SwapIcon(string key, string? data, double stroke, ActionSwapTone tone, bool motion, bool spring)
    {
        if (_icon is null || key == _iconKey) return;
        _iconKey = key;
        var previous = _iconPhase;
        _iconPhase = null;
        if (previous is not null)
        {
            if (!motion) _icon.Children.Remove(previous);
            else if (!spring) Tween.To(previous, OpacityProperty, 0, Tween.Instant, null, done: () => _icon?.Children.Remove(previous));
            else
            {
                var scale = EnsureScale(previous);
                Tween.To(previous, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, done: () => _icon?.Children.Remove(previous));
                Tween.To(scale, ScaleTransform.ScaleXProperty, .5, Tween.Fast, ArcEasing.Standard);
                Tween.To(scale, ScaleTransform.ScaleYProperty, .5, Tween.Fast, ArcEasing.Standard);
                Soften(previous, 0, BlurSoft, Tween.Fast, ArcEasing.Standard);
            }
        }
        if (data is null) return;
        var phase = MakeIcon(data, stroke, tone);
        _iconPhase = phase;
        _icon.Children.Add(phase);
        if (!motion) return;
        phase.Opacity = 0;
        if (!spring) { Tween.To(phase, OpacityProperty, 1, Tween.Instant, null); return; }
        var enter = EnsureScale(phase);
        enter.ScaleX = enter.ScaleY = .5;
        Tween.To(phase, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
        Soften(phase, BlurSoft, 0, Tween.Standard, ArcEasing.Enter);
        // Success lands with a little more give while its stroke wipes in from the leading edge.
        bool success = tone == ActionSwapTone.Success;
        Tween.Spring(enter, ScaleTransform.ScaleXProperty, 1, success ? .45 : Tween.Snappy);
        Tween.Spring(enter, ScaleTransform.ScaleYProperty, 1, success ? .45 : Tween.Snappy);
        if (!success) return;
        var wipe = new RectangleGeometry(new Rect(0, 0, 0, IconSize));
        phase.Clip = wipe;
        var reveal = new RectAnimation(new Rect(0, 0, IconSize, IconSize), TimeSpan.FromSeconds(.36)) { EasingFunction = ArcEasing.Standard, BeginTime = TimeSpan.FromSeconds(.02) };
        reveal.Completed += (_, _) => { if (ReferenceEquals(phase.Clip, wipe)) phase.Clip = null; };
        wipe.BeginAnimation(RectangleGeometry.RectProperty, reveal);
    }

    private FrameworkElement MakeIcon(string data, double stroke, ActionSwapTone tone)
    {
        if (data == "spinner")
            return new Grid { Width = IconSize, Height = IconSize, RenderTransformOrigin = new Point(.5, .5), HorizontalAlignment = HorizontalAlignment.Center, Children = { RingSpinner.Create(_ink, ArcMotion.GetLevel(this) != ArcMotionLevel.Off) } };
        var host = new Grid { Width = IconSize, Height = IconSize, RenderTransformOrigin = new Point(.5, .5), HorizontalAlignment = HorizontalAlignment.Center, Children = { Lucide.Glyph(data, IconSize, stroke) } };
        string? ink = tone switch { ActionSwapTone.Success => "SwapSuccess", ActionSwapTone.Warning => "SwapWarning", ActionSwapTone.Danger => "SwapDanger", _ => null };
        if (ink is not null) host.SetResourceReference(TextElement.ForegroundProperty, $"ArcWpf.Brush.{ink}");
        return host;
    }

    private static ScaleTransform EnsureScale(FrameworkElement element)
    {
        if (element.RenderTransform is ScaleTransform scale) return scale;
        scale = new ScaleTransform(1, 1);
        element.RenderTransform = scale;
        return scale;
    }

    private static void Soften(FrameworkElement element, double from, double to, double seconds, IEasingFunction ease)
    {
        var effect = new BlurEffect { Radius = from };
        element.Effect = effect;
        var animation = new DoubleAnimation(to, TimeSpan.FromSeconds(seconds)) { EasingFunction = ease };
        if (to == 0) animation.Completed += (_, _) => { if (ReferenceEquals(element.Effect, effect)) element.Effect = null; };
        effect.BeginAnimation(BlurEffect.RadiusProperty, animation);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsPressedProperty && _phase != Phase.Working)
            Tween.Press(_press, IsPressed, ActualWidth > 220 ? .985 : .97, IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full, Tween.Instant);
        else if (e.Property == IsEnabledProperty) UpdateTone(false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ActionSwapPeer(this);
    private sealed class ActionSwapPeer(ActionSwap owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        protected override string GetClassNameCore() => nameof(ActionSwap);
        protected override string GetItemStatusCore() => owner.IsWorking ? "Busy" : string.Empty;
    }

    /// <summary>Morphs one label into the next: kept letters stay, new ones rise out of a soft blur. Letters are spaced by advance, like inline-block spans.</summary>
    private sealed class GlyphRow(Canvas host)
    {
        private sealed record Glyph(char Char, TextBlock Text, double X);
        private readonly List<Glyph> _glyphs = [];
        private string _text = string.Empty;
        private const double Stagger = .016, MaxStagger = .12, Lead = .1;

        public double Set(string text, double start, Typeface typeface, double fontSize, double pixelsPerDip, bool motion, bool spring)
        {
            string previous = _text;
            bool wasEmpty = previous.Length == 0;
            int head = 0, tail = 0;
            while (head < previous.Length && head < text.Length && previous[head] == text[head]) head++;
            while (tail < previous.Length - head && tail < text.Length - head && previous[^(tail + 1)] == text[^(tail + 1)]) tail++;
            // Shared runs under two characters read as coincidence, so the whole word is replaced.
            if (head < 2) head = 0;
            if (tail < 2) tail = 0;
            var kept = _glyphs.ToArray();
            var leaving = kept[head..(kept.Length - tail)];
            _glyphs.Clear();
            double x = start;
            for (int i = 0; i < text.Length; i++)
            {
                double advance = Advance(text[i], typeface, fontSize, pixelsPerDip);
                if (i < head) _glyphs.Add(Move(kept[i], x, spring));
                else if (i >= text.Length - tail) _glyphs.Add(Move(kept[kept.Length - (text.Length - i)], x, spring));
                else _glyphs.Add(Enter(text[i], x, i, wasEmpty ? Lead : 0, motion, spring));
                x += advance;
            }
            foreach (var glyph in leaving) Leave(glyph, motion, spring);
            _text = text;
            return text.Length == 0 ? 0 : x;
        }

        private static double Advance(char c, Typeface typeface, double fontSize, double pixelsPerDip)
            => new FormattedText(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, Brushes.Black, pixelsPerDip).WidthIncludingTrailingWhitespace;

        private static Glyph Move(Glyph glyph, double x, bool spring)
        {
            if (glyph.X == x) return glyph;
            Canvas.SetLeft(glyph.Text, x);
            if (spring && glyph.Text.RenderTransform is TranslateTransform move)
            {
                move.BeginAnimation(TranslateTransform.XProperty, null);
                move.X = glyph.X - x;
                Tween.Spring(move, TranslateTransform.XProperty, 0, Tween.Morph);
            }
            return glyph with { X = x };
        }

        private Glyph Enter(char c, double x, int order, double lead, bool motion, bool spring)
        {
            var text = new TextBlock { Text = c.ToString(), RenderTransform = new TranslateTransform(), TextWrapping = TextWrapping.NoWrap, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
            Canvas.SetLeft(text, x);
            Canvas.SetTop(text, 0);
            host.Children.Add(text);
            if (spring) Tween.Present(text, true, 4, BlurSoft, Tween.Standard, ArcEasing.Enter, lead + Math.Min(order * Stagger, MaxStagger));
            else if (motion) { text.Opacity = 0; Tween.To(text, OpacityProperty, 1, Tween.Instant, null); }
            return new Glyph(c, text, x);
        }

        private void Leave(Glyph glyph, bool motion, bool spring)
        {
            if (spring) Tween.Present(glyph.Text, false, -3, BlurSoft, Tween.Fast, ArcEasing.Standard, done: () => host.Children.Remove(glyph.Text));
            else if (motion) Tween.To(glyph.Text, OpacityProperty, 0, Tween.Instant, null, done: () => host.Children.Remove(glyph.Text));
            else host.Children.Remove(glyph.Text);
        }
    }
}

/// <summary>A quiet ring: a faint full track with one bright arc turning around it.</summary>
internal static class RingSpinner
{
    public static FrameworkElement Create(Brush ink, bool spin)
    {
        const double size = 14, thickness = 1.5, corner = 2.32, radius = (size - thickness) / 2;
        var track = new Ellipse { Stroke = ink, StrokeThickness = thickness, Opacity = .24 };
        var arc = new Path { Stroke = ink, StrokeThickness = thickness, Data = Geometry.Parse(FormattableString.Invariant($"M{corner},{corner} A{radius},{radius} 0 0 1 {size - corner},{corner}")) };
        var turn = new RotateTransform();
        if (spin) turn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(.75)) { RepeatBehavior = RepeatBehavior.Forever });
        return new Grid { Width = size, Height = size, RenderTransformOrigin = new Point(.5, .5), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransform = turn, Children = { track, arc } };
    }
}
