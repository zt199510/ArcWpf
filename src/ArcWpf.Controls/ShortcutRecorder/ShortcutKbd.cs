using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>
/// A key cap: a one pixel edge with a slightly heavier bottom, so it reads as a key without a shadow. Held, it drops a pixel and takes the
/// accent (90 ms); a warning tone marks a shortcut that is already taken.
/// </summary>
public class ShortcutKbd : Border
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(ShortcutKbd), new PropertyMetadata("", (d, e) => ((ShortcutKbd)d)._text.Text = (string)e.NewValue));
    public static readonly DependencyProperty IsPressedProperty = DependencyProperty.Register(nameof(IsPressed), typeof(bool), typeof(ShortcutKbd), new PropertyMetadata(false, (d, _) => ((ShortcutKbd)d).Update(true)));
    public static readonly DependencyProperty IsWarningProperty = DependencyProperty.Register(nameof(IsWarning), typeof(bool), typeof(ShortcutKbd), new PropertyMetadata(false, (d, _) => ((ShortcutKbd)d).Update(true)));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(ControlSize), typeof(ShortcutKbd), new PropertyMetadata(ControlSize.Medium, (d, _) => ((ShortcutKbd)d).ApplySize()));
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(ShortcutKbd), new PropertyMetadata(null, (d, _) => ((ShortcutKbd)d).Update(false)));

    private readonly TextBlock _text = new() { FontSize = 12, FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
    private readonly SolidColorBrush _edge = new(Colors.Gray), _fill = new(Colors.White), _ink = new(Colors.Black);
    private readonly TranslateTransform _drop = new();

    public ShortcutKbd()
    {
        Child = _text;
        BorderBrush = _edge;
        Background = _fill;
        _text.Foreground = _ink;
        RenderTransform = _drop;
        SnapsToDevicePixels = true;
        VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetIsOffscreenBehavior(this, IsOffscreenBehavior.Default);
        ApplySize();
        SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border");
        SetResourceReference(TextElement.FontFamilyProperty, "ArcWpf.Font.Body");
        Loaded += (_, _) => Update(false);
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    /// <summary>Draws the key pushed down and lit, such as while it is held.</summary>
    public bool IsPressed { get => (bool)GetValue(IsPressedProperty); set => SetValue(IsPressedProperty, value); }
    public bool IsWarning { get => (bool)GetValue(IsWarningProperty); set => SetValue(IsWarningProperty, value); }
    /// <summary>Medium is 26 DIP with an 8 DIP radius; Small is 22 with 7.</summary>
    public ControlSize Size { get => (ControlSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private void ApplySize()
    {
        bool small = Size == ControlSize.Small;
        MinWidth = small ? 22 : 26;
        Height = small ? 22 : 26;
        CornerRadius = new CornerRadius(small ? 7 : 8);
        Padding = new Thickness(small ? 5 : 7, 0, small ? 5 : 7, 1);
        BorderThickness = new Thickness(1, 1, 1, IsPressed ? 1 : 2);
    }

    private void Update(bool animate)
    {
        var level = Tween.Level(this);
        animate &= IsLoaded && level != ArcMotionLevel.Off;
        bool full = animate && level == ArcMotionLevel.Full;
        double seconds = IsPressed ? .09 : Tween.Fast;
        string edge = IsPressed ? "KbdPressedBorder" : IsWarning ? "KbdWarningBorder" : "BorderStrong";
        string fill = IsPressed ? "AccentSubtle" : IsWarning ? "KbdWarningSurface" : "SurfaceRaised";
        string ink = IsPressed ? "AccentStrong" : "Foreground";
        Tween.Color(_edge, Tween.Resolve(this, $"ArcWpf.Brush.{edge}"), seconds, animate);
        Tween.Color(_fill, Tween.Resolve(this, $"ArcWpf.Brush.{fill}"), seconds, animate);
        Tween.Color(_ink, Tween.Resolve(this, $"ArcWpf.Brush.{ink}"), seconds, animate);
        BorderThickness = new Thickness(1, 1, 1, IsPressed ? 1 : 2);
        // translateY(1px) rides the spring token, held keys drop in 90 ms; reduced motion keeps the cap still.
        double drop = IsPressed && level != ArcMotionLevel.Off && level != ArcMotionLevel.Reduced ? 1 : 0;
        if (full) Tween.To(_drop, TranslateTransform.YProperty, drop, IsPressed ? .09 : Tween.SpringTokenSeconds, IsPressed ? ArcEasing.Standard : Tween.SpringToken);
        else Tween.Set(_drop, TranslateTransform.YProperty, drop);
    }
}

/// <summary>A shortcut as key caps 4 DIP apart, with a spoken name for assistive technology. Caps whose key is held light up.</summary>
public class ShortcutKeys : StackPanel
{
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(nameof(Shortcut), typeof(string), typeof(ShortcutKeys), new PropertyMetadata("", (d, _) => ((ShortcutKeys)d).Build()));
    public static readonly DependencyProperty PlatformProperty = DependencyProperty.Register(nameof(Platform), typeof(ShortcutPlatform), typeof(ShortcutKeys), new PropertyMetadata(ShortcutPlatform.Other, (d, _) => ((ShortcutKeys)d).Build()));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(ControlSize), typeof(ShortcutKeys), new PropertyMetadata(ControlSize.Medium, (d, _) => ((ShortcutKeys)d).Build()));
    private IReadOnlySet<string> _held = new HashSet<string>();

    public ShortcutKeys() { Orientation = Orientation.Horizontal; VerticalAlignment = VerticalAlignment.Center; }

    public string Shortcut { get => (string)GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }
    public ShortcutPlatform Platform { get => (ShortcutPlatform)GetValue(PlatformProperty); set => SetValue(PlatformProperty, value); }
    public ControlSize Size { get => (ControlSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>Held key ids, as tracked by <see cref="ShortcutList"/>; matching caps light up.</summary>
    public void SetHeld(IReadOnlySet<string> held)
    {
        _held = held;
        var tokens = Shortcuts.Tokens(Shortcut, Platform);
        for (int index = 0; index < Children.Count && index < tokens.Count; index++) ((ShortcutKbd)Children[index]).IsPressed = held.Contains(tokens[index].Id);
    }

    private void Build()
    {
        Children.Clear();
        var tokens = Shortcuts.Tokens(Shortcut, Platform);
        foreach (var token in tokens) Children.Add(new ShortcutKbd { Text = token.Label, Size = Size, IsPressed = _held.Contains(token.Id), Margin = new Thickness(Children.Count == 0 ? 0 : 4, 0, 0, 0) });
        AutomationProperties.SetName(this, Shortcuts.Format(Shortcut, Platform).Spoken);
    }
}
