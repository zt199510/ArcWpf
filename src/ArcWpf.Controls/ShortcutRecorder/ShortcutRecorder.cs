using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class ShortcutChangedEventArgs(RoutedEvent routedEvent, object source, string? shortcut, ShortcutBinding? replaced) : RoutedEventArgs(routedEvent, source)
{
    public string? Shortcut { get; } = shortcut;
    /// <summary>The binding the new shortcut was taken from after "Use anyway", so the caller can unbind it.</summary>
    public ShortcutBinding? Replaced { get; } = replaced;
}

/// <summary>
/// A field that records a keyboard shortcut (shortcut-recorder.tsx, frozen 2026-10-31). Press it, then press the combination: held modifiers
/// light up as caps while the chord is built, the finished shortcut settles into place, and a combination that is already taken asks before
/// it is used. Escape cancels, Backspace clears, and the reset button restores the default.
/// </summary>
[TemplatePart(Name = "PART_Control", Type = typeof(Border))]
[TemplatePart(Name = "PART_Recorder", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Row", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Leaving", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_Actions", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Message", Type = typeof(Grid))]
public class ShortcutRecorder : Control
{
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(nameof(Shortcut), typeof(string), typeof(ShortcutRecorder), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ShortcutRecorder)d).Render(true)));
    public static readonly DependencyProperty ResetValueProperty = DependencyProperty.Register(nameof(ResetValue), typeof(string), typeof(ShortcutRecorder), new PropertyMetadata(null, (d, _) => ((ShortcutRecorder)d).Render(true)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ShortcutRecorder), new PropertyMetadata(""));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ShortcutRecorder), new PropertyMetadata("Record shortcut", (d, _) => ((ShortcutRecorder)d).Render(false)));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(ShortcutRecorder), new PropertyMetadata("", (d, _) => ((ShortcutRecorder)d).Render(true)));
    public static readonly DependencyProperty BindingsProperty = DependencyProperty.Register(nameof(Bindings), typeof(IList<ShortcutBinding>), typeof(ShortcutRecorder), new PropertyMetadata(null));
    public static readonly DependencyProperty WarnReservedProperty = DependencyProperty.Register(nameof(WarnReserved), typeof(bool), typeof(ShortcutRecorder), new PropertyMetadata(true));
    public static readonly DependencyProperty RequireModifierProperty = DependencyProperty.Register(nameof(RequireModifier), typeof(bool), typeof(ShortcutRecorder), new PropertyMetadata(true));
    public static readonly DependencyProperty PlatformProperty = DependencyProperty.Register(nameof(Platform), typeof(ShortcutPlatform), typeof(ShortcutRecorder), new PropertyMetadata(ShortcutPlatform.Other, (d, _) => ((ShortcutRecorder)d).Render(true)));
    private static readonly DependencyPropertyKey IsRecordingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsRecording), typeof(bool), typeof(ShortcutRecorder), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsRecordingProperty = IsRecordingPropertyKey.DependencyProperty;
    public static readonly RoutedEvent ShortcutChangedEvent = EventManager.RegisterRoutedEvent(nameof(ShortcutChanged), RoutingStrategy.Bubble, typeof(EventHandler<ShortcutChangedEventArgs>), typeof(ShortcutRecorder));
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(ShortcutRecorder), new PropertyMetadata(null, (d, _) => ((ShortcutRecorder)d).UpdateControl(false)));

    /// <summary>The shortcut, such as "mod+k"; null or empty when none is set.</summary>
    public string? Shortcut { get => (string?)GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }
    /// <summary>What the reset button restores.</summary>
    public string? ResetValue { get => (string?)GetValue(ResetValueProperty); set => SetValue(ResetValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Shortcuts already in use. Recording one of them asks before taking it.</summary>
    public IList<ShortcutBinding>? Bindings { get => (IList<ShortcutBinding>?)GetValue(BindingsProperty); set => SetValue(BindingsProperty, value); }
    /// <summary>Also warn about combinations the system keeps, such as Ctrl+W and Ctrl+C.</summary>
    public bool WarnReserved { get => (bool)GetValue(WarnReservedProperty); set => SetValue(WarnReservedProperty, value); }
    /// <summary>Require Ctrl, Alt, or ⌘ (function keys are always allowed).</summary>
    public bool RequireModifier { get => (bool)GetValue(RequireModifierProperty); set => SetValue(RequireModifierProperty, value); }
    public ShortcutPlatform Platform { get => (ShortcutPlatform)GetValue(PlatformProperty); set => SetValue(PlatformProperty, value); }
    public bool IsRecording => (bool)GetValue(IsRecordingProperty);
    public event EventHandler<ShortcutChangedEventArgs> ShortcutChanged { add => AddHandler(ShortcutChangedEvent, value); remove => RemoveHandler(ShortcutChangedEvent, value); }

    private sealed record Pending(string Shortcut, ShortcutBinding Conflict, bool Reserved);

    private Border? _control;
    private System.Windows.Controls.Button? _recorder;
    private Panel? _row, _actions;
    private Canvas? _leaving;
    private FieldMessage? _message;
    private readonly SolidColorBrush _edge = new(Colors.Gray), _surface = new(Colors.White);
    private readonly Dictionary<string, FrameworkElement> _chips = [];
    private readonly DispatcherTimer _settle = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private FrameworkElement? _placeholder, _dot;
    private string? _placeholderKey;
    private FieldIconButton? _reset, _clear;
    private IReadOnlyList<string> _live = [];
    private Pending? _pending;
    private string? _nudge;
    private bool _settled;

    static ShortcutRecorder()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ShortcutRecorder), new FrameworkPropertyMetadata(typeof(ShortcutRecorder)));
        FocusableProperty.OverrideMetadata(typeof(ShortcutRecorder), new FrameworkPropertyMetadata(false));
    }

    public ShortcutRecorder()
    {
        _settle.Tick += (_, _) => { _settle.Stop(); _settled = false; Render(true); };
        SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border");
        Unloaded += (_, _) => _settle.Stop();
    }

    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;

    public override void OnApplyTemplate()
    {
        if (_recorder is not null)
        {
            _recorder.Click -= RecorderClick;
            _recorder.PreviewKeyDown -= RecorderKeyDown;
            _recorder.PreviewKeyUp -= RecorderKeyUp;
            _recorder.LostKeyboardFocus -= RecorderBlur;
        }
        base.OnApplyTemplate();
        _control = GetTemplateChild("PART_Control") as Border;
        _recorder = GetTemplateChild("PART_Recorder") as System.Windows.Controls.Button;
        _row = GetTemplateChild("PART_Row") as Panel;
        _leaving = GetTemplateChild("PART_Leaving") as Canvas;
        _actions = GetTemplateChild("PART_Actions") as Panel;
        var host = GetTemplateChild("PART_Message") as Grid;
        _chips.Clear();
        _placeholder = _dot = null;
        _placeholderKey = null;
        _reset = _clear = null;
        _row?.Children.Clear();
        _actions?.Children.Clear();
        host?.Children.Clear();
        _message = host is null ? null : new FieldMessage();
        if (_message is not null) host!.Children.Add(_message);
        if (_control is not null) { _control.BorderBrush = _edge; _control.Background = _surface; }
        if (_recorder is not null)
        {
            _recorder.Click += RecorderClick;
            _recorder.PreviewKeyDown += RecorderKeyDown;
            _recorder.PreviewKeyUp += RecorderKeyUp;
            _recorder.LostKeyboardFocus += RecorderBlur;
        }
        Render(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty || e.Property == IsKeyboardFocusWithinProperty || e.Property == IsEnabledProperty) UpdateControl(true);
    }

    /// <summary>Starts recording, as pressing the field does.</summary>
    public void StartRecording()
    {
        if (!IsEnabled) return;
        _pending = null;
        _nudge = null;
        _live = [];
        SetValue(IsRecordingPropertyKey, true);
        Announce("Recording. Press the new shortcut, or Escape to cancel.");
        _recorder?.Focus();
        Render(true);
    }

    private void Stop()
    {
        SetValue(IsRecordingPropertyKey, false);
        _live = [];
    }

    private void Commit(string? next, ShortcutBinding? replaced = null)
    {
        _pending = null;
        _nudge = null;
        SetCurrentValue(ShortcutProperty, next);
        RaiseEvent(new ShortcutChangedEventArgs(ShortcutChangedEvent, this, next, replaced));
        // The settled caps stay lit for 700 ms, then release.
        _settled = true;
        _settle.Stop();
        _settle.Start();
        Announce(string.IsNullOrEmpty(next) ? "Shortcut cleared" : $"Shortcut set to {Shortcuts.Format(next, Platform).Spoken}");
        Render(true);
    }

    private Pending? ConflictFor(string shortcut)
    {
        var normal = Shortcuts.Normalize(shortcut, Platform);
        if (!string.IsNullOrEmpty(Shortcut) && Shortcuts.Normalize(Shortcut, Platform) == normal) return null;
        var taken = Bindings?.FirstOrDefault(binding => Shortcuts.Normalize(binding.Shortcut, Platform) == normal);
        if (taken is not null) return new(shortcut, taken, false);
        var reserved = WarnReserved ? Shortcuts.Reserved.FirstOrDefault(binding => Shortcuts.Normalize(binding.Shortcut, Platform) == normal) : null;
        return reserved is null ? null : new(shortcut, reserved, true);
    }

    // A mouse press while recording cancels; keyboard presses never reach Click while recording because the chord owns the keys.
    private void RecorderClick(object sender, RoutedEventArgs e)
    {
        if (!IsRecording) { StartRecording(); return; }
        Stop();
        Announce("Recording canceled");
        Render(true);
    }

    private void RecorderBlur(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!IsRecording) return;
        Stop();
        _nudge = null;
        Render(true);
    }

    private void RecorderKeyDown(object sender, KeyEventArgs e)
    {
        var key = Shortcuts.RealKey(e);
        var modifiers = Keyboard.Modifiers;
        if (!IsRecording)
        {
            if (key is Key.Back or Key.Delete && !string.IsNullOrEmpty(Shortcut)) { e.Handled = true; Commit(null); }
            return;
        }
        if (key == Key.Tab && modifiers == ModifierKeys.None) { Stop(); Render(true); return; }
        // The chord belongs to the recorder: keep it from the page's own shortcuts, such as a Ctrl+K command menu.
        e.Handled = true;
        bool bare = modifiers == ModifierKeys.None;
        if (bare && key == Key.Escape) { Stop(); Announce("Recording canceled"); Render(true); return; }
        if (bare && key is Key.Back or Key.Delete) { Stop(); Commit(null); return; }
        var shortcut = Shortcuts.FromKey(key, modifiers, Platform);
        if (shortcut is null) { _live = Shortcuts.LiveModifiers(modifiers, Platform); Render(true); return; }
        if (RequireModifier && !Shortcuts.HasStrongModifier(shortcut))
        {
            _nudge = Platform == ShortcutPlatform.Mac ? "Include ⌘, ⌃, or ⌥" : "Include Ctrl or Alt";
            Announce(_nudge);
            _live = Shortcuts.LiveModifiers(modifiers, Platform);
            Render(true);
            return;
        }
        Stop();
        var conflict = ConflictFor(shortcut);
        if (conflict is not null)
        {
            _pending = conflict;
            Announce($"{Shortcuts.Format(shortcut, Platform).Spoken} is {(conflict.Reserved ? "reserved for" : "used by")} {conflict.Conflict.Label}. Use anyway, or record another.");
            Render(true);
            return;
        }
        Commit(shortcut);
    }

    private void RecorderKeyUp(object sender, KeyEventArgs e)
    {
        if (!IsRecording) return;
        e.Handled = true;
        _live = Shortcuts.LiveModifiers(Keyboard.Modifiers, Platform);
        Render(true);
    }

    private void Announce(string message) { if (_recorder is not null) Announcer.Say(_recorder, message); }

    private string? RestoreTo => string.IsNullOrEmpty(ResetValue) ? null : ResetValue;
    private string? Current => string.IsNullOrEmpty(Shortcut) ? null : Shortcut;

    private void Render(bool animate)
    {
        if (_recorder is null) return;
        animate &= IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;
        string? shown = _pending?.Shortcut ?? Current;
        var tokens = IsRecording ? Shortcuts.Tokens(string.Join("+", _live), Platform) : shown is not null ? Shortcuts.Tokens(shown, Platform) : [];
        RenderRow(tokens, animate);
        bool canReset = !IsRecording && (_pending is not null || Current != RestoreTo);
        bool canClear = !IsRecording && Current is not null && _pending is null;
        RenderActions(canReset, canClear, animate);
        RenderMessage(animate);
        UpdateControl(animate);
        AutomationProperties.SetName(_recorder, $"{Label} {(IsRecording ? "Recording" : shown is not null ? Shortcuts.Format(shown, Platform).Spoken : Placeholder)}".Trim());
        AutomationProperties.SetItemStatus(_recorder, IsRecording ? "Recording" : "");
    }

    // .control: border-strong; idle hover or keyboard focus foreground; recording accent over a 4% accent wash; conflict warning (.16 standard).
    private void UpdateControl(bool animate)
    {
        animate &= Full;
        string edge = IsRecording ? "Accent" : _pending is not null ? "Warning" : IsKeyboardFocusWithin || (IsMouseOver && IsEnabled) ? "Foreground" : "BorderStrong";
        string surface = IsRecording ? "RecorderSurface" : IsEnabled ? "Surface" : "SurfaceMuted";
        Tween.Color(_edge, Tween.Resolve(this, $"ArcWpf.Brush.{edge}"), Tween.Fast, animate);
        Tween.Color(_surface, Tween.Resolve(this, $"ArcWpf.Brush.{surface}"), Tween.Fast, animate);
        if (_control is not null) _control.Opacity = IsEnabled ? 1 : .5;
    }

    private void RenderRow(IReadOnlyList<ShortcutToken> tokens, bool animate)
    {
        if (_row is null) return;
        bool full = animate && Full;
        var before = full ? Flip.Capture(_row) : [];
        // The recording light: a slow 1.4 s breath, steady with reduced motion.
        if (IsRecording && _dot is null)
        {
            var dot = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(3, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, RenderTransform = new TranslateTransform() };
            dot.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Accent");
            if (Full) dot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, .35, TimeSpan.FromSeconds(.7)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ArcEasing.InOut });
            _dot = dot;
            _row.Children.Insert(0, dot);
        }
        else if (!IsRecording && _dot is not null) { _row.Children.Remove(_dot); _dot = null; }

        bool lit = IsRecording || (_settled && _pending is null);
        var ids = tokens.Select(token => token.Id).ToList();
        foreach (var id in _chips.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            var chip = _chips[id];
            _chips.Remove(id);
            Leave(chip, full, animate, chipExit: true);
        }
        for (int index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (_chips.TryGetValue(token.Id, out var existing))
            {
                var kbd = (ShortcutKbd)((Border)existing).Child;
                kbd.Text = token.Label;
                kbd.IsPressed = lit;
                kbd.IsWarning = _pending is not null;
                int at = _row.Children.IndexOf(existing), want = index + (_dot is null ? 0 : 1);
                if (at != want) { _row.Children.RemoveAt(at); _row.Children.Insert(want, existing); }
                continue;
            }
            var cap = new ShortcutKbd { Text = token.Label, IsPressed = lit, IsWarning = _pending is not null };
            var slot = new Border { Child = cap, Margin = new Thickness(0, 0, 4, 0), RenderTransform = new TranslateTransform(), RenderTransformOrigin = new Point(.5, .5) };
            _chips[token.Id] = slot;
            _row.Children.Insert(index + (_dot is null ? 0 : 1), slot);
            if (full) EnterChip(slot, IsRecording ? 0 : index * .035);
            else if (animate) { slot.Opacity = 0; Tween.To(slot, OpacityProperty, 1, .1, null); }
        }

        string? key = tokens.Count == 0 ? IsRecording ? "prompt" : "placeholder" : null;
        if (key != _placeholderKey)
        {
            if (_placeholder is not null) { var old = _placeholder; _placeholder = null; Leave(old, full, animate, chipExit: false); }
            _placeholderKey = key;
            if (key is not null)
            {
                var text = new TextBlock { Text = IsRecording ? "Press keys" : Placeholder, Padding = new Thickness(3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, RenderTransform = new TranslateTransform() };
                text.SetResourceReference(TextBlock.ForegroundProperty, IsRecording ? "ArcWpf.Brush.TextSecondary" : "ArcWpf.Brush.TextMuted");
                _placeholder = text;
                _row.Children.Add(text);
                if (full) Tween.Present(text, true, 4, 2, Tween.Fast, ArcEasing.Enter);
                else if (animate) Tween.Present(text, true, 0, 0, .1, ArcEasing.Standard);
            }
        }
        else if (_placeholder is TextBlock placeholder && key == "placeholder") placeholder.Text = Placeholder;
        if (full) Flip.Play(_row, before, Tween.Snappy);
    }

    // enter: scale .72, y 4, subtle blur → rest on the snappy spring (opacity .14, blur .16), staggered .035 s per cap after a recording.
    private static void EnterChip(FrameworkElement slot, double delay)
    {
        var scale = new ScaleTransform(.72, .72);
        var move = new TranslateTransform(0, 4);
        slot.RenderTransform = new TransformGroup { Children = { scale, move } };
        slot.Opacity = 0;
        Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy, delay);
        Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy, delay);
        Tween.Spring(move, TranslateTransform.YProperty, 0, Tween.Snappy, delay, () =>
        {
            // Hand the slot back to a plain translate so later reorders can glide (layout="position").
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            slot.RenderTransform = new TranslateTransform();
        });
        Tween.To(slot, OpacityProperty, 1, .14, ArcEasing.Standard, delay);
        var blur = new BlurEffect { Radius = 2 };
        slot.Effect = blur;
        var clear = new DoubleAnimation(0, TimeSpan.FromSeconds(.16)) { BeginTime = TimeSpan.FromSeconds(delay) };
        clear.Completed += (_, _) => { if (ReferenceEquals(slot.Effect, blur)) slot.Effect = null; };
        blur.BeginAnimation(BlurEffect.RadiusProperty, clear);
    }

    // popLayout: a leaving element is lifted out of the row where it stood, so the rest close up at once while it fades.
    private void Leave(FrameworkElement element, bool full, bool animate, bool chipExit)
    {
        if (_row is null) return;
        if (!animate || _leaving is null || !element.IsLoaded) { _row.Children.Remove(element); return; }
        var at = element.TranslatePoint(new Point(), _leaving);
        _row.Children.Remove(element);
        element.RenderTransform = new ScaleTransform(1, 1, element.ActualWidth / 2, element.ActualHeight / 2);
        Canvas.SetLeft(element, at.X);
        Canvas.SetTop(element, at.Y);
        element.IsHitTestVisible = false;
        _leaving.Children.Add(element);
        double seconds = chipExit ? (full ? .1 : .06) : .08;
        Tween.To(element, OpacityProperty, 0, seconds, null, done: () => _leaving.Children.Remove(element));
        if (chipExit && full)
        {
            var scale = (ScaleTransform)element.RenderTransform;
            Tween.To(scale, ScaleTransform.ScaleXProperty, .8, seconds, null);
            Tween.To(scale, ScaleTransform.ScaleYProperty, .8, seconds, null);
            element.Effect = new BlurEffect { Radius = 2 };
        }
    }

    private void RenderActions(bool canReset, bool canClear, bool animate)
    {
        if (_actions is null) return;
        string restore = RestoreTo is null ? "none" : Shortcuts.Format(RestoreTo, Platform).Spoken;
        _reset = Toggle(_reset, canReset, 0, Lucide.RotateCcw, $"Reset to {restore}", () => { Commit(RestoreTo); _recorder?.Focus(); }, animate);
        if (_reset is not null) AutomationProperties.SetName(_reset, $"Reset to {restore}");
        _clear = Toggle(_clear, canClear, _reset is null ? 0 : 1, Lucide.X, "Clear shortcut", () => { Commit(null); _recorder?.Focus(); }, animate);
    }

    // Action buttons pop in from scale .6 on the snappy spring and leave in .1 s.
    private FieldIconButton? Toggle(FieldIconButton? button, bool show, int index, string glyph, string name, Action run, bool animate)
    {
        if (_actions is null) return button;
        bool full = animate && Full;
        if (show && button is null)
        {
            var created = new FieldIconButton(glyph, 16, 32, 13) { RestInk = "TextMuted", Margin = new Thickness(2, 0, 0, 0), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1) };
            AutomationProperties.SetName(created, name);
            created.Click += (_, _) => run();
            _actions.Children.Insert(Math.Min(index, _actions.Children.Count), created);
            if (full)
            {
                var scale = (ScaleTransform)created.RenderTransform;
                scale.ScaleX = scale.ScaleY = .6;
                created.Opacity = 0;
                Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
                Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
                Tween.Spring(created, OpacityProperty, 1, Tween.Snappy);
            }
            return created;
        }
        if (!show && button is not null)
        {
            button.IsHitTestVisible = false;
            button.Focusable = false;
            if (!full) { _actions.Children.Remove(button); return null; }
            var scale = (ScaleTransform)button.RenderTransform;
            Tween.To(scale, ScaleTransform.ScaleXProperty, .6, .1, null);
            Tween.To(scale, ScaleTransform.ScaleYProperty, .6, .1, null);
            Tween.To(button, OpacityProperty, 0, .1, null, done: () => _actions.Children.Remove(button));
            return null;
        }
        return button;
    }

    private void RenderMessage(bool animate)
    {
        if (_message is null) return;
        if (_pending is { } pending)
        {
            _message.Show($"c-{pending.Shortcut}", ConflictMessage(pending), animate);
            return;
        }
        string? text = _nudge ?? (IsRecording ? "Esc to cancel, Backspace to clear" : string.IsNullOrEmpty(Description) ? null : Description);
        string key = _nudge is not null ? $"n-{_nudge}" : IsRecording ? "rec" : "d";
        if (text is null) { _message.Hide(animate); return; }
        _message.Show(key, FieldMessage.Line(text, "ArcWpf.Brush.TextMuted"), animate);
    }

    // Warning tone: secondary copy behind a 6 DIP warning dot, the binding in foreground 500, then "Use anyway" and a quiet "Cancel".
    private FrameworkElement ConflictMessage(Pending pending)
    {
        var line = new DockPanel { Margin = new Thickness(0, 8, 0, 0), LastChildFill = false };
        line.SetValue(TextElement.FontSizeProperty, 12d);
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, VerticalAlignment = VerticalAlignment.Center };
        text.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        var dot = new Ellipse { Width = 6, Height = 6, Margin = new Thickness(1, 0, 7, 1) };
        dot.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Warning");
        text.Inlines.Add(new InlineUIContainer(dot) { BaselineAlignment = BaselineAlignment.Center });
        text.Inlines.Add(new Run(pending.Reserved ? "Reserved for " : "Already used by "));
        var strong = new Run(pending.Conflict.Label) { FontWeight = FontWeights.Medium };
        strong.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.Foreground");
        text.Inlines.Add(strong);
        DockPanel.SetDock(text, Dock.Left);
        line.Children.Add(text);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(actions, Dock.Right);
        actions.Children.Add(LinkButton("Use anyway", false, () => { var taken = pending; Commit(taken.Shortcut, taken.Reserved ? null : taken.Conflict); _recorder?.Focus(); }));
        actions.Children.Add(LinkButton("Cancel", true, () => { _pending = null; Announce("Kept the previous shortcut"); Render(true); _recorder?.Focus(); }));
        line.Children.Add(actions);
        return line;
    }

    private static System.Windows.Controls.Button LinkButton(string text, bool quiet, Action run)
    {
        var label = new TextBlock { Text = text, FontWeight = quiet ? FontWeights.Normal : FontWeights.Medium };
        var underline = new TextDecoration { Location = TextDecorationLocation.Underline, PenOffset = 3 };
        underline.Pen = new Pen(Brushes.Gray, 1);
        if (!quiet) label.TextDecorations = [underline];
        var button = new System.Windows.Controls.Button { Content = label, Cursor = Cursors.Hand, FocusVisualStyle = null, Margin = new Thickness(12, 0, 0, 0), Template = LinkTemplate };
        void Paint()
        {
            bool hot = button.IsMouseOver || button.IsKeyboardFocused;
            label.SetResourceReference(TextBlock.ForegroundProperty, quiet && !hot ? "ArcWpf.Brush.TextMuted" : "ArcWpf.Brush.Foreground");
            underline.Pen = new Pen(hot ? label.Foreground : (Brush?)button.TryFindResource("ArcWpf.Brush.BorderStrong") ?? Brushes.Gray, 1);
            if (!quiet) label.TextDecorations = [underline];
        }
        button.MouseEnter += (_, _) => Paint();
        button.MouseLeave += (_, _) => Paint();
        button.GotKeyboardFocus += (_, _) => Paint();
        button.LostKeyboardFocus += (_, _) => Paint();
        button.Loaded += (_, _) => Paint();
        button.Click += (_, _) => run();
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static readonly ControlTemplate LinkTemplate = CreateLinkTemplate();
    private static ControlTemplate CreateLinkTemplate()
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        template.Seal();
        return template;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
