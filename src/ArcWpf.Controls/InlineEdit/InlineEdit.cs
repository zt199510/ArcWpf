using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum InlineEditVariant { Title, Body }
public enum InlineEditPhase { Idle, Saving, Saved, Failed }

/// <summary>
/// Click-to-edit text. The text turns into a field in place with the same metrics, so nothing around it moves, and the frame grows with what
/// is typed. Enter saves optimistically and a check draws once the save lands; Escape rolls the text back. A failed save restores the last
/// saved value and says why.
/// </summary>
[TemplatePart(Name = "PART_Box", Type = typeof(InlineEditBox))]
[TemplatePart(Name = "PART_Display", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_TextHost", Type = typeof(Panel))]
[TemplatePart(Name = "PART_LeavingHost", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_Editor", Type = typeof(System.Windows.Controls.TextBox))]
[TemplatePart(Name = "PART_Frame", Type = typeof(Border))]
[TemplatePart(Name = "PART_Ring", Type = typeof(Border))]
[TemplatePart(Name = "PART_Actions", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Save", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Cancel", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Spinner", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_SpinnerPath", Type = typeof(Shape))]
[TemplatePart(Name = "PART_Saved", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_SavedPath", Type = typeof(Shape))]
[TemplatePart(Name = "PART_FailedIcon", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Pencil", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_PencilGlyph", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Reveal", Type = typeof(InlineEditReveal))]
[TemplatePart(Name = "PART_MessageHost", Type = typeof(Panel))]
public class InlineEdit : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(InlineEdit), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(InlineEdit), new FrameworkPropertyMetadata("Value", OnTextPropertyChanged));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(InlineEdit), new FrameworkPropertyMetadata(string.Empty, OnTextPropertyChanged));
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(InlineEditVariant), typeof(InlineEdit), new FrameworkPropertyMetadata(InlineEditVariant.Title, OnMetricsChanged));
    public static readonly DependencyProperty MultilineProperty = DependencyProperty.Register(nameof(Multiline), typeof(bool), typeof(InlineEdit), new FrameworkPropertyMetadata(false, OnMetricsChanged));
    public static readonly DependencyProperty ReadOnlyProperty = DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(InlineEdit), new FrameworkPropertyMetadata(false, OnAppearanceChanged));
    public static readonly DependencyProperty SaveAsyncProperty = DependencyProperty.Register(nameof(SaveAsync), typeof(Func<string, Task>), typeof(InlineEdit));
    public static readonly DependencyProperty ValidateProperty = DependencyProperty.Register(nameof(Validate), typeof(Func<string, string?>), typeof(InlineEdit));
    private static readonly DependencyPropertyKey IsEditingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsEditing), typeof(bool), typeof(InlineEdit), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsEditingProperty = IsEditingPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey PhasePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Phase), typeof(InlineEditPhase), typeof(InlineEdit), new FrameworkPropertyMetadata(InlineEditPhase.Idle));
    public static readonly DependencyProperty PhaseProperty = PhasePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey ErrorPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Error), typeof(string), typeof(InlineEdit), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty ErrorProperty = ErrorPropertyKey.DependencyProperty;
    // Theme inputs; the style feeds them with dynamic resources so a theme switch repaints the owned, animated brushes.
    public static readonly DependencyProperty InkBrushProperty = ThemeBrush(nameof(InkBrush));
    public static readonly DependencyProperty MutedBrushProperty = ThemeBrush(nameof(MutedBrush));
    public static readonly DependencyProperty SecondaryBrushProperty = ThemeBrush(nameof(SecondaryBrush));
    public static readonly DependencyProperty SurfaceBrushProperty = ThemeBrush(nameof(SurfaceBrush));
    public static readonly DependencyProperty HoverBrushProperty = ThemeBrush(nameof(HoverBrush));
    public static readonly DependencyProperty DangerBrushProperty = ThemeBrush(nameof(DangerBrush));
    public static readonly DependencyProperty SuccessBrushProperty = ThemeBrush(nameof(SuccessBrush));
    public static readonly DependencyProperty CanvasBrushProperty = ThemeBrush(nameof(CanvasBrush));
    public static readonly DependencyProperty LineBrushProperty = ThemeBrush(nameof(LineBrush));
    public static readonly DependencyProperty LineStrongBrushProperty = ThemeBrush(nameof(LineStrongBrush));
    public static readonly RoutedEvent SavedEvent = EventManager.RegisterRoutedEvent(nameof(Saved), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(InlineEdit));
    public static readonly RoutedEvent SaveFailedEvent = EventManager.RegisterRoutedEvent(nameof(SaveFailed), RoutingStrategy.Bubble, typeof(EventHandler<InlineEditSaveFailedEventArgs>), typeof(InlineEdit));
    public static readonly RoutedEvent EditCancelledEvent = EventManager.RegisterRoutedEvent(nameof(EditCancelled), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(InlineEdit));

    // motion-tokens.ts: durations, blur radii and springs (settling at about 2.07 × visualDuration).
    private const double Instant = .12, Fast = .16, Standard = .24, Considered = .48, SubtleBlur = 2, SoftBlur = 4, Rise = .3;
    private const double Snappy = .26, Smooth = .4, Morph = .42, Typing = .16;
    private static double Spring(double visualDuration) => visualDuration * 2.07;
    private const double PadX = 8, CheckDash = 11.5;
    private const string ZeroWidth = "\u200b";
    // lucide circle-alert, as in the theme; built here because the message row is created in code.
    private static readonly Geometry AlertGeometry = Frozen(Geometry.Parse("M2,12 a10,10 0 1 0 20,0 a10,10 0 1 0 -20,0 M12,8 v4 M12,16 h0.01"));
    private static Geometry Frozen(Geometry geometry) { geometry.Freeze(); return geometry; }

    private enum Slot { None, Idle, Edit, Saving, Saved, Failed }
    private enum Flash { None, On, Off }
    private enum Source { Key, Button, Blur }

    private InlineEditBox? _box;
    private System.Windows.Controls.Button? _display, _save, _cancel;
    private Panel? _textHost, _messageHost;
    private Canvas? _leavingHost;
    private System.Windows.Controls.TextBox? _editor;
    private Border? _frame, _ring;
    private FrameworkElement? _actions, _spinner, _saved, _failedIcon, _pencil, _pencilGlyph;
    private Shape? _spinnerPath, _savedPath;
    private InlineEditReveal? _reveal;
    private TextBlock? _layer;
    private FrameworkElement? _message;
    private string? _messageKey;
    private readonly SolidColorBrush _frameFill = new(Colors.Transparent), _frameLine = new(Colors.Transparent), _ringLine = new(Colors.Transparent);
    private readonly SolidColorBrush _saveFill = new(), _saveInk = new(), _cancelFill = new(), _cancelLine = new(), _cancelInk = new();
    private readonly ScaleTransform _saveScale = new(1, 1), _cancelScale = new(1, 1);
    private readonly OwnedMotion _sizeMotion = new(), _textMotion = new(), _slotMotion = new(), _colorMotion = new(), _revealMotion = new();
    private readonly List<DispatcherTimer> _timers = [];
    private readonly RotateTransform _spin = new();
    private string _committed = string.Empty, _shown = string.Empty, _seen = string.Empty, _draft = string.Empty;
    private string? _failed;
    private Flash _flash;
    private Slot _slot;
    private int _saveRun;
    private int? _selectAt; private bool _selectAll, _focusDisplay, _syncingEditor, _internalValue, _contentPending, _wasEditing, _revealMeasured;
    private double _size;
    private Window? _window;

    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public InlineEditVariant Variant { get => (InlineEditVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool Multiline { get => (bool)GetValue(MultilineProperty); set => SetValue(MultilineProperty, value); }
    public bool ReadOnly { get => (bool)GetValue(ReadOnlyProperty); set => SetValue(ReadOnlyProperty, value); }
    /// <summary>Persists the new value. A faulted task rolls the text back and offers a retry.</summary>
    public Func<string, Task>? SaveAsync { get => (Func<string, Task>?)GetValue(SaveAsyncProperty); set => SetValue(SaveAsyncProperty, value); }
    /// <summary>Returns a message when the draft cannot be saved.</summary>
    public Func<string, string?>? Validate { get => (Func<string, string?>?)GetValue(ValidateProperty); set => SetValue(ValidateProperty, value); }
    public bool IsEditing => (bool)GetValue(IsEditingProperty);
    public InlineEditPhase Phase => (InlineEditPhase)GetValue(PhaseProperty);
    public string Error => (string)GetValue(ErrorProperty);
    [EditorBrowsable(EditorBrowsableState.Never)] public Brush? InkBrush { get => (Brush?)GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }
    [EditorBrowsable(EditorBrowsableState.Never)] public Brush? MutedBrush { get => (Brush?)GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    [EditorBrowsable(EditorBrowsableState.Never)] public Brush? SecondaryBrush { get => (Brush?)GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }
    [EditorBrowsable(EditorBrowsableState.Never)] public Brush? SurfaceBrush { get => (Brush?)GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    [EditorBrowsable(EditorBrowsableState.Never)] public Brush? HoverBrush { get => (Brush?)GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }
    [EditorBrowsable(EditorBrowsableState.Never)] public Brush? DangerBrush { get => (Brush?)GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    [EditorBrowsable(EditorBrowsableState.Never)] public Brush? SuccessBrush { get => (Brush?)GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }
    [EditorBrowsable(EditorBrowsableState.Never)] public Brush? CanvasBrush { get => (Brush?)GetValue(CanvasBrushProperty); set => SetValue(CanvasBrushProperty, value); }
    [EditorBrowsable(EditorBrowsableState.Never)] public Brush? LineBrush { get => (Brush?)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    [EditorBrowsable(EditorBrowsableState.Never)] public Brush? LineStrongBrush { get => (Brush?)GetValue(LineStrongBrushProperty); set => SetValue(LineStrongBrushProperty, value); }
    public event RoutedEventHandler Saved { add => AddHandler(SavedEvent, value); remove => RemoveHandler(SavedEvent, value); }
    public event EventHandler<InlineEditSaveFailedEventArgs> SaveFailed { add => AddHandler(SaveFailedEvent, value); remove => RemoveHandler(SaveFailedEvent, value); }
    public event RoutedEventHandler EditCancelled { add => AddHandler(EditCancelledEvent, value); remove => RemoveHandler(EditCancelledEvent, value); }

    private ArcMotionLevel Level => ArcMotion.GetLevel(this);
    private bool Moving => Level != ArcMotionLevel.Off && IsLoaded;
    private bool Full => Level == ArcMotionLevel.Full && IsLoaded;
    private bool Saving => Phase == InlineEditPhase.Saving;
    private string LayerText => IsEditing ? _draft : _shown;
    private string Noun => Label.ToLowerInvariant();
    // Whole-pixel line heights, as the CSS rounds them: title round(size × 1.3, 2px), body round(size × 1.5, 1px).
    private double Leading => Variant == InlineEditVariant.Title ? Math.Round(FontSize * 1.3 / 2) * 2 : Math.Round(FontSize * 1.5);
    private double PadY => Variant == InlineEditVariant.Title ? 3 : 5;

    static InlineEdit() { DefaultStyleKeyProperty.OverrideMetadata(typeof(InlineEdit), new FrameworkPropertyMetadata(typeof(InlineEdit))); }
    public InlineEdit()
    {
        Loaded += (_, _) => { _contentPending = false; UpdateSpinner(); UpdateAppearance(false); };
        Unloaded += (_, _) => { foreach (var timer in _timers) timer.Stop(); _timers.Clear(); UnhookWindow(); _textMotion.Stop(); _slotMotion.Stop(); _sizeMotion.Stop(); _revealMotion.Stop(); _spin.BeginAnimation(RotateTransform.AngleProperty, null); };
        SetCurrentValue(AutomationProperties.LiveSettingProperty, AutomationLiveSetting.Polite);
    }
    private static DependencyProperty ThemeBrush(string name) => DependencyProperty.Register(name, typeof(Brush), typeof(InlineEdit), new FrameworkPropertyMetadata(null, OnAppearanceChanged));

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _box = GetTemplateChild("PART_Box") as InlineEditBox; _display = GetTemplateChild("PART_Display") as System.Windows.Controls.Button; _textHost = GetTemplateChild("PART_TextHost") as Panel; _leavingHost = GetTemplateChild("PART_LeavingHost") as Canvas;
        _editor = GetTemplateChild("PART_Editor") as System.Windows.Controls.TextBox; _frame = GetTemplateChild("PART_Frame") as Border; _ring = GetTemplateChild("PART_Ring") as Border;
        _actions = GetTemplateChild("PART_Actions") as FrameworkElement; _save = GetTemplateChild("PART_Save") as System.Windows.Controls.Button; _cancel = GetTemplateChild("PART_Cancel") as System.Windows.Controls.Button;
        _spinner = GetTemplateChild("PART_Spinner") as FrameworkElement; _spinnerPath = GetTemplateChild("PART_SpinnerPath") as Shape; _saved = GetTemplateChild("PART_Saved") as FrameworkElement; _savedPath = GetTemplateChild("PART_SavedPath") as Shape;
        _failedIcon = GetTemplateChild("PART_FailedIcon") as FrameworkElement; _pencil = GetTemplateChild("PART_Pencil") as FrameworkElement; _pencilGlyph = GetTemplateChild("PART_PencilGlyph") as FrameworkElement;
        _reveal = GetTemplateChild("PART_Reveal") as InlineEditReveal; _messageHost = GetTemplateChild("PART_MessageHost") as Panel;
        if (_box is not null) _box.NaturalSizeChanged += BoxNaturalSizeChanged;
        if (_display is not null) { _display.Click += DisplayClick; _display.MouseEnter += HoverChanged; _display.MouseLeave += HoverChanged; _display.GotKeyboardFocus += HoverChanged; _display.LostKeyboardFocus += HoverChanged; }
        if (_editor is not null) { _editor.TextChanged += EditorChanged; _editor.PreviewKeyDown += EditorKeyDown; }
        if (_save is not null) { _save.Click += SaveClick; Wire(_save, _saveFill, _saveFill, _saveInk, _saveScale); }
        if (_cancel is not null) { _cancel.Click += CancelClick; Wire(_cancel, _cancelFill, _cancelLine, _cancelInk, _cancelScale); }
        if (_pencil is not null) { _pencil.MouseLeftButtonUp += PencilClick; _pencil.MouseEnter += HoverChanged; _pencil.MouseLeave += HoverChanged; }
        if (_frame is not null) { _frame.Background = _frameFill; _frame.BorderBrush = _frameLine; _frame.SizeChanged += FrameSizeChanged; }
        if (_ring is not null) _ring.BorderBrush = _ringLine;
        if (_reveal is not null) _reveal.NaturalHeightChanged += RevealNaturalHeightChanged;
        if (_spinnerPath is not null) _spinnerPath.RenderTransform = _spin;
        foreach (var item in SlotItems()) { item.RenderTransform = new ScaleTransform(1, 1); item.RenderTransformOrigin = new Point(.5, .5); }
        _layer = null; _message = null; _messageKey = null; _slot = Slot.None; _size = 0; _revealMeasured = false;
        if (!IsEditing && Phase != InlineEditPhase.Saving) { _shown = _committed = _seen = Value ?? string.Empty; }
        ApplyMetrics(); NewLayer(1, false); SyncEditor(); UpdateSlot(false); UpdateMessage(false); UpdateAppearance(false); UpdateAutomation();
    }
    private void ReleaseTemplate()
    {
        if (_box is not null) _box.NaturalSizeChanged -= BoxNaturalSizeChanged;
        if (_display is not null) { _display.Click -= DisplayClick; _display.MouseEnter -= HoverChanged; _display.MouseLeave -= HoverChanged; _display.GotKeyboardFocus -= HoverChanged; _display.LostKeyboardFocus -= HoverChanged; }
        if (_editor is not null) { _editor.TextChanged -= EditorChanged; _editor.PreviewKeyDown -= EditorKeyDown; }
        if (_save is not null) { _save.Click -= SaveClick; Unwire(_save); }
        if (_cancel is not null) { _cancel.Click -= CancelClick; Unwire(_cancel); }
        if (_pencil is not null) { _pencil.MouseLeftButtonUp -= PencilClick; _pencil.MouseEnter -= HoverChanged; _pencil.MouseLeave -= HoverChanged; }
        if (_frame is not null) _frame.SizeChanged -= FrameSizeChanged;
        if (_reveal is not null) _reveal.NaturalHeightChanged -= RevealNaturalHeightChanged;
        _textMotion.Stop(); _slotMotion.Stop(); _sizeMotion.Stop(); _revealMotion.Stop();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((InlineEdit)d).ValueChanged((string?)e.NewValue ?? string.Empty);
    private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var edit = (InlineEdit)d; edit.MarkContent(); edit.UpdateLayerText(); edit.UpdateMessage(true); edit.UpdateAutomation(); }
    private static void OnMetricsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var edit = (InlineEdit)d; edit.MarkContent(); edit.ApplyMetrics(); edit.UpdateLayerText(); }
    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var edit = (InlineEdit)d; edit.UpdateAppearance(false); edit.UpdateLayerText(); edit.UpdateSlot(false); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) { base.OnPropertyChanged(e); if (e.Property == FontSizeProperty) { MarkContent(); ApplyMetrics(); } }

    /// <summary>A new value from outside replaces the text, unless the person is typing or a save is still in flight.</summary>
    private void ValueChanged(string value)
    {
        if (_internalValue || value == _seen) return;
        _seen = value;
        if (value == _committed) return;
        _committed = value;
        if (!IsEditing && !Saving && value != _shown) { _shown = value; MarkContent(); NewLayer(1, true); UpdateAutomation(); }
    }

    // ---- Editing --------------------------------------------------------------------------------------------------------------------

    private void StartEdit(int? at, string? text = null)
    {
        if (IsEditing || Saving || ReadOnly || !IsEnabled) return;
        _draft = text ?? _shown;
        _failed = null; SetFlash(Flash.None);
        SetValue(ErrorPropertyKey, string.Empty);
        if (Phase != InlineEditPhase.Idle) SetValue(PhasePropertyKey, InlineEditPhase.Idle);
        _selectAt = at; _selectAll = at is null;
        MarkContent(); SetValue(IsEditingPropertyKey, true);
        HookWindow(); ApplyMetrics(); SyncEditor(); UpdateLayerText(); UpdateSlot(true); UpdateMessage(true); UpdateAppearance(true); UpdateAutomation();
        FocusEditor();
    }
    private void FocusEditor()
    {
        if (_editor is null || !IsEditing) return;
        if (!_editor.Focus()) { Dispatcher.BeginInvoke(new Action(() => { if (IsEditing) { _editor.Focus(); ApplySelection(); } }), DispatcherPriority.Loaded); return; }
        ApplySelection();
    }
    private void ApplySelection()
    {
        if (_editor is null) return;
        if (_selectAll) _editor.SelectAll(); else if (_selectAt is int at) { _editor.Select(Math.Min(at, _editor.Text.Length), 0); }
        _selectAll = false; _selectAt = null;
    }

    private string Clean(string text) => Multiline ? text.Trim() : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private void Submit(Source source)
    {
        if (!IsEditing) return;
        string next = Clean(_draft);
        string? problem = Validate?.Invoke(next);
        if (!string.IsNullOrEmpty(problem)) { SetValue(ErrorPropertyKey, problem); if (source != Source.Blur) _editor?.Focus(); UpdateMessage(true); UpdateAppearance(true); UpdateAutomation(); return; }
        // Trimmed spaces would shift the text in one frame, so a cleaned value arrives with the text motion instead.
        bool cleaned = next != _draft;
        bool changed = next != _shown;
        string previous = _committed;
        if (changed) { _shown = next; SetValue(PhasePropertyKey, InlineEditPhase.Saving); }
        EndEditing(source != Source.Blur);
        if (cleaned) NewLayer(1, true); else UpdateLayerText();
        UpdateSlot(true); UpdateMessage(true); UpdateAppearance(true); UpdateAutomation();
        if (!changed) return;
        int run = ++_saveRun;
        Announce($"Saving {Noun}");
        Task task;
        try { task = SaveAsync?.Invoke(next) ?? Task.CompletedTask; }
        catch (Exception exception) { task = Task.FromException(exception); }
        task.ContinueWith(t => Dispatcher.BeginInvoke(new Action(() => { if (t.IsFaulted || t.IsCanceled) SaveFailedCore(run, previous, next, t.Exception?.GetBaseException() ?? new TaskCanceledException()); else SaveSucceeded(run, next); })), TaskScheduler.Default);
    }
    private void SaveSucceeded(int run, string next)
    {
        if (run != _saveRun) return;
        _committed = _seen = next;
        _internalValue = true; try { SetCurrentValue(ValueProperty, next); } finally { _internalValue = false; }
        SetValue(PhasePropertyKey, InlineEditPhase.Saved);
        Announce($"{Label} saved");
        UpdateSlot(true); UpdateAppearance(true);
        Later(1800, () => { if (Phase == InlineEditPhase.Saved) { SetValue(PhasePropertyKey, InlineEditPhase.Idle); UpdateSlot(true); } });
        RaiseEvent(new RoutedEventArgs(SavedEvent, this));
    }
    private void SaveFailedCore(int run, string previous, string next, Exception exception)
    {
        if (run != _saveRun) return;
        _shown = previous; MarkContent(); NewLayer(-1, true);
        SetValue(PhasePropertyKey, InlineEditPhase.Failed);
        _failed = next; SetFlash(Flash.On); Announce(string.Empty);
        Later(1400, () => { if (_flash == Flash.On) SetFlash(Flash.Off); });
        Later(2000, () => { if (_flash == Flash.Off) SetFlash(Flash.None); });
        UpdateSlot(true); UpdateMessage(true); UpdateAppearance(true); UpdateAutomation();
        RaiseEvent(new InlineEditSaveFailedEventArgs(SaveFailedEvent, this, exception));
    }
    private void Cancel()
    {
        if (!IsEditing) return;
        bool rollBack = _draft != _shown;
        SetValue(ErrorPropertyKey, string.Empty);
        EndEditing(true);
        if (rollBack) NewLayer(-1, true); else UpdateLayerText();
        UpdateSlot(true); UpdateMessage(true); UpdateAppearance(true); UpdateAutomation();
        RaiseEvent(new RoutedEventArgs(EditCancelledEvent, this));
    }
    private void Retry() { if (_failed is string failed) StartEdit(failed.Length, failed); }
    private void EndEditing(bool focusDisplay)
    {
        MarkContent(); _focusDisplay = focusDisplay;
        SetValue(IsEditingPropertyKey, false); UnhookWindow(); ApplyMetrics(); SyncEditor();
        // Focus follows the mode: back to the text after Enter or Escape.
        if (_focusDisplay && _display is not null) { _focusDisplay = false; _display.Focus(); }
    }

    private void SyncEditor()
    {
        if (_editor is null) return;
        bool editing = IsEditing;
        if (editing && _editor.Text != _draft) { _syncingEditor = true; try { _editor.Text = _draft; } finally { _syncingEditor = false; } }
        _editor.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        if (_display is not null) { _display.Visibility = editing ? Visibility.Hidden : Visibility.Visible; _display.IsTabStop = !editing; }
    }

    private void DisplayClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Saving) return;
        // A keyboard activation selects everything, ready to retype; a click puts the caret where it landed.
        bool keyboard = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        StartEdit(keyboard ? null : CaretAt());
    }
    private void PencilClick(object? sender, MouseButtonEventArgs e) { e.Handled = true; StartEdit(_shown.Length); }
    /// <summary>The caret lands on the character that was clicked, the way it would in a text field.</summary>
    private int CaretAt()
    {
        if (_layer is null || _shown.Length == 0) return _shown.Length;
        var position = _layer.GetPositionFromPoint(Mouse.GetPosition(_layer), true);
        if (position is null) return _shown.Length;
        return Math.Min(new TextRange(_layer.ContentStart, position).Text.Length, _shown.Length);
    }
    private void EditorChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingEditor || !IsEditing || _editor is null) return;
        _draft = _editor.Text;
        if (Error.Length > 0) { SetValue(ErrorPropertyKey, Validate?.Invoke(Clean(_draft)) ?? string.Empty); UpdateMessage(true); UpdateAppearance(true); }
        MarkContent(); UpdateLayerText();
    }
    private void EditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; Cancel(); return; }
        if (e.Key == Key.Enter && !(Multiline && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))) { e.Handled = true; Submit(Source.Key); }
    }
    private void SaveClick(object? sender, RoutedEventArgs e) { e.Handled = true; Submit(Source.Button); }
    private void CancelClick(object? sender, RoutedEventArgs e) { e.Handled = true; Cancel(); }

    // Leaving the component saves, the way a rename does; switching windows does not.
    protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusWithinChanged(e);
        if ((bool)e.NewValue || !IsEditing) return;
        // Focus going nowhere means another window took it; WindowPreviewMouseDown covers clicks on empty space.
        if (Keyboard.FocusedElement is null) return;
        if (Keyboard.FocusedElement is DependencyObject focused && IsInside(focused)) return;
        Submit(Source.Blur);
    }
    // WPF keeps focus in the field when empty space is clicked; a browser would blur it, so a press outside counts as leaving.
    private void WindowPreviewMouseDown(object? sender, MouseButtonEventArgs e) { if (IsEditing && e.OriginalSource is DependencyObject source && !IsInside(source)) Submit(Source.Blur); }
    private bool IsInside(DependencyObject element)
    {
        for (DependencyObject? node = element; node is not null; node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (ReferenceEquals(node, this)) return true;
        return false;
    }
    private void HookWindow() { UnhookWindow(); _window = Window.GetWindow(this); if (_window is not null) _window.PreviewMouseDown += WindowPreviewMouseDown; }
    private void UnhookWindow() { if (_window is not null) _window.PreviewMouseDown -= WindowPreviewMouseDown; _window = null; }

    // ---- Text layers and sizing -------------------------------------------------------------------------------------------------

    private void ApplyMetrics()
    {
        if (_display is null) return;
        double padY = PadY, leading = Leading;
        bool roomy = IsEditing && !Multiline;
        // While editing, a single line keeps a little extra room, so the next character never has to wait for the box.
        _display.Padding = new Thickness(PadX, padY, roomy ? PadX + 16 : PadX, padY);
        _display.MinWidth = roomy ? 5 * FontSize : 0;
        if (_editor is not null)
        {
            _editor.Padding = new Thickness(PadX, padY, PadX, padY);
            _editor.AcceptsReturn = Multiline; _editor.TextWrapping = Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap;
            TextBlock.SetLineHeight(_editor, leading); TextBlock.SetLineStackingStrategy(_editor, LineStackingStrategy.BlockLineHeight);
            _editor.SpellCheck.IsEnabled = Variant == InlineEditVariant.Body;
        }
        if (_box is not null) _box.SlotTop = padY + leading / 2 - 13;
        foreach (var layer in Layers()) StyleLayer(layer);
    }
    private IEnumerable<TextBlock> Layers()
    {
        if (_layer is not null) yield return _layer;
        if (_leavingHost is not null) foreach (var child in _leavingHost.Children.OfType<TextBlock>()) yield return child;
    }
    private void StyleLayer(TextBlock layer)
    {
        layer.LineHeight = Leading; layer.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        layer.TextWrapping = Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap;
        layer.TextTrimming = Multiline ? TextTrimming.None : TextTrimming.CharacterEllipsis;
    }
    private string LayerString()
    {
        // A zero-width space keeps the line box, so an empty field never collapses.
        string text = LayerText;
        return (text.Length > 0 ? text : Placeholder.Length > 0 ? Placeholder : ZeroWidth) + (Multiline && IsEditing ? ZeroWidth : string.Empty);
    }
    private void UpdateLayerText()
    {
        if (_layer is null) return;
        _layer.Text = LayerString();
        if (LayerText.Length == 0) _layer.Foreground = MutedBrush; else _layer.ClearValue(TextBlock.ForegroundProperty);
    }
    /// <summary>Text rises in from a soft blur. A rollback runs the other way, dropping the old words down and the saved ones in from above.</summary>
    private void NewLayer(int direction, bool animate)
    {
        if (_textHost is null) return;
        var old = _layer;
        _layer = new TextBlock();
        StyleLayer(_layer); UpdateLayerText();
        _textHost.Children.Insert(0, _layer);
        bool moving = animate && Moving && old is not null, full = moving && Full;
        if (old is not null)
        {
            _textHost.Children.Remove(old);
            if (moving && _leavingHost is not null)
            {
                // popLayout: the leaving text keeps its size but no longer takes part in layout.
                old.Width = old.ActualWidth; _leavingHost.Children.Add(old);
                Exit(old, direction, full, () => _leavingHost.Children.Remove(old));
            }
        }
        if (moving) Enter(_layer, direction, full, Standard);
    }
    private void Enter(FrameworkElement element, int direction, bool full, double duration)
    {
        element.Opacity = 0;
        _textMotion.Double(element, OpacityProperty, 1, full ? duration : Fast, true, enter: true);
        if (!full) return;
        var move = new TranslateTransform(0, direction * Rise * RiseSize(element)); element.RenderTransform = move;
        _textMotion.Double(move, TranslateTransform.YProperty, 0, duration, true, enter: true);
        var blur = new BlurEffect { Radius = SoftBlur }; element.Effect = blur;
        _textMotion.Double(blur, BlurEffect.RadiusProperty, 0, duration, true, enter: true, completed: () => { if (element.Effect == blur) element.Effect = null; });
    }
    private void Exit(FrameworkElement element, int direction, bool full, Action removed)
    {
        _textMotion.Double(element, OpacityProperty, 0, full ? Fast : Instant, true, ease: StandardEase(), completed: removed);
        if (!full) return;
        var move = new TranslateTransform(); element.RenderTransform = move;
        _textMotion.Double(move, TranslateTransform.YProperty, direction * -Rise * RiseSize(element), Fast, true, ease: StandardEase());
        var blur = new BlurEffect { Radius = 0 }; element.Effect = blur;
        _textMotion.Double(blur, BlurEffect.RadiusProperty, SubtleBlur, Fast, true, ease: StandardEase());
    }
    private double RiseSize(FrameworkElement element) => element is TextBlock text ? text.FontSize : TextElement.GetFontSize(element);

    private void MarkContent()
    {
        if (_contentPending) return;
        _contentPending = true;
        Dispatcher.BeginInvoke(new Action(() => _contentPending = false), DispatcherPriority.Loaded);
    }
    // Changes the person caused (typing, switching modes, a rollback) spring from the size on screen; a late font or a resized container follows at once.
    private void BoxNaturalSizeChanged(object? sender, EventArgs e)
    {
        if (_box is null) return;
        bool multiline = Multiline;
        double next = multiline ? _box.NaturalSize.Height : _box.NaturalSize.Width;
        double previous = _size; bool typing = IsEditing && _wasEditing, content = _contentPending;
        _size = next;
        if (content) _wasEditing = IsEditing;
        if (next <= 0) return;
        var property = multiline ? InlineEditBox.BoxHeightProperty : InlineEditBox.FrameWidthProperty;
        double current = (double)_box.GetValue(property);
        if (!content || previous <= 0 || !Full || !double.IsFinite(current)) { _sizeMotion.Stop(); _box.ClearValue(multiline ? InlineEditBox.FrameWidthProperty : InlineEditBox.BoxHeightProperty); _box.SetValue(property, next); return; }
        if (Math.Abs(next - previous) < .1) return;
        _sizeMotion.Double(_box, property, next, Spring(multiline ? Smooth : typing ? Typing : Morph), true, spring: true);
    }
    private void FrameSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // radius-control (18px) on a box shorter than 36px clamps to a true half-height pill, as CSS does.
        double radius = Math.Min(18, e.NewSize.Height / 2);
        if (_frame is not null) _frame.CornerRadius = new CornerRadius(radius);
        if (_ring is not null) _ring.CornerRadius = new CornerRadius(radius + 3);
    }

    // ---- Slot: pencil, actions, spinner, saved, failed ------------------------------------------------------------------------

    private IEnumerable<FrameworkElement> SlotItems() => new[] { _actions, _spinner, _saved, _failedIcon, _pencil }.OfType<FrameworkElement>();
    private FrameworkElement? SlotElement(Slot slot) => slot switch { Slot.Edit => _actions, Slot.Saving => _spinner, Slot.Saved => _saved, Slot.Failed => _failedIcon, Slot.Idle => _pencil, _ => null };
    private void UpdateSlot(bool animate)
    {
        var next = IsEditing ? Slot.Edit : Phase switch { InlineEditPhase.Saving => Slot.Saving, InlineEditPhase.Saved => Slot.Saved, InlineEditPhase.Failed => Slot.Failed, _ => ReadOnly ? Slot.None : Slot.Idle };
        if (next == _slot) { UpdatePencil(animate); return; }
        bool moving = animate && Moving && _slot != Slot.None, full = moving && Full;
        var leaving = SlotElement(_slot); var entering = SlotElement(next);
        _slot = next;
        foreach (var item in SlotItems()) if (item != leaving && item != entering) item.Visibility = Visibility.Collapsed;
        if (leaving is not null)
        {
            leaving.IsHitTestVisible = false;
            if (!moving) leaving.Visibility = Visibility.Collapsed;
            else
            {
                _slotMotion.Double(leaving, OpacityProperty, 0, full ? Fast : Instant, true, ease: StandardEase(), completed: () => { if (SlotElement(_slot) != leaving) leaving.Visibility = Visibility.Collapsed; });
                if (full && leaving.RenderTransform is ScaleTransform outScale) { _slotMotion.Double(outScale, ScaleTransform.ScaleXProperty, .6, Fast, true, ease: StandardEase()); _slotMotion.Double(outScale, ScaleTransform.ScaleYProperty, .6, Fast, true, ease: StandardEase()); }
                if (full) { var blur = new BlurEffect { Radius = 0 }; leaving.Effect = blur; _slotMotion.Double(blur, BlurEffect.RadiusProperty, SubtleBlur, Fast, true, ease: StandardEase()); }
            }
        }
        if (entering is not null)
        {
            entering.Visibility = Visibility.Visible; entering.IsHitTestVisible = true;
            // Scale rides the snappy spring; opacity and blur tween so the blur never overshoots below zero.
            if (moving) entering.Opacity = 0;
            _slotMotion.Double(entering, OpacityProperty, 1, Fast, moving, enter: true);
            if (entering.RenderTransform is ScaleTransform inScale)
            {
                if (full) inScale.ScaleX = inScale.ScaleY = .6;
                _slotMotion.Double(inScale, ScaleTransform.ScaleXProperty, 1, Spring(Snappy), full, spring: true);
                _slotMotion.Double(inScale, ScaleTransform.ScaleYProperty, 1, Spring(Snappy), full, spring: true);
            }
            if (full) { var blur = new BlurEffect { Radius = SubtleBlur }; entering.Effect = blur; _slotMotion.Double(blur, BlurEffect.RadiusProperty, 0, Fast, true, enter: true, completed: () => { if (entering.Effect == blur) entering.Effect = null; }); }
            else entering.Effect = null;
        }
        if (next == Slot.Saved && _savedPath is not null)
        {
            // The tick draws itself from its short stroke, the way a hand would write it.
            _savedPath.StrokeDashArray = new DoubleCollection([CheckDash, CheckDash]);
            if (full) { _slotMotion.Double(_savedPath, OpacityProperty, 0, 0, false); _slotMotion.Double(_savedPath, Shape.StrokeDashOffsetProperty, CheckDash, 0, false); _slotMotion.Double(_savedPath, Shape.StrokeDashOffsetProperty, 0, .32, true, delay: .06, enter: true); _slotMotion.Double(_savedPath, OpacityProperty, 1, .05, true, delay: .06); }
            else { _slotMotion.Double(_savedPath, Shape.StrokeDashOffsetProperty, 0, 0, false); _slotMotion.Double(_savedPath, OpacityProperty, 1, 0, false); }
        }
        UpdateSpinner(); UpdatePencil(animate);
    }
    private void UpdateSpinner()
    {
        bool spinning = _slot == Slot.Saving && IsLoaded && Level == ArcMotionLevel.Full;
        if (spinning) _spin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(.7)) { RepeatBehavior = RepeatBehavior.Forever });
        else { _spin.BeginAnimation(RotateTransform.AngleProperty, null); _spin.Angle = Level == ArcMotionLevel.Full ? 0 : -90; }
    }
    private void HoverChanged(object? sender, EventArgs e) { UpdatePencil(true); UpdateAppearance(true); }
    /// <summary>The pencil only shows while the text is hovered or keyboard-focused, or while the pencil itself is hovered.</summary>
    private void UpdatePencil(bool animate)
    {
        if (_pencilGlyph is null) return;
        bool show = _display is not null && (_display.IsMouseOver || _display.IsKeyboardFocused) || _pencil?.IsMouseOver == true;
        _slotMotion.Double(_pencilGlyph, OpacityProperty, show ? 1 : 0, Fast, animate && Moving);
    }

    // ---- Frame, actions and colours ---------------------------------------------------------------------------------------------

    private void SetFlash(Flash flash) { if (_flash == flash) return; _flash = flash; UpdateAppearance(true); }
    /// <summary>The frame is the only surface: a hover tint at rest, a bordered field while editing, a soft danger wash when a save fails.</summary>
    private void UpdateAppearance(bool animate)
    {
        bool moving = animate && Moving;
        Color ink = Solid(InkBrush), surface = Solid(SurfaceBrush), hover = Solid(HoverBrush), danger = Solid(DangerBrush);
        bool editing = IsEditing, invalid = editing && Error.Length > 0;
        bool hovered = !editing && !Saving && !ReadOnly && _display?.IsMouseOver == true;
        Color fill, line, ring;
        if (editing) { line = invalid ? danger : ink; fill = surface; ring = invalid ? Alpha(danger, .24) : Alpha(danger, 0); }
        else if (_flash == Flash.On) { line = Alpha(danger, .3); fill = Alpha(danger, .09); ring = Alpha(danger, 0); }
        else { line = Alpha(_frameLine.Color, 0); fill = hovered ? hover : Alpha(_frameFill.Color, 0); ring = Alpha(_ringLine.Color, 0); }
        // The wash leaves slowly, without any movement, once the message has been read.
        double duration = _flash == Flash.Off ? Considered : Fast;
        _colorMotion.Color(_frameFill, fill, duration, moving);
        _colorMotion.Color(_frameLine, line, duration, moving);
        _colorMotion.Color(_ringLine, ring, _flash == Flash.Off ? Considered : Standard, moving);
        if (_textHost is not null) _colorMotion.Double(_textHost, OpacityProperty, Saving ? .5 : 1, Saving ? Fast : Standard, moving);
        if (_display is not null) _display.Cursor = Saving ? Cursors.AppStarting : ReadOnly ? Cursors.Arrow : Cursors.IBeam;
        UpdateActions(animate);
    }
    private void Wire(System.Windows.Controls.Button button, SolidColorBrush fill, SolidColorBrush line, SolidColorBrush ink, ScaleTransform scale)
    {
        button.Background = fill; button.BorderBrush = line; button.Foreground = ink; button.RenderTransform = scale;
        button.MouseEnter += ActionStateChanged; button.MouseLeave += ActionStateChanged; button.PreviewMouseLeftButtonDown += ActionStateChanged; button.PreviewMouseLeftButtonUp += ActionStateChanged; button.LostMouseCapture += ActionStateChanged;
    }
    private void Unwire(System.Windows.Controls.Button button) { button.MouseEnter -= ActionStateChanged; button.MouseLeave -= ActionStateChanged; button.PreviewMouseLeftButtonDown -= ActionStateChanged; button.PreviewMouseLeftButtonUp -= ActionStateChanged; button.LostMouseCapture -= ActionStateChanged; }
    private void ActionStateChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() => UpdateActions(true)), DispatcherPriority.Input);
    private void UpdateActions(bool animate)
    {
        bool moving = animate && Moving;
        Color ink = Solid(InkBrush), canvas = Solid(CanvasBrush);
        bool saveHover = _save?.IsMouseOver == true, cancelHover = _cancel?.IsMouseOver == true;
        // .save:hover mixes 86% foreground into the background (approximated in sRGB).
        _colorMotion.Color(_saveFill, saveHover ? Mix(ink, canvas, .86) : ink, Fast, moving);
        _colorMotion.Color(_saveInk, canvas, Fast, moving);
        _colorMotion.Color(_cancelFill, cancelHover ? Solid(HoverBrush) : Solid(SurfaceBrush), Fast, moving);
        _colorMotion.Color(_cancelLine, cancelHover ? Solid(LineStrongBrush) : Solid(LineBrush), Fast, moving);
        _colorMotion.Color(_cancelInk, cancelHover ? ink : Solid(SecondaryBrush), Fast, moving);
        bool full = moving && Full;
        foreach (var (button, scale) in new[] { (_save, _saveScale), (_cancel, _cancelScale) })
        {
            double target = button?.IsPressed == true && full ? .96 : 1;
            _colorMotion.Double(scale, ScaleTransform.ScaleXProperty, target, Instant, full);
            _colorMotion.Double(scale, ScaleTransform.ScaleYProperty, target, Instant, full);
        }
    }
    private static Color Solid(Brush? brush) => (brush as SolidColorBrush)?.Color ?? Colors.Transparent;
    private static Color Alpha(Color color, double share) => Color.FromArgb((byte)Math.Round(255 * share), color.R, color.G, color.B);
    private static Color Mix(Color a, Color b, double share) => Color.FromArgb(255, (byte)Math.Round(a.R * share + b.R * (1 - share)), (byte)Math.Round(a.G * share + b.G * (1 - share)), (byte)Math.Round(a.B * share + b.B * (1 - share)));
    private static KeySpline StandardEase() => new(.22, 1, .36, 1);

    // ---- Message row -----------------------------------------------------------------------------------------------------------

    /// <summary>A message row that opens and closes its height on a spring while the words rise in, so an error never pushes the page in one frame.</summary>
    private void UpdateMessage(bool animate)
    {
        if (_messageHost is null) return;
        string? key = Error.Length > 0 ? $"error:{Error}" : _failed is not null && !IsEditing ? $"failed:{_failed}" : null;
        if (key == _messageKey) return;
        _messageKey = key;
        bool moving = animate && Moving, full = moving && Full;
        var old = _message;
        _message = key is null ? null : BuildMessage(Error.Length > 0);
        if (old is not null)
        {
            if (moving)
            {
                // popLayout: the leaving words keep their place but no longer hold the row open.
                var width = old.ActualWidth; _messageHost.Children.Remove(old);
                var holder = _messageHost.Children.OfType<Canvas>().FirstOrDefault() ?? AddCanvas(_messageHost);
                old.Width = width; holder.Children.Add(old);
                Exit(old, 1, full, () => holder.Children.Remove(old));
            }
            else _messageHost.Children.Remove(old);
        }
        if (_message is null) return;
        _messageHost.Children.Insert(0, _message);
        if (moving) Enter(_message, 1, full, Standard);
    }
    private static Canvas AddCanvas(Panel host) { var canvas = new Canvas(); host.Children.Add(canvas); return canvas; }
    private FrameworkElement BuildMessage(bool error)
    {
        var icon = new Viewbox { Width = 14, Height = 14, Margin = new Thickness(0, 2, 6, 0), VerticalAlignment = VerticalAlignment.Top, Child = new System.Windows.Shapes.Path { Width = 24, Height = 24, Stretch = Stretch.None, Data = AlertGeometry, Stroke = DangerBrush, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round } };
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        if (error) text.Text = Error;
        else
        {
            text.Inlines.Add(new Run($"Couldn\u2019t save \u201c{_failed}\u201d, so the last saved {Noun} is back. "));
            var retry = new Hyperlink(new Run("Try again")) { FontWeight = FontWeights.Medium, Foreground = InkBrush, Cursor = Cursors.Hand, FocusVisualStyle = null };
            var underline = new TextDecoration(TextDecorationLocation.Underline, new Pen(LineStrongBrush, 1), 3, TextDecorationUnit.Pixel, TextDecorationUnit.Pixel);
            retry.TextDecorations = new TextDecorationCollection([underline]);
            retry.MouseEnter += (_, _) => retry.TextDecorations = new TextDecorationCollection([new TextDecoration(TextDecorationLocation.Underline, new Pen(InkBrush, 1), 3, TextDecorationUnit.Pixel, TextDecorationUnit.Pixel)]);
            retry.MouseLeave += (_, _) => retry.TextDecorations = new TextDecorationCollection([underline]);
            retry.Click += (_, e) => { e.Handled = true; Retry(); };
            text.Inlines.Add(retry);
        }
        var row = new DockPanel { Margin = new Thickness(PadX + 1, 6, PadX, 4), LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon); row.Children.Add(text);
        TextElement.SetFontSize(row, 12); TextElement.SetFontWeight(row, FontWeights.Normal);
        TextElement.SetForeground(row, error ? DangerBrush : SecondaryBrush);
        return row;
    }
    private void RevealNaturalHeightChanged(object? sender, EventArgs e)
    {
        if (_reveal is null) return;
        double next = _reveal.NaturalHeight;
        if (!_revealMeasured || !Full) { _revealMeasured = true; _revealMotion.Stop(); _reveal.RevealHeight = next; return; }
        _revealMotion.Double(_reveal, InlineEditReveal.RevealHeightProperty, next, Spring(Smooth), true, spring: true);
    }

    // ---- Timers and accessibility ----------------------------------------------------------------------------------------------

    private void Later(int milliseconds, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); _timers.Remove(timer); action(); };
        _timers.Add(timer); timer.Start();
    }
    private void Announce(string text)
    {
        AutomationProperties.SetItemStatus(this, text);
        if (text.Length > 0) UIElementAutomationPeer.FromElement(this)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
    private void UpdateAutomation()
    {
        if (_display is not null) { AutomationProperties.SetName(_display, $"{Label}: {(_shown.Length > 0 ? _shown : Placeholder)}"); AutomationProperties.SetHelpText(_display, "Activate to edit."); }
        if (_editor is not null) { AutomationProperties.SetName(_editor, Label); AutomationProperties.SetHelpText(_editor, (Multiline ? "Enter saves, Shift+Enter adds a line break, Escape cancels." : "Enter saves, Escape cancels.") + (Error.Length > 0 ? " " + Error : string.Empty)); }
        if (_save is not null) AutomationProperties.SetName(_save, $"Save {Noun}");
        if (_cancel is not null) AutomationProperties.SetName(_cancel, "Cancel editing");
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new InlineEditAutomationPeer(this);
    private sealed class InlineEditAutomationPeer(InlineEdit owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(InlineEdit);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.Label;
    }
}

public sealed class InlineEditSaveFailedEventArgs(RoutedEvent routedEvent, object source, Exception exception) : RoutedEventArgs(routedEvent, source) { public Exception Exception { get; } = exception; }
