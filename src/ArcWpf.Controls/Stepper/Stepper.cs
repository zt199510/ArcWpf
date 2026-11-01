using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

public sealed class StepperStep
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
}

public class Stepper : Control
{
    public static readonly DependencyProperty CurrentIndexProperty = DependencyProperty.Register(nameof(CurrentIndex), typeof(int), typeof(Stepper), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIndexChanged));
    public static readonly DependencyProperty AllowStepClickProperty = DependencyProperty.Register(nameof(AllowStepClick), typeof(bool), typeof(Stepper), new PropertyMetadata(true));
    public static readonly RoutedEvent CurrentStepChangedEvent = EventManager.RegisterRoutedEvent(nameof(CurrentStepChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Stepper));
    private ItemsControl? _items;
    public int CurrentIndex { get => (int)GetValue(CurrentIndexProperty); set => SetValue(CurrentIndexProperty, value); }
    public bool AllowStepClick { get => (bool)GetValue(AllowStepClickProperty); set => SetValue(AllowStepClickProperty, value); }
    public ObservableCollection<StepperStep> Steps { get; } = [];
    public event RoutedEventHandler CurrentStepChanged { add => AddHandler(CurrentStepChangedEvent, value); remove => RemoveHandler(CurrentStepChangedEvent, value); }
    static Stepper() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Stepper), new FrameworkPropertyMetadata(typeof(Stepper))); }
    public Stepper() { Steps.CollectionChanged += (_, _) => Refresh(); }
    public override void OnApplyTemplate() { base.OnApplyTemplate(); _items = GetTemplateChild("PART_Items") as ItemsControl; Refresh(); }
    private static void OnIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (Stepper)d; c.Refresh(); c.RaiseEvent(new RoutedEventArgs(CurrentStepChangedEvent, c)); }
    private void Refresh() { if (_items is not null) _items.ItemsSource = Steps; }
    public void Next() { if (CurrentIndex < Steps.Count - 1) SetCurrentValue(CurrentIndexProperty, CurrentIndex + 1); }
    public void Previous() { if (CurrentIndex > 0) SetCurrentValue(CurrentIndexProperty, CurrentIndex - 1); }
    protected override AutomationPeer OnCreateAutomationPeer() => new StepperPeer(this);
    private sealed class StepperPeer(Stepper owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group; protected override string GetNameCore() => "Steps"; protected override string GetItemStatusCore() => $"Step {owner.CurrentIndex + 1} of {owner.Steps.Count}"; }
}
