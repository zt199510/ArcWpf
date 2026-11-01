using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class MoneyValueChangedEventArgs(RoutedEvent routedEvent, object source, long? value, MoneyCurrency currency) : RoutedEventArgs(routedEvent, source)
{
    /// <summary>Minor units (cents for USD, yen for JPY); null is empty.</summary>
    public long? Value { get; } = value;
    public string Currency { get; } = currency.Code;
    /// <summary>The amount in major units, such as 12.5 for $12.50.</summary>
    public decimal? Major { get; } = value is null ? null : value.Value / (decimal)MoneyMath.Pow10(currency.Digits);
    /// <summary>The amount as a full currency string, such as "$12.50".</summary>
    public string Formatted { get; } = value is null ? "" : currency.Format(value.Value);
}

/// <summary>
/// A currency amount field (money-input.tsx, frozen 2026-10-31). Digits group as they are typed without moving the caret, the missing cents wait as
/// faint ghosts so the amount never changes width when it settles, long amounts scale down to fit instead of scrolling, and quick add chips and arrow
/// keys roll the digits. The value is in minor units, so $12.50 is 1250.
/// </summary>
[TemplatePart(Name = "PART_Control", Type = typeof(Border))]
[TemplatePart(Name = "PART_Wrap", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_Group", Type = typeof(StackPanel))]
[TemplatePart(Name = "PART_Mirror", Type = typeof(StackPanel))]
[TemplatePart(Name = "PART_Input", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_CurrencyButton", Type = typeof(ToggleButton))]
[TemplatePart(Name = "PART_Chevron", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Menu", Type = typeof(Popup))]
[TemplatePart(Name = "PART_MenuSurface", Type = typeof(Border))]
[TemplatePart(Name = "PART_List", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Chips", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Range", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Message", Type = typeof(Grid))]
public class MoneyInput : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(long?), typeof(MoneyInput), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((MoneyInput)d).OnValueChanged((long?)e.NewValue)));
    public static readonly DependencyProperty CurrencyProperty = DependencyProperty.Register(nameof(Currency), typeof(string), typeof(MoneyInput), new FrameworkPropertyMetadata("USD", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((MoneyInput)d).OnCurrencyChanged((string)e.OldValue)));
    public static readonly DependencyProperty CurrenciesProperty = DependencyProperty.Register(nameof(Currencies), typeof(IList<string>), typeof(MoneyInput), new PropertyMetadata(null, (d, _) => ((MoneyInput)d).BuildMenu()));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(long?), typeof(MoneyInput), new PropertyMetadata(null, (d, _) => ((MoneyInput)d).RenderChrome(true)));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(long?), typeof(MoneyInput), new PropertyMetadata(null, (d, _) => ((MoneyInput)d).RenderChrome(true)));
    public static readonly DependencyProperty QuickAddProperty = DependencyProperty.Register(nameof(QuickAdd), typeof(IList<decimal>), typeof(MoneyInput), new PropertyMetadata(null, (d, _) => ((MoneyInput)d).BuildChips()));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(long), typeof(MoneyInput), new PropertyMetadata(0L));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(MoneyInput), new PropertyMetadata(""));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(MoneyInput), new PropertyMetadata("", (d, _) => ((MoneyInput)d).RenderChrome(true)));
    public static readonly DependencyProperty ErrorProperty = DependencyProperty.Register(nameof(Error), typeof(string), typeof(MoneyInput), new PropertyMetadata("", (d, _) => ((MoneyInput)d).RenderChrome(true)));
    private static readonly DependencyPropertyKey IsMenuOpenPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsMenuOpen), typeof(bool), typeof(MoneyInput), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsMenuOpenProperty = IsMenuOpenPropertyKey.DependencyProperty;
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble, typeof(EventHandler<MoneyValueChangedEventArgs>), typeof(MoneyInput));
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(MoneyInput), new PropertyMetadata(null, (d, _) => ((MoneyInput)d).Repaint()));

    public static IReadOnlyList<string> DefaultCurrencies { get; } = ["USD", "EUR", "GBP", "JPY", "CAD", "AUD", "CHF", "INR"];
    public static IReadOnlyList<decimal> DefaultQuickAdd { get; } = [10, 50, 100];

    /// <summary>Amount in minor units (cents for USD, yen for JPY). Null is empty.</summary>
    public long? Value { get => (long?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>ISO 4217 code, such as "USD".</summary>
    public string Currency { get => (string)GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }
    /// <summary>Codes offered by the currency menu. One code hides the menu. Null uses <see cref="DefaultCurrencies"/>.</summary>
    public IList<string>? Currencies { get => (IList<string>?)GetValue(CurrenciesProperty); set => SetValue(CurrenciesProperty, value); }
    /// <summary>Lowest amount in minor units. Checked when the field loses focus.</summary>
    public long? Minimum { get => (long?)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    /// <summary>Highest amount in minor units. Typing past it is refused with a nudge.</summary>
    public long? Maximum { get => (long?)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>Quick add chips in major units. An empty list hides them; null uses <see cref="DefaultQuickAdd"/>.</summary>
    public IList<decimal>? QuickAdd { get => (IList<decimal>?)GetValue(QuickAddProperty); set => SetValue(QuickAddProperty, value); }
    /// <summary>Arrow keys move by this many minor units; 0 means one major unit. Shift moves ten times as far.</summary>
    public long Step { get => (long)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Replaces the built-in range message.</summary>
    public string Error { get => (string)GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }
    public bool IsMenuOpen => (bool)GetValue(IsMenuOpenProperty);
    public event EventHandler<MoneyValueChangedEventArgs> ValueChanged { add => AddHandler(ValueChangedEvent, value); remove => RemoveHandler(ValueChangedEvent, value); }

    private enum Source { Type, Chip, Key, External }

    private Border? _control, _menuSurface;
    private Canvas? _wrap;
    private StackPanel? _group, _mirror;
    private TextBox? _input;
    private ToggleButton? _currencyButton;
    private FrameworkElement? _chevron;
    private Popup? _menu;
    private Grid? _list;
    private Panel? _chips;
    private TextBlock? _range;
    private FieldMessage? _message;
    private SymbolSlot? _symbolFirst, _symbolLast;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _bump = new();
    private readonly RotateTransform _chevronTurn = new();
    private readonly SolidColorBrush _edge = new(Colors.Gray), _rangeInk = new(Colors.Gray), _buttonFill = new(Colors.Transparent);
    private readonly Dictionary<string, GlyphColumn> _columns = [];
    private readonly DispatcherTimer _flash = new() { Interval = TimeSpan.FromMilliseconds(1400) };
    private readonly Border _highlight = new() { CornerRadius = new CornerRadius(14), VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Opacity = 0, RenderTransform = new TranslateTransform() };
    private readonly List<(MoneyCurrency Info, Border Row)> _options = [];
    private string _draft = "";
    private Source _source = Source.External;
    private int _direction = 1;
    private bool _touched, _syncing, _flashing, _emitting, _choosing;
    private string _active = "USD";
    private (string Buffer, long At) _typeahead = ("", 0);
    private Window? _window;
    private double _fitted = 1;

    static MoneyInput()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MoneyInput), new FrameworkPropertyMetadata(typeof(MoneyInput)));
        FocusableProperty.OverrideMetadata(typeof(MoneyInput), new FrameworkPropertyMetadata(false));
    }

    public MoneyInput()
    {
        _flash.Tick += (_, _) => { _flash.Stop(); _flashing = false; PaintRange(true); };
        SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border");
        Unloaded += (_, _) => { _flash.Stop(); CloseMenu(false, false); };
        _highlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.FieldHighlight");
    }

    private MoneyCurrency Info => MoneyMath.Currency(Currency);
    private long? Minor => MoneyMath.ToMinor(_draft, Info.Digits);
    private long StepSize => Step > 0 ? Step : MoneyMath.Pow10(Info.Digits);
    private IReadOnlyList<string> CurrencyCodes => (IReadOnlyList<string>?)Currencies?.ToList() ?? DefaultCurrencies;
    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;
    private bool Focused => _input?.IsKeyboardFocused == true;

    public override void OnApplyTemplate()
    {
        if (_input is not null)
        {
            _input.TextChanged -= InputChanged;
            _input.PreviewKeyDown -= InputKeyDown;
            _input.GotKeyboardFocus -= InputFocus;
            _input.LostKeyboardFocus -= InputBlur;
            DataObject.RemovePastingHandler(_input, InputPaste);
        }
        if (_currencyButton is not null) { _currencyButton.Click -= CurrencyClick; _currencyButton.PreviewKeyDown -= CurrencyKeyDown; }
        if (_wrap is not null) { _wrap.SizeChanged -= Fit; _wrap.PreviewMouseLeftButtonDown -= WrapMouseDown; }
        base.OnApplyTemplate();
        _control = GetTemplateChild("PART_Control") as Border;
        _wrap = GetTemplateChild("PART_Wrap") as Canvas;
        _group = GetTemplateChild("PART_Group") as StackPanel;
        _mirror = GetTemplateChild("PART_Mirror") as StackPanel;
        _input = GetTemplateChild("PART_Input") as TextBox;
        _currencyButton = GetTemplateChild("PART_CurrencyButton") as ToggleButton;
        _chevron = GetTemplateChild("PART_Chevron") as FrameworkElement;
        _menu = GetTemplateChild("PART_Menu") as Popup;
        _menuSurface = GetTemplateChild("PART_MenuSurface") as Border;
        _list = GetTemplateChild("PART_List") as Grid;
        _chips = GetTemplateChild("PART_Chips") as Panel;
        _range = GetTemplateChild("PART_Range") as TextBlock;
        var host = GetTemplateChild("PART_Message") as Grid;
        _columns.Clear();
        _mirror?.Children.Clear();
        host?.Children.Clear();
        _message = host is null ? null : new FieldMessage();
        if (_message is not null) host!.Children.Add(_message);
        if (_control is not null) _control.BorderBrush = _edge;
        if (_range is not null) _range.Foreground = _rangeInk;
        if (_group is not null)
        {
            _group.RenderTransform = new TransformGroup { Children = { _scale, _bump } };
            _group.SizeChanged += Fit;
            _symbolFirst = new SymbolSlot { Margin = new Thickness(0, 0, 36 * .06, 0) };
            _symbolLast = new SymbolSlot { Margin = new Thickness(36 * .18, 0, 0, 0) };
            _group.Children.Insert(0, _symbolFirst);
            _group.Children.Add(_symbolLast);
        }
        if (_wrap is not null) { _wrap.SizeChanged += Fit; _wrap.PreviewMouseLeftButtonDown += WrapMouseDown; }
        if (_chevron is not null) { _chevron.RenderTransform = _chevronTurn; _chevron.RenderTransformOrigin = new Point(.5, .5); }
        if (_currencyButton is not null)
        {
            _currencyButton.Click += CurrencyClick;
            _currencyButton.PreviewKeyDown += CurrencyKeyDown;
            // The button's own template is not applied yet; without it the surface has no fill to hit and the code stays blank.
            _currencyButton.ApplyTemplate();
            if (_currencyButton.Template?.FindName("Surface", _currencyButton) is Border surface) surface.Background = _buttonFill;
            _currencyButton.MouseEnter += (_, _) => PaintButton(true);
            _currencyButton.MouseLeave += (_, _) => PaintButton(true);
        }
        if (_menu is not null)
        {
            _menu.Placement = PlacementMode.Custom;
            _menu.PlacementTarget = _control;
            // The menu opens 8 DIP below the shell with its right edge on the shell's right edge.
            _menu.CustomPopupPlacementCallback = (popup, target, _) => [new CustomPopupPlacement(new Point(target.Width - popup.Width, target.Height + 8), PopupPrimaryAxis.None)];
            BindingOperations.SetBinding(_menu.Child, ArcMotion.LevelProperty, new Binding { Source = this, Path = new PropertyPath(ArcMotion.LevelProperty) });
        }
        if (_list is not null)
        {
            _list.PreviewKeyDown += ListKeyDown;
            _list.Children.Add(_highlight);
        }
        if (_input is not null)
        {
            _input.TextChanged += InputChanged;
            _input.PreviewKeyDown += InputKeyDown;
            _input.GotKeyboardFocus += InputFocus;
            _input.LostKeyboardFocus += InputBlur;
            DataObject.AddPastingHandler(_input, InputPaste);
            AutomationProperties.SetName(_input, Label);
        }
        _draft = MoneyMath.FromMinor(Value, Info.Digits);
        BuildMenu();
        BuildChips();
        RenderAmount(false);
        RenderChrome(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty || e.Property == IsEnabledProperty) PaintEdge(true);
        if (e.Property == LabelProperty && _input is not null) AutomationProperties.SetName(_input, Label);
    }

    /* ---------------------------------------------- Value flow ---------------------------------------------- */

    private void OnValueChanged(long? value)
    {
        if (_emitting || value == Minor) return;
        _direction = (value ?? 0) >= (Minor ?? 0) ? 1 : -1;
        _source = Source.External;
        _draft = MoneyMath.FromMinor(value, Info.Digits);
        RenderAmount(true);
        RenderChrome(true);
    }

    private void Emit(long? minor, MoneyCurrency? info = null)
    {
        _emitting = true;
        SetCurrentValue(ValueProperty, minor);
        _emitting = false;
        RaiseEvent(new MoneyValueChangedEventArgs(ValueChangedEvent, this, minor, info ?? Info));
    }

    private void SetAmount(string draft, Source source, int? caret)
    {
        var info = Info;
        long? next = MoneyMath.ToMinor(draft, info.Digits), previous = Minor;
        _direction = (next ?? 0) >= (previous ?? 0) ? 1 : -1;
        _source = source;
        _draft = draft;
        if (next != previous) Emit(next);
        if (source != Source.Type) Announce(next is null ? "" : info.Format(next.Value));
        RenderAmount(true, caret);
        RenderChrome(true);
    }

    private string Money(long amount) => Info.Format(amount);

    // A sideways kick when a limit refuses the change: the amount strains toward the press and springs home (physical .42, velocity ±160).
    private void Refuse(int toward, long limit)
    {
        if (Full)
        {
            double omega = 2 * Math.PI / (.42 * 1.2);
            var frames = new DoubleAnimationUsingKeyFrames();
            for (int i = 0; i <= 36; i++)
            {
                double t = i * .6 / 36;
                frames.KeyFrames.Add(new LinearDoubleKeyFrame(toward * 160 * t * Math.Exp(-omega * t), KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t))));
            }
            frames.Freeze();
            _bump.BeginAnimation(TranslateTransform.XProperty, frames);
        }
        _flashing = true;
        _flash.Stop();
        _flash.Start();
        PaintRange(true);
        Announce($"{(toward > 0 ? "Maximum" : "Minimum")} is {Money(limit)}");
    }

    private void Announce(string message) { if (_input is not null) Announcer.Say(_input, message); }

    /* ---------------------------------------------- Typing ---------------------------------------------- */

    private void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || _input is null) return;
        ApplyRaw(_input.Text, _input.CaretIndex);
    }

    private void ApplyRaw(string raw, int caret)
    {
        var result = MoneyMath.Sanitize(raw, caret, Info.Digits);
        // A refused keystroke keeps the caret where it was: one significant character before where the edit left it.
        if (result is null) { Restore(Math.Max(0, MoneyMath.SignificantBefore(raw, caret) - 1)); return; }
        long? next = MoneyMath.ToMinor(result.Value.Draft, Info.Digits);
        if (Maximum is long max && next is long value && value > max)
        {
            Restore(Math.Max(0, result.Value.Before - 1));
            Refuse(1, max);
            return;
        }
        SetAmount(result.Value.Draft, Source.Type, result.Value.Before);
    }

    private void Restore(int count)
    {
        SyncInput(MoneyMath.Display(_draft), count);
    }

    private void SyncInput(string text, int? count)
    {
        if (_input is null) return;
        _syncing = true;
        if (_input.Text != text) _input.Text = text;
        if (count is int significant && _input.IsKeyboardFocused) _input.CaretIndex = significant == int.MaxValue ? text.Length : MoneyMath.PositionAfter(text, significant);
        _syncing = false;
    }

    private void InputKeyDown(object sender, KeyEventArgs e)
    {
        if (_input is null) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        int move = key switch { Key.Up => 1, Key.Down => -1, Key.PageUp => 10, Key.PageDown => -10, _ => 0 };
        if (move != 0)
        {
            e.Handled = true;
            bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            Nudge(move * StepSize * (shift && Math.Abs(move) == 1 ? 10 : 1), Source.Key);
            return;
        }
        if (key == Key.Enter) { _draft = MoneyMath.Settle(_draft, Info.Digits); RenderAmount(true, int.MaxValue); return; }
        int start = _input.SelectionStart, length = _input.SelectionLength;
        if (length > 0 || Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;
        string text = _input.Text;
        // Deleting a group separator deletes the digit before it instead of doing nothing.
        if (key == Key.Back && start > 1 && text[start - 1].ToString() == MoneyMath.Group) { e.Handled = true; ApplyRaw(text[..(start - 2)] + text[(start - 1)..], start - 2); }
        else if (key == Key.Delete && start < text.Length && text[start].ToString() == MoneyMath.Group) { e.Handled = true; ApplyRaw(text[..(start + 1)] + text[Math.Min(text.Length, start + 2)..], start); }
    }

    private void InputPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (_input is null || e.DataObject.GetData(DataFormats.UnicodeText) is not string text) return;
        var pasted = MoneyMath.ParsePaste(text, Info.Digits);
        if (pasted is null) return;
        int start = _input.SelectionStart, end = start + _input.SelectionLength;
        string current = _input.Text;
        bool all = start == 0 && end == current.Length || current.Length == 0;
        string raw = all ? pasted : current[..start] + pasted + current[end..];
        ApplyRaw(raw, all ? raw.Length : start + pasted.Length);
    }

    private void InputFocus(object sender, KeyboardFocusChangedEventArgs e) { PaintEdge(true); RenderChrome(true); }

    private void InputBlur(object sender, KeyboardFocusChangedEventArgs e)
    {
        _touched = true;
        if (_draft.Length > 0) { _draft = MoneyMath.Settle(_draft, Info.Digits); RenderAmount(true); }
        PaintEdge(true);
        RenderChrome(true);
    }

    // Pressing the amount anywhere but the input puts the caret at the end.
    private void WrapMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_input is null || !IsEnabled || _input.IsMouseOver) return;
        e.Handled = true;
        _input.Focus();
        _input.CaretIndex = _input.Text.Length;
    }

    /* ---------------------------------------------- Steps and chips ---------------------------------------------- */

    private void Nudge(long amount, Source source)
    {
        long from = Minor ?? 0, floor = Minimum ?? 0, ceiling = Maximum ?? long.MaxValue / 4;
        long next = from + amount;
        if (next > ceiling) { if (from >= ceiling) { Refuse(1, ceiling); return; } next = ceiling; Refuse(1, ceiling); }
        if (next < floor) { if (from <= floor && Minor is not null) { Refuse(-1, floor); return; } next = floor; }
        SetAmount(MoneyMath.FromMinor(next, Info.Digits), source, int.MaxValue);
    }

    private void BuildChips()
    {
        if (_chips is null) return;
        _chips.Children.Clear();
        foreach (var amount in ((IEnumerable<decimal>?)QuickAdd ?? DefaultQuickAdd).Where(amount => amount > 0))
        {
            var chip = new QuickAddChip(amount);
            chip.Click += (_, _) => Nudge((long)(amount * MoneyMath.Pow10(Info.Digits)), Source.Chip);
            _chips.Children.Add(chip);
        }
        RenderChrome(false);
    }

    /* ---------------------------------------------- Rendering ---------------------------------------------- */

    private void RenderAmount(bool animate, int? caret = null)
    {
        if (_mirror is null) return;
        var info = Info;
        animate &= IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;
        bool instant = _source == Source.Type || !animate;
        var parts = MoneyMath.Parts(_draft, info.Digits);
        var keys = parts.Select(part => part.Key).ToHashSet();
        foreach (var (key, column) in _columns.Where(pair => !keys.Contains(pair.Key)).ToList())
        {
            _columns.Remove(key);
            column.Leave(instant || Reduced, () => _mirror.Children.Remove(column));
        }
        int at = 0;
        foreach (var part in parts)
        {
            if (!_columns.TryGetValue(part.Key, out var column))
            {
                column = new GlyphColumn(part.Char, part.Ghost);
                _columns[part.Key] = column;
                // Leaving columns keep their place; new ones enter at their place-value position.
                _mirror.Children.Insert(Math.Min(IndexFor(at), _mirror.Children.Count), column);
                column.Enter(instant || Reduced);
            }
            else column.Set(part.Char, part.Ghost, _direction, instant ? GlyphColumn.Mode.Cut : Reduced ? GlyphColumn.Mode.Fade : GlyphColumn.Mode.Roll);
            at++;
        }
        _symbolFirst?.Set(info.SymbolFirst ? info.Symbol : "", animate && Full, animate);
        _symbolLast?.Set(info.SymbolFirst ? "" : info.Symbol, animate && Full, animate);
        SyncInput(MoneyMath.Display(_draft), caret);
        UpdatePeer();
    }

    private int IndexFor(int liveIndex)
    {
        if (_mirror is null) return liveIndex;
        int live = 0;
        for (int index = 0; index < _mirror.Children.Count; index++)
        {
            if (_mirror.Children[index] is GlyphColumn { Leaving: false }) { if (live == liveIndex) return index; live++; }
        }
        return _mirror.Children.Count;
    }

    // The amount scales down to fit rather than scroll (smooth spring), so a long number is always read whole.
    private void Fit(object sender, SizeChangedEventArgs e)
    {
        if (_wrap is null || _group is null) return;
        double natural = _group.ActualWidth, room = _wrap.ActualWidth;
        double next = natural > 0 && room > 0 ? Math.Min(1, room / natural) : 1;
        Canvas.SetTop(_group, Math.Max(0, (_wrap.ActualHeight - _group.ActualHeight) / 2));
        _scale.CenterY = _group.ActualHeight / 2;
        if (Math.Abs(next - _fitted) < .001) return;
        _fitted = next;
        if (Full) { Tween.Spring(_scale, ScaleTransform.ScaleXProperty, next, Tween.Smooth); Tween.Spring(_scale, ScaleTransform.ScaleYProperty, next, Tween.Smooth); }
        else { Tween.Set(_scale, ScaleTransform.ScaleXProperty, next); Tween.Set(_scale, ScaleTransform.ScaleYProperty, next); }
    }

    private string? Range()
    {
        if (Minimum is long min && Maximum is long max) return $"{Money(min)} to {Money(max)}";
        if (Maximum is long ceiling) return $"Up to {Money(ceiling)}";
        if (Minimum is long floor) return $"At least {Money(floor)}";
        return null;
    }

    private string? BuiltIn()
    {
        long? minor = Minor;
        if (!_touched || Focused || minor is null) return null;
        if (Minimum is long min && minor < min) return $"The minimum is {Money(min)}";
        if (Maximum is long max && minor > max) return $"The maximum is {Money(max)}";
        return null;
    }

    private void RenderChrome(bool animate)
    {
        if (_range is not null)
        {
            var range = Range();
            _range.Text = range ?? "";
            _range.Visibility = range is null ? Visibility.Collapsed : Visibility.Visible;
            AutomationProperties.SetName(_range, range ?? "");
        }
        if (_chips is not null)
        {
            bool atMax = Maximum is long max && (Minor ?? 0) >= max;
            foreach (var chip in _chips.Children.OfType<QuickAddChip>()) chip.Update(Info, atMax);
            if (_chips.Parent is FrameworkElement toolbar) toolbar.Visibility = _chips.Children.Count > 0 || Range() is not null ? Visibility.Visible : Visibility.Collapsed;
        }
        string? message = string.IsNullOrEmpty(Error) ? BuiltIn() : Error;
        if (_message is not null)
        {
            animate &= IsLoaded;
            if (message is not null) _message.Show($"e:{message}", FieldMessage.Line(message, "ArcWpf.Brush.Danger"), animate);
            else if (!string.IsNullOrEmpty(Description)) _message.Show($"d:{Description}", FieldMessage.Line(Description, "ArcWpf.Brush.TextMuted"), animate);
            else _message.Hide(animate);
            if (message is not null && animate) Announce(message);
        }
        if (_input is not null) AutomationProperties.SetHelpText(_input, string.Join(". ", new[] { Range(), message ?? (string.IsNullOrEmpty(Description) ? null : Description) }.Where(text => text is not null)));
        PaintEdge(animate);
    }

    // .control: border-strong; hover (valid) or focused foreground; invalid danger (.16 standard).
    private void PaintEdge(bool animate)
    {
        bool invalid = !string.IsNullOrEmpty(Error) || BuiltIn() is not null;
        string key = invalid ? "Danger" : Focused || (IsMouseOver && IsEnabled) ? "Foreground" : "BorderStrong";
        Tween.Color(_edge, Tween.Resolve(this, $"ArcWpf.Brush.{key}"), Tween.Fast, animate && Full);
        if (_control is not null) { _control.Opacity = IsEnabled ? 1 : .5; _control.SetResourceReference(Border.BackgroundProperty, IsEnabled ? "ArcWpf.Brush.Surface" : "ArcWpf.Brush.SurfaceMuted"); }
    }

    // .range flashes warning in .12 and fades back over .24 after 1.4 s.
    private void PaintRange(bool animate)
    {
        Tween.Color(_rangeInk, Tween.Resolve(this, _flashing ? "ArcWpf.Brush.Warning" : "ArcWpf.Brush.TextMuted"), _flashing ? Tween.Instant : Tween.Standard, animate && IsLoaded && !Reduced);
    }

    private void PaintButton(bool animate)
    {
        bool hot = IsMenuOpen || _currencyButton?.IsMouseOver == true || _currencyButton?.IsPressed == true;
        var fill = Tween.Resolve(this, "ArcWpf.Brush.SurfaceMuted");
        if (!hot) fill.A = 0;
        Tween.Color(_buttonFill, fill, Tween.Fast, animate && Full);
    }

    private void Repaint()
    {
        PaintEdge(false);
        PaintRange(false);
        PaintButton(false);
        foreach (var column in _columns.Values) column.Repaint();
        PaintOptions(false);
    }

    /* ---------------------------------------------- Currency menu ---------------------------------------------- */

    private void OnCurrencyChanged(string? previous)
    {
        if (_mirror is null) return;
        var before = MoneyMath.Currency(previous);
        var next = Info;
        if (!_choosing && before.Digits != next.Digits)
        {
            string draft = Focused ? MoneyMath.ConvertDraft(_draft, next.Digits) : MoneyMath.Settle(MoneyMath.ConvertDraft(_draft, next.Digits), next.Digits);
            _draft = draft;
            _source = Source.External;
            Emit(MoneyMath.ToMinor(draft, next.Digits), next);
        }
        RenderAmount(true);
        RenderChrome(true);
        PaintOptions(false);
        UpdateCurrencyButton();
    }

    private void UpdateCurrencyButton()
    {
        if (_currencyButton is null) return;
        if (_currencyButton.Template?.FindName("Code", _currencyButton) is TextBlock code) code.Text = Info.Code;
        AutomationProperties.SetName(_currencyButton, $"Currency, {Info.Name}");
    }

    private void BuildMenu()
    {
        _options.Clear();
        if (_list is null) return;
        foreach (var row in _list.Children.OfType<Border>().Where(child => child != _highlight).ToList()) _list.Children.Remove(row);
        _list.RowDefinitions.Clear();
        var codes = CurrencyCodes;
        bool canSwitch = codes.Count > 1;
        if (_currencyButton is not null) _currencyButton.Visibility = canSwitch ? Visibility.Visible : Visibility.Collapsed;
        if (GetTemplateChild("PART_CodeStatic") is TextBlock fixedCode) { fixedCode.Visibility = canSwitch ? Visibility.Collapsed : Visibility.Visible; fixedCode.Text = Info.Code; }
        for (int index = 0; index < codes.Count; index++)
        {
            var info = MoneyMath.Currency(codes[index]);
            _list.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = OptionRow(info);
            row.Margin = new Thickness(0, index == 0 ? 0 : 2, 0, 0);
            Grid.SetRow(row, index);
            _list.Children.Add(row);
            _options.Add((info, row));
        }
        Grid.SetRowSpan(_highlight, Math.Max(1, codes.Count));
        Panel.SetZIndex(_highlight, -1);
        UpdateCurrencyButton();
        PaintOptions(false);
    }

    // .option: 36 high, 10 apart inside, secondary ink → foreground when active or selected; symbol 22 wide, code 500, name xs muted, check.
    private Border OptionRow(MoneyCurrency info)
    {
        var grid = new Grid();
        foreach (var width in new[] { new GridLength(22), GridLength.Auto, new GridLength(1, GridUnitType.Star), new GridLength(16) }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        var symbol = new TextBlock { Text = info.Symbol, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        symbol.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        var code = new TextBlock { Text = info.Code, FontWeight = FontWeights.Medium, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = info.Name, FontSize = 12, Margin = new Thickness(10, 0, 10, 0), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        name.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        var check = Lucide.Glyph(Lucide.Check, 16);
        check.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.Foreground");
        check.RenderTransformOrigin = new Point(.5, .5);
        check.RenderTransform = new ScaleTransform(.6, .6);
        check.Opacity = 0;
        Grid.SetColumn(code, 1);
        Grid.SetColumn(name, 2);
        Grid.SetColumn(check, 3);
        grid.Children.Add(symbol);
        grid.Children.Add(code);
        grid.Children.Add(name);
        grid.Children.Add(check);
        var row = new Border { MinHeight = 36, Padding = new Thickness(10, 0, 10, 0), CornerRadius = new CornerRadius(14), Background = Brushes.Transparent, Child = grid, Cursor = Cursors.Hand, Tag = info.Code };
        row.SetValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.Gray));
        row.MouseMove += (_, _) => { if (_active != info.Code) { _active = info.Code; PaintOptions(true); } };
        row.MouseLeftButtonDown += (_, e) => { e.Handled = true; };
        row.MouseLeftButtonUp += (_, e) => { e.Handled = true; Choose(info); };
        AutomationProperties.SetName(row, $"{info.Code} {info.Name}");
        return row;
    }

    private void PaintOptions(bool animate)
    {
        animate &= Full;
        foreach (var (info, row) in _options)
        {
            bool on = info.Code == Info.Code;
            bool active = info.Code == _active;
            if (row.GetValue(TextElement.ForegroundProperty) is SolidColorBrush ink) Tween.Color(ink, Tween.Resolve(this, on || active ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextSecondary"), Tween.Fast, animate);
            if (row.Child is Grid grid && grid.Children[3] is FrameworkElement check && check.RenderTransform is ScaleTransform scale)
            {
                if (animate)
                {
                    Tween.To(check, OpacityProperty, on ? 1 : 0, Tween.Fast, ArcEasing.Standard);
                    Tween.To(scale, ScaleTransform.ScaleXProperty, on ? 1 : .6, Tween.SpringTokenSeconds, Tween.SpringToken);
                    Tween.To(scale, ScaleTransform.ScaleYProperty, on ? 1 : .6, Tween.SpringTokenSeconds, Tween.SpringToken);
                }
                else { Tween.Set(check, OpacityProperty, on ? 1 : 0); Tween.Set(scale, ScaleTransform.ScaleXProperty, on ? 1 : .6); Tween.Set(scale, ScaleTransform.ScaleYProperty, on ? 1 : .6); }
            }
        }
        MoveHighlight(animate);
    }

    // The highlight glides between options (physical .28) and jumps into place when the menu first shows it.
    private void MoveHighlight(bool animate)
    {
        var target = _options.FirstOrDefault(option => option.Info.Code == _active).Row;
        if (!IsMenuOpen || target is null || _list is null) { _highlight.Opacity = 0; return; }
        _list.UpdateLayout();
        double y = target.TranslatePoint(new Point(), _list).Y, height = target.ActualHeight;
        var move = (TranslateTransform)_highlight.RenderTransform;
        if (_highlight.Opacity < .05 || !animate) { Tween.Set(move, TranslateTransform.YProperty, y); Tween.Set(_highlight, HeightProperty, height); }
        else { Tween.Spring(move, TranslateTransform.YProperty, y, .28); Tween.Spring(_highlight, HeightProperty, height, .28); }
        _highlight.Opacity = 1;
        target.BringIntoView();
    }

    private long _closedAt;

    private void CurrencyClick(object sender, RoutedEventArgs e)
    {
        if (_currencyButton is not null) _currencyButton.IsChecked = IsMenuOpen;
        if (IsMenuOpen) { CloseMenu(true, true); return; }
        if (Environment.TickCount64 - _closedAt < 250) return;
        OpenMenu();
    }

    private void CurrencyKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up && !IsMenuOpen) { e.Handled = true; OpenMenu(); }
    }

    public void OpenMenu()
    {
        if (!IsEnabled || _menu is null || CurrencyCodes.Count < 2) return;
        _active = Info.Code;
        SetValue(IsMenuOpenPropertyKey, true);
        if (_currencyButton is not null) _currencyButton.IsChecked = true;
        _menu.IsOpen = true;
        _menu.Child?.UpdateLayout();
        PaintOptions(false);
        PaintButton(true);
        TurnChevron(true);
        _list?.Focus();
        Keyboard.Focus(_list);
        AttachOutside();
        if (_menuSurface is null) return;
        // enter: opacity 0, scale .96, y -4, subtle blur → rest on the snappy spring, grown from the currency button's corner.
        var scale = new ScaleTransform(1, 1, _menuSurface.ActualWidth - 44, -46);
        var move = new TranslateTransform();
        _menuSurface.RenderTransform = new TransformGroup { Children = { scale, move } };
        if (Full)
        {
            scale.ScaleX = scale.ScaleY = .96;
            move.Y = -4;
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
            Tween.Spring(move, TranslateTransform.YProperty, 0, Tween.Snappy);
            Tween.Present(_menuSurface, true, 0, 2, Tween.Fast, ArcEasing.Enter);
        }
        else { _menuSurface.Opacity = 0; Tween.To(_menuSurface, OpacityProperty, 1, Tween.Instant, null); }
    }

    private void CloseMenu(bool focusButton, bool animate)
    {
        if (!IsMenuOpen) return;
        SetValue(IsMenuOpenPropertyKey, false);
        _closedAt = Environment.TickCount64;
        if (_currencyButton is not null) _currencyButton.IsChecked = false;
        DetachOutside();
        PaintButton(true);
        TurnChevron(true);
        if (focusButton) _currencyButton?.Focus();
        var surface = _menuSurface;
        if (!animate || surface is null || !IsLoaded || Tween.Level(this) == ArcMotionLevel.Off) { if (_menu is not null) _menu.IsOpen = false; return; }
        // exit: opacity 0, scale .97, y -2, subtle blur over .12 standard; reduced motion only fades.
        Tween.To(surface, OpacityProperty, 0, Tween.Instant, ArcEasing.Standard, done: () => { if (!IsMenuOpen && _menu is not null) _menu.IsOpen = false; });
        if (Full && surface.RenderTransform is TransformGroup { Children: [ScaleTransform scale, TranslateTransform move] })
        {
            Tween.To(scale, ScaleTransform.ScaleXProperty, .97, Tween.Instant, ArcEasing.Standard);
            Tween.To(scale, ScaleTransform.ScaleYProperty, .97, Tween.Instant, ArcEasing.Standard);
            Tween.To(move, TranslateTransform.YProperty, -2, Tween.Instant, ArcEasing.Standard);
            surface.Effect = new BlurEffect { Radius = 2 };
        }
    }

    private void TurnChevron(bool animate)
    {
        if (Full && animate) Tween.Spring(_chevronTurn, RotateTransform.AngleProperty, IsMenuOpen ? 180 : 0, .28);
        else Tween.Set(_chevronTurn, RotateTransform.AngleProperty, IsMenuOpen ? 180 : 0);
    }

    private void AttachOutside()
    {
        _window = Window.GetWindow(this);
        if (_window is null) return;
        _window.PreviewMouseDown += OutsideDown;
        _window.Deactivated += OutsideDeactivated;
    }

    private void DetachOutside()
    {
        if (_window is null) return;
        _window.PreviewMouseDown -= OutsideDown;
        _window.Deactivated -= OutsideDeactivated;
        _window = null;
    }

    private void OutsideDown(object sender, MouseButtonEventArgs e) { if (!Outside.IsWithin(e.OriginalSource, _currencyButton, _menu?.Child)) CloseMenu(false, true); }
    private void OutsideDeactivated(object? sender, EventArgs e) => CloseMenu(false, true);

    private void Choose(MoneyCurrency next)
    {
        CloseMenu(true, true);
        if (next.Code == Info.Code) return;
        string draft = Focused ? MoneyMath.ConvertDraft(_draft, next.Digits) : MoneyMath.Settle(MoneyMath.ConvertDraft(_draft, next.Digits), next.Digits);
        long? minor = MoneyMath.ToMinor(draft, next.Digits);
        _draft = draft;
        _source = Source.External;
        _choosing = true;
        SetCurrentValue(CurrencyProperty, next.Code);
        _choosing = false;
        Emit(minor, next);
        Announce($"{next.Name}{(minor is null ? "" : $", {next.Format(minor.Value)}")}");
    }

    private void ListKeyDown(object sender, KeyEventArgs e)
    {
        int at = _options.FindIndex(option => option.Info.Code == _active);
        void Go(int index) { e.Handled = true; _active = _options[Math.Clamp(index, 0, _options.Count - 1)].Info.Code; PaintOptions(true); }
        switch (e.Key)
        {
            case Key.Down: Go(at + 1); return;
            case Key.Up: Go(at - 1); return;
            case Key.Home: Go(0); return;
            case Key.End: Go(_options.Count - 1); return;
            case Key.Enter or Key.Space: e.Handled = true; if (at >= 0) Choose(_options[at].Info); return;
            case Key.Escape: e.Handled = true; CloseMenu(true, true); return;
            case Key.Tab: CloseMenu(false, true); return;
        }
        var id = Shortcuts.KeyId(e.Key);
        if (id is { Length: 1 } && char.IsLetterOrDigit(id[0]))
        {
            long now = Environment.TickCount64;
            _typeahead = (now - _typeahead.At > 600 ? id : _typeahead.Buffer + id, now);
            var hit = _options.FirstOrDefault(option => option.Info.Code.StartsWith(_typeahead.Buffer, StringComparison.OrdinalIgnoreCase) || option.Info.Name.StartsWith(_typeahead.Buffer, StringComparison.OrdinalIgnoreCase));
            if (hit.Info is not null) { e.Handled = true; _active = hit.Info.Code; PaintOptions(true); }
        }
    }

    /* ---------------------------------------------- Automation ---------------------------------------------- */

    private void UpdatePeer()
    {
        if (_input is null) return;
        long? minor = Minor;
        AutomationProperties.SetItemStatus(_input, minor is null ? "Empty" : Money(minor.Value));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MoneyInputPeer(this);

    private sealed class MoneyInputPeer(MoneyInput owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        protected override string GetClassNameCore() => nameof(MoneyInput);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;
        protected override string GetNameCore() => owner.Label;
        public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.RangeValue ? this : base.GetPattern(pattern);
        private double Scale => MoneyMath.Pow10(owner.Info.Digits);
        public double Value => (owner.Minor ?? 0) / Scale;
        public bool IsReadOnly => !owner.IsEnabled;
        public double Maximum => (owner.Maximum ?? long.MaxValue / 4) / Scale;
        public double Minimum => (owner.Minimum ?? 0) / Scale;
        public double LargeChange => owner.StepSize * 10 / Scale;
        public double SmallChange => owner.StepSize / Scale;
        public void SetValue(double value) => owner.SetAmount(MoneyMath.FromMinor((long)Math.Round(value * Scale), owner.Info.Digits), Source.External, int.MaxValue);
    }
}

/// <summary>
/// One mirrored character: a hidden sizer holds the width and the glyph rolls inside, feathered at the top and bottom edge. Columns open their width
/// (morph) while the digit rises in the direction of change; typing cuts straight to the result so the caret is never behind.
/// </summary>
internal sealed class GlyphColumn : Grid
{
    public enum Mode { Roll, Fade, Cut }
    private readonly TextBlock _sizer = new() { Visibility = Visibility.Hidden };
    private readonly SolidColorBrush _ink = new(Colors.Black);
    private TextBlock _glyph;
    private bool _ghost;
    private const double Feather = 36 * .18;

    public GlyphColumn(string text, bool ghost)
    {
        _ghost = ghost;
        ClipToBounds = true;
        Margin = new Thickness(0, -Feather, 0, -Feather);
        _sizer.Text = text;
        _sizer.Margin = new Thickness(0, Feather, 0, Feather);
        Children.Add(_sizer);
        _glyph = Glyph(text);
        Children.Add(_glyph);
        SetValue(TextElement.ForegroundProperty, _ink);
        OpacityMask = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
            GradientStops = { new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Black, .14), new GradientStop(Colors.Black, .86), new GradientStop(Colors.Transparent, 1) },
        };
        Loaded += (_, _) => Repaint();
    }

    public bool Leaving { get; private set; }

    private TextBlock Glyph(string text) => new() { Text = text, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, Feather, 0, Feather), RenderTransform = new TranslateTransform() };

    public void Repaint() => Tween.Color(_ink, Ink(), Tween.Standard, false);

    private Color Ink() => Tween.Resolve(this, _ghost ? "ArcWpf.Brush.MoneyGhost" : "ArcWpf.Brush.Foreground");

    public void Enter(bool cut)
    {
        if (cut || !IsLoadedOrParented()) return;
        _sizer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = _sizer.DesiredSize.Width;
        Width = 0;
        Opacity = 0;
        Tween.To(this, OpacityProperty, 1, Tween.Fast, null);
        Tween.Spring(this, WidthProperty, width, Tween.Morph, done: () => Tween.Set(this, WidthProperty, double.NaN));
    }

    private bool IsLoadedOrParented() => Parent is FrameworkElement { IsLoaded: true };

    public void Leave(bool cut, Action done)
    {
        Leaving = true;
        if (cut || !IsLoaded) { done(); return; }
        Width = ActualWidth;
        Tween.To(this, OpacityProperty, 0, Tween.Instant, null);
        Tween.Spring(this, WidthProperty, 0, Tween.Smooth, done: done);
    }

    public void Set(string text, bool ghost, int direction, Mode mode)
    {
        if (ghost != _ghost) { _ghost = ghost; Tween.Color(_ink, Ink(), Tween.Standard, mode != Mode.Cut && IsLoaded); }
        if (_glyph.Text == text) return;
        _sizer.Text = text;
        var old = _glyph;
        _glyph = Glyph(text);
        Children.Add(_glyph);
        if (mode == Mode.Cut || !IsLoaded) { Children.Remove(old); return; }
        if (mode == Mode.Fade)
        {
            Children.Remove(old);
            _glyph.Opacity = 0;
            Tween.To(_glyph, OpacityProperty, 1, Tween.Instant, null);
            return;
        }
        double em = 36 * .55;
        // enter: y dir·.55em, soft blur → snappy, opacity .16 enter, blur .18; exit: y −dir·.55em, subtle blur, .12 standard.
        Tween.Present(old, false, -em * direction, 2, Tween.Instant, ArcEasing.Standard, done: () => Children.Remove(old));
        var move = (TranslateTransform)_glyph.RenderTransform;
        _glyph.Opacity = 0;
        Tween.To(_glyph, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
        Tween.Set(move, TranslateTransform.YProperty, em * direction);
        Tween.Spring(move, TranslateTransform.YProperty, 0, Tween.Snappy);
        var blur = new BlurEffect { Radius = 4 };
        _glyph.Effect = blur;
        var glyph = _glyph;
        var clear = new DoubleAnimation(0, TimeSpan.FromSeconds(Tween.Exit)) { EasingFunction = ArcEasing.Enter };
        clear.Completed += (_, _) => { if (ReferenceEquals(glyph.Effect, blur)) glyph.Effect = null; };
        blur.BeginAnimation(BlurEffect.RadiusProperty, clear);
    }
}

/// <summary>The currency symbol swaps with a short roll while its slot springs to the new width (morph).</summary>
internal sealed class SymbolSlot : Grid
{
    private TextBlock? _glyph;
    private string _symbol = "\0";

    public SymbolSlot()
    {
        ClipToBounds = false;
        SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
    }

    public void Set(string symbol, bool full, bool animate)
    {
        if (symbol == _symbol) return;
        bool first = _symbol == "\0";
        _symbol = symbol;
        Visibility = symbol.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        var old = _glyph;
        _glyph = symbol.Length == 0 ? null : new TextBlock { Text = symbol, RenderTransform = new TranslateTransform() };
        double from = ActualWidth;
        if (_glyph is not null) Children.Add(_glyph);
        if (old is not null)
        {
            old.IsHitTestVisible = false;
            if (animate && !first) Tween.Present(old, false, full ? -.3 * 36 : 0, 0, Tween.Instant, ArcEasing.Standard, done: () => Children.Remove(old));
            else Children.Remove(old);
        }
        if (_glyph is null) return;
        _glyph.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double to = _glyph.DesiredSize.Width;
        if (!animate || first || !full) { Tween.Set(this, WidthProperty, double.NaN); if (animate && !first) Tween.Present(_glyph, true, 0, 0, Tween.Instant, ArcEasing.Standard); return; }
        Width = from;
        Tween.Spring(this, WidthProperty, to, Tween.Morph);
        var glyph = _glyph;
        Tween.To(glyph, OpacityProperty, 1, Tween.Fast, null);
        glyph.Opacity = 0;
        Tween.Set((TranslateTransform)glyph.RenderTransform, TranslateTransform.YProperty, .4 * 36);
        Tween.Spring((TranslateTransform)glyph.RenderTransform, TranslateTransform.YProperty, 0, Tween.Snappy);
        var blur = new BlurEffect { Radius = 4 };
        glyph.Effect = blur;
        var clear = new DoubleAnimation(0, TimeSpan.FromSeconds(Tween.Exit));
        clear.Completed += (_, _) => { if (ReferenceEquals(glyph.Effect, blur)) glyph.Effect = null; };
        blur.BeginAnimation(BlurEffect.RadiusProperty, clear);
    }
}

/// <summary>A 30 DIP quick add pill: border, xs 500 tabular copy, hover fill, press .96; at the maximum it dims to .45 but stays pressable.</summary>
internal sealed class QuickAddChip : System.Windows.Controls.Button
{
    private readonly decimal _amount;
    private readonly TextBlock _text = new() { FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
    private readonly SolidColorBrush _fill = new(Colors.White), _ink = new(Colors.Gray);
    private readonly ScaleTransform _scale = new(1, 1);
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(QuickAddChip), new PropertyMetadata(null, (d, _) => ((QuickAddChip)d).Paint(false)));

    public QuickAddChip(decimal amount)
    {
        _amount = amount;
        Cursor = Cursors.Hand;
        FocusVisualStyle = null;
        Margin = new Thickness(0, 0, 6, 0);
        var surface = new Border { Height = 30, Padding = new Thickness(12, 0, 12, 0), CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(1), Background = _fill, Child = _text, RenderTransform = _scale, RenderTransformOrigin = new Point(.5, .5) };
        surface.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _text.Foreground = _ink;
        var template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        Template = template;
        Content = surface;
        SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border");
    }

    public void Update(MoneyCurrency info, bool atMax)
    {
        _text.Text = "+" + info.FormatWhole(_amount);
        AutomationProperties.SetName(this, $"Add {info.FormatWhole(_amount)}");
        Opacity = atMax || !IsEnabled ? .45 : 1;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty || e.Property == IsKeyboardFocusedProperty) Paint(true);
        if (e.Property == IsPressedProperty) Tween.Press(_scale, IsPressed, .96, IsLoaded && Tween.Level(this) == ArcMotionLevel.Full);
    }

    private void Paint(bool animate)
    {
        bool hot = IsMouseOver || IsKeyboardFocused;
        animate &= IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
        Tween.Color(_fill, Tween.Resolve(this, hot ? "ArcWpf.Brush.SurfaceMuted" : "ArcWpf.Brush.Surface"), Tween.Fast, animate);
        Tween.Color(_ink, Tween.Resolve(this, hot ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextSecondary"), Tween.Fast, animate);
    }
}
