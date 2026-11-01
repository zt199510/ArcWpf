using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

/// <summary>A requirement checked on the device. <see cref="Remaining"/> counts characters still missing.</summary>
public sealed record PasswordRule(string Id, string Label, Func<string, bool> Test, Func<string, int>? Remaining = null);

/// <summary>Level 0 (empty) to 4; <see cref="Short"/> holds the meter at its first step.</summary>
public sealed record PasswordStrengthResult(int Level, string Label, IReadOnlyList<string> Met, bool Short = false);

// password-strength.tsx (frozen 2026-10-31): a new password field. Four segments fill and change tone, each rule checks off with a drawn
// tick, and the strength word rises when strength improves and drops when it falls. Scoring happens on the device.
[TemplatePart(Name = "PART_Shell", Type = typeof(Border))]
[TemplatePart(Name = "PART_PasswordBox", Type = typeof(PasswordBox))]
[TemplatePart(Name = "PART_RevealBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Reveal", Type = typeof(PasswordRevealToggle))]
[TemplatePart(Name = "PART_Error", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Meter", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Word", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Rules", Type = typeof(StackPanel))]
public class PasswordStrength : Control
{
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(nameof(Password), typeof(string), typeof(PasswordStrength), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((PasswordStrength)d).OnPasswordChanged()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(PasswordStrength), new PropertyMetadata(""));
    public static readonly DependencyProperty ErrorProperty = DependencyProperty.Register(nameof(Error), typeof(string), typeof(PasswordStrength), new FrameworkPropertyMetadata("", (d, e) => ((PasswordStrength)d).OnErrorChanged((string?)e.OldValue)));
    public static readonly DependencyProperty IsRevealedProperty = DependencyProperty.Register(nameof(IsRevealed), typeof(bool), typeof(PasswordStrength), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((PasswordStrength)d).OnRevealChanged()));
    public static readonly DependencyProperty RulesProperty = DependencyProperty.Register(nameof(Rules), typeof(IList<PasswordRule>), typeof(PasswordStrength), new FrameworkPropertyMetadata(null, (d, _) => ((PasswordStrength)d).BuildRules()));
    public static readonly DependencyProperty ShortLengthProperty = DependencyProperty.Register(nameof(ShortLength), typeof(int), typeof(PasswordStrength), new FrameworkPropertyMetadata(8, (d, _) => ((PasswordStrength)d).Refresh(true)));
    private static readonly DependencyPropertyKey LevelPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Level), typeof(int), typeof(PasswordStrength), new FrameworkPropertyMetadata(0));
    public static readonly DependencyProperty LevelProperty = LevelPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey StrengthLabelPropertyKey = DependencyProperty.RegisterReadOnly(nameof(StrengthLabel), typeof(string), typeof(PasswordStrength), new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty StrengthLabelProperty = StrengthLabelPropertyKey.DependencyProperty;
    public static readonly RoutedEvent PasswordChangedEvent = EventManager.RegisterRoutedEvent(nameof(PasswordChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PasswordStrength));
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(PasswordStrength), new PropertyMetadata(null, (d, _) => ((PasswordStrength)d).Recolor(false)));

    public string Password { get => (string)GetValue(PasswordProperty); set => SetValue(PasswordProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Error copy tied to the field. The field shakes once each time a new error appears.</summary>
    public string Error { get => (string)GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }
    public bool IsRevealed { get => (bool)GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }
    /// <summary>Rules to check. Strength is the share of rules met, spread over four steps. Null uses <see cref="DefaultRules"/>.</summary>
    public IList<PasswordRule>? Rules { get => (IList<PasswordRule>?)GetValue(RulesProperty); set => SetValue(RulesProperty, value); }
    /// <summary>Below this many characters the meter holds at its first step and reads "Too short".</summary>
    public int ShortLength { get => (int)GetValue(ShortLengthProperty); set => SetValue(ShortLengthProperty, value); }
    public int Level => (int)GetValue(LevelProperty);
    public string StrengthLabel => (string)GetValue(StrengthLabelProperty);
    public event RoutedEventHandler PasswordChanged { add => AddHandler(PasswordChangedEvent, value); remove => RemoveHandler(PasswordChangedEvent, value); }

    public static IReadOnlyList<PasswordRule> DefaultRules { get; } = CreateRules();

    /// <summary>The built-in rules (length, mixed case, number, symbol). Characters are counted as code points.</summary>
    public static IReadOnlyList<PasswordRule> CreateRules(int minLength = 12) =>
    [
        new("length", $"At least {minLength} characters", p => CountCharacters(p) >= minLength, p => Math.Max(0, minLength - CountCharacters(p))),
        new("case", "Upper and lowercase letters", p => p.Any(char.IsLower) && p.Any(char.IsUpper)),
        new("number", "At least one number", p => p.EnumerateRunes().Any(Rune.IsNumber)),
        new("symbol", "At least one symbol", p => p.EnumerateRunes().Any(r => !Rune.IsLetter(r) && !Rune.IsNumber(r) && !Rune.IsWhiteSpace(r))),
    ];

    /// <summary>Counts code points, so an emoji or other astral character counts as one.</summary>
    public static int CountCharacters(string password) => password.EnumerateRunes().Count();

    /// <summary>One step per share of rules met, capped at the first step while shorter than <paramref name="shortLength"/>.</summary>
    public static PasswordStrengthResult EstimateStrength(string password, IEnumerable<PasswordRule>? rules = null, int shortLength = 8)
    {
        var list = (rules ?? DefaultRules).ToList();
        if (string.IsNullOrEmpty(password)) return new(0, "", []);
        var met = list.Where(rule => rule.Test(password)).Select(rule => rule.Id).ToList();
        if (CountCharacters(password) < shortLength) return new(1, "Too short", met, true);
        int level = Math.Clamp((int)Math.Round(met.Count / (double)Math.Max(list.Count, 1) * 4, MidpointRounding.AwayFromZero), 1, 4);
        return new(level, new[] { "Weak", "Fair", "Good", "Strong" }[level - 1], met);
    }

    private Border? _shell;
    private PasswordBox? _hidden;
    private TextBox? _shown;
    private ToggleButton? _reveal;
    private Grid? _errorHost, _meter, _word;
    private StackPanel? _rules;
    private FieldMessage? _error;
    private readonly List<(Border Fill, TranslateTransform Move)> _segments = [];
    private readonly List<RuleRow> _rows = [];
    private readonly SolidColorBrush _tone = new(Colors.Gray), _shellInk = new(Colors.Gray);
    private readonly TranslateTransform _shake = new();
    private double _rank;
    private int _direction = 1, _previousLevel;
    private HashSet<string> _met = [];
    private bool _syncing;

    static PasswordStrength()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PasswordStrength), new FrameworkPropertyMetadata(typeof(PasswordStrength)));
        FocusableProperty.OverrideMetadata(typeof(PasswordStrength), new FrameworkPropertyMetadata(false));
    }

    public PasswordStrength() { SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border"); }

    private IReadOnlyList<PasswordRule> ActiveRules => (IReadOnlyList<PasswordRule>?)Rules?.ToList() ?? DefaultRules;
    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private bool Animating => IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;

    public override void OnApplyTemplate()
    {
        if (_hidden is not null) _hidden.PasswordChanged -= HiddenChanged;
        if (_shown is not null) _shown.TextChanged -= ShownChanged;
        if (_reveal is not null) _reveal.Click -= RevealClick;
        base.OnApplyTemplate();
        _shell = GetTemplateChild("PART_Shell") as Border;
        _hidden = GetTemplateChild("PART_PasswordBox") as PasswordBox;
        _shown = GetTemplateChild("PART_RevealBox") as TextBox;
        _reveal = GetTemplateChild("PART_Reveal") as ToggleButton;
        _errorHost = GetTemplateChild("PART_Error") as Grid;
        _meter = GetTemplateChild("PART_Meter") as Grid;
        _word = GetTemplateChild("PART_Word") as Grid;
        _rules = GetTemplateChild("PART_Rules") as StackPanel;
        if (_shell is not null) { _shell.BorderBrush = _shellInk; _shell.RenderTransform = _shake; }
        if (_hidden is not null) { _hidden.Password = Password; _hidden.PasswordChanged += HiddenChanged; }
        if (_shown is not null) { _shown.Text = Password; _shown.TextChanged += ShownChanged; }
        if (_reveal is not null) { _reveal.IsChecked = IsRevealed; _reveal.Click += RevealClick; }
        _errorHost?.Children.Clear();
        _error = _errorHost is null ? null : new FieldMessage();
        if (_error is not null) _errorHost!.Children.Add(_error);
        BuildMeter();
        BuildRules();
        ApplyReveal(false);
        OnErrorChanged(null);
        Recolor(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty || e.Property == IsKeyboardFocusWithinProperty) UpdateShell(true);
    }

    private void HiddenChanged(object sender, RoutedEventArgs e) { if (!_syncing) SetCurrentValue(PasswordProperty, _hidden!.Password); }
    private void ShownChanged(object sender, TextChangedEventArgs e) { if (!_syncing) SetCurrentValue(PasswordProperty, _shown!.Text); }
    private void RevealClick(object sender, RoutedEventArgs e) => SetCurrentValue(IsRevealedProperty, _reveal!.IsChecked == true);

    private void OnPasswordChanged()
    {
        _syncing = true;
        if (_hidden is not null && _hidden.Password != Password) _hidden.Password = Password;
        if (_shown is not null && _shown.Text != Password) { _shown.Text = Password; _shown.CaretIndex = Password.Length; }
        _syncing = false;
        Refresh(true);
        RaiseEvent(new RoutedEventArgs(PasswordChangedEvent, this));
    }

    private void OnRevealChanged()
    {
        if (_reveal is not null) _reveal.IsChecked = IsRevealed;
        ApplyReveal(true);
    }

    // Dots and characters resolve into each other instead of snapping: the field comes in from opacity .35 and a 2 DIP blur (.24 enter).
    private void ApplyReveal(bool animate)
    {
        if (_hidden is null || _shown is null) return;
        bool focus = _hidden.IsKeyboardFocused || _shown.IsKeyboardFocused;
        _shown.Visibility = IsRevealed ? Visibility.Visible : Visibility.Collapsed;
        _hidden.Visibility = IsRevealed ? Visibility.Collapsed : Visibility.Visible;
        Control editor = IsRevealed ? _shown : _hidden;
        if (focus) { editor.Focus(); if (editor is TextBox box) box.CaretIndex = box.Text.Length; }
        if (!animate || !Full) return;
        editor.Opacity = .35;
        Tween.To(editor, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
        var blur = new BlurEffect { Radius = 2 };
        editor.Effect = blur;
        var clear = new DoubleAnimation(0, TimeSpan.FromSeconds(Tween.Standard)) { EasingFunction = ArcEasing.Enter };
        clear.Completed += (_, _) => { if (ReferenceEquals(editor.Effect, blur)) editor.Effect = null; };
        blur.BeginAnimation(BlurEffect.RadiusProperty, clear);
    }

    private void OnErrorChanged(string? previous)
    {
        bool has = !string.IsNullOrEmpty(Error);
        UpdateShell(true);
        if (_error is null) return;
        if (!has) { _error.Hide(Animating); return; }
        _error.Show(Error, FieldMessage.Line(Error, "ArcWpf.Brush.Danger"), Animating);
        if (Error != previous && Full) Shake();
    }

    // A spring released with sideways velocity rings out on its own (velocity -240, stiffness 900, damping 57).
    private void Shake()
    {
        double omega = 30, zeta = 57 / (2 * omega), damped = omega * Math.Sqrt(1 - zeta * zeta);
        var frames = new DoubleAnimationUsingKeyFrames();
        for (int i = 0; i <= 40; i++)
        {
            double t = i * .5 / 40;
            frames.KeyFrames.Add(new LinearDoubleKeyFrame(-240 / damped * Math.Exp(-zeta * omega * t) * Math.Sin(damped * t), KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t))));
        }
        frames.Freeze();
        _shake.BeginAnimation(TranslateTransform.XProperty, frames);
    }

    // .shell: border-strong; hover (not focused) text-muted; focus-within foreground; invalid danger (.16 standard).
    private void UpdateShell(bool animate)
    {
        string key = !string.IsNullOrEmpty(Error) ? "Danger" : IsKeyboardFocusWithin ? "Foreground" : IsMouseOver ? "TextMuted" : "BorderStrong";
        Tween.Color(_shellInk, Tween.Resolve(this, $"ArcWpf.Brush.{key}"), Tween.Fast, animate && Full);
    }

    private void BuildMeter()
    {
        _segments.Clear();
        if (_meter is null) return;
        _meter.Children.Clear();
        _meter.ColumnDefinitions.Clear();
        for (int i = 0; i < 4; i++)
        {
            _meter.ColumnDefinitions.Add(new ColumnDefinition());
            // Each fill slides in from the left inside its clipped track, so the rounded ends keep their shape at every frame.
            var track = new Border { Height = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(i == 0 ? 0 : 2.5, 0, i == 3 ? 0 : 2.5, 0), ClipToBounds = true };
            track.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Border");
            var move = new TranslateTransform();
            var fill = new Border { CornerRadius = new CornerRadius(2), Background = _tone, RenderTransform = move };
            track.Child = fill;
            track.SizeChanged += (_, e) => { if (!move.HasAnimatedProperties) move.X = Level > _segments.FindIndex(s => s.Fill == fill) ? 0 : -1.01 * e.NewSize.Width; };
            track.Clip = null;
            Grid.SetColumn(track, i);
            _meter.Children.Add(track);
            _segments.Add((fill, move));
        }
        Refresh(false);
    }

    private void BuildRules()
    {
        _rows.Clear();
        if (_rules is null) return;
        _rules.Children.Clear();
        foreach (var rule in ActiveRules)
        {
            var row = new RuleRow(rule) { Margin = new Thickness(0, _rules.Children.Count == 0 ? 0 : 6, 0, 0) };
            _rows.Add(row);
            _rules.Children.Add(row);
        }
        _met = [];
        Refresh(false);
    }

    private void Refresh(bool animate)
    {
        var rules = ActiveRules;
        var strength = EstimateStrength(Password, rules, ShortLength);
        animate &= Animating;
        bool full = animate && Full;
        int level = strength.Level;
        double rank = strength.Short ? .5 : level;
        int previousLevel = Level;
        if (rank != _rank) { _direction = rank > _rank ? 1 : -1; _previousLevel = previousLevel; _rank = rank; }
        SetValue(LevelPropertyKey, level);
        SetValue(StrengthLabelPropertyKey, strength.Label);
        Recolor(full);
        for (int index = 0; index < _segments.Count; index++)
        {
            var (fill, move) = _segments[index];
            bool on = index < level;
            double width = fill.ActualWidth > 0 ? fill.ActualWidth : (fill.Parent as FrameworkElement)?.ActualWidth ?? 0;
            double to = on ? 0 : -1.01 * width;
            // A pasted password fills left to right; clearing empties right to left.
            int wave = on ? index - _previousLevel : _previousLevel - 1 - index;
            if (full) Tween.Spring(move, TranslateTransform.XProperty, to, Tween.Smooth, Math.Max(0, wave) * (on ? .05 : .03));
            else Tween.Set(move, TranslateTransform.XProperty, to);
        }
        SetWord(strength.Label, full, animate);
        var met = strength.Met.ToHashSet();
        var changed = rules.Where(rule => met.Contains(rule.Id) != _met.Contains(rule.Id)).Select(rule => rule.Id).ToList();
        foreach (var row in _rows)
        {
            bool ok = met.Contains(row.Rule.Id);
            int order = changed.IndexOf(row.Rule.Id);
            int remaining = Password.Length > 0 && !ok ? row.Rule.Remaining?.Invoke(Password) ?? 0 : 0;
            row.Update(ok, order > 0 ? order * .05 : 0, remaining, full, animate);
        }
        _met = met;
        if (animate && level > 0) Announcer.Say(this, $"Strength: {strength.Label}. {met.Count} of {rules.Count} requirements met.");
    }

    // The tone follows the level: 1 danger, 2 warning, 3–4 success, else border-strong (.24 standard).
    private void Recolor(bool animate)
    {
        string tone = Level switch { 1 => "Danger", 2 => "Warning", 3 or 4 => "Success", _ => "BorderStrong" };
        Tween.Color(_tone, Tween.Resolve(this, $"ArcWpf.Brush.{tone}"), Tween.Standard, animate);
        UpdateShell(false);
        foreach (var row in _rows) row.Recolor(this, false);
    }

    // A fixed 72 DIP slot: the word rises when strength improves and drops when it falls (.3em, soft blur, .24 enter; exit .16 standard).
    private void SetWord(string text, bool full, bool animate)
    {
        if (_word is null) return;
        var current = _word.Children.OfType<TextBlock>().LastOrDefault(t => t.IsHitTestVisible);
        if ((current?.Text ?? "") == text) return;
        if (current is not null)
        {
            current.IsHitTestVisible = false;
            if (full) Tween.Present(current, false, -.3 * 14 * _direction, 2, Tween.Fast, ArcEasing.Standard, done: () => _word.Children.Remove(current));
            else if (animate) Tween.To(current, OpacityProperty, 0, Tween.Instant, null, done: () => _word.Children.Remove(current));
            else _word.Children.Remove(current);
        }
        if (text.Length == 0) return;
        var line = new TextBlock { Text = text, FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Right, RenderTransform = new TranslateTransform() };
        _word.Children.Add(line);
        if (full) Tween.Present(line, true, .3 * 14 * _direction, 4, Tween.Standard, ArcEasing.Enter);
        else if (animate) Tween.Present(line, true, 0, 0, Tween.Fast, ArcEasing.Standard);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PasswordStrengthPeer(this);

    private sealed class PasswordStrengthPeer(PasswordStrength owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        protected override string GetClassNameCore() => nameof(PasswordStrength);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
        protected override string GetNameCore() => "Password strength";
        protected override string GetHelpTextCore() => owner.Level > 0 ? owner.StrengthLabel : "No password yet";
        public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.RangeValue ? this : base.GetPattern(pattern);
        public double Value => owner.Level;
        public bool IsReadOnly => true;
        public double Maximum => 4;
        public double Minimum => 0;
        public double LargeChange => 1;
        public double SmallChange => 1;
        public void SetValue(double value) => throw new InvalidOperationException();
    }
}

/// <summary>One requirement: the outline hands over to a faint tint while the tick draws on top; an unmet rule with a count shows "N more".</summary>
internal sealed class RuleRow : Grid
{
    private const double TickLength = 9.56;
    private readonly Border _ring = new() { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.25), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _tint = new() { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5) };
    private readonly ScaleTransform _tintScale = new(.5, .5);
    private readonly Path _tick = new() { Data = Geometry.Parse("M4.75,8.25 L7,10.5 L11.25,5.75"), StrokeThickness = 1.75, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, StrokeDashCap = PenLineCap.Round, Width = 16, Height = 16, Opacity = 0 };
    private readonly SolidColorBrush _ink = new(Colors.Gray), _ringInk = new(Colors.Gray);
    private readonly Grid _remaining = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8, 0, 0, 0) };
    private readonly RollingNumber _count = new();
    private FrameworkElement? _remainingText;
    private bool _ok;
    private bool _initial = true;

    public RuleRow(PasswordRule rule)
    {
        Rule = rule;
        MinHeight = 20;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var mark = new Grid { Width = 16, Height = 16, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        _ring.BorderBrush = _ringInk;
        _tint.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.PasswordRuleFill");
        _tint.RenderTransform = _tintScale;
        _tint.Opacity = 0;
        _tick.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.Success");
        _tick.StrokeDashArray = [0, 20];
        mark.Children.Add(_tint);
        mark.Children.Add(_ring);
        mark.Children.Add(_tick);
        Children.Add(mark);
        var label = new TextBlock { Text = rule.Label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = _ink, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        SetColumn(label, 1);
        Children.Add(label);
        SetColumn(_remaining, 2);
        _remaining.SetValue(TextElement.FontSizeProperty, 12d);
        _remaining.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        Children.Add(_remaining);
    }

    public PasswordRule Rule { get; }

    public void Recolor(FrameworkElement owner, bool animate)
    {
        Tween.Color(_ink, Tween.Resolve(owner, _ok ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextMuted"), Tween.Standard, animate);
        var ring = Tween.Resolve(owner, "ArcWpf.Brush.BorderStrong");
        if (_ok) ring.A = 0;
        Tween.Color(_ringInk, ring, Tween.Fast, animate);
    }

    public void Update(bool ok, double delay, int remaining, bool full, bool animate)
    {
        bool changed = ok != _ok || _initial;
        _ok = ok;
        _initial = false;
        if (changed)
        {
            Recolor(this, full);
            // The tint grows under the tick (scale .5 → 1 snappy, opacity .16) while the tick draws (.24 enter, .06 later); unchecking retracts faster.
            if (full)
            {
                Tween.Spring(_tintScale, ScaleTransform.ScaleXProperty, ok ? 1 : .5, Tween.Snappy, delay);
                Tween.Spring(_tintScale, ScaleTransform.ScaleYProperty, ok ? 1 : .5, Tween.Snappy, delay);
                Tween.To(_tint, OpacityProperty, ok ? 1 : 0, ok ? Tween.Fast : Tween.Instant, ArcEasing.Standard, delay);
                Draw(ok ? 1 : 0, ok ? Tween.Standard : Tween.Fast, ok ? ArcEasing.Enter : ArcEasing.Standard, ok ? delay + .06 : 0);
                Tween.To(_tick, OpacityProperty, ok ? 1 : 0, ok ? .05 : Tween.Fast, null, ok ? delay + .06 : .06);
            }
            else
            {
                Tween.Set(_tintScale, ScaleTransform.ScaleXProperty, ok ? 1 : .5);
                Tween.Set(_tintScale, ScaleTransform.ScaleYProperty, ok ? 1 : .5);
                Tween.Set(_tint, OpacityProperty, ok ? 1 : 0);
                Tween.Set(_tick, OpacityProperty, ok ? 1 : 0);
                Draw(ok ? 1 : 0, 0, null, 0);
            }
        }
        SetRemaining(remaining, full, animate);
    }

    private void Draw(double fraction, double seconds, IEasingFunction? ease, double delay)
    {
        double length = TickLength / 1.75;
        if (seconds <= 0) { _tick.BeginAnimation(Shape.StrokeDashOffsetProperty, null); _tick.StrokeDashArray = [length, 20]; _tick.StrokeDashOffset = length * (1 - fraction); return; }
        _tick.StrokeDashArray = [length, 20];
        Tween.To(_tick, Shape.StrokeDashOffsetProperty, length * (1 - fraction), seconds, ease, delay);
    }

    // "N more" rises in while a rule is unmet and has a count; the digits turn like a counter.
    private void SetRemaining(int remaining, bool full, bool animate)
    {
        if (remaining > 0)
        {
            if (_remainingText is null)
            {
                var text = new StackPanel { Orientation = Orientation.Horizontal, RenderTransform = new TranslateTransform() };
                (_count.Parent as Panel)?.Children.Remove(_count);
                text.Children.Add(_count);
                text.Children.Add(new TextBlock { Text = " more" });
                _remainingText = text;
                _remaining.Children.Add(text);
                _count.Set(remaining, false);
                if (full) Tween.Present(text, true, .3 * 12, 4, Tween.Standard, ArcEasing.Enter);
                else if (animate) Tween.Present(text, true, 0, 0, Tween.Fast, ArcEasing.Standard);
            }
            else _count.Set(remaining, full);
            return;
        }
        if (_remainingText is null) return;
        var leaving = _remainingText;
        _remainingText = null;
        leaving.IsHitTestVisible = false;
        if (full) Tween.Present(leaving, false, -.3 * 12, 2, Tween.Fast, ArcEasing.Standard, done: () => _remaining.Children.Remove(leaving));
        else _remaining.Children.Remove(leaving);
    }
}

/// <summary>Digits keyed by place value: 10 → 9 lets the tens column narrow away (morph) while the ones column turns (snappy, .45em).</summary>
internal sealed class RollingNumber : StackPanel
{
    private readonly List<(Grid Column, TextBlock Digit)> _places = [];
    private int _value = -1;

    public RollingNumber() { Orientation = Orientation.Horizontal; FlowDirection = FlowDirection.LeftToRight; }

    public void Set(int value, bool full)
    {
        if (value == _value) return;
        int direction = value > _value ? 1 : -1;
        bool animate = full && _value >= 0;
        _value = value;
        string digits = value.ToString(CultureInfo.InvariantCulture);
        double em = TextElement.GetFontSize(this);
        while (_places.Count < digits.Length)
        {
            var column = new Grid();
            var digit = new TextBlock { RenderTransform = new TranslateTransform() };
            column.Children.Add(digit);
            _places.Add((column, digit));
            Children.Insert(0, column);
            if (animate) { column.Opacity = 0; Tween.To(column, OpacityProperty, 1, Tween.Fast, null); }
        }
        while (_places.Count > digits.Length)
        {
            var (column, _) = _places[^1];
            _places.RemoveAt(_places.Count - 1);
            if (!animate) { Children.Remove(column); continue; }
            column.ClipToBounds = true;
            column.Width = column.ActualWidth;
            Tween.To(column, OpacityProperty, 0, Tween.Instant, null);
            Tween.Spring(column, WidthProperty, 0, Tween.Smooth, done: () => Children.Remove(column));
        }
        for (int place = 0; place < digits.Length; place++)
        {
            var (column, digit) = _places[place];
            string next = digits[digits.Length - 1 - place].ToString();
            if (digit.Text == next) continue;
            if (!animate || digit.Text.Length == 0) { digit.Text = next; continue; }
            var old = new TextBlock { Text = digit.Text, RenderTransform = new TranslateTransform(), IsHitTestVisible = false };
            column.Children.Add(old);
            Tween.Present(old, false, -.45 * em * direction, 2, Tween.Fast, ArcEasing.Standard, done: () => column.Children.Remove(old));
            digit.Text = next;
            var move = (TranslateTransform)digit.RenderTransform;
            Tween.Present(digit, true, 0, 2, Tween.Fast, ArcEasing.Standard);
            Tween.Set(move, TranslateTransform.YProperty, .45 * em * direction);
            Tween.Spring(move, TranslateTransform.YProperty, 0, Tween.Snappy);
        }
    }
}
