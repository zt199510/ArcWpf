using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ArcWpf.Controls;

public enum ToggleGroupType { Single, Multiple }
public enum ToggleGroupSize { Small, Medium }

public class ToggleGroup : ListBox
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ToggleGroup), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(nameof(Type), typeof(ToggleGroupType), typeof(ToggleGroup), new FrameworkPropertyMetadata(ToggleGroupType.Single, OnTypeChanged));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(ToggleGroupSize), typeof(ToggleGroup), new FrameworkPropertyMetadata(ToggleGroupSize.Medium));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ToggleGroupType Type { get => (ToggleGroupType)GetValue(TypeProperty); set => SetValue(TypeProperty, value); }
    public ToggleGroupSize Size { get => (ToggleGroupSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    static ToggleGroup() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleGroup), new FrameworkPropertyMetadata(typeof(ToggleGroup))); SelectionModeProperty.OverrideMetadata(typeof(ToggleGroup), new FrameworkPropertyMetadata(SelectionMode.Single)); }
    public ToggleGroup() { SetCurrentValue(KeyboardNavigation.TabNavigationProperty, KeyboardNavigationMode.Once); }
    protected override bool IsItemItsOwnContainerOverride(object item) => item is ToggleGroupItem;
    protected override DependencyObject GetContainerForItemOverride() => new ToggleGroupItem();
    private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ToggleGroup)d).SelectionMode = (ToggleGroupType)e.NewValue == ToggleGroupType.Multiple ? SelectionMode.Multiple : SelectionMode.Single;
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None) { base.OnPreviewKeyDown(e); return; }
        var valid = Enumerable.Range(0, Items.Count).Select(i => ItemContainerGenerator.ContainerFromIndex(i)).OfType<ToggleGroupItem>().Where(i => i.IsEnabled).ToArray();
        var current = valid.IndexOf(Keyboard.FocusedElement as ToggleGroupItem);
        if (current >= 0 && e.Key is Key.Left or Key.Up or Key.Right or Key.Down or Key.Home or Key.End)
        {
            var next = e.Key == Key.Home ? 0 : e.Key == Key.End ? valid.Length - 1 : (current + (e.Key is Key.Right or Key.Down ? 1 : -1) + valid.Length) % valid.Length;
            valid[next].Focus(); e.Handled = true; return;
        }
        base.OnPreviewKeyDown(e);
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeerImpl(this);
    private sealed class FrameworkElementAutomationPeerImpl(ToggleGroup owner) : FrameworkElementAutomationPeer(owner) { protected override string GetNameCore() => owner.Label; protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List; }
}

public class ToggleGroupItem : ListBoxItem
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(ToggleGroupItem), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ToggleGroupItem), new FrameworkPropertyMetadata(string.Empty));
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    static ToggleGroupItem() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleGroupItem), new FrameworkPropertyMetadata(typeof(ToggleGroupItem)));
    public ToggleGroupItem() { SetCurrentValue(FocusVisualStyleProperty, null); }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { if (IsEnabled) { Focus(); SetCurrentValue(IsSelectedProperty, !IsSelected || ((ItemsControl.ItemsControlFromItemContainer(this) as ToggleGroup)?.Type == ToggleGroupType.Single)); e.Handled = true; } }
}
