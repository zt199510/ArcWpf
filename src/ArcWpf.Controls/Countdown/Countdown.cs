using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum CountdownVariant { Large, Compact }
public enum CountdownUnit { Days, Hours, Minutes, Seconds }

/// <summary>A live countdown to a fixed moment: digit wheels with labels, or one compact pill. The shell springs to whatever it holds, so the digits morph into the finished label.</summary>
[TemplatePart(Name = "PART_Shell", Type = typeof(Border))]
[TemplatePart(Name = "PART_Content", Type = typeof(Grid))]
public class Countdown : Control
{
    private static readonly IReadOnlyDictionary<CountdownUnit, long> UnitSeconds = new Dictionary<CountdownUnit, long> { [CountdownUnit.Days] = 86400, [CountdownUnit.Hours] = 3600, [CountdownUnit.Minutes] = 60, [CountdownUnit.Seconds] = 1 };
    private static readonly IReadOnlyDictionary<CountdownUnit, string[]> UnitNames = new Dictionary<CountdownUnit, string[]> { [CountdownUnit.Days] = ["Days", "d", "day"], [CountdownUnit.Hours] = ["Hours", "h", "hour"], [CountdownUnit.Minutes] = ["Minutes", "m", "minute"], [CountdownUnit.Seconds] = ["Seconds", "s", "second"] };

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(nameof(Target), typeof(DateTime), typeof(Countdown), new FrameworkPropertyMetadata(DateTime.UtcNow, OnTargetChanged));
    public static readonly DependencyProperty TargetTextProperty = DependencyProperty.Register(nameof(TargetText), typeof(string), typeof(Countdown), new FrameworkPropertyMetadata(string.Empty, OnTargetChanged));
    public static readonly DependencyProperty TimeZoneIdProperty = DependencyProperty.Register(nameof(TimeZoneId), typeof(string), typeof(Countdown), new FrameworkPropertyMetadata("UTC", OnTargetChanged));
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(CountdownVariant), typeof(Countdown), new PropertyMetadata(CountdownVariant.Large, (d, _) => ((Countdown)d).Rebuild()));
    public static readonly DependencyProperty CompleteLabelProperty = DependencyProperty.Register(nameof(CompleteLabel), typeof(string), typeof(Countdown), new PropertyMetadata("Live now", (d, _) => ((Countdown)d).Rebuild()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Countdown), new PropertyMetadata("Time remaining"));
    public static readonly DependencyProperty UnitsProperty = DependencyProperty.Register(nameof(Units), typeof(ObservableCollection<CountdownUnit>), typeof(Countdown), new PropertyMetadata(null, OnUnitsChanged));
    private static readonly DependencyPropertyKey IsCompletePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsComplete), typeof(bool), typeof(Countdown), new PropertyMetadata(false));
    public static readonly DependencyProperty IsCompleteProperty = IsCompletePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey RemainingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Remaining), typeof(TimeSpan), typeof(Countdown), new PropertyMetadata(TimeSpan.Zero));
    public static readonly DependencyProperty RemainingProperty = RemainingPropertyKey.DependencyProperty;
    public static readonly RoutedEvent CompletedEvent = EventManager.RegisterRoutedEvent(nameof(Completed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Countdown));

    public DateTime Target { get => (DateTime)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public string TargetText { get => (string)GetValue(TargetTextProperty); set => SetValue(TargetTextProperty, value); }
    public string TimeZoneId { get => (string)GetValue(TimeZoneIdProperty); set => SetValue(TimeZoneIdProperty, value); }
    public CountdownVariant Variant { get => (CountdownVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public string CompleteLabel { get => (string)GetValue(CompleteLabelProperty); set => SetValue(CompleteLabelProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ObservableCollection<CountdownUnit> Units { get => (ObservableCollection<CountdownUnit>)GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }
    public bool IsComplete => (bool)GetValue(IsCompleteProperty);
    public TimeSpan Remaining => (TimeSpan)GetValue(RemainingProperty);
    /// <summary>Clock source. Pass a server-synced clock when the device time cannot be trusted.</summary>
    public Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;
    public event RoutedEventHandler Completed { add => AddHandler(CompletedEvent, value); remove => RemoveHandler(CompletedEvent, value); }

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);
    private readonly List<(CountdownUnit Unit, StackPanel Number, List<CountdownWheel> Wheels)> _groups = [];
    private readonly SpringDriver _shellDriver;
    private readonly ControlSpring _width, _height;
    private Border? _shell;
    private Grid? _content;
    private FrameworkElement? _face;
    private bool _complete;
    private bool _shellPlaced;

    static Countdown() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Countdown), new FrameworkPropertyMetadata(typeof(Countdown)));

    public Countdown()
    {
        _shellDriver = new SpringDriver(() => { if (_shell is not null) { _shell.Width = _width!.Position; _shell.Height = _height!.Position; } }) { Settled = () => { if (_shell is not null) { _shell.Width = double.NaN; _shell.Height = double.NaN; } } };
        _width = _shellDriver.Add(0, Tween.Morph);
        _height = _shellDriver.Add(0, Tween.Smooth);
        Units = [CountdownUnit.Days, CountdownUnit.Hours, CountdownUnit.Minutes, CountdownUnit.Seconds];
        _timer.Tick += (_, _) => Update();
        Loaded += (_, _) => { Update(); _timer.Start(); };
        Unloaded += (_, _) => { _timer.Stop(); _shellDriver.Stop(); };
        SizeChanged += (_, e) => { if (e.WidthChanged && Variant == CountdownVariant.Large) ApplyLargeSize(); };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _shell = GetTemplateChild("PART_Shell") as Border;
        _content = GetTemplateChild("PART_Content") as Grid;
        Rebuild();
    }

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Countdown)d).Update();

    private static void OnUnitsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var countdown = (Countdown)d;
        if (e.OldValue is ObservableCollection<CountdownUnit> old) old.CollectionChanged -= countdown.OnUnitsCollectionChanged;
        if (e.NewValue is ObservableCollection<CountdownUnit> next) next.CollectionChanged += countdown.OnUnitsCollectionChanged;
        countdown.Rebuild();
    }

    private void OnUnitsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Rebuild();

    private CountdownUnit[] ShownUnits => (Units is { Count: > 0 } ? Units.AsEnumerable() : [CountdownUnit.Days, CountdownUnit.Hours, CountdownUnit.Minutes, CountdownUnit.Seconds]).Distinct().OrderByDescending(unit => UnitSeconds[unit]).ToArray();

    /// <summary>Whole seconds left, split across the units on show. The largest unit absorbs the rest, so hours can pass 24 when days are left out.</summary>
    internal static IReadOnlyList<(CountdownUnit Unit, long Value)> Split(long seconds, IReadOnlyList<CountdownUnit> units)
    {
        var parts = new List<(CountdownUnit, long)>(units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            long value = seconds / UnitSeconds[units[i]];
            if (i > 0) value %= units[i] switch { CountdownUnit.Hours => 24, CountdownUnit.Days => long.MaxValue, _ => 60 };
            parts.Add((units[i], value));
        }
        return parts;
    }

    private (long Seconds, TimeSpan Left) Read()
    {
        var left = ResolveTarget() - Clock();
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        return ((long)Math.Ceiling(left.TotalSeconds), left);
    }

    private void Update()
    {
        var (seconds, left) = Read();
        bool was = IsComplete;
        SetValue(RemainingPropertyKey, left);
        SetValue(IsCompletePropertyKey, left == TimeSpan.Zero);
        // Wake just after the next whole second, aligned to the target rather than to when the page loaded.
        double wake = left.TotalMilliseconds % 1000;
        _timer.Interval = TimeSpan.FromMilliseconds((wake <= 0 ? 1000 : wake) + 8);
        if (left == TimeSpan.Zero) _timer.Stop();
        else if (IsLoaded && !_timer.IsEnabled) _timer.Start();
        if (IsComplete != _complete) Rebuild(animate: IsLoaded);
        else WriteDigits(seconds, true);
        if (!was && IsComplete) RaiseEvent(new RoutedEventArgs(CompletedEvent, this));
    }

    private void WriteDigits(long seconds, bool animate)
    {
        var parts = Split(seconds, ShownUnits);
        var level = ArcMotion.GetLevel(this);
        foreach (var (unit, value) in parts)
        {
            var group = _groups.FirstOrDefault(entry => entry.Unit == unit);
            if (group.Number is null) continue;
            string text = value.ToString("00", CultureInfo.InvariantCulture);
            if (group.Wheels.Count != text.Length) RebuildWheels(group.Number, group.Wheels, unit, unit == parts[0].Unit, text.Length, _groups.IndexOf(group));
            for (int i = 0; i < text.Length; i++) group.Wheels[i].SetDigit(text[i] - '0', animate && IsLoaded, level);
        }
        AutomationProperties.SetName(this, IsComplete ? CompleteLabel : $"{Label}: {Spoken(parts, seconds)}");
    }

    private static string Spoken(IReadOnlyList<(CountdownUnit Unit, long Value)> parts, long seconds)
        => string.Join(", ", parts.Where((part, index) => part.Value > 0 || index == parts.Count - 1).Where(part => seconds < 60 || part.Unit != CountdownUnit.Seconds).Select(part => $"{part.Value} {UnitNames[part.Unit][2]}{(part.Value == 1 ? string.Empty : "s")}"));

    // Wheel sizes per column: a tens column that only ever shows 0 to 5 turns on a six-digit wheel, so 00 → 59 is a single step.
    private static int BaseFor(CountdownUnit unit, bool largest, int length, int index) => index == length - 2 && length == 2 && !largest ? (unit == CountdownUnit.Hours ? 3 : 6) : 10;

    private void RebuildWheels(StackPanel number, List<CountdownWheel> wheels, CountdownUnit unit, bool largest, int length, int group)
    {
        number.Children.Clear();
        wheels.Clear();
        bool compact = Variant == CountdownVariant.Compact;
        for (int i = 0; i < length; i++)
        {
            var wheel = new CountdownWheel(BaseFor(unit, largest, length, i), compact ? 14 : LargeFontSize, Math.Min((group * 2 + i) * .03, .3), compact ? 18.2 : LargeFontSize);
            wheels.Add(wheel);
            number.Children.Add(wheel);
        }
    }

    private double LargeFontSize => Math.Clamp(Math.Max(ActualWidth, _shell?.ActualWidth ?? 0) > 0 ? ActualWidth * .125 : 58.95, 28, 72);

    private void ApplyLargeSize()
    {
        if (_content is null || _complete) return;
        double font = LargeFontSize;
        foreach (var (_, _, wheels) in _groups) foreach (var wheel in wheels) wheel.Resize(font, font);
        foreach (var block in _content.Children.OfType<StackPanel>().SelectMany(Descend).OfType<TextBlock>().Where(block => block.Tag as string == "colon"))
        {
            block.FontSize = font;
            block.LineHeight = font;
            block.Padding = new Thickness(font * .12, 0, font * .12, 0);
            block.RenderTransform = new TranslateTransform(0, -.06 * font);
        }
        foreach (var column in _groupsColumns) column.MinWidth = Math.Min((ActualWidth - 1.6 * font) / 4, 12 * 4.2);
    }

    private readonly List<FrameworkElement> _groupsColumns = [];

    private static IEnumerable<DependencyObject> Descend(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var deeper in Descend(child)) yield return deeper;
        }
    }

    private void Rebuild() => Rebuild(false);

    private void Rebuild(bool animate)
    {
        if (_content is null) return;
        var (seconds, left) = Read();
        SetValue(RemainingPropertyKey, left);
        SetValue(IsCompletePropertyKey, left == TimeSpan.Zero);
        _complete = IsComplete;
        double fromWidth = _shell?.ActualWidth ?? 0, fromHeight = _shell?.ActualHeight ?? 0;
        var level = ArcMotion.GetLevel(this);
        var next = _complete ? BuildDone() : BuildUnits();
        var old = _face;
        _face = next;
        next.RenderTransformOrigin = new Point(.5, .5);
        _content.Children.Add(next);
        if (!_complete) WriteDigits(seconds, false);
        else AutomationProperties.SetName(this, CompleteLabel);
        if (old is null) return;
        if (!animate || level == ArcMotionLevel.Off) { _content.Children.Remove(old); return; }
        Swap(next, old, level == ArcMotionLevel.Full);
        if (level == ArcMotionLevel.Full && _shell is not null) MorphShell(next, fromWidth, fromHeight);
    }

    private void Swap(FrameworkElement next, FrameworkElement old, bool full)
    {
        next.Opacity = 0;
        Tween.To(next, OpacityProperty, 1, Tween.Morph, ArcEasing.Spring(Tween.Morph, Tween.Morph));
        Tween.To(old, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, 0, () => _content!.Children.Remove(old));
        if (!full) return;
        var grow = new ScaleTransform(.92, .92);
        next.RenderTransform = grow;
        Tween.Spring(grow, ScaleTransform.ScaleXProperty, 1, Tween.Morph);
        Tween.Spring(grow, ScaleTransform.ScaleYProperty, 1, Tween.Morph);
        var shrink = new ScaleTransform(1, 1);
        old.RenderTransformOrigin = new Point(.5, .5);
        old.RenderTransform = shrink;
        Tween.To(shrink, ScaleTransform.ScaleXProperty, .92, Tween.Fast, ArcEasing.Standard);
        Tween.To(shrink, ScaleTransform.ScaleYProperty, .92, Tween.Fast, ArcEasing.Standard);
        next.Effect = new BlurEffect { Radius = 6 };
        var clear = new DoubleAnimation(0, TimeSpan.FromSeconds(Tween.Morph)) { EasingFunction = ArcEasing.Standard };
        clear.Completed += (_, _) => next.Effect = null;
        next.Effect.BeginAnimation(BlurEffect.RadiusProperty, clear);
    }

    private void MorphShell(FrameworkElement next, double fromWidth, double fromHeight)
    {
        var padding = Variant == CountdownVariant.Compact ? 24 : 0;
        var border = Variant == CountdownVariant.Compact ? 2 : 0;
        next.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double toWidth = next.DesiredSize.Width + padding + border;
        double toHeight = Math.Max(next.DesiredSize.Height, Variant == CountdownVariant.Compact ? 32 : 0) + border;
        if (!_shellPlaced || fromWidth <= 0) _shellPlaced = true;
        _shellDriver.Snap(_width, fromWidth);
        _shellDriver.Snap(_height, fromHeight);
        _shellDriver.Animate(_width, toWidth, Tween.Morph);
        _shellDriver.Animate(_height, toHeight, Tween.Smooth);
    }

    private FrameworkElement BuildUnits()
    {
        _groups.Clear();
        _groupsColumns.Clear();
        bool compact = Variant == CountdownVariant.Compact;
        double font = LargeFontSize;
        var units = new StackPanel { Orientation = Orientation.Horizontal };
        var shown = ShownUnits;
        for (int i = 0; i < shown.Length; i++)
        {
            var number = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var name = new TextBlock { Text = UnitNames[shown[i]][compact ? 1 : 0] };
            name.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            if (compact)
            {
                name.FontSize = 14;
                name.FontWeight = FontWeights.Normal;
                name.LineHeight = 18.2;
                name.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                name.Margin = new Thickness(1, 0, 0, 0);
                var group = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(i > 0 ? 6 : 0, 0, 0, 0) };
                group.Children.Add(number);
                group.Children.Add(name);
                units.Children.Add(group);
            }
            else
            {
                if (i > 0)
                {
                    var colon = new TextBlock { Text = ":", Tag = "colon", FontSize = font, LineHeight = font, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = FontWeights.Medium, Opacity = .6, Padding = new Thickness(font * .12, 0, font * .12, 0), RenderTransform = new TranslateTransform(0, -.06 * font), VerticalAlignment = VerticalAlignment.Top };
                    colon.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
                    units.Children.Add(colon);
                }
                name.FontSize = 12;
                name.LineHeight = 12;
                name.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                name.HorizontalAlignment = HorizontalAlignment.Center;
                name.Margin = new Thickness(0, 6, 0, 0);
                var group = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
                group.Children.Add(number);
                group.Children.Add(name);
                _groupsColumns.Add(group);
                units.Children.Add(group);
            }
            _groups.Add((shown[i], number, []));
        }
        for (int i = 0; i < _groups.Count; i++) RebuildWheels(_groups[i].Number, _groups[i].Wheels, _groups[i].Unit, i == 0, 2, i);
        if (!compact && ActualWidth > 0) foreach (var column in _groupsColumns) column.MinWidth = Math.Min((ActualWidth - 1.6 * font) / 4, 12 * 4.2);
        return units;
    }

    // Finished: the digits give way to the label and a live mark.
    private FrameworkElement BuildDone()
    {
        bool compact = Variant == CountdownVariant.Compact;
        double font = compact ? 14 : Math.Clamp(ActualWidth * .11, 28, 52);
        double dot = Math.Max(font * .3, 8);
        var done = new StackPanel { Orientation = Orientation.Horizontal };
        var mark = new Grid { Width = dot, Height = dot, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, font * .4, 0) };
        var core = new Ellipse();
        core.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.LiveSuccess");
        var pulse = new Ellipse { RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(), Opacity = 0 };
        pulse.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.LiveSuccess");
        mark.Children.Add(pulse);
        mark.Children.Add(core);
        if (ArcMotion.GetLevel(this) == ArcMotionLevel.Full) StartPulse(pulse);
        var text = new TextBlock { Text = CompleteLabel, FontSize = font, FontWeight = FontWeights.Medium, LineHeight = font * (compact ? 1.3 : 1.1), LineStackingStrategy = LineStackingStrategy.BlockLineHeight, VerticalAlignment = VerticalAlignment.Center };
        text.SetResourceReference(TextBlock.ForegroundProperty, compact ? "ArcWpf.Brush.LiveSuccess" : "ArcWpf.Brush.Foreground");
        done.Children.Add(mark);
        done.Children.Add(text);
        return done;
    }

    private static void StartPulse(Ellipse pulse)
    {
        var duration = new Duration(TimeSpan.FromSeconds(2));
        KeyTime stop = KeyTime.FromPercent(.7);
        var opacity = new DoubleAnimationUsingKeyFrames { Duration = duration, RepeatBehavior = RepeatBehavior.Forever };
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(.5, KeyTime.FromPercent(0)));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(0, stop, ArcEasing.Standard));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        var scale = new DoubleAnimationUsingKeyFrames { Duration = duration, RepeatBehavior = RepeatBehavior.Forever };
        scale.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0)));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(2.6, stop, ArcEasing.Standard));
        scale.KeyFrames.Add(new LinearDoubleKeyFrame(2.6, KeyTime.FromPercent(1)));
        pulse.BeginAnimation(OpacityProperty, opacity);
        var transform = (ScaleTransform)pulse.RenderTransform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }

    private DateTimeOffset ResolveTarget()
    {
        if (string.IsNullOrWhiteSpace(TargetText)) return new DateTimeOffset(Target.ToUniversalTime());
        bool hasOffset = TargetText.EndsWith('Z') || TargetText.EndsWith('z') || Regex.IsMatch(TargetText, @"[+-]\d{2}:?\d{2}$");
        if (hasOffset && DateTimeOffset.TryParse(TargetText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var exact)) return exact.ToUniversalTime();
        if (DateTime.TryParse(TargetText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall))
        {
            try { return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId))); }
            catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { }
        }
        return new DateTimeOffset(Target.ToUniversalTime());
    }
}

/// <summary>A digit wheel that takes the short way round, so a countdown ticks downward and a wrap such as 00 → 59 is one step. Its window feathers out at the edges.</summary>
internal sealed class CountdownWheel : Grid
{
    private const double Feather = .16;
    private readonly int _base;
    private readonly double _delay;
    private readonly Canvas _strip = new();
    private readonly TextBlock _sizer;
    private readonly List<TextBlock> _glyphs = [];
    private readonly SpringDriver _driver;
    private readonly ControlSpring _position;
    private double _font, _line;
    private int _digit;
    private bool _revealed;

    public CountdownWheel(int digitBase, double font, double delay, double line)
    {
        _base = digitBase;
        _delay = delay;
        _font = font;
        _line = line;
        IsHitTestVisible = false;
        _driver = new SpringDriver(Place);
        _position = _driver.Add(0, Tween.Smooth);
        _sizer = new TextBlock { Text = "0", Visibility = Visibility.Hidden };
        Children.Add(_sizer);
        Children.Add(_strip);
        for (int digit = 0; digit < digitBase; digit++)
        {
            var glyph = new TextBlock { Text = digit.ToString(CultureInfo.InvariantCulture), TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
            _glyphs.Add(glyph);
            _strip.Children.Add(glyph);
        }
        Unloaded += (_, _) => _driver.Finish();
        Resize(font, line);
    }

    public void Resize(double font, double line)
    {
        _font = font;
        _line = line;
        foreach (var block in _glyphs.Append(_sizer))
        {
            block.FontSize = font;
            block.FontWeight = FontWeights.Medium;
            block.LineHeight = line;
            block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        }
        _sizer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = _sizer.DesiredSize.Width, feather = font * Feather;
        Width = width;
        Height = line + feather * 2;
        Margin = new Thickness(0, -feather, 0, -feather);
        _sizer.Margin = new Thickness(0, feather, 0, feather);
        foreach (var glyph in _glyphs) glyph.Width = width;
        ClipToBounds = true;
        double stop = feather * 1.5 / Height;
        OpacityMask = new LinearGradientBrush(new GradientStopCollection { new(Colors.Transparent, 0), new(Colors.Black, stop), new(Colors.Black, 1 - stop), new(Colors.Transparent, 1) }, new Point(0, 0), new Point(0, 1));
        Place();
    }

    private void Place()
    {
        double feather = _font * Feather, position = _position.Position;
        for (int digit = 0; digit < _base; digit++)
        {
            double offset = ((digit - position) % _base + _base * 1.5) % _base - _base / 2d;
            var glyph = _glyphs[digit];
            double distance = Math.Abs(offset);
            glyph.Visibility = distance >= 1 ? Visibility.Hidden : Visibility.Visible;
            glyph.Opacity = Math.Max(0, 1 - distance);
            Canvas.SetTop(glyph, feather + offset * _font);
        }
    }

    public void SetDigit(int digit, bool animate, ArcMotionLevel level)
    {
        if (digit >= _base) digit %= _base;
        if (digit == _digit && _revealed) return;
        int up = ((digit - _digit) % _base + _base) % _base, down = _base - up;
        double target = _position.Target + (down <= up ? -down : up);
        if (up == 0) target = _position.Target;
        _digit = digit;
        if (animate && level == ArcMotionLevel.Full) _driver.Animate(_position, target, Tween.Snappy);
        else _driver.Snap(_position, target);
        _revealed = true;
    }
}
