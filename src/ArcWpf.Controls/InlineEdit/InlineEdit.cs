using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum InlineEditVariant { Title, Body }
public enum InlineEditPhase { Idle, Saving, Saved, Failed }

[TemplatePart(Name = "PART_Display", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Editor", Type = typeof(System.Windows.Controls.TextBox))]
[TemplatePart(Name = "PART_Frame", Type = typeof(Border))]
[TemplatePart(Name = "PART_Actions", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Save", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Cancel", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Message", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Spinner", Type = typeof(FrameworkElement))]
public class InlineEdit : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(InlineEdit), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(InlineEdit), new FrameworkPropertyMetadata("Value", OnVisualChanged));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(InlineEdit), new FrameworkPropertyMetadata(string.Empty, OnVisualChanged));
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(InlineEditVariant), typeof(InlineEdit), new FrameworkPropertyMetadata(InlineEditVariant.Title, OnVisualChanged));
    public static readonly DependencyProperty MultilineProperty = DependencyProperty.Register(nameof(Multiline), typeof(bool), typeof(InlineEdit), new FrameworkPropertyMetadata(false, OnVisualChanged));
    public static readonly DependencyProperty ReadOnlyProperty = DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(InlineEdit), new FrameworkPropertyMetadata(false, OnVisualChanged));
    public static readonly DependencyProperty SaveAsyncProperty = DependencyProperty.Register(nameof(SaveAsync), typeof(Func<string, Task>), typeof(InlineEdit));
    public static readonly DependencyProperty ValidateProperty = DependencyProperty.Register(nameof(Validate), typeof(Func<string, string?>), typeof(InlineEdit));
    private static readonly DependencyPropertyKey IsEditingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsEditing), typeof(bool), typeof(InlineEdit), new FrameworkPropertyMetadata(false, OnVisualChanged));
    public static readonly DependencyProperty IsEditingProperty = IsEditingPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey PhasePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Phase), typeof(InlineEditPhase), typeof(InlineEdit), new FrameworkPropertyMetadata(InlineEditPhase.Idle, OnVisualChanged));
    public static readonly DependencyProperty PhaseProperty = PhasePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey ErrorPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Error), typeof(string), typeof(InlineEdit), new FrameworkPropertyMetadata(string.Empty, OnVisualChanged));
    public static readonly DependencyProperty ErrorProperty = ErrorPropertyKey.DependencyProperty;
    public static readonly RoutedEvent SavedEvent = EventManager.RegisterRoutedEvent(nameof(Saved), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(InlineEdit));
    public static readonly RoutedEvent SaveFailedEvent = EventManager.RegisterRoutedEvent(nameof(SaveFailed), RoutingStrategy.Bubble, typeof(EventHandler<InlineEditSaveFailedEventArgs>), typeof(InlineEdit));
    public static readonly RoutedEvent EditCancelledEvent = EventManager.RegisterRoutedEvent(nameof(EditCancelled), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(InlineEdit));

    private System.Windows.Controls.Button? _display, _save, _cancel;
    private System.Windows.Controls.TextBox? _editor;
    private Border? _frame;
    private Panel? _actions;
    private TextBlock? _message; private FrameworkElement? _spinner;
    private string _committed = string.Empty;
    private string _draft = string.Empty;
    private int _saveGeneration;
    private bool _internalValueUpdate;
    private DispatcherTimer? _savedTimer;
    private bool _templateReady;

    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public InlineEditVariant Variant { get => (InlineEditVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool Multiline { get => (bool)GetValue(MultilineProperty); set => SetValue(MultilineProperty, value); }
    public bool ReadOnly { get => (bool)GetValue(ReadOnlyProperty); set => SetValue(ReadOnlyProperty, value); }
    public Func<string, Task>? SaveAsync { get => (Func<string, Task>?)GetValue(SaveAsyncProperty); set => SetValue(SaveAsyncProperty, value); }
    public Func<string, string?>? Validate { get => (Func<string, string?>?)GetValue(ValidateProperty); set => SetValue(ValidateProperty, value); }
    public bool IsEditing => (bool)GetValue(IsEditingProperty);
    public InlineEditPhase Phase => (InlineEditPhase)GetValue(PhaseProperty);
    public string Error => (string)GetValue(ErrorProperty);
    public event RoutedEventHandler Saved { add => AddHandler(SavedEvent, value); remove => RemoveHandler(SavedEvent, value); }
    public event EventHandler<InlineEditSaveFailedEventArgs> SaveFailed { add => AddHandler(SaveFailedEvent, value); remove => RemoveHandler(SaveFailedEvent, value); }
    public event RoutedEventHandler EditCancelled { add => AddHandler(EditCancelledEvent, value); remove => RemoveHandler(EditCancelledEvent, value); }

    static InlineEdit() { DefaultStyleKeyProperty.OverrideMetadata(typeof(InlineEdit), new FrameworkPropertyMetadata(typeof(InlineEdit))); }
    public InlineEdit() { Loaded += (_, _) => { _committed = Value; RefreshTemplate(); }; Unloaded += (_, _) => { _savedTimer?.Stop(); }; }

    public override void OnApplyTemplate()
    {
        ReleaseTemplate(); base.OnApplyTemplate();
        _display = GetTemplateChild("PART_Display") as System.Windows.Controls.Button; _editor = GetTemplateChild("PART_Editor") as System.Windows.Controls.TextBox; _frame = GetTemplateChild("PART_Frame") as Border; _actions = GetTemplateChild("PART_Actions") as Panel; _save = GetTemplateChild("PART_Save") as System.Windows.Controls.Button; _cancel = GetTemplateChild("PART_Cancel") as System.Windows.Controls.Button; _message = GetTemplateChild("PART_Message") as TextBlock; _spinner = GetTemplateChild("PART_Spinner") as FrameworkElement;
        if (_display is not null) _display.Click += DisplayClick;
        if (_editor is not null) { _editor.TextChanged += EditorChanged; _editor.PreviewKeyDown += EditorKeyDown; }
        if (_save is not null) _save.Click += SaveClick;
        if (_cancel is not null) _cancel.Click += CancelClick;
        AddHandler(LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(LostFocusWithin), true); _templateReady = true; RefreshTemplate();
    }
    private void ReleaseTemplate()
    {
        if (_display is not null) _display.Click -= DisplayClick; if (_editor is not null) { _editor.TextChanged -= EditorChanged; _editor.PreviewKeyDown -= EditorKeyDown; } if (_save is not null) _save.Click -= SaveClick; if (_cancel is not null) _cancel.Click -= CancelClick; RemoveHandler(LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(LostFocusWithin)); _templateReady = false;
    }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((InlineEdit)d).RefreshTemplate();
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (InlineEdit)d; if (!c._internalValueUpdate && !c.IsEditing && c.Phase != InlineEditPhase.Saving) c._committed = (string)e.NewValue; c.RefreshTemplate(); }
    private void RefreshTemplate()
    {
        if (!_templateReady) return; bool editing = IsEditing; string text = editing ? _draft : Value; if (_display is not null) { _display.Content = string.IsNullOrEmpty(text) ? Placeholder : text; _display.IsEnabled = !ReadOnly && Phase != InlineEditPhase.Saving; _display.Visibility = editing ? Visibility.Collapsed : Visibility.Visible; }
        if (_editor is not null) { _editor.Text = _draft; _editor.Visibility = editing ? Visibility.Visible : Visibility.Collapsed; _editor.AcceptsReturn = Multiline; _editor.TextWrapping = Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap; _editor.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled; _editor.MinLines = Multiline ? 1 : 1; }
        if (_frame is not null) { _frame.Visibility = editing || Phase is InlineEditPhase.Failed or InlineEditPhase.Saving or InlineEditPhase.Saved ? Visibility.Visible : Visibility.Collapsed; _frame.BorderBrush = Error.Length > 0 || Phase == InlineEditPhase.Failed ? (Brush?)TryFindResource("ArcWpf.Brush.Danger") : (Brush?)TryFindResource(editing ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.SurfaceMuted"); _frame.Background = editing ? (Brush?)TryFindResource("ArcWpf.Brush.Surface") : Brushes.Transparent; }
        if (_actions is not null) _actions.Visibility = editing && Phase != InlineEditPhase.Saving ? Visibility.Visible : Visibility.Collapsed;
        if (_spinner is not null) _spinner.Visibility = Phase == InlineEditPhase.Saving ? Visibility.Visible : Visibility.Collapsed;
        if (_message is not null) { _message.Text = Error; _message.Visibility = string.IsNullOrEmpty(Error) ? Visibility.Collapsed : Visibility.Visible; }
    }
    private void DisplayClick(object? sender, RoutedEventArgs e) { if (!ReadOnly && Phase != InlineEditPhase.Saving) BeginEdit(); e.Handled = true; }
    private void BeginEdit()
    {
        _draft = Value; SetValue(IsEditingPropertyKey, true); SetValue(PhasePropertyKey, InlineEditPhase.Idle); SetValue(ErrorPropertyKey, string.Empty); RefreshTemplate(); Dispatcher.BeginInvoke(new Action(() => { _editor?.Focus(); _editor?.SelectAll(); }), DispatcherPriority.Input);
    }
    private void EditorChanged(object? sender, TextChangedEventArgs e) { if (IsEditing && _editor is not null) _draft = _editor.Text; }
    private void EditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; CancelEdit(); return; }
        if (e.Key == Key.Enter && !(Multiline && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) && !e.IsRepeat) { e.Handled = true; Submit(); }
    }
    private void SaveClick(object? sender, RoutedEventArgs e) { e.Handled = true; Submit(); }
    private void CancelClick(object? sender, RoutedEventArgs e) { e.Handled = true; CancelEdit(); }
    private void LostFocusWithin(object? sender, KeyboardFocusChangedEventArgs e)
    {
        if (!IsEditing || e.NewFocus is DependencyObject d && IsInsideControl(d)) return;
        Submit();
    }
    private bool IsInsideControl(DependencyObject d) => d is Visual v && (ReferenceEquals(v, this) || v.IsDescendantOf(this));
    private void CancelEdit() { _saveGeneration++; SetValue(IsEditingPropertyKey, false); SetValue(ErrorPropertyKey, string.Empty); _draft = Value; SetValue(PhasePropertyKey, InlineEditPhase.Idle); RefreshTemplate(); RaiseEvent(new RoutedEventArgs(EditCancelledEvent, this)); _display?.Focus(); }
    private void Submit()
    {
        if (!IsEditing || Phase == InlineEditPhase.Saving) return; string next = Multiline ? _draft.Trim() : string.Join(" ", _draft.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim(); string? error = Validate?.Invoke(next); if (!string.IsNullOrEmpty(error)) { SetValue(ErrorPropertyKey, error); RefreshTemplate(); _editor?.Focus(); return; }
        if (next == Value) { SetValue(IsEditingPropertyKey, false); SetValue(ErrorPropertyKey, string.Empty); SetValue(PhasePropertyKey, InlineEditPhase.Idle); RefreshTemplate(); _display?.Focus(); return; }
        SetValue(IsEditingPropertyKey, false); SetValue(ErrorPropertyKey, string.Empty); _committed = Value; _internalValueUpdate = true; SetCurrentValue(ValueProperty, next); _internalValueUpdate = false;
        int run = ++_saveGeneration; SetValue(PhasePropertyKey, InlineEditPhase.Saving); RefreshTemplate(); Task task; try { task = SaveAsync?.Invoke(next) ?? Task.CompletedTask; } catch (Exception ex) { HandleSaveFailure(run, ex); return; }
        _ = task.ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (t.IsFaulted) HandleSaveFailure(run, t.Exception?.GetBaseException() ?? new Exception("Save failed"));
            else HandleSaveSuccess(run);
        }), DispatcherPriority.Normal), TaskScheduler.Default);
    }
    private void HandleSaveSuccess(int run) { if (run != _saveGeneration) return; SetValue(PhasePropertyKey, InlineEditPhase.Saved); RefreshTemplate(); RaiseEvent(new RoutedEventArgs(SavedEvent, this)); _savedTimer?.Stop(); _savedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) }; _savedTimer.Tick += (_, _) => { _savedTimer!.Stop(); if (run == _saveGeneration) { SetValue(PhasePropertyKey, InlineEditPhase.Idle); RefreshTemplate(); } }; _savedTimer.Start(); _display?.Focus(); }
    private void HandleSaveFailure(int run, Exception exception) { if (run != _saveGeneration) return; _internalValueUpdate = true; SetCurrentValue(ValueProperty, _committed); _internalValueUpdate = false; SetValue(PhasePropertyKey, InlineEditPhase.Failed); SetValue(ErrorPropertyKey, exception.Message); RefreshTemplate(); RaiseEvent(new InlineEditSaveFailedEventArgs(SaveFailedEvent, this, exception)); _display?.Focus(); }
    protected override AutomationPeer OnCreateAutomationPeer() => new InlineEditAutomationPeer(this);
    private sealed class InlineEditAutomationPeer(InlineEdit owner) : FrameworkElementAutomationPeer(owner) { protected override string GetClassNameCore() => nameof(InlineEdit); protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit; protected override string GetNameCore() => owner.Label; }
}

public sealed class InlineEditSaveFailedEventArgs(RoutedEvent routedEvent, object source, Exception exception) : RoutedEventArgs(routedEvent, source) { public Exception Exception { get; } = exception; }
