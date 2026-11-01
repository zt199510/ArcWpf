using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

public sealed class HoverCardStat
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

public class HoverCardProfile : Control
{
    public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register(nameof(DisplayName), typeof(string), typeof(HoverCardProfile), new PropertyMetadata(""));
    public static readonly DependencyProperty RoleProperty = DependencyProperty.Register(nameof(Role), typeof(string), typeof(HoverCardProfile), new PropertyMetadata(""));
    public static readonly DependencyProperty BioProperty = DependencyProperty.Register(nameof(Bio), typeof(string), typeof(HoverCardProfile), new PropertyMetadata(""));
    public static readonly DependencyProperty AvatarSourceProperty = DependencyProperty.Register(nameof(AvatarSource), typeof(ImageSource), typeof(HoverCardProfile));
    public static readonly DependencyProperty MetaProperty = DependencyProperty.Register(nameof(Meta), typeof(object), typeof(HoverCardProfile));

    public string DisplayName { get => (string)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
    public string Role { get => (string)GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public string Bio { get => (string)GetValue(BioProperty); set => SetValue(BioProperty, value); }
    public ImageSource? AvatarSource { get => (ImageSource?)GetValue(AvatarSourceProperty); set => SetValue(AvatarSourceProperty, value); }
    public object? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public ObservableCollection<HoverCardStat> Stats { get; } = [];

    static HoverCardProfile() => DefaultStyleKeyProperty.OverrideMetadata(typeof(HoverCardProfile), new FrameworkPropertyMetadata(typeof(HoverCardProfile)));
    protected override AutomationPeer OnCreateAutomationPeer() => new ProfilePeer(this);
    private sealed class ProfilePeer(HoverCardProfile owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.DisplayName;
        protected override string GetHelpTextCore() => string.Join(". ", new[] { owner.Role, owner.Bio }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
