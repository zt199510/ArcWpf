using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

// rating.tsx (frozen 2026-10-31): a star radio group. Each star is an outline, a faint mark of the committed value and a live fill that
// sweeps left to right; hovering previews, a commit pops the stars in a wave, and the value word rolls in the direction it moved.
[TemplatePart(Name = "PART_Stars", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Value", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Description", Type = typeof(Grid))]
public class Rating : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(Rating), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Rating)d).Refresh(true), Normalize));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(Rating), new FrameworkPropertyMetadata(5, OnStructureChanged, (_, v) => Math.Clamp((int)v, 1, 20)));
    public static readonly DependencyProperty AllowHalfProperty = DependencyProperty.Register(nameof(AllowHalf), typeof(bool), typeof(Rating), new FrameworkPropertyMetadata(false, OnStructureChanged));
    public static readonly DependencyProperty AllowClearProperty = DependencyProperty.Register(nameof(AllowClear), typeof(bool), typeof(Rating), new PropertyMetadata(true));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(Rating), new FrameworkPropertyMetadata(false, (d, _) => ((Rating)d).Refresh(false)));
    public static readonly DependencyProperty ShowValueProperty = DependencyProperty.Register(nameof(ShowValue), typeof(bool), typeof(Rating), new FrameworkPropertyMetadata(true, OnStructureChanged));
    public static readonly DependencyProperty ValueLabelsProperty = DependencyProperty.Register(nameof(ValueLabels), typeof(IList<string>), typeof(Rating), new FrameworkPropertyMetadata(null, OnStructureChanged));
    public static readonly DependencyProperty EmptyLabelProperty = DependencyProperty.Register(nameof(EmptyLabel), typeof(string), typeof(Rating), new FrameworkPropertyMetadata("Not rated", OnStructureChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Rating), new PropertyMetadata(""));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(Rating), new FrameworkPropertyMetadata("", (d, _) => ((Rating)d).Refresh(true)));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(ControlSize), typeof(Rating), new FrameworkPropertyMetadata(ControlSize.Medium, OnStructureChanged));
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<double>), typeof(Rating));
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(Rating), new PropertyMetadata(null, (d, _) => ((Rating)d).Refresh(false)));

    /// <summary>Committed value; 0 means not rated. Snaps to whole (or half) stars.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>Pointer and arrow keys move in half stars.</summary>
    public bool AllowHalf { get => (bool)GetValue(AllowHalfProperty); set => SetValue(AllowHalfProperty, value); }
    /// <summary>Choosing the current value again clears it; Backspace and Delete clear from the keyboard.</summary>
    public bool AllowClear { get => (bool)GetValue(AllowClearProperty); set => SetValue(AllowClearProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    /// <summary>Show the word or number for the previewed value beside the stars.</summary>
    public bool ShowValue { get => (bool)GetValue(ShowValueProperty); set => SetValue(ShowValueProperty, value); }
    /// <summary>One word per whole star. Five whole stars default to Poor through Excellent; anything else reads "4 out of 5".</summary>
    public IList<string>? ValueLabels { get => (IList<string>?)GetValue(ValueLabelsProperty); set => SetValue(ValueLabelsProperty, value); }
    public string EmptyLabel { get => (string)GetValue(EmptyLabelProperty); set => SetValue(EmptyLabelProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public ControlSize Size { get => (ControlSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public event RoutedPropertyChangedEventHandler<double> ValueChanged { add => AddHandler(ValueChangedEvent, value); remove => RemoveHandler(ValueChangedEvent, value); }

    private static readonly string[] DefaultWords = ["Poor", "Fair", "Good", "Great", "Excellent"];
    private const double ItemStagger = .035, WaveGap = .26, PopVelocity = 9;
    private Panel? _stars;
    private Grid? _valueHost, _descriptionHost;
    private RollingWords? _value, _description;
    private readonly List<StarGlyph> _glyphs = [];
    private readonly SolidColorBrush _valueInk = new(Colors.Gray);
    private double? _preview, _heldAt;
    private int? _pressed;
    private (double From, double To) _sweep;
    private (double Start, double Last, bool Scrub)? _gesture;
    private double _lastCommit = double.NegativeInfinity;

    static Rating()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Rating), new FrameworkPropertyMetadata(typeof(Rating)));
        FocusableProperty.OverrideMetadata(typeof(Rating), new FrameworkPropertyMetadata(true));
        IsEnabledProperty.OverrideMetadata(typeof(Rating), new UIPropertyMetadata((d, _) => ((Rating)d).Refresh(false)));
    }

    public Rating() { SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border"); }

    private bool Interactive => IsEnabled && !IsReadOnly;
    private double Step => AllowHalf ? .5 : 1;
    private bool Full => Tween.Level(this) == ArcMotionLevel.Full && IsLoaded;
    private double Shown => _preview ?? Value;

    private static object Normalize(DependencyObject d, object value)
    {
        var rating = (Rating)d;
        double step = rating.AllowHalf ? .5 : 1, raw = (double)value;
        return double.IsFinite(raw) ? Math.Clamp(Math.Round(raw / step) * step, 0, rating.Maximum) : 0d;
    }

    private static void OnStructureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var rating = (Rating)d;
        rating.CoerceValue(ValueProperty);
        rating.Build();
    }

    public override void OnApplyTemplate()
    {
        if (_stars is not null)
        {
            _stars.MouseMove -= StarsMove; _stars.MouseLeftButtonDown -= StarsDown; _stars.MouseLeftButtonUp -= StarsUp; _stars.MouseLeave -= StarsLeave;
        }
        base.OnApplyTemplate();
        _stars = GetTemplateChild("PART_Stars") as Panel;
        _valueHost = GetTemplateChild("PART_Value") as Grid;
        _descriptionHost = GetTemplateChild("PART_Description") as Grid;
        if (_stars is not null)
        {
            _stars.Background = Brushes.Transparent;
            _stars.MouseMove += StarsMove; _stars.MouseLeftButtonDown += StarsDown; _stars.MouseLeftButtonUp += StarsUp; _stars.MouseLeave += StarsLeave;
        }
        Build();
    }

    // Cells are wider than their stars; the negative inline margin lines the first star up with the label.
    private (double Cell, double Glyph) Metrics => Size switch { ControlSize.Small => (28, 18), ControlSize.Large => (44, 30), _ => (36, 24) };

    private void Build()
    {
        if (_stars is null) return;
        var (cell, glyph) = Metrics;
        _stars.Children.Clear();
        _glyphs.Clear();
        _stars.Margin = new Thickness((glyph - cell) / 2, 0, (glyph - cell) / 2, 0);
        double shown = Shown;
        for (int index = 0; index < Maximum; index++)
        {
            var star = new StarGlyph(glyph, Math.Clamp(shown - index, 0, 1), Math.Clamp(Value - index, 0, 1)) { Width = cell, Height = cell, Margin = new Thickness(index == 0 ? 0 : 2, 0, 0, 0) };
            _glyphs.Add(star);
            _stars.Children.Add(star);
        }
        _sweep = (shown, shown);
        if (_valueHost is not null)
        {
            _valueHost.Children.Clear();
            _value = null;
            _valueHost.Visibility = ShowValue ? Visibility.Visible : Visibility.Collapsed;
            if (ShowValue)
            {
                // Hidden sizers hold the widest word, so the row never shifts as the word rolls.
                foreach (double next in Steps().Prepend(0)) _valueHost.Children.Add(new TextBlock { Text = TextFor(next), Visibility = Visibility.Hidden });
                _value = new RollingWords(false);
                _valueHost.Children.Add(_value);
                _valueHost.SetValue(TextElement.ForegroundProperty, _valueInk);
            }
        }
        if (_descriptionHost is not null && _description is null) { _description = new RollingWords(true); _descriptionHost.Children.Add(_description); }
        Refresh(false);
    }

    private IEnumerable<double> Steps() { for (int i = 1; i <= Math.Round(Maximum / Step); i++) yield return i * Step; }
    private IList<string>? Words => ValueLabels ?? (Maximum == 5 && !AllowHalf ? DefaultWords : null);
    private string? WordFor(double value) => value % 1 == 0 && Words is { } words && value >= 1 && value <= words.Count ? words[(int)value - 1] : null;
    internal string TextFor(double value) => value <= 0 ? EmptyLabel : WordFor(value) ?? $"{value:0.#} out of {Maximum}";
    internal string SpokenFor(double value) => string.Join(", ", new[] { WordFor(value), $"{value:0.#} of {Maximum} stars" }.Where(s => s is not null));

    private void Refresh(bool animate)
    {
        if (_stars is null || _glyphs.Count != Maximum) return;
        animate &= IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;
        bool full = animate && Full;
        double shown = Shown;
        if (_sweep.To != shown) _sweep = (_sweep.To, shown);
        int direction = _sweep.To >= _sweep.From ? 1 : -1;
        bool rated = shown > 0;
        for (int index = 0; index < _glyphs.Count; index++)
        {
            double fill = Math.Clamp(shown - index, 0, 1);
            int order = _sweep.To > _sweep.From ? index - (int)Math.Floor(_sweep.From) : (int)Math.Ceiling(_sweep.From) - 1 - index;
            var star = _glyphs[index];
            star.SetFill(fill, Math.Max(0, order) * ItemStagger, full);
            star.SetKept(Math.Clamp(Value - index, 0, 1), full);
            star.SetBase(rated, this, full);
            star.SetLift(_pressed == index && Tween.Level(this) == ArcMotionLevel.Full ? .86 : rated && fill == 0 ? .9 : 1, full);
        }
        _stars.Opacity = IsEnabled ? 1 : .5;
        if (_valueHost is not null) _valueHost.Opacity = IsEnabled ? 1 : .5;
        Tween.Color(_valueInk, Tween.Resolve(this, shown == 0 ? "ArcWpf.Brush.TextMuted" : "ArcWpf.Brush.TextSecondary"), Tween.Fast, full);
        _value?.SetText(TextFor(shown), direction, full);
        _description?.SetText(Description ?? "", direction, full);
        if (_descriptionHost is not null) _descriptionHost.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
        Cursor = Interactive ? Cursors.Hand : null;
    }

    /// <summary>Commits a value as a pointer or key would: toggling the current value clears it when clearing is allowed.</summary>
    internal void Commit(double next, bool toggle = false)
    {
        if (!Interactive) return;
        double chosen = Math.Clamp(Math.Round(next / Step) * Step, 0, Maximum);
        if (toggle && AllowClear && chosen == Value) chosen = 0;
        if (!AllowClear) chosen = Math.Max(Step, chosen);
        if (_preview is not null) _heldAt = _preview;
        _preview = null;
        double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        bool wave = now - _lastCommit > WaveGap;
        _lastCommit = now;
        double old = Value;
        if (chosen != old) SetCurrentValue(ValueProperty, chosen);
        else Refresh(true);
        if (chosen > 0 && Full)
        {
            int last = (int)Math.Ceiling(chosen) - 1;
            for (int index = 0; index <= last && index < _glyphs.Count; index++)
                if (wave || index == last) _glyphs[index].Pop(PopVelocity, wave ? index * ItemStagger : 0);
        }
        if (chosen != old) RaiseEvent(new RoutedPropertyChangedEventArgs<double>(old, chosen, ValueChangedEvent));
    }

    /// <summary>Maps a pointer position on the stars to a value; dragging past the first star clears when that is allowed.</summary>
    private double ValueAt(double x)
    {
        for (int index = 0; index < _glyphs.Count; index++)
        {
            var star = _glyphs[index];
            double left = star.TranslatePoint(new Point(), _stars).X, width = star.ActualWidth;
            if (index == 0 && x < left - width / 2) return AllowClear ? 0 : Step;
            if (x <= left + width + 1 || index == _glyphs.Count - 1) return AllowHalf && x < left + width / 2 ? index + .5 : index + 1;
        }
        return Value;
    }
    private static int? StarOf(double value) => value > 0 ? (int)Math.Ceiling(value) - 1 : null;

    private void StarsDown(object sender, MouseButtonEventArgs e)
    {
        if (!Interactive) return;
        Focus();
        double at = ValueAt(e.GetPosition(_stars).X);
        _gesture = (at, at, false);
        _pressed = StarOf(at);
        Refresh(true);
        e.Handled = true;
    }

    private void StarsMove(object sender, MouseEventArgs e)
    {
        if (!Interactive) return;
        double at = ValueAt(e.GetPosition(_stars).X);
        if (_gesture is { } active)
        {
            if (at != active.Last) _gesture = (active.Start, at, true);
            _pressed = StarOf(at);
            if (_gesture.Value.Scrub) _preview = at;
            Refresh(true);
            return;
        }
        // After a pointer commit the hovered preview steps aside until the pointer reaches a different value.
        if (_heldAt == at) return;
        _heldAt = null;
        if (_preview == at) return;
        _preview = at;
        Refresh(true);
    }

    private void StarsUp(object sender, MouseButtonEventArgs e)
    {
        var active = _gesture;
        _gesture = null;
        _pressed = null;
        if (active is null) { Refresh(true); return; }
        double at = ValueAt(e.GetPosition(_stars).X);
        Commit(at, toggle: !active.Value.Scrub);
        _heldAt = at;
        e.Handled = true;
    }

    private void StarsLeave(object sender, MouseEventArgs e)
    {
        _gesture = null;
        _heldAt = null;
        _pressed = null;
        _preview = null;
        Refresh(true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Interactive || e.Handled) return;
        double? next = e.Key switch
        {
            Key.Right or Key.Up => Value + Step,
            Key.Left or Key.Down => Value - Step,
            Key.Home => Step,
            Key.End => Maximum,
            Key.Back or Key.Delete when AllowClear => 0,
            >= Key.D1 and <= Key.D9 when e.Key - Key.D0 <= Maximum => e.Key - Key.D0,
            >= Key.NumPad1 and <= Key.NumPad9 when e.Key - Key.NumPad0 <= Maximum => e.Key - Key.NumPad0,
            _ => null,
        };
        if (next is not double value) return;
        e.Handled = true;
        Commit(value == 0 ? 0 : Math.Clamp(value, Step, Maximum));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RatingPeer(this);

    private sealed class RatingPeer(Rating owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        protected override string GetClassNameCore() => nameof(Rating);
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.IsReadOnly ? AutomationControlType.Image : AutomationControlType.Slider;
        protected override string GetNameCore() => owner.IsReadOnly ? $"{owner.Label}: {(owner.Value > 0 ? $"{owner.Value:0.#} out of {owner.Maximum}" : owner.EmptyLabel)}" : owner.Label;
        protected override string GetHelpTextCore() => owner.Description ?? "";
        public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.RangeValue && !owner.IsReadOnly ? this : base.GetPattern(pattern);
        public double Value => owner.Value;
        public bool IsReadOnly => !owner.Interactive;
        public double Maximum => owner.Maximum;
        public double Minimum => 0;
        public double LargeChange => 1;
        public double SmallChange => owner.Step;
        public void SetValue(double value) { if (IsReadOnly) throw new ElementNotEnabledException(); owner.Commit(value); }
    }
}

/// <summary>One star in three layers: the empty outline, a faint mark of the committed value, and the live fill. The lift answers
/// press and softening; the pop inside carries the commit impulse, so the two transforms never fight.</summary>
internal sealed class StarGlyph : Grid
{
    internal static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(double), typeof(StarGlyph), new PropertyMetadata(0d, (d, e) => ((StarGlyph)d).Clip(((StarGlyph)d)._fillClip, (double)e.NewValue)));
    internal static readonly DependencyProperty KeptProperty = DependencyProperty.Register(nameof(Kept), typeof(double), typeof(StarGlyph), new PropertyMetadata(0d, (d, e) => ((StarGlyph)d).Clip(((StarGlyph)d)._keptClip, (double)e.NewValue)));
    internal double Fill => (double)GetValue(FillProperty);
    internal double Kept => (double)GetValue(KeptProperty);

    private readonly double _glyph;
    private readonly RectangleGeometry _fillClip = new(), _keptClip = new();
    private readonly SolidColorBrush _baseStroke = new(Colors.Gray), _baseFill = new(Colors.Transparent);
    private readonly ScaleTransform _lift = new(1, 1), _pop = new(1, 1);
    private double _fillTarget, _keptTarget, _liftTarget = 1;

    public StarGlyph(double glyph, double fill, double kept)
    {
        _glyph = glyph;
        var lift = new Grid { Width = glyph, Height = glyph, RenderTransform = _lift, RenderTransformOrigin = new Point(.5, .5), IsHitTestVisible = false };
        var pop = new Grid { RenderTransform = _pop, RenderTransformOrigin = new Point(.5, .5) };
        pop.Children.Add(Layer(_baseFill, _baseStroke, null));
        var keptLayer = Layer(null, null, _keptClip); keptLayer.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.RatingKept");
        var fillLayer = Layer(null, null, _fillClip); fillLayer.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.Warning");
        pop.Children.Add(keptLayer);
        pop.Children.Add(fillLayer);
        lift.Children.Add(pop);
        Children.Add(lift);
        Background = Brushes.Transparent;
        _fillTarget = fill; _keptTarget = kept;
        SetValue(FillProperty, fill);
        SetValue(KeptProperty, kept);
        Clip(_fillClip, fill);
        Clip(_keptClip, kept);
    }

    private FrameworkElement Layer(Brush? fill, Brush? stroke, Geometry? clip)
    {
        var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(Lucide.Star), Width = 24, Height = 24, StrokeThickness = 1.75, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        if (stroke is not null) { path.Stroke = stroke; path.Fill = fill; }
        else
        {
            var ink = new System.Windows.Data.Binding { Path = new PropertyPath(TextElement.ForegroundProperty), RelativeSource = System.Windows.Data.RelativeSource.Self };
            path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, ink);
            path.SetBinding(System.Windows.Shapes.Shape.FillProperty, ink);
        }
        return new Viewbox { Width = _glyph, Height = _glyph, Child = path, Clip = clip };
    }

    private void Clip(RectangleGeometry clip, double fraction) => clip.Rect = new Rect(0, -2, _glyph * Math.Clamp(fraction, 0, 1), _glyph + 4);

    public void SetFill(double fill, double delay, bool animate)
    {
        if (_fillTarget == fill && (animate || !HasAnimatedProperties)) return;
        _fillTarget = fill;
        if (animate) Tween.Spring(this, FillProperty, fill, Tween.Snappy, delay);
        else Tween.Set(this, FillProperty, fill);
    }

    public void SetKept(double kept, bool animate)
    {
        if (_keptTarget == kept) return;
        _keptTarget = kept;
        if (animate) Tween.Spring(this, KeptProperty, kept, Tween.Smooth);
        else Tween.Set(this, KeptProperty, kept);
    }

    // Once a value shows, empty stars soften from an outline into a quiet solid (fill and stroke --border, .24 standard).
    public void SetBase(bool rated, FrameworkElement owner, bool animate)
    {
        Tween.Color(_baseStroke, Tween.Resolve(owner, rated ? "ArcWpf.Brush.Border" : "ArcWpf.Brush.BorderStrong"), Tween.Standard, animate);
        var fill = rated ? Tween.Resolve(owner, "ArcWpf.Brush.Border") : Colors.Transparent;
        if (!rated) { fill = _baseFill.Color; fill.A = 0; }
        Tween.Color(_baseFill, fill, Tween.Standard, animate);
    }

    public void SetLift(double scale, bool animate)
    {
        if (_liftTarget == scale) return;
        _liftTarget = scale;
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            if (animate) Tween.Spring(_lift, property, scale, Tween.Snappy);
            else Tween.Set(_lift, property, scale);
        }
    }

    /// <summary>spring.snappy in physical terms released from rest with a scale velocity: it peaks near 1.16, then settles back to 1.</summary>
    public void Pop(double velocity, double delay)
    {
        double omega = 2 * Math.PI / (Tween.Snappy * 1.2);
        var frames = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromSeconds(delay) };
        for (int i = 0; i <= 30; i++)
        {
            double t = i * .4 / 30;
            frames.KeyFrames.Add(new LinearDoubleKeyFrame(1 + velocity * t * Math.Exp(-omega * t), KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t))));
        }
        frames.Freeze();
        _pop.BeginAnimation(ScaleTransform.ScaleXProperty, frames);
        _pop.BeginAnimation(ScaleTransform.ScaleYProperty, frames);
    }
}

/// <summary>Changed words roll in the direction the value moved (.3em, soft blur, .24 enter) and leave the other way (.25em, subtle blur,
/// .12 standard) out of flow; unchanged words stay put. Reduced and Off swap words at once.</summary>
internal sealed class RollingWords : Grid
{
    private readonly WrapPanel _live = new();
    private readonly Canvas _exits = new() { IsHitTestVisible = false };
    private readonly Dictionary<string, FrameworkElement> _words = [];
    private string? _text;

    public RollingWords(bool wrap)
    {
        if (!wrap) _live.Orientation = Orientation.Horizontal;
        Children.Add(_live);
        Children.Add(_exits);
    }

    public string Text => _text ?? "";

    public void SetText(string text, int direction, bool animate)
    {
        if (text == _text) return;
        bool first = _text is null;
        _text = text;
        animate &= !first;
        double em = TextElement.GetFontSize(this);
        var parts = text.Length == 0 ? [] : text.Split(' ');
        var keys = parts.Select((word, i) => $"{i}:{word}").ToList();
        foreach (var pair in _words.Where(p => !keys.Contains(p.Key)).ToArray())
        {
            var word = pair.Value;
            _words.Remove(pair.Key);
            if (!animate || !_live.Children.Contains(word)) { _live.Children.Remove(word); continue; }
            var at = word.TranslatePoint(new Point(), this);
            _live.Children.Remove(word);
            Canvas.SetLeft(word, at.X); Canvas.SetTop(word, at.Y);
            _exits.Children.Add(word);
            Tween.Present(word, false, direction * -.25 * em, 2, Tween.Instant, ArcEasing.Standard, done: () => _exits.Children.Remove(word));
        }
        _live.Children.Clear();
        for (int i = 0; i < parts.Length; i++)
        {
            if (!_words.TryGetValue(keys[i], out var word))
            {
                word = new TextBlock { Text = parts[i] + (i < parts.Length - 1 ? " " : ""), RenderTransform = new TranslateTransform() };
                _words[keys[i]] = word;
                if (animate) Tween.Present(word, true, direction * .3 * em, 4, Tween.Standard, ArcEasing.Enter);
            }
            _live.Children.Add(word);
        }
    }
}
