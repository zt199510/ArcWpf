using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ArcWpf.Controls;

public class TagInput : Control
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TagInput), new PropertyMetadata("Tags"));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(TagInput), new PropertyMetadata("Add a tag"));
    public static readonly DependencyProperty MaxTagsProperty = DependencyProperty.Register(nameof(MaxTags), typeof(int), typeof(TagInput), new PropertyMetadata(0));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(TagInput), new PropertyMetadata(false));
    public static readonly RoutedEvent TagAddedEvent = EventManager.RegisterRoutedEvent(nameof(TagAdded), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TagInput));
    public static readonly RoutedEvent TagRemovedEvent = EventManager.RegisterRoutedEvent(nameof(TagRemoved), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TagInput));
    private TextBox? _input; private ItemsControl? _items;
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public int MaxTags { get => (int)GetValue(MaxTagsProperty); set => SetValue(MaxTagsProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public ObservableCollection<string> Tags { get; } = [];
    public event RoutedEventHandler TagAdded { add => AddHandler(TagAddedEvent, value); remove => RemoveHandler(TagAddedEvent, value); }
    public event RoutedEventHandler TagRemoved { add => AddHandler(TagRemovedEvent, value); remove => RemoveHandler(TagRemovedEvent, value); }
    static TagInput() { DefaultStyleKeyProperty.OverrideMetadata(typeof(TagInput), new FrameworkPropertyMetadata(typeof(TagInput))); }
    public TagInput() { Tags.CollectionChanged += TagsChanged; }
    public override void OnApplyTemplate() { ReleaseTemplate(); base.OnApplyTemplate(); _input = GetTemplateChild("PART_Input") as TextBox; _items = GetTemplateChild("PART_Items") as ItemsControl; if (_input is not null) _input.PreviewKeyDown += InputKeyDown; if (_items is not null) _items.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(TagButtonClick), true); Refresh(); }
    private void ReleaseTemplate() { if (_input is not null) _input.PreviewKeyDown -= InputKeyDown; if (_items is not null) _items.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(TagButtonClick)); _input = null; _items = null; }
    private void TagButtonClick(object? sender, RoutedEventArgs e) { if (e.OriginalSource is Button button && button.Tag is string tag) { RemoveTag(tag); e.Handled = true; } }
    private void TagsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
    private void Refresh() { if (_items is not null) _items.ItemsSource = Tags; if (_input is not null) { _input.IsEnabled = !IsReadOnly && (MaxTags <= 0 || Tags.Count < MaxTags); _input.Text = _input.Text; } }
    private void InputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.OemComma or Key.Separator) { e.Handled = true; AddCurrent(); }
        else if (e.Key == Key.Back && _input?.Text.Length == 0 && Tags.Count > 0) { Tags.RemoveAt(Tags.Count - 1); RaiseEvent(new RoutedEventArgs(TagRemovedEvent, this)); e.Handled = true; }
    }
    private void AddCurrent() { if (_input is null || IsReadOnly || (MaxTags > 0 && Tags.Count >= MaxTags)) return; string value = _input.Text.Trim().TrimEnd(','); if (value.Length == 0 || Tags.Contains(value, StringComparer.CurrentCultureIgnoreCase)) { _input.Clear(); return; } Tags.Add(value); _input.Clear(); RaiseEvent(new RoutedEventArgs(TagAddedEvent, this)); }
    public void RemoveTag(string tag) { if (Tags.Remove(tag)) RaiseEvent(new RoutedEventArgs(TagRemovedEvent, this)); }
    protected override AutomationPeer OnCreateAutomationPeer() => new TagInputPeer(this);
    private sealed class TagInputPeer(TagInput owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit; protected override string GetNameCore() => owner.Label; }
}
