using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation;
using System.Windows.Controls;

namespace ArcWpf.Controls;

public enum ThemeSwitchVariant { Reveal, Eclipse, Split, Rise }
public enum ThemeSwitchTheme { Light, Dark }

public sealed class ThemeChangedEventArgs(RoutedEvent routedEvent, object source, ThemeSwitchTheme theme, ThemeSwitchVariant variant) : RoutedEventArgs(routedEvent, source)
{
    public ThemeSwitchTheme Theme { get; } = theme;
    public ThemeSwitchVariant Variant { get; } = variant;
}

public class ThemeSwitch : System.Windows.Controls.Button
{
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(nameof(Theme), typeof(ThemeSwitchTheme), typeof(ThemeSwitch), new FrameworkPropertyMetadata(ThemeSwitchTheme.Light, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(ThemeSwitchVariant), typeof(ThemeSwitch), new FrameworkPropertyMetadata(ThemeSwitchVariant.Reveal));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ThemeSwitch), new FrameworkPropertyMetadata("Switch theme"));
    public static readonly DependencyProperty IconOnlyProperty = DependencyProperty.Register(nameof(IconOnly), typeof(bool), typeof(ThemeSwitch), new FrameworkPropertyMetadata(false));
    public static readonly RoutedEvent ThemeChangedEvent = EventManager.RegisterRoutedEvent(nameof(ThemeChanged), RoutingStrategy.Bubble, typeof(EventHandler<ThemeChangedEventArgs>), typeof(ThemeSwitch));

    public ThemeSwitchTheme Theme { get => (ThemeSwitchTheme)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
    public ThemeSwitchVariant Variant { get => (ThemeSwitchVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IconOnly { get => (bool)GetValue(IconOnlyProperty); set => SetValue(IconOnlyProperty, value); }
    public event EventHandler<ThemeChangedEventArgs> ThemeChanged { add => AddHandler(ThemeChangedEvent, value); remove => RemoveHandler(ThemeChangedEvent, value); }

    static ThemeSwitch() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ThemeSwitch), new FrameworkPropertyMetadata(typeof(ThemeSwitch)));
    public ThemeSwitch() { Click += (_, _) => Toggle(); AutomationProperties.SetName(this, "Switch theme"); }
    public void Toggle()
    {
        if (!IsEnabled) return;
        var next = Theme == ThemeSwitchTheme.Light ? ThemeSwitchTheme.Dark : ThemeSwitchTheme.Light;
        SetCurrentValue(ThemeProperty, next);
        RaiseEvent(new ThemeChangedEventArgs(ThemeChangedEvent, this, next, Variant));
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonAutomationPeer(this);
}
