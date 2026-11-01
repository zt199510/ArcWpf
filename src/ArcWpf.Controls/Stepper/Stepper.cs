using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Automation;

namespace ArcWpf.Controls;

public enum StepperOrientation { Horizontal, Vertical }
public enum StepperDetails { All, Current }

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
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(StepperOrientation), typeof(Stepper), new PropertyMetadata(StepperOrientation.Horizontal, OnInteractionChanged));
    public static readonly DependencyProperty DetailsProperty = DependencyProperty.Register(nameof(Details), typeof(StepperDetails), typeof(Stepper), new PropertyMetadata(StepperDetails.All, OnInteractionChanged));
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(nameof(Compact), typeof(bool), typeof(Stepper), new PropertyMetadata(false, OnInteractionChanged));
    public static readonly DependencyProperty CompleteLabelProperty = DependencyProperty.Register(nameof(CompleteLabel), typeof(string), typeof(Stepper), new PropertyMetadata("All steps complete", OnInteractionChanged));
    private static readonly DependencyPropertyKey IsCompactPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsCompact), typeof(bool), typeof(Stepper), new PropertyMetadata(false));
    public static readonly DependencyProperty IsCompactProperty = IsCompactPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey CurrentLabelPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentLabel), typeof(string), typeof(Stepper), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty CurrentLabelProperty = CurrentLabelPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey CurrentDescriptionPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentDescription), typeof(string), typeof(Stepper), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty CurrentDescriptionProperty = CurrentDescriptionPropertyKey.DependencyProperty;
    public static readonly RoutedEvent CurrentStepChangedEvent = EventManager.RegisterRoutedEvent(nameof(CurrentStepChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Stepper));
    private ItemsControl? _items;
    private readonly HashSet<StepperStep> _observed = [];
    private readonly ObservableCollection<StepView> _views = [];
    private readonly OwnedMotion _motion = new();
    private int _visualVersion;
    private int _renderedIndex = -1;
    public int CurrentIndex { get => (int)GetValue(CurrentIndexProperty); set => SetValue(CurrentIndexProperty, value); }
    public bool AllowStepClick { get => (bool)GetValue(AllowStepClickProperty); set => SetValue(AllowStepClickProperty, value); }
    public StepperOrientation Orientation { get => (StepperOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public StepperDetails Details { get => (StepperDetails)GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }
    public string CompleteLabel { get => (string)GetValue(CompleteLabelProperty); set => SetValue(CompleteLabelProperty, value); }
    public bool IsCompact => (bool)GetValue(IsCompactProperty);
    public string CurrentLabel => (string)GetValue(CurrentLabelProperty);
    public string CurrentDescription => (string)GetValue(CurrentDescriptionProperty);
    public ObservableCollection<StepperStep> Steps { get; } = [];
    public event RoutedEventHandler CurrentStepChanged { add => AddHandler(CurrentStepChangedEvent, value); remove => RemoveHandler(CurrentStepChangedEvent, value); }
    private sealed class StepView(StepperStep step, int index) : INotifyPropertyChanged
    {
        public StepperStep Step { get; } = step;
        public int Index { get; } = index;
        private bool _isCurrent, _isComplete, _isSelectable, _isError, _hasNext, _showDescription;
        public bool IsCurrent { get => _isCurrent; private set => Set(ref _isCurrent, value); }
        public bool IsComplete { get => _isComplete; private set => Set(ref _isComplete, value); }
        public bool IsSelectable { get => _isSelectable; private set => Set(ref _isSelectable, value); }
        public bool IsError { get => _isError; private set => Set(ref _isError, value); }
        public bool HasNext { get => _hasNext; private set => Set(ref _hasNext, value); }
        public bool ShowDescription { get => _showDescription; private set => Set(ref _showDescription, value); }
        public string Title => Step.Title;
        public string Description => IsError ? Step.Error : ShowDescription ? Step.Description : string.Empty;
        public string Mark => IsError ? "!" : IsComplete ? "✓" : (Index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Update(Stepper owner)
        {
            IsCurrent = Index == owner.CurrentIndex;
            IsComplete = Index < owner.CurrentIndex;
            IsSelectable = owner.AllowStepClick && Step.IsEnabled && IsComplete;
            IsError = !string.IsNullOrEmpty(Step.Error);
            HasNext = Index < owner.Steps.Count - 1;
            ShowDescription = owner.Details == StepperDetails.All || IsCurrent;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mark)));
        }
        private void Set(ref bool field, bool value, [CallerMemberName] string? name = null)
        {
            if (field == value) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    static Stepper() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Stepper), new FrameworkPropertyMetadata(typeof(Stepper))); }
    public Stepper()
    {
        Steps.CollectionChanged += StepsChanged;
        Unloaded += (_, _) => { _motion.Stop(); _renderedIndex = -1; };
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }
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
        _motion.Stop();
        _renderedIndex = -1;
        if (_items is not null)
        {
            _items.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(StepClicked));
            _items.PreviewKeyDown -= StepKeyDown;
        }
        base.OnApplyTemplate();
        _items = GetTemplateChild("PART_Items") as ItemsControl;
        if (_items is not null)
        {
            _items.ItemsSource = _views;
            _items.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(StepClicked));
            _items.PreviewKeyDown += StepKeyDown;
        }
        Refresh();
    }
    private static void OnIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Stepper)d;
        control.Refresh();
        control.RaiseEvent(new RoutedEventArgs(CurrentStepChangedEvent, control));
        UIElementAutomationPeer.FromElement(control)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
    private static void OnInteractionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Stepper)d;
        if (e.Property == OrientationProperty)
        {
            control._motion.Stop();
            control._renderedIndex = -1;
        }
        control.Refresh();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty && _items is not null) UpdateVisuals();
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateCompact();
    }
    private void UpdateCompact() => SetValue(IsCompactPropertyKey, Compact || Orientation == StepperOrientation.Horizontal && ActualWidth > 0 && ActualWidth <= 480);
    private void Refresh()
    {
        UpdateCompact();
        var current = CurrentIndex < Steps.Count ? Steps[CurrentIndex] : null;
        SetValue(CurrentLabelPropertyKey, current?.Title ?? CompleteLabel);
        SetValue(CurrentDescriptionPropertyKey, current is null ? string.Empty : !string.IsNullOrEmpty(current.Error) ? current.Error : current.Description);
        if (_views.Count != Steps.Count || _views.Where((view, index) => !ReferenceEquals(view.Step, Steps[index])).Any())
        {
            _motion.Stop();
            _renderedIndex = -1;
            _views.Clear();
            for (int index = 0; index < Steps.Count; index++) _views.Add(new StepView(Steps[index], index));
        }
        foreach (var view in _views) view.Update(this);
        int version = ++_visualVersion;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (version == _visualVersion) UpdateVisuals();
        });
    }
    private void UpdateVisuals()
    {
        if (_items is null || _views.Count == 0) return;
        bool animate = _renderedIndex >= 0 && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        int from = _renderedIndex;
        bool found = false;
        for (int index = 0; index < _views.Count; index++)
        {
            if (_items.ItemContainerGenerator.ContainerFromIndex(index) is not ContentPresenter container) continue;
            if (_items.ItemTemplate.FindName("ConnectorFill", container) is Border fill)
            {
                found = true;
                if (fill.RenderTransform is not ScaleTransform scale || scale.IsFrozen)
                    fill.RenderTransform = scale = new ScaleTransform(1, 1);
                bool vertical = Orientation == StepperOrientation.Vertical;
                double target = index < CurrentIndex ? 1 : 0;
                double delay = animate && index >= Math.Min(from, CurrentIndex) && index < Math.Max(from, CurrentIndex)
                    ? (CurrentIndex > from ? index - from : from - 1 - index) * .08 : 0;
                _motion.Double(scale, vertical ? ScaleTransform.ScaleYProperty : ScaleTransform.ScaleXProperty, target, .42, animate, spring: true, delay: delay);
                _motion.Double(scale, vertical ? ScaleTransform.ScaleXProperty : ScaleTransform.ScaleYProperty, 1, 0, false);
            }
            if (_items.ItemTemplate.FindName("Ring", container) is Border ring)
            {
                found = true;
                if (ring.RenderTransform is not ScaleTransform ringScale || ringScale.IsFrozen)
                    ring.RenderTransform = ringScale = new ScaleTransform(.6, .6);
                double target = index == CurrentIndex ? 1 : 0;
                double delay = animate && target > 0 ? Math.Max(0, Math.Abs(CurrentIndex - from) - 1) * .08 + .12 : 0;
                _motion.Double(ring, OpacityProperty, target, .16, animate, delay: delay);
                _motion.Double(ringScale, ScaleTransform.ScaleXProperty, target > 0 ? 1 : .6, .26, animate, spring: true, delay: delay);
                _motion.Double(ringScale, ScaleTransform.ScaleYProperty, target > 0 ? 1 : .6, .26, animate, spring: true, delay: delay);
            }
        }
        if (found) _renderedIndex = CurrentIndex;
    }
    private void StepClicked(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button { DataContext: StepView view } || !view.IsSelectable) return;
        SetCurrentValue(CurrentIndexProperty, view.Index);
        e.Handled = true;
    }
    public void Next() { if (CurrentIndex < Steps.Count) SetCurrentValue(CurrentIndexProperty, CurrentIndex + 1); }
    public void Previous() { if (CurrentIndex > 0) SetCurrentValue(CurrentIndexProperty, CurrentIndex - 1); }
    private void StepKeyDown(object sender, KeyEventArgs e)
    {
        if (_items is null || e.OriginalSource is not System.Windows.Controls.Button { DataContext: StepView source }) return;
        bool rtl = Orientation == StepperOrientation.Horizontal && FlowDirection == FlowDirection.RightToLeft;
        int target = e.Key switch
        {
            Key.Home => 0,
            Key.End => Math.Min(CurrentIndex - 1, Steps.Count - 1),
            Key.Up => source.Index - 1,
            Key.Down => source.Index + 1,
            Key.Left => source.Index + (rtl ? 1 : -1),
            Key.Right => source.Index + (rtl ? -1 : 1),
            _ => -1
        };
        if (target < 0 || target >= Math.Min(CurrentIndex, Steps.Count) || target == source.Index) return;
        if (_items.ItemContainerGenerator.ContainerFromIndex(target) is not DependencyObject container) return;
        if (FindStepButton(container) is not { IsEnabled: true } button) return;
        button.Focus();
        e.Handled = true;
    }
    private static System.Windows.Controls.Button? FindStepButton(DependencyObject root)
    {
        if (root is System.Windows.Controls.Button button) return button;
        for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
            if (FindStepButton(System.Windows.Media.VisualTreeHelper.GetChild(root, index)) is { } child) return child;
        return null;
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new StepperPeer(this);
    private sealed class StepperPeer(Stepper owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group; protected override string GetNameCore() => "Steps"; protected override string GetItemStatusCore() => owner.CurrentIndex >= owner.Steps.Count ? owner.CompleteLabel : $"Step {owner.CurrentIndex + 1} of {owner.Steps.Count}: {owner.CurrentLabel}"; }
}
