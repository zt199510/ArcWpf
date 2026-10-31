using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_Trigger", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Input", Type = typeof(System.Windows.Controls.TextBox))]
[TemplatePart(Name = "PART_Clear", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Results", Type = typeof(Border))]
public class ExpandingSearch : Control
{
    // collapsed 只保留圆形触发器；expanded 同时显示输入框和 Recent 信息面板。
    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(nameof(Query), typeof(string), typeof(ExpandingSearch), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnQueryChanged));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ExpandingSearch), new FrameworkPropertyMetadata("Search", OnVisualChanged));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ExpandingSearch), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExpandedChanged));
    public static readonly DependencyProperty WidthCollapsedProperty = DependencyProperty.Register(nameof(WidthCollapsed), typeof(double), typeof(ExpandingSearch), new FrameworkPropertyMetadata(40d));
    public static readonly DependencyProperty WidthExpandedProperty = DependencyProperty.Register(nameof(WidthExpanded), typeof(double), typeof(ExpandingSearch), new FrameworkPropertyMetadata(320d));
    public static readonly DependencyProperty ExpandOnFocusProperty = DependencyProperty.Register(nameof(ExpandOnFocus), typeof(bool), typeof(ExpandingSearch), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty SubmitCommandProperty = DependencyProperty.Register(nameof(SubmitCommand), typeof(ICommand), typeof(ExpandingSearch));
    public static readonly DependencyProperty SubmitCommandParameterProperty = DependencyProperty.Register(nameof(SubmitCommandParameter), typeof(object), typeof(ExpandingSearch));
    public static readonly RoutedEvent ExpandedEvent = EventManager.RegisterRoutedEvent(nameof(Expanded), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ExpandingSearch));
    public static readonly RoutedEvent CollapsedEvent = EventManager.RegisterRoutedEvent(nameof(Collapsed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ExpandingSearch));
    public static readonly RoutedEvent SubmittedEvent = EventManager.RegisterRoutedEvent(nameof(Submitted), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ExpandingSearch));

    private Border? _surface, _results; private System.Windows.Controls.Button? _trigger, _clear; private System.Windows.Controls.TextBox? _input; private readonly OwnedMotion _motion = new();
    public string Query { get => (string)GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public double WidthCollapsed { get => (double)GetValue(WidthCollapsedProperty); set => SetValue(WidthCollapsedProperty, value); }
    public double WidthExpanded { get => (double)GetValue(WidthExpandedProperty); set => SetValue(WidthExpandedProperty, value); }
    public bool ExpandOnFocus { get => (bool)GetValue(ExpandOnFocusProperty); set => SetValue(ExpandOnFocusProperty, value); }
    public ICommand? SubmitCommand { get => (ICommand?)GetValue(SubmitCommandProperty); set => SetValue(SubmitCommandProperty, value); }
    public object? SubmitCommandParameter { get => GetValue(SubmitCommandParameterProperty); set => SetValue(SubmitCommandParameterProperty, value); }
    public ObservableCollection<ExpandingSearchItem> Items { get; } = [];
    public event RoutedEventHandler Expanded { add => AddHandler(ExpandedEvent, value); remove => RemoveHandler(ExpandedEvent, value); }
    public event RoutedEventHandler Collapsed { add => AddHandler(CollapsedEvent, value); remove => RemoveHandler(CollapsedEvent, value); }
    public event RoutedEventHandler Submitted { add => AddHandler(SubmittedEvent, value); remove => RemoveHandler(SubmittedEvent, value); }

    static ExpandingSearch() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ExpandingSearch), new FrameworkPropertyMetadata(typeof(ExpandingSearch))); }
    public ExpandingSearch() { Items.CollectionChanged += (_, _) => Refresh(false); Loaded += (_, _) => Refresh(false); Unloaded += (_, _) => _motion.Stop(); }
    public override void OnApplyTemplate()
    {
        ReleaseTemplate(); base.OnApplyTemplate(); _surface = GetTemplateChild("PART_Surface") as Border; _results = GetTemplateChild("PART_Results") as Border; _trigger = GetTemplateChild("PART_Trigger") as System.Windows.Controls.Button; _input = GetTemplateChild("PART_Input") as System.Windows.Controls.TextBox; _clear = GetTemplateChild("PART_Clear") as System.Windows.Controls.Button;
        if (_trigger is not null) _trigger.Click += TriggerClick; if (_input is not null) { _input.TextChanged += InputChanged; _input.PreviewKeyDown += InputKeyDown; _input.GotKeyboardFocus += InputFocus; }
        if (_clear is not null) _clear.Click += ClearClick; Refresh(false);
    }
    private void ReleaseTemplate() { if (_trigger is not null) _trigger.Click -= TriggerClick; if (_input is not null) { _input.TextChanged -= InputChanged; _input.PreviewKeyDown -= InputKeyDown; _input.GotKeyboardFocus -= InputFocus; } if (_clear is not null) _clear.Click -= ClearClick; _trigger = _clear = null; _input = null; _surface = _results = null; _motion.Stop(); }
    private static void OnQueryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (ExpandingSearch)d; if (c._input is not null && c._input.Text != (string)e.NewValue) c._input.Text = (string)e.NewValue; c.Refresh(false); }
    private static void OnExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ExpandingSearch)d).Refresh(true);
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ExpandingSearch)d).Refresh(false);
    // 输入框和结果面板使用同一个展开宽度，保证网页中的上下边界对齐。
    private void Refresh(bool animate)
    {
        if (_surface is null) return; bool full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full; bool motion = animate && IsLoaded && IsVisible && ArcMotion.GetLevel(this) != ArcMotionLevel.Off; _motion.Stop(); double target = IsExpanded ? WidthExpanded : WidthCollapsed; _motion.Double(_surface, FrameworkElement.WidthProperty, target, motion ? .22 : 0, motion, spring: full && IsExpanded, enter: true); bool showResults = IsExpanded && Items.Count > 0; if (_results is not null) { _results.Visibility = showResults ? Visibility.Visible : Visibility.Collapsed; _motion.Double(_results, FrameworkElement.WidthProperty, showResults ? WidthExpanded : 0, motion ? .22 : 0, motion, spring: full && showResults, enter: true); } if (_trigger is not null) _trigger.Visibility = IsExpanded ? Visibility.Collapsed : Visibility.Visible; if (_input is not null) { _input.Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed; _input.Text = Query; } if (_clear is not null) _clear.Visibility = IsExpanded && Query.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void TriggerClick(object? sender, RoutedEventArgs e) { Expand(); e.Handled = true; }
    private void InputFocus(object? sender, RoutedEventArgs e) { if (ExpandOnFocus && !IsExpanded) Expand(); }
    private void Expand() { if (IsExpanded || !IsEnabled) return; SetCurrentValue(IsExpandedProperty, true); RaiseEvent(new RoutedEventArgs(ExpandedEvent, this)); Dispatcher.BeginInvoke(new Action(() => { _input?.Focus(); _input?.SelectAll(); }), System.Windows.Threading.DispatcherPriority.Input); }
    private void Collapse() { if (!IsExpanded) return; SetCurrentValue(IsExpandedProperty, false); RaiseEvent(new RoutedEventArgs(CollapsedEvent, this)); _trigger?.Focus(); }
    private void InputChanged(object? sender, TextChangedEventArgs e) { if (_input is not null && Query != _input.Text) SetCurrentValue(QueryProperty, _input.Text); }
    private void InputKeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; if (Query.Length > 0) SetCurrentValue(QueryProperty, string.Empty); else Collapse(); } else if (e.Key == Key.Enter) { e.Handled = true; if (SubmitCommand?.CanExecute(SubmitCommandParameter) == true) SubmitCommand.Execute(SubmitCommandParameter); RaiseEvent(new RoutedEventArgs(SubmittedEvent, this)); } }
    private void ClearClick(object? sender, RoutedEventArgs e) { SetCurrentValue(QueryProperty, string.Empty); _input?.Focus(); e.Handled = true; }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); if (!IsExpanded) return; if (e.NewFocus is not DependencyObject d || d is Visual v && !v.IsDescendantOf(this)) Collapse(); }
    protected override AutomationPeer OnCreateAutomationPeer() => new ExpandingSearchPeer(this);
    private sealed class ExpandingSearchPeer(ExpandingSearch owner) : FrameworkElementAutomationPeer(owner) { protected override string GetClassNameCore() => nameof(ExpandingSearch); protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit; protected override string GetNameCore() => owner.Placeholder; }
}

public sealed class ExpandingSearchItem
{
    public string Title { get; set; } = "";
    public string Meta { get; set; } = "";
    public string IconGlyph { get; set; } = "▤";
}
