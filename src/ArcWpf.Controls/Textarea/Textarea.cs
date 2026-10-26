using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_ContentHost", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Label", Type = typeof(FrameworkElement))]
public class Textarea : TextBox
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Textarea), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(Textarea), new FrameworkPropertyMetadata(string.Empty, OnPlaceholderChanged));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(Textarea), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(Textarea), new FrameworkPropertyMetadata(string.Empty, OnErrorChanged));
    private static readonly DependencyPropertyKey HasErrorPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasError), typeof(bool), typeof(Textarea), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty HasErrorProperty = HasErrorPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey EffectiveErrorMessagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(EffectiveErrorMessage), typeof(string), typeof(Textarea), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty EffectiveErrorMessageProperty = EffectiveErrorMessagePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsPlaceholderVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPlaceholderVisible), typeof(bool), typeof(Textarea), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsPlaceholderVisibleProperty = IsPlaceholderVisiblePropertyKey.DependencyProperty;

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public bool HasError => (bool)GetValue(HasErrorProperty);
    public string EffectiveErrorMessage => (string)GetValue(EffectiveErrorMessageProperty);
    public bool IsPlaceholderVisible => (bool)GetValue(IsPlaceholderVisibleProperty);

    private INotifyCollectionChanged? _errors;
    private bool _observing;
    private bool _composing;
    private FrameworkElement? _labelPart;

    static Textarea() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Textarea), new FrameworkPropertyMetadata(typeof(Textarea)));

    public Textarea()
    {
        AcceptsReturn = true;
        TextWrapping = TextWrapping.Wrap;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalContentAlignment = VerticalAlignment.Top;
        Loaded += (_, _) => StartObserving();
        Unloaded += (_, _) => StopObserving();
        StartObserving();
    }

    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Textarea)d).UpdatePlaceholder();
    private static void OnErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Textarea)d).UpdateErrors();
    protected override void OnTextChanged(TextChangedEventArgs e) { base.OnTextChanged(e); UpdatePlaceholder(); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == Validation.ErrorsProperty || e.Property == Validation.HasErrorProperty)
        {
            if (_observing) ObserveErrors();
            UpdateErrors();
        }
    }
    private void StartObserving()
    {
        if (_observing) return;
        _observing = true;
        AddHandler(TextCompositionManager.PreviewTextInputStartEvent, new TextCompositionEventHandler(CompositionStarted), true);
        AddHandler(TextCompositionManager.PreviewTextInputUpdateEvent, new TextCompositionEventHandler(CompositionStarted), true);
        AddHandler(TextCompositionManager.PreviewTextInputEvent, new TextCompositionEventHandler(CompositionEnded), true);
        ObserveErrors(); UpdateErrors(); AttachLabel();
    }
    private void StopObserving()
    {
        _observing = false; ReleaseLabel();
        if (_errors is not null) { CollectionChangedEventManager.RemoveHandler(_errors, ErrorsChanged); _errors = null; }
        _composing = false; UpdatePlaceholder();
    }
    private void ObserveErrors()
    {
        var errors = Validation.GetErrors(this) as INotifyCollectionChanged;
        if (ReferenceEquals(errors, _errors)) return;
        if (_errors is not null) CollectionChangedEventManager.RemoveHandler(_errors, ErrorsChanged);
        _errors = errors;
        if (_errors is not null) CollectionChangedEventManager.AddHandler(_errors, ErrorsChanged);
    }
    private void ErrorsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateErrors();
    private void UpdateErrors()
    {
        var errors = Validation.GetErrors(this);
        bool hasError = !string.IsNullOrWhiteSpace(ErrorMessage) || errors.Count > 0;
        string message = !string.IsNullOrWhiteSpace(ErrorMessage) ? ErrorMessage :
            errors.Select(error => error.ErrorContent as string).FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ??
            (errors.Count > 0 ? TryFindResource("ArcWpf.String.InvalidInput") as string ?? "输入值无效" : string.Empty);
        bool changed = EffectiveErrorMessage != message;
        SetValue(HasErrorPropertyKey, hasError); SetValue(EffectiveErrorMessagePropertyKey, message);
        if (changed && UIElementAutomationPeer.FromElement(this) is { } peer && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
    private void CompositionStarted(object sender, TextCompositionEventArgs e) { _composing = true; UpdatePlaceholder(); }
    private void CompositionEnded(object sender, TextCompositionEventArgs e) { _composing = false; UpdatePlaceholder(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); _composing = false; UpdatePlaceholder(); }
    private void UpdatePlaceholder() => SetValue(IsPlaceholderVisiblePropertyKey, string.IsNullOrEmpty(Text) && !_composing && !string.IsNullOrEmpty(Placeholder));
    public override void OnApplyTemplate()
    {
        ReleaseLabel(); base.OnApplyTemplate();
        if (GetTemplateChild("PART_ContentHost") is not ScrollViewer and not Decorator) Trace.TraceWarning("ArcWpf.Textarea requires a ScrollViewer or Decorator named PART_ContentHost.");
        AttachLabel(); UpdatePlaceholder(); UpdateErrors();
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate) { ReleaseLabel(); base.OnTemplateChanged(oldTemplate, newTemplate); }
    private void FocusFromLabel(object sender, MouseButtonEventArgs e) { if (IsEnabled) { Focus(); e.Handled = true; } }
    private void AttachLabel() { ReleaseLabel(); _labelPart = GetTemplateChild("PART_Label") as FrameworkElement; if (_labelPart is not null) _labelPart.MouseLeftButtonDown += FocusFromLabel; }
    private void ReleaseLabel() { if (_labelPart is not null) { _labelPart.MouseLeftButtonDown -= FocusFromLabel; _labelPart = null; } }
    protected override AutomationPeer OnCreateAutomationPeer() => new TextareaPeer(this);
    private sealed class TextareaPeer(Textarea owner) : TextBoxAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Textarea);
        protected override string GetNameCore() { string name = base.GetNameCore(); return string.IsNullOrEmpty(name) ? owner.Label ?? string.Empty : name; }
        protected override string GetHelpTextCore() { string help = AutomationProperties.GetHelpText(owner); return !string.IsNullOrEmpty(help) ? help : string.Join(" ", new[] { owner.Description, owner.EffectiveErrorMessage }.Where(text => !string.IsNullOrWhiteSpace(text))); }
    }
}
