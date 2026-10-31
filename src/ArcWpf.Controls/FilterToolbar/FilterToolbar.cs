using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

public sealed class FilterToolbarFilter : DependencyObject
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(FilterToolbarFilter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(FilterToolbarFilter), new PropertyMetadata(string.Empty));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}

public class FilterToolbar : Control
{
    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(nameof(Query), typeof(string), typeof(FilterToolbar), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(FilterToolbar), new PropertyMetadata("Search"));
    public static readonly DependencyProperty ClearLabelProperty = DependencyProperty.Register(nameof(ClearLabel), typeof(string), typeof(FilterToolbar), new PropertyMetadata("Clear all"));
    public static readonly DependencyProperty HasActiveFiltersProperty = DependencyProperty.Register(nameof(HasActiveFilters), typeof(bool), typeof(FilterToolbar), new FrameworkPropertyMetadata(false, OnVisualChanged));
    public static readonly RoutedEvent QueryChangedEvent = EventManager.RegisterRoutedEvent(nameof(QueryChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilterToolbar));
    public static readonly RoutedEvent FiltersClearedEvent = EventManager.RegisterRoutedEvent(nameof(FiltersCleared), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilterToolbar));
    private TextBox? _query; private ItemsControl? _filters; private Button? _clear;
    public string Query { get => (string)GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string ClearLabel { get => (string)GetValue(ClearLabelProperty); set => SetValue(ClearLabelProperty, value); }
    public bool HasActiveFilters { get => (bool)GetValue(HasActiveFiltersProperty); set => SetValue(HasActiveFiltersProperty, value); }
    public ObservableCollection<FilterToolbarFilter> Filters { get; } = [];
    public event RoutedEventHandler QueryChanged { add => AddHandler(QueryChangedEvent, value); remove => RemoveHandler(QueryChangedEvent, value); }
    public event RoutedEventHandler FiltersCleared { add => AddHandler(FiltersClearedEvent, value); remove => RemoveHandler(FiltersClearedEvent, value); }
    static FilterToolbar() { DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterToolbar), new FrameworkPropertyMetadata(typeof(FilterToolbar))); }
    public FilterToolbar() { Filters.CollectionChanged += FiltersChanged; Loaded += (_, _) => Refresh(); }
    public override void OnApplyTemplate()
    {
        ReleaseTemplate(); base.OnApplyTemplate(); _query = GetTemplateChild("PART_Query") as TextBox; _filters = GetTemplateChild("PART_Filters") as ItemsControl; _clear = GetTemplateChild("PART_Clear") as Button;
        if (_query is not null) _query.TextChanged += QueryTextChanged; if (_clear is not null) _clear.Click += ClearClick; Refresh();
    }
    private void ReleaseTemplate() { if (_query is not null) _query.TextChanged -= QueryTextChanged; if (_clear is not null) _clear.Click -= ClearClick; _query = null; _filters = null; _clear = null; }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FilterToolbar)d).Refresh();
    private void FiltersChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
    private void Refresh() { if (_query is not null && _query.Text != Query) _query.Text = Query; if (_filters is not null) _filters.ItemsSource = Filters; if (_clear is not null) _clear.Visibility = HasActiveFilters || Query.Length > 0 ? Visibility.Visible : Visibility.Collapsed; }
    private void QueryTextChanged(object? sender, TextChangedEventArgs e) { if (_query is not null && Query != _query.Text) { SetCurrentValue(QueryProperty, _query.Text); RaiseEvent(new RoutedEventArgs(QueryChangedEvent, this)); } }
    private void ClearClick(object? sender, RoutedEventArgs e) { SetCurrentValue(QueryProperty, string.Empty); HasActiveFilters = false; RaiseEvent(new RoutedEventArgs(FiltersClearedEvent, this)); e.Handled = true; }
    protected override AutomationPeer OnCreateAutomationPeer() => new FilterToolbarPeer(this);
    private sealed class FilterToolbarPeer(FilterToolbar owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar; protected override string GetNameCore() => "Filters"; }
}
