using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace ArcWpf.Controls;

public sealed class StepperStep : INotifyPropertyChanged
{
    private string _title = "";
    private string _description = "";
    private string _error = "";
    private bool _isEnabled = true;
    public string Title { get => _title; set => Set(ref _title, value); }
    public string Description { get => _description; set => Set(ref _description, value); }
    public string Error { get => _error; set => Set(ref _error, value); }
    public bool IsEnabled { get => _isEnabled; set => Set(ref _isEnabled, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class Stepper : Control
{
    public static readonly DependencyProperty CurrentIndexProperty = DependencyProperty.Register(nameof(CurrentIndex), typeof(int), typeof(Stepper), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIndexChanged), value => value is int index && index >= 0);
    public static readonly DependencyProperty AllowStepClickProperty = DependencyProperty.Register(nameof(AllowStepClick), typeof(bool), typeof(Stepper), new PropertyMetadata(true, OnInteractionChanged));
    public static readonly RoutedEvent CurrentStepChangedEvent = EventManager.RegisterRoutedEvent(nameof(CurrentStepChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Stepper));
    private ItemsControl? _items;
    private readonly HashSet<StepperStep> _observed = [];
    public int CurrentIndex { get => (int)GetValue(CurrentIndexProperty); set => SetValue(CurrentIndexProperty, value); }
    public bool AllowStepClick { get => (bool)GetValue(AllowStepClickProperty); set => SetValue(AllowStepClickProperty, value); }
    public ObservableCollection<StepperStep> Steps { get; } = [];
    public event RoutedEventHandler CurrentStepChanged { add => AddHandler(CurrentStepChangedEvent, value); remove => RemoveHandler(CurrentStepChangedEvent, value); }
    private sealed record StepView(StepperStep Step, int Index, bool IsCurrent, bool IsComplete, bool IsSelectable, bool IsError)
    {
        public string Title => Step.Title;
        public string Description => IsError ? Step.Error : Step.Description;
        public string Mark => IsError ? "!" : IsComplete ? "✓" : (Index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
    }
    static Stepper() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Stepper), new FrameworkPropertyMetadata(typeof(Stepper))); }
    public Stepper() { Steps.CollectionChanged += StepsChanged; }
    private void StepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var step in _observed.Except(Steps).ToArray())
        {
            step.PropertyChanged -= StepChanged;
            _observed.Remove(step);
        }
        foreach (var step in Steps)
            if (_observed.Add(step)) step.PropertyChanged += StepChanged;
        Refresh();
    }
    private void StepChanged(object? sender, PropertyChangedEventArgs e) => Refresh();
    public override void OnApplyTemplate()
    {
        if (_items is not null) _items.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(StepClicked));
        base.OnApplyTemplate();
        _items = GetTemplateChild("PART_Items") as ItemsControl;
        if (_items is not null) _items.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(StepClicked));
        Refresh();
    }
    private static void OnIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (Stepper)d; c.Refresh(); c.RaiseEvent(new RoutedEventArgs(CurrentStepChangedEvent, c)); }
    private static void OnInteractionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Stepper)d).Refresh();
    private void Refresh()
    {
        if (_items is not null)
            _items.ItemsSource = Steps.Select((step, index) => new StepView(step, index, index == CurrentIndex, index < CurrentIndex, AllowStepClick && step.IsEnabled && index < CurrentIndex, !string.IsNullOrEmpty(step.Error))).ToArray();
    }
    private void StepClicked(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button { DataContext: StepView view } || !view.IsSelectable) return;
        SetCurrentValue(CurrentIndexProperty, view.Index);
        e.Handled = true;
    }
    public void Next() { if (CurrentIndex < Steps.Count) SetCurrentValue(CurrentIndexProperty, CurrentIndex + 1); }
    public void Previous() { if (CurrentIndex > 0) SetCurrentValue(CurrentIndexProperty, CurrentIndex - 1); }
    protected override AutomationPeer OnCreateAutomationPeer() => new StepperPeer(this);
    private sealed class StepperPeer(Stepper owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group; protected override string GetNameCore() => "Steps"; protected override string GetItemStatusCore() => $"Step {owner.CurrentIndex + 1} of {owner.Steps.Count}"; }
}
