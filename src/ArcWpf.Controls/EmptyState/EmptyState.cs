using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

public class EmptyState : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyState), new FrameworkPropertyMetadata("Nothing here yet"));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(EmptyState), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(object), typeof(EmptyState));
    public static readonly DependencyProperty ActionContentProperty = DependencyProperty.Register(nameof(ActionContent), typeof(object), typeof(EmptyState));
    public static readonly DependencyProperty ActionCommandProperty = DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(EmptyState));
    public static readonly DependencyProperty ActionCommandParameterProperty = DependencyProperty.Register(nameof(ActionCommandParameter), typeof(object), typeof(EmptyState));
    public static readonly DependencyProperty IsActionVisibleProperty = DependencyProperty.Register(nameof(IsActionVisible), typeof(bool), typeof(EmptyState), new FrameworkPropertyMetadata(true));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public object? ActionContent { get => GetValue(ActionContentProperty); set => SetValue(ActionContentProperty, value); }
    public ICommand? ActionCommand { get => (ICommand?)GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }
    public object? ActionCommandParameter { get => GetValue(ActionCommandParameterProperty); set => SetValue(ActionCommandParameterProperty, value); }
    public bool IsActionVisible { get => (bool)GetValue(IsActionVisibleProperty); set => SetValue(IsActionVisibleProperty, value); }
    static EmptyState() { DefaultStyleKeyProperty.OverrideMetadata(typeof(EmptyState), new FrameworkPropertyMetadata(typeof(EmptyState))); }
    protected override AutomationPeer OnCreateAutomationPeer() => new EmptyStatePeer(this);
    private sealed class EmptyStatePeer(EmptyState owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group; protected override string GetNameCore() => owner.Title; protected override string GetHelpTextCore() => owner.Description; }
}
