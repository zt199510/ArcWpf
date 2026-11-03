using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ArcWpf.Controls;

// Arc motion tokens (motion-tokens.ts) as ControlSpring tunings.
internal static class ArcSpring
{
    public static readonly double Snappy = ControlSpring.Physical(.26);
    public static readonly double Smooth = ControlSpring.Physical(.4);
    public static readonly double Morph = ControlSpring.Physical(.42);
    public static double Of(double visualDuration) => ControlSpring.Physical(visualDuration);
}

// Runs a callback when the theme swaps its brushes, for code that resolves theme colors by hand.
internal static class ThemeWatch
{
    private static readonly ConditionalWeakTable<DependencyObject, Action> Callbacks = [];
    private static readonly DependencyProperty ProbeProperty = DependencyProperty.RegisterAttached("Probe", typeof(object), typeof(ThemeWatch), new PropertyMetadata(null, OnProbe));
    public static void Add(FrameworkElement element, Action callback)
    {
        Callbacks.AddOrUpdate(element, callback);
        element.SetResourceReference(ProbeProperty, "ArcWpf.Brush.Surface");
    }
    private static void OnProbe(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is not null && Callbacks.TryGetValue(d, out var callback)) { callback(); }
    }
}

// A brush that crosses to a theme role over Arc's fast transition, the way a CSS transition does.
internal sealed class ThemedBrush(FrameworkElement owner, OwnedMotion motion)
{
    public SolidColorBrush Brush { get; } = new(Colors.Transparent);
    public void To(string? role, bool animate, double seconds = .16)
    {
        var color = Colors.Transparent;
        if (role is not null && owner.TryFindResource("ArcWpf.Brush." + role) is SolidColorBrush brush) { color = brush.Color; }
        else if (role is null && Brush.Color.A != 0) { color = Brush.Color; color.A = 0; }
        motion.Color(Brush, color, seconds, animate && owner.IsLoaded && ArcMotion.GetLevel(owner) != ArcMotionLevel.Off);
    }
}

internal static class ArcEffects
{
    // CSS blur(n) as a BlurEffect, dropped once it lands so text renders crisply at rest.
    public static void BlurIn(OwnedMotion motion, UIElement element, double css, double seconds, double delay = 0)
    {
        var effect = new System.Windows.Media.Effects.BlurEffect { Radius = css * 2, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
        element.Effect = effect;
        motion.Double(effect, System.Windows.Media.Effects.BlurEffect.RadiusProperty, 0, seconds, true, completed: () => { if (ReferenceEquals(element.Effect, effect)) { element.Effect = null; } }, delay: delay, enter: true);
    }

    public static void BlurOut(OwnedMotion motion, UIElement element, double css, double seconds)
    {
        var effect = new System.Windows.Media.Effects.BlurEffect { Radius = 0, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
        element.Effect = effect;
        motion.Double(effect, System.Windows.Media.Effects.BlurEffect.RadiusProperty, css * 2, seconds, true);
    }

    public static void Announce(TextBlock? region, string text)
    {
        if (region is null) { return; }
        region.Text = text;
        System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(region)?.RaiseAutomationEvent(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged);
    }
}

internal static class ArcText
{
    // CSS line boxes: a fixed line height with the text centered in it.
    public static TextBlock Block(string text, double size, double lineHeight, string role = "Foreground", bool medium = false)
    {
        var block = new TextBlock { Text = text, FontSize = size, LineHeight = lineHeight, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = medium ? FontWeights.Medium : FontWeights.Normal };
        block.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush." + role);
        return block;
    }

    public static ArcDataGridIcon Icon(string name, double size, Brush stroke, double thickness = 1.75) =>
        new() { Icon = name, Size = size, Stroke = stroke, StrokeThickness = thickness, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
}

// A button whose fill, edge and ink cross to the role its state asks for. Subclasses and owners supply the roles and the content.
internal class ArcChromeButton : System.Windows.Controls.Button
{
    private static readonly Dictionary<double, ControlTemplate> Templates = [];
    private static readonly Dictionary<double, Style> FocusStyles = [];
    private readonly OwnedMotion _motion = new();
    private readonly ThemedBrush _fill, _edge, _ink;
    public Func<ArcChromeButton, (string? Fill, string? Edge, string? Ink)>? Roles { get; set; }
    public Brush Ink => _ink.Brush;

    public ArcChromeButton(double radius, double borderThickness = 0)
    {
        OverridesDefaultStyle = true;
        Template = TemplateFor(radius);
        FocusVisualStyle = FocusFor(radius);
        _fill = new ThemedBrush(this, _motion);
        _edge = new ThemedBrush(this, _motion);
        _ink = new ThemedBrush(this, _motion);
        Background = _fill.Brush;
        BorderBrush = _edge.Brush;
        Foreground = _ink.Brush;
        BorderThickness = new Thickness(borderThickness);
        Cursor = System.Windows.Input.Cursors.Hand;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        MouseEnter += (_, _) => Refresh(true);
        MouseLeave += (_, _) => Refresh(true);
        IsEnabledChanged += (_, _) => Refresh(true);
        Loaded += (_, _) => Refresh(false);
        Unloaded += (_, _) => _motion.Stop();
        ThemeWatch.Add(this, () => Refresh(false));
    }

    protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsPressedChanged(e);
        Refresh(true);
    }

    public void Refresh(bool animate)
    {
        if (Roles is null) { return; }
        var (fill, edge, ink) = Roles(this);
        _fill.To(fill, animate);
        _edge.To(edge, animate);
        _ink.To(ink, animate);
    }

    private static ControlTemplate TemplateFor(double radius)
    {
        if (Templates.TryGetValue(radius, out var template)) { return template; }
        var frame = new FrameworkElementFactory(typeof(Border));
        frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        frame.SetBinding(Border.BackgroundProperty, new Binding(nameof(Background)) { RelativeSource = RelativeSource.TemplatedParent });
        frame.SetBinding(Border.BorderBrushProperty, new Binding(nameof(BorderBrush)) { RelativeSource = RelativeSource.TemplatedParent });
        frame.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(BorderThickness)) { RelativeSource = RelativeSource.TemplatedParent });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetBinding(MarginProperty, new Binding(nameof(Padding)) { RelativeSource = RelativeSource.TemplatedParent });
        content.SetBinding(HorizontalAlignmentProperty, new Binding(nameof(HorizontalContentAlignment)) { RelativeSource = RelativeSource.TemplatedParent });
        content.SetBinding(VerticalAlignmentProperty, new Binding(nameof(VerticalContentAlignment)) { RelativeSource = RelativeSource.TemplatedParent });
        frame.AppendChild(content);
        template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = frame };
        template.Seal();
        return Templates[radius] = template;
    }

    private static Style FocusFor(double radius)
    {
        if (FocusStyles.TryGetValue(radius, out var style)) { return style; }
        var ring = new FrameworkElementFactory(typeof(Border));
        ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius + 2));
        ring.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        ring.SetValue(MarginProperty, new Thickness(-2));
        ring.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.AccentStrong");
        var template = new ControlTemplate(typeof(Control)) { VisualTree = ring };
        template.Seal();
        style = new Style(typeof(Control));
        style.Setters.Add(new Setter(TemplateProperty, template));
        style.Seal();
        return FocusStyles[radius] = style;
    }
}
