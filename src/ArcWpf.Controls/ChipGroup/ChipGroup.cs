using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ArcWpf.Controls;

public sealed class ChipItem
{
    public string Label { get; set; } = "";
    public object? Value { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class ChipGroup : ListBox
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ChipGroup), new PropertyMetadata("Options"));
    public static readonly DependencyProperty AllowEmptyProperty = DependencyProperty.Register(nameof(AllowEmpty), typeof(bool), typeof(ChipGroup), new PropertyMetadata(true));
    public static readonly RoutedEvent SelectionCommittedEvent = EventManager.RegisterRoutedEvent(nameof(SelectionCommitted), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChipGroup));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool AllowEmpty { get => (bool)GetValue(AllowEmptyProperty); set => SetValue(AllowEmptyProperty, value); }
    public event RoutedEventHandler SelectionCommitted { add => AddHandler(SelectionCommittedEvent, value); remove => RemoveHandler(SelectionCommittedEvent, value); }
    static ChipGroup() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ChipGroup), new FrameworkPropertyMetadata(typeof(ChipGroup))); SelectionModeProperty.OverrideMetadata(typeof(ChipGroup), new FrameworkPropertyMetadata(SelectionMode.Multiple)); }
    protected override bool IsItemItsOwnContainerOverride(object item) => item is ListBoxItem;
    protected override DependencyObject GetContainerForItemOverride() => new ListBoxItem();
    protected override void OnSelectionChanged(SelectionChangedEventArgs e) { base.OnSelectionChanged(e); RaiseEvent(new RoutedEventArgs(SelectionCommittedEvent, this)); }
    protected override AutomationPeer OnCreateAutomationPeer() => new ChipGroupPeer(this);
    private sealed class ChipGroupPeer(ChipGroup owner) : ListBoxAutomationPeer(owner) { protected override string GetClassNameCore() => nameof(ChipGroup); protected override string GetNameCore() => owner.Label; }
}
