using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

// tag-input.tsx (frozen 2026-10-31): tags keep the field still. A new tag blurs in where its text was typed while the caret glides aside,
// a removed tag leaves its slot and the rest glide in, and the shell follows wrapped rows on a spring. Backspace or the arrow keys pick a
// tag first, a ring glides to it, and the next Backspace removes it.
[TemplatePart(Name = "PART_Shell", Type = typeof(Border))]
[TemplatePart(Name = "PART_Content", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Tags", Type = typeof(WrapPanel))]
[TemplatePart(Name = "PART_Input", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Placeholder", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Ring", Type = typeof(Border))]
[TemplatePart(Name = "PART_Message", Type = typeof(Grid))]
public class TagInput : Control
{
    public static readonly DependencyProperty TagsProperty = DependencyProperty.Register(nameof(Tags), typeof(IList<string>), typeof(TagInput), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((TagInput)d).Sync(true)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TagInput), new PropertyMetadata(""));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(TagInput), new PropertyMetadata("Add a tag"));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(TagInput), new FrameworkPropertyMetadata("", (d, _) => ((TagInput)d).UpdateMessage(true)));
    public static readonly RoutedEvent TagsChangedEvent = EventManager.RegisterRoutedEvent(nameof(TagsChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TagInput));

    /// <summary>Tags as plain strings. The control replaces the list on every change.</summary>
    public IList<string>? Tags { get => (IList<string>?)GetValue(TagsProperty); set => SetValue(TagsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public event RoutedEventHandler TagsChanged { add => AddHandler(TagsChangedEvent, value); remove => RemoveHandler(TagsChangedEvent, value); }

    private Border? _shell, _ring;
    private Grid? _content, _messageHost;
    private WrapPanel? _panel;
    private TextBox? _input;
    private TextBlock? _placeholder;
    private PasswordMessage? _message;
    private readonly Canvas _leaving = new() { IsHitTestVisible = false };
    private readonly Dictionary<string, TagChip> _chips = [];
    private string? _picked;
    private Rect? _ringAt;
    private double _contentHeight;
    private readonly SolidColorBrush _shellInk = new(Colors.Gray);
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(TagInput), new PropertyMetadata(null, (d, _) => ((TagInput)d).UpdateShell(false)));

    static TagInput()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TagInput), new FrameworkPropertyMetadata(typeof(TagInput)));
        FocusableProperty.OverrideMetadata(typeof(TagInput), new FrameworkPropertyMetadata(false));
    }

    public TagInput() { SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border"); }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty || e.Property == IsKeyboardFocusWithinProperty) UpdateShell(true);
    }

    // .control: border, hover (not focused) border-strong, focus-within foreground, over .16 standard.
    private void UpdateShell(bool animate)
    {
        string key = IsKeyboardFocusWithin ? "Foreground" : IsMouseOver ? "BorderStrong" : "Border";
        Tween.Color(_shellInk, Tween.Resolve(this, $"ArcWpf.Brush.{key}"), Tween.Fast, animate && Full);
    }

    private IList<string> Current => Tags ?? [];
    private bool Animating => IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;
    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    internal string? Picked => _picked;

    public override void OnApplyTemplate()
    {
        if (_input is not null) { _input.PreviewKeyDown -= InputKeyDown; _input.TextChanged -= InputChanged; _input.LostKeyboardFocus -= InputBlur; }
        if (_content is not null) { _content.SizeChanged -= ContentResized; _content.MouseLeftButtonDown -= ContentDown; }
        (_leaving.Parent as Panel)?.Children.Remove(_leaving);
        _message?.Detach();
        base.OnApplyTemplate();
        _shell = GetTemplateChild("PART_Shell") as Border;
        if (_shell is not null) _shell.BorderBrush = _shellInk;
        UpdateShell(false);
        _content = GetTemplateChild("PART_Content") as Grid;
        _panel = GetTemplateChild("PART_Tags") as WrapPanel;
        _input = GetTemplateChild("PART_Input") as TextBox;
        _placeholder = GetTemplateChild("PART_Placeholder") as TextBlock;
        _ring = GetTemplateChild("PART_Ring") as Border;
        _messageHost = GetTemplateChild("PART_Message") as Grid;
        if (_input is not null)
        {
            _input.PreviewKeyDown += InputKeyDown; _input.TextChanged += InputChanged; _input.LostKeyboardFocus += InputBlur;
            _input.RenderTransform = new TranslateTransform();
        }
        if (_content is not null) { _content.SizeChanged += ContentResized; _content.MouseLeftButtonDown += ContentDown; _content.Children.Add(_leaving); }
        if (_messageHost is not null) { _message = new PasswordMessage(Tween.Instant); _messageHost.Children.Add(_message); }
        _chips.Clear();
        Sync(false);
        UpdateMessage(false);
    }

    private void UpdateMessage(bool animate) => _message?.SetText(Description ?? "", animate);

    /// <summary>Adds the typed draft as a tag. A duplicate (ignoring case) pulses the tag already there instead.</summary>
    public bool Add(string text)
    {
        string tag = text.Trim();
        if (tag.Length == 0) return false;
        string? existing = Current.FirstOrDefault(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (Full && _chips.TryGetValue(existing, out var chip)) chip.Pulse();
            Announcer.Say(this, $"{existing} is already added");
            return false;
        }
        _picked = null;
        if (_input is not null) _input.Text = "";
        Update([.. Current, tag]);
        Announcer.Say(this, $"Added {tag}");
        return true;
    }

    public void Remove(string tag)
    {
        _picked = null;
        Update(Current.Where(t => t != tag).ToList());
        Announcer.Say(this, $"Removed {tag}");
        _input?.Focus();
    }

    private void Update(IList<string> next)
    {
        SetCurrentValue(TagsProperty, next);
        RaiseEvent(new RoutedEventArgs(TagsChangedEvent, this));
    }

    internal void Pick(string? tag)
    {
        _picked = tag;
        UpdateRing(Animating);
        foreach (var chip in _chips.Values) chip.SetPicked(chip.Value == tag);
        if (tag is not null) Announcer.Say(this, $"{tag} selected. Press Backspace to remove it.");
    }

    private void Sync(bool animate)
    {
        if (_panel is null || _input is null) return;
        animate &= Animating;
        bool full = animate && Full;
        var tags = Current;
        if (_picked is not null && !tags.Contains(_picked)) _picked = null;
        var before = Flip.Capture(_panel);
        foreach (var pair in _chips.Where(p => !tags.Contains(p.Key)).ToArray())
        {
            _chips.Remove(pair.Key);
            var chip = pair.Value;
            bool had = before.TryGetValue(chip, out var at);
            _panel.Children.Remove(chip);
            // A removed tag leaves its slot at once (popLayout) and fades out with a shrink and a subtle blur (.12 standard).
            if (!animate || !had) continue;
            var origin = _panel.TranslatePoint(new Point(), _content);
            Canvas.SetLeft(chip, origin.X + at.X); Canvas.SetTop(chip, origin.Y + at.Y);
            _leaving.Children.Add(chip);
            chip.Exit(full, () => _leaving.Children.Remove(chip));
        }
        _panel.Children.Remove(_input);
        foreach (var tag in tags)
        {
            if (!_chips.TryGetValue(tag, out var chip))
            {
                chip = new TagChip(this, tag);
                _chips[tag] = chip;
                if (animate) chip.Enter(full);
                _panel.Children.Add(chip);
            }
            else if (!_panel.Children.Contains(chip)) _panel.Children.Add(chip);
            chip.SetPicked(tag == _picked);
        }
        // Keep tag order: re-append in list order, then the input last.
        var ordered = tags.Select(t => (UIElement)_chips[t]).ToList();
        _panel.Children.Clear();
        foreach (var element in ordered) _panel.Children.Add(element);
        _panel.Children.Add(_input);
        if (full) Flip.Play(_panel, before, Tween.Morph);
        UpdatePlaceholder(animate);
        UpdateRing(animate);
    }

    // The visible placeholder rises in when the last tag leaves; the native one stays for assistive tech.
    private void UpdatePlaceholder(bool animate)
    {
        if (_placeholder is null || _input is null) return;
        bool show = _input.Text.Length == 0 && Current.Count == 0;
        bool shown = _placeholder.Visibility == Visibility.Visible;
        if (show == shown) return;
        if (!show) { _placeholder.BeginAnimation(OpacityProperty, null); _placeholder.Visibility = Visibility.Collapsed; return; }
        _placeholder.Visibility = Visibility.Visible;
        if (!animate) { Tween.Set(_placeholder, OpacityProperty, 1); return; }
        if (Full) Tween.Present(_placeholder, true, .3 * 14, 4, Tween.Standard, ArcEasing.Enter);
        else Tween.Present(_placeholder, true, 0, 0, Tween.Instant, ArcEasing.Enter);
    }

    // The ring measures the picked tag's resting box, glides between picks (morph), and fades in place when the pick clears.
    private void UpdateRing(bool animate)
    {
        if (_ring is null || _panel is null || _content is null) return;
        bool full = animate && Full;
        if (_picked is null || !_chips.TryGetValue(_picked, out var chip))
        {
            if (_ringAt is not null)
            {
                if (full) { Tween.To(_ring, OpacityProperty, 0, Tween.Instant, ArcEasing.Standard); Scale(.9, true, Tween.Instant); }
                else Tween.Set(_ring, OpacityProperty, 0);
            }
            _ringAt = null;
            return;
        }
        _panel.UpdateLayout();
        var at = chip.TranslatePoint(new Point(), _content);
        var move = (TranslateTransform)chip.RenderTransform;
        var box = new Rect(at.X - move.X, at.Y - move.Y, chip.ActualWidth, chip.ActualHeight);
        if (_ringAt == box) return;
        var place = (TranslateTransform)((TransformGroup)_ring.RenderTransform).Children[1];
        bool glide = full && (_ringAt is not null || _ring.Opacity > .02);
        _ringAt = box;
        if (glide)
        {
            Tween.Spring(place, TranslateTransform.XProperty, box.X, Tween.Morph);
            Tween.Spring(place, TranslateTransform.YProperty, box.Y, Tween.Morph);
            Tween.Spring(_ring, WidthProperty, box.Width, Tween.Morph);
            Tween.Spring(_ring, HeightProperty, box.Height, Tween.Morph);
            Tween.To(_ring, OpacityProperty, 1, Tween.Fast, ArcEasing.Standard);
            Scale(1, true, Tween.Snappy);
            return;
        }
        Tween.Set(place, TranslateTransform.XProperty, box.X);
        Tween.Set(place, TranslateTransform.YProperty, box.Y);
        Tween.Set(_ring, WidthProperty, box.Width);
        Tween.Set(_ring, HeightProperty, box.Height);
        if (animate)
        {
            Tween.Set(_ring, OpacityProperty, 0);
            Tween.To(_ring, OpacityProperty, 1, full ? Tween.Fast : Tween.Instant, ArcEasing.Standard);
            if (full) { Scale(.9, false, 0); Scale(1, true, Tween.Snappy); }
        }
        else Tween.Set(_ring, OpacityProperty, 1);
    }

    private void Scale(double to, bool animate, double seconds)
    {
        var scale = (ScaleTransform)((TransformGroup)_ring!.RenderTransform).Children[0];
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            if (!animate) Tween.Set(scale, property, to);
            else if (to == 1) Tween.Spring(scale, property, to, seconds);
            else Tween.To(scale, property, to, seconds, ArcEasing.Standard);
        }
    }

    // The shell animates its height to the wrapping rows on the smooth spring instead of jumping.
    private void ContentResized(object sender, SizeChangedEventArgs e)
    {
        if (_shell is null || !e.HeightChanged) return;
        double from = _contentHeight;
        _contentHeight = e.NewSize.Height;
        if (from <= 0 || !Full) return;
        var current = _shell.HasAnimatedProperties ? _shell.ActualHeight : from + _shell.BorderThickness.Top + _shell.BorderThickness.Bottom;
        double to = e.NewSize.Height + _shell.BorderThickness.Top + _shell.BorderThickness.Bottom;
        _shell.BeginAnimation(HeightProperty, new DoubleAnimation(current, to, TimeSpan.FromSeconds(Tween.Smooth * 2)) { EasingFunction = ArcEasing.Spring(Tween.Smooth, Tween.Smooth * 2), FillBehavior = FillBehavior.Stop });
    }

    private void ContentDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == _content || e.OriginalSource == _panel) { _input?.Focus(); e.Handled = true; }
    }

    private void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (_picked is not null) Pick(null);
        UpdatePlaceholder(Animating);
    }

    private void InputBlur(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_input is null || IsKeyboardFocusWithin) return;
        Add(_input.Text);
        if (_picked is not null) Pick(null);
    }

    private void InputKeyDown(object sender, KeyEventArgs e)
    {
        if (_input is null) return;
        var tags = Current;
        bool atStart = _input.SelectionStart == 0 && _input.SelectionLength == 0;
        int index = _picked is null ? tags.Count : tags.IndexOf(_picked);
        bool handled = true;
        if (e.Key == Key.Enter || e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.None) Add(_input.Text);
        else if ((e.Key == Key.Back || e.Key == Key.Delete) && _picked is not null) Remove(_picked);
        else if (e.Key == Key.Back && atStart && tags.Count > 0) Pick(tags[^1]);
        else if (e.Key == Key.Left && (atStart || _picked is not null) && index > 0) Pick(tags[index - 1]);
        else if (e.Key == Key.Right && _picked is not null) Pick(index + 1 < tags.Count ? tags[index + 1] : null);
        else if (e.Key == Key.Escape && _picked is not null) Pick(null);
        else handled = false;
        e.Handled = handled;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TagInputPeer(this);

    private sealed class TagInputPeer(TagInput owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(TagInput);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.Label;
        protected override string GetHelpTextCore() => owner.Description ?? "";
    }
}

/// <summary>A pill: label plus a 22 DIP round remove button. It enters at scale .9 with a soft blur and glides on the morph spring.</summary>
internal sealed class TagChip : Border
{
    private readonly TagInput _owner;
    private readonly Grid _scaleHost = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly FieldIconButton _remove;

    public TagChip(TagInput owner, string tag)
    {
        _owner = owner;
        Value = tag;
        RenderTransform = new TranslateTransform();
        var pill = new Border { MinHeight = 28, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(9, 0, 4, 0) };
        pill.SetResourceReference(BorderBrushProperty, "ArcWpf.Brush.Border");
        pill.SetResourceReference(BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var label = new TextBlock { Text = tag, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
        label.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        // .tag button: 22 round, text-secondary; hover/focus-visible surface + foreground; press .96 (instant), spring release.
        _remove = new FieldIconButton(Lucide.X, 14, 22, 11, 2) { Margin = new Thickness(3, 0, 0, 0), HoverFill = "Surface", PressScale = .96 };
        System.Windows.Automation.AutomationProperties.SetName(_remove, $"Remove {tag}");
        _remove.Click += (_, _) => owner.Remove(tag);
        row.Children.Add(label);
        row.Children.Add(_remove);
        pill.Child = row;
        _scaleHost.Children.Add(pill);
        _scaleHost.RenderTransform = _scale;
        _scaleHost.RenderTransformOrigin = new Point(.5, .5);
        Child = _scaleHost;
        Margin = new Thickness(0, 0, 6, 6);
        Cursor = Cursors.Arrow;
        // A click on a tag picks it without taking focus from the field. The remove button keeps its own press.
        MouseLeftButtonDown += (_, e) =>
        {
            if (IsWithinRemove(e.OriginalSource as DependencyObject)) return;
            owner.Pick(owner.Picked == tag ? null : tag);
            e.Handled = true;
        };
    }

    public string Value { get; }

    private bool IsWithinRemove(DependencyObject? node)
    {
        for (; node is not null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node)) if (ReferenceEquals(node, _remove)) return true;
        return false;
    }

    // .tag[data-picked] button { color: foreground }
    public void SetPicked(bool picked) { _remove.RestInk = picked ? "Foreground" : "TextSecondary"; _remove.Refresh(false); }

    // opacity .16 enter, scale on the morph spring, blur 4 → 0 over .24 enter. Reduced fades .12.
    public void Enter(bool full)
    {
        Opacity = 0;
        Tween.To(this, OpacityProperty, 1, full ? Tween.Fast : Tween.Instant, ArcEasing.Enter);
        if (!full) return;
        _scale.ScaleX = _scale.ScaleY = .9;
        Tween.Spring(_scale, ScaleTransform.ScaleXProperty, 1, Tween.Morph);
        Tween.Spring(_scale, ScaleTransform.ScaleYProperty, 1, Tween.Morph);
        var blur = new BlurEffect { Radius = 4 };
        _scaleHost.Effect = blur;
        var clear = new DoubleAnimation(0, TimeSpan.FromSeconds(Tween.Standard)) { EasingFunction = ArcEasing.Enter };
        clear.Completed += (_, _) => { if (ReferenceEquals(_scaleHost.Effect, blur)) _scaleHost.Effect = null; };
        blur.BeginAnimation(BlurEffect.RadiusProperty, clear);
    }

    public void Exit(bool full, Action done)
    {
        IsHitTestVisible = false;
        Tween.To(this, OpacityProperty, 0, full ? Tween.Instant : .001, ArcEasing.Standard, done: done);
        if (!full) return;
        Tween.To(_scale, ScaleTransform.ScaleXProperty, .9, Tween.Instant, ArcEasing.Standard);
        Tween.To(_scale, ScaleTransform.ScaleYProperty, .9, Tween.Instant, ArcEasing.Standard);
        var blur = new BlurEffect { Radius = 0 };
        _scaleHost.Effect = blur;
        blur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(2, TimeSpan.FromSeconds(Tween.Instant)) { EasingFunction = ArcEasing.Standard });
    }

    // A duplicate pulses the tag that already exists: scale 1 → 1.06 → 1 over .36 s (ease in-out).
    public void Pulse()
    {
        var frames = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(Tween.Standard + Tween.Instant) };
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(1.06, KeyTime.FromPercent(.5), ArcEasing.InOut));
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1), ArcEasing.InOut));
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, frames);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, frames);
    }
}
