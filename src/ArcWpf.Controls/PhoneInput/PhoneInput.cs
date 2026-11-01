using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

public sealed class PhoneValueChangedEventArgs(RoutedEvent routedEvent, object source, string value, PhoneCountry country, string digits) : RoutedEventArgs(routedEvent, source)
{
    /// <summary>E.164, such as "+14155550132"; empty when there are no digits.</summary>
    public string Value { get; } = value;
    public PhoneCountry Country { get; } = country;
    /// <summary>The number as shown in the field, without the calling code.</summary>
    public string Formatted { get; } = PhoneNumbers.FormatNational(country, digits);
    public PhoneStatus Status { get; } = PhoneNumbers.StatusOf(country, digits);
    public bool IsValid => Status == PhoneStatus.Valid;
}

/// <summary>
/// A phone number field with a country picker (phone-input.tsx, frozen 2026-10-31). The country button grows into a searchable list, the number
/// formats as it is typed with a faint guide showing the rest of the expected shape, pasted international numbers pick their own country, and the
/// value comes out in E.164. Typing "+" in the number jumps into the picker with a calling code search.
/// </summary>
[TemplatePart(Name = "PART_Control", Type = typeof(Border))]
[TemplatePart(Name = "PART_Anchor", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Trigger", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Slot", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Chevron", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Number", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Guide", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Valid", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_Shape", Type = typeof(Border))]
[TemplatePart(Name = "PART_SearchRow", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Search", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_ListFace", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Scroll", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_Options", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Hint", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Error", Type = typeof(Grid))]
public class PhoneInput : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(PhoneInput), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((PhoneInput)d).OnValueChanged((string?)e.NewValue ?? "")));
    public static readonly DependencyProperty CountryProperty = DependencyProperty.Register(nameof(Country), typeof(string), typeof(PhoneInput), new FrameworkPropertyMetadata("US", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((PhoneInput)d).OnCountryChanged((string)e.OldValue)));
    public static readonly DependencyProperty CountriesProperty = DependencyProperty.Register(nameof(Countries), typeof(IList<string>), typeof(PhoneInput), new PropertyMetadata(null));
    public static readonly DependencyProperty PreferredCountriesProperty = DependencyProperty.Register(nameof(PreferredCountries), typeof(IList<string>), typeof(PhoneInput), new PropertyMetadata(null));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(PhoneInput), new PropertyMetadata(""));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(PhoneInput), new PropertyMetadata("", (d, _) => ((PhoneInput)d).RenderMessages(true)));
    public static readonly DependencyProperty ErrorProperty = DependencyProperty.Register(nameof(Error), typeof(string), typeof(PhoneInput), new PropertyMetadata("", (d, _) => ((PhoneInput)d).RenderMessages(true)));
    public static readonly DependencyProperty ValidateProperty = DependencyProperty.Register(nameof(Validate), typeof(bool), typeof(PhoneInput), new PropertyMetadata(true, (d, _) => ((PhoneInput)d).RenderMessages(true)));
    private static readonly DependencyPropertyKey StatusPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Status), typeof(PhoneStatus), typeof(PhoneInput), new FrameworkPropertyMetadata(PhoneStatus.Empty));
    public static readonly DependencyProperty StatusProperty = StatusPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsDropDownOpenPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDropDownOpen), typeof(bool), typeof(PhoneInput), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsDropDownOpenProperty = IsDropDownOpenPropertyKey.DependencyProperty;
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble, typeof(EventHandler<PhoneValueChangedEventArgs>), typeof(PhoneInput));
    private static readonly DependencyProperty ShapeRadiusProperty = DependencyProperty.Register("ShapeRadius", typeof(double), typeof(PhoneInput), new PropertyMetadata(17d, (d, e) => { if (((PhoneInput)d)._shape is { } shape) shape.CornerRadius = new CornerRadius((double)e.NewValue); }));
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(PhoneInput), new PropertyMetadata(null, (d, _) => ((PhoneInput)d).PaintEdge(false)));

    /// <summary>The number in E.164, such as "+14155550132". An empty string clears the field.</summary>
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>ISO code of the selected country, such as "US".</summary>
    public string Country { get => (string)GetValue(CountryProperty); set => SetValue(CountryProperty, value); }
    /// <summary>Limit the picker to these ISO codes.</summary>
    public IList<string>? Countries { get => (IList<string>?)GetValue(CountriesProperty); set => SetValue(CountriesProperty, value); }
    /// <summary>Pinned at the top of the picker under "Suggested". Null uses US, CA, GB.</summary>
    public IList<string>? PreferredCountries { get => (IList<string>?)GetValue(PreferredCountriesProperty); set => SetValue(PreferredCountriesProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Replaces the built-in validation message.</summary>
    public string Error { get => (string)GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }
    /// <summary>Show a message after the field loses focus when the number is incomplete.</summary>
    public bool Validate { get => (bool)GetValue(ValidateProperty); set => SetValue(ValidateProperty, value); }
    public PhoneStatus Status => (PhoneStatus)GetValue(StatusProperty);
    public bool IsValid => Status == PhoneStatus.Valid;
    public bool IsDropDownOpen => (bool)GetValue(IsDropDownOpenProperty);
    public event EventHandler<PhoneValueChangedEventArgs> ValueChanged { add => AddHandler(ValueChangedEvent, value); remove => RemoveHandler(ValueChangedEvent, value); }

    private const double PanelMax = 340, ClosedRadius = 17, OpenRadius = 22, Lid = 42;
    private const int Page = 8;

    private Border? _control, _shape;
    private Grid? _anchor, _slot, _searchRow, _options, _hintHost, _errorHost;
    private System.Windows.Controls.Button? _trigger;
    private FrameworkElement? _chevron, _valid, _listFace;
    private TextBox? _number, _search;
    private TextBlock? _guide, _searchPlaceholder;
    private Popup? _popup;
    private ScrollViewer? _scroll;
    private FieldMessage? _hint, _error;
    private FieldIconButton? _clear;
    private FrameworkElement? _layer;
    private readonly RotateTransform _turn = new();
    private readonly SolidColorBrush _edge = new(Colors.Gray);
    private readonly Border _highlight = new() { CornerRadius = new CornerRadius(16), VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Opacity = 0, RenderTransform = new TranslateTransform() };
    private readonly List<(string Key, PhoneCountry Entry, Border Row)> _rows = [];
    private string _digits = "", _iso = "US", _lastE164 = "";
    private string? _active;
    private bool _touched, _syncing, _validShown;
    private int _roll = 1;
    private Window? _window;

    static PhoneInput()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PhoneInput), new FrameworkPropertyMetadata(typeof(PhoneInput)));
        FocusableProperty.OverrideMetadata(typeof(PhoneInput), new FrameworkPropertyMetadata(false));
    }

    public PhoneInput()
    {
        SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border");
        _highlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.FieldHighlight");
        Unloaded += (_, _) => Close(null, false);
    }

    private IReadOnlyList<PhoneCountry> Pool => Countries is { Count: > 0 } codes ? PhoneNumbers.Countries.Where(entry => codes.Contains(entry.Iso)).ToList() : PhoneNumbers.Countries;
    private PhoneCountry Current => PhoneNumbers.ByIso(_iso) ?? PhoneNumbers.Countries[0];
    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;

    public override void OnApplyTemplate()
    {
        if (_number is not null)
        {
            _number.TextChanged -= NumberChanged;
            _number.PreviewKeyDown -= NumberKeyDown;
            _number.PreviewTextInput -= NumberTextInput;
            _number.LostKeyboardFocus -= NumberBlur;
            _number.GotKeyboardFocus -= NumberFocus;
            DataObject.RemovePastingHandler(_number, NumberPaste);
        }
        base.OnApplyTemplate();
        _control = GetTemplateChild("PART_Control") as Border;
        _anchor = GetTemplateChild("PART_Anchor") as Grid;
        _trigger = GetTemplateChild("PART_Trigger") as System.Windows.Controls.Button;
        _slot = GetTemplateChild("PART_Slot") as Grid;
        _chevron = GetTemplateChild("PART_Chevron") as FrameworkElement;
        _number = GetTemplateChild("PART_Number") as TextBox;
        _guide = GetTemplateChild("PART_Guide") as TextBlock;
        _valid = GetTemplateChild("PART_Valid") as FrameworkElement;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _shape = GetTemplateChild("PART_Shape") as Border;
        _searchRow = GetTemplateChild("PART_SearchRow") as Grid;
        _search = GetTemplateChild("PART_Search") as TextBox;
        _searchPlaceholder = GetTemplateChild("PART_SearchPlaceholder") as TextBlock;
        _listFace = GetTemplateChild("PART_ListFace") as FrameworkElement;
        _scroll = GetTemplateChild("PART_Scroll") as ScrollViewer;
        _options = GetTemplateChild("PART_Options") as Grid;
        _hintHost = GetTemplateChild("PART_Hint") as Grid;
        _errorHost = GetTemplateChild("PART_Error") as Grid;
        _hintHost?.Children.Clear();
        _errorHost?.Children.Clear();
        _hint = _hintHost is null ? null : new FieldMessage();
        _error = _errorHost is null ? null : new FieldMessage();
        if (_hint is not null) _hintHost!.Children.Add(_hint);
        if (_error is not null) _errorHost!.Children.Add(_error);
        if (_control is not null) _control.BorderBrush = _edge;
        if (_chevron is not null) { _chevron.RenderTransform = _turn; _chevron.RenderTransformOrigin = new Point(.5, .5); }
        if (_trigger is not null)
        {
            _trigger.Click += (_, _) => { if (IsDropDownOpen) Close("trigger", true); else OpenList(""); };
            _trigger.PreviewKeyDown += (_, e) => { if (e.Key is Key.Down or Key.Up) { e.Handled = true; OpenList(""); } };
            _trigger.PreviewTextInput += (_, e) => { if (e.Text.Length == 1 && e.Text != " " && !char.IsControl(e.Text[0])) { e.Handled = true; OpenList(e.Text); } };
        }
        if (_number is not null)
        {
            _number.TextChanged += NumberChanged;
            _number.PreviewKeyDown += NumberKeyDown;
            _number.PreviewTextInput += NumberTextInput;
            _number.LostKeyboardFocus += NumberBlur;
            _number.GotKeyboardFocus += NumberFocus;
            DataObject.AddPastingHandler(_number, NumberPaste);
        }
        if (_popup is not null)
        {
            _popup.Placement = PlacementMode.Custom;
            _popup.PlacementTarget = _control;
            // Open, the surface steps out over the shell's own border so the two edges never double up.
            _popup.CustomPopupPlacementCallback = (_, _, _) => [new CustomPopupPlacement(new Point(-1, -1), PopupPrimaryAxis.None)];
            if (_popup.Child is not null) BindingOperations.SetBinding(_popup.Child, ArcMotion.LevelProperty, new Binding { Source = this, Path = new PropertyPath(ArcMotion.LevelProperty) });
        }
        if (_search is not null)
        {
            _search.TextChanged += (_, _) => OnQuery();
            _search.PreviewKeyDown += SearchKeyDown;
        }
        if (GetTemplateChild("PART_Close") is System.Windows.Controls.Button closeHit) closeHit.Click += (_, _) => Close("trigger", true);
        if (_options is not null) { _options.Children.Clear(); _options.Children.Add(_highlight); }
        var parsed = PhoneNumbers.Parse(Value, Pool);
        if (parsed is { } hit) { _iso = hit.Country.Iso; _digits = hit.National; SetCurrentValue(CountryProperty, _iso); }
        else _iso = PhoneNumbers.ByIso(Country)?.Iso ?? "US";
        _layer = null;
        _slot?.Children.Clear();
        Render(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty || e.Property == IsEnabledProperty) PaintEdge(true);
    }

    /* ---------------------------------------------- Value flow ---------------------------------------------- */

    private void OnValueChanged(string value)
    {
        if (value == _lastE164 || _number is null) return;
        var parsed = PhoneNumbers.Parse(value, Pool);
        if (parsed is { } hit) { SetCountry(hit.Country.Iso); _digits = hit.National; }
        else _digits = "";
        _lastE164 = PhoneNumbers.ToE164(Current, _digits);
        Render(true);
    }

    private void OnCountryChanged(string? previous)
    {
        if (_syncing || _number is null || PhoneNumbers.ByIso(Country) is not { } entry || entry.Iso == _iso) return;
        Roll(entry);
        _iso = entry.Iso;
        _digits = PhoneNumbers.CapDigits(entry, _digits);
        Emit();
        Render(true);
    }

    private void SetCountry(string iso)
    {
        _iso = iso;
        _syncing = true;
        SetCurrentValue(CountryProperty, iso);
        _syncing = false;
    }

    private void Emit()
    {
        var entry = Current;
        _lastE164 = PhoneNumbers.ToE164(entry, _digits);
        SetCurrentValue(ValueProperty, _lastE164);
        RaiseEvent(new PhoneValueChangedEventArgs(ValueChangedEvent, this, _lastE164, entry, _digits));
    }

    private void SetNumber(string digits, int? caretDigits, PhoneCountry? entry = null)
    {
        entry ??= Current;
        string capped = PhoneNumbers.CapDigits(entry, digits);
        bool same = capped == _digits && entry.Iso == _iso;
        if (entry.Iso != _iso) { Roll(entry); SetCountry(entry.Iso); }
        _digits = capped;
        if (!same) Emit();
        Render(true, caretDigits is null ? null : Math.Min(caretDigits.Value, capped.Length));
    }

    private void Announce(string message) { if (_number is not null) Announcer.Say(_number, message); }

    /* ---------------------------------------------- Number entry ---------------------------------------------- */

    private void NumberChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || _number is null) return;
        string raw = _number.Text;
        // Autofill and dropped text arrive as a change: an international number chooses its own country.
        if (raw.Contains('+') || raw.TrimStart().StartsWith("00", StringComparison.Ordinal) && PhoneNumbers.OnlyDigits(raw).Length > 6)
        {
            if (PhoneNumbers.Parse(raw[Math.Max(0, raw.IndexOf('+'))..], Pool) is { } parsed && parsed.National.Length > 0)
            {
                if (parsed.Country.Iso != _iso) Announce($"Country set to {parsed.Country.Name}");
                SetNumber(parsed.National, parsed.National.Length, parsed.Country);
                return;
            }
        }
        int caret = _number.CaretIndex;
        SetNumber(PhoneNumbers.OnlyDigits(raw), PhoneNumbers.OnlyDigits(raw[..Math.Min(caret, raw.Length)]).Length);
    }

    // Typing "+" jumps into the picker with a calling code search.
    private void NumberTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text == "+" && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; OpenList("+"); }
    }

    private void NumberKeyDown(object sender, KeyEventArgs e)
    {
        if (_number is null || _number.SelectionLength > 0 || Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;
        int start = _number.SelectionStart;
        string text = _number.Text;
        // Deleting a separator deletes the digit beside it instead of doing nothing.
        if (e.Key == Key.Back && start > 0 && !char.IsAsciiDigit(text[start - 1]))
        {
            e.Handled = true;
            int index = PhoneNumbers.OnlyDigits(text[..start]).Length;
            if (index > 0) SetNumber(_digits[..(index - 1)] + _digits[index..], index - 1);
        }
        else if (e.Key == Key.Delete && start < text.Length && !char.IsAsciiDigit(text[start]))
        {
            e.Handled = true;
            int index = PhoneNumbers.OnlyDigits(text[..start]).Length;
            if (index < _digits.Length) SetNumber(_digits[..index] + _digits[(index + 1)..], index);
        }
    }

    private void NumberPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (_number is null || e.DataObject.GetData(DataFormats.UnicodeText) is not string text || text.Length == 0) return;
        var trimmed = text.TrimStart();
        if ((trimmed.StartsWith('+') || trimmed.StartsWith("00", StringComparison.Ordinal)) && PhoneNumbers.Parse(text, Pool) is { } parsed)
        {
            if (parsed.Country.Iso != _iso) Announce($"Country set to {parsed.Country.Name}");
            SetNumber(parsed.National, parsed.National.Length, parsed.Country);
            return;
        }
        string pasted = PhoneNumbers.OnlyDigits(text);
        var current = Current;
        // "1 415 555 0132" pasted into a US field: the leading calling code is dropped when the number would not fit otherwise.
        if (pasted.StartsWith(current.Dial, StringComparison.Ordinal) && pasted.Length > PhoneNumbers.CapDigits(current, pasted).Length) pasted = pasted[current.Dial.Length..];
        // A whole number replaces what was there; a fragment goes in at the caret.
        if (pasted.Length >= current.Lengths.Min()) { SetNumber(pasted, pasted.Length); return; }
        string value = _number.Text;
        int a = PhoneNumbers.OnlyDigits(value[.._number.SelectionStart]).Length;
        int b = PhoneNumbers.OnlyDigits(value[..(_number.SelectionStart + _number.SelectionLength)]).Length;
        SetNumber(_digits[..a] + pasted + _digits[b..], a + pasted.Length);
    }

    private void NumberFocus(object sender, KeyboardFocusChangedEventArgs e) => PaintEdge(true);

    private void NumberBlur(object sender, KeyboardFocusChangedEventArgs e)
    {
        _touched = _digits.Length > 0;
        PaintEdge(true);
        RenderMessages(true);
    }

    /* ---------------------------------------------- Rendering ---------------------------------------------- */

    private void Render(bool animate, int? caretDigits = null)
    {
        if (_number is null) return;
        var entry = Current;
        string formatted = PhoneNumbers.FormatNational(entry, _digits);
        _syncing = true;
        if (_number.Text != formatted) _number.Text = formatted;
        if (caretDigits is int count && _number.IsKeyboardFocused) _number.CaretIndex = PhoneNumbers.CaretAfterDigits(formatted, count);
        _syncing = false;
        if (_guide is not null)
        {
            _guide.Inlines.Clear();
            _guide.Inlines.Add(new Run(formatted) { Foreground = Brushes.Transparent });
            _guide.Inlines.Add(new Run(PhoneNumbers.Guide(entry, _digits)));
        }
        var status = PhoneNumbers.StatusOf(entry, _digits);
        SetValue(StatusPropertyKey, status);
        ShowValid(status == PhoneStatus.Valid, animate);
        RenderFace(animate);
        RenderMessages(animate);
        AutomationProperties.SetName(_number, Label);
        if (_trigger is not null) AutomationProperties.SetName(_trigger, $"Country, {entry.Name} +{entry.Dial}");
    }

    // The valid check pops in (scale .6, subtle blur → snappy) at the right edge of the number.
    private void ShowValid(bool show, bool animate)
    {
        if (_valid is null || show == _validShown) { if (_valid is not null && !animate) _valid.Opacity = show ? 1 : 0; return; }
        _validShown = show;
        if (show && animate) Announce($"Valid {Current.Name} number");
        var scale = _valid.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        _valid.RenderTransform = scale;
        _valid.RenderTransformOrigin = new Point(.5, .5);
        animate &= IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;
        if (!animate) { Tween.Set(_valid, OpacityProperty, show ? 1 : 0); Tween.Set(scale, ScaleTransform.ScaleXProperty, 1); Tween.Set(scale, ScaleTransform.ScaleYProperty, 1); return; }
        if (!Full) { Tween.To(_valid, OpacityProperty, show ? 1 : 0, show ? Tween.Instant : .1, null); return; }
        if (show)
        {
            Tween.Set(scale, ScaleTransform.ScaleXProperty, .6);
            Tween.Set(scale, ScaleTransform.ScaleYProperty, .6);
            Tween.Present(_valid, true, 0, 2, Tween.Snappy, ArcEasing.Enter);
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
        }
        else
        {
            Tween.To(_valid, OpacityProperty, 0, .1, null);
            Tween.To(scale, ScaleTransform.ScaleXProperty, .8, .1, null);
            Tween.To(scale, ScaleTransform.ScaleYProperty, .8, .1, null);
        }
    }

    private static FrameworkElement Flag(string iso)
    {
        var text = new TextBlock { Text = iso, FontSize = 9, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var badge = new Border { Width = 20, Height = 15, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1), Child = text, VerticalAlignment = VerticalAlignment.Center };
        badge.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderStrong");
        return badge;
    }

    private static FrameworkElement Face(PhoneCountry entry)
    {
        var face = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, RenderTransform = new TranslateTransform() };
        face.Children.Add(Flag(entry.Iso));
        face.Children.Add(new TextBlock { Text = $"+{entry.Dial}", FontWeight = FontWeights.Medium, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return face;
    }

    private void Roll(PhoneCountry next)
    {
        var order = Pool.OrderBy(entry => entry.Name, StringComparer.CurrentCulture).Select(entry => entry.Iso).ToList();
        _roll = Math.Sign(order.IndexOf(next.Iso) - order.IndexOf(_iso)) is var sign && sign != 0 ? sign : 1;
    }

    // The flag and calling code roll in the direction of the list (GLIDE .28): a country further down rises from below. The anchor springs to
    // the closed button's new width (WIDTH .42).
    private void RenderFace(bool animate)
    {
        if (_slot is null || _anchor is null) return;
        var entry = Current;
        if (_layer?.Tag as string == entry.Iso) return;
        animate &= IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;
        bool full = animate && Full;
        var old = _layer;
        _layer = Face(entry);
        _layer.Tag = entry.Iso;
        _slot.Children.Add(_layer);
        double em = 14;
        if (old is not null)
        {
            old.IsHitTestVisible = false;
            if (full) Tween.Present(old, false, -.5 * em * _roll, 2, .12, ArcEasing.Standard, done: () => _slot.Children.Remove(old));
            else if (animate) Tween.To(old, OpacityProperty, 0, .08, null, done: () => _slot.Children.Remove(old));
            else _slot.Children.Remove(old);
            if (full)
            {
                var move = (TranslateTransform)_layer.RenderTransform;
                Tween.Set(move, TranslateTransform.YProperty, .6 * em * _roll);
                Tween.Spring(move, TranslateTransform.YProperty, 0, .28);
                Tween.Present(_layer, true, 0, 4, .22, ArcEasing.Enter, opacitySeconds: .2);
            }
            else if (animate) { _layer.Opacity = 0; Tween.To(_layer, OpacityProperty, 1, .12, null); }
        }
        // Closed width: 3 inset + 9 padding + face + 33 padding + 3 inset.
        _layer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = 12 + _layer.DesiredSize.Width + 36;
        if (full && _anchor.ActualWidth > 0) { _anchor.Width = _anchor.ActualWidth; Tween.Spring(_anchor, WidthProperty, width, Tween.Morph); }
        else Tween.Set(_anchor, WidthProperty, width);
    }

    private void RenderMessages(bool animate)
    {
        if (_hint is null || _error is null) return;
        animate &= IsLoaded;
        var entry = Current;
        var status = PhoneNumbers.StatusOf(entry, _digits);
        string? builtIn = Validate && _touched && status is PhoneStatus.Incomplete or PhoneStatus.TooLong ? $"Numbers in {entry.Name} have {PhoneNumbers.LengthCopy(entry)} digits" : null;
        string? message = string.IsNullOrEmpty(Error) ? builtIn : Error;
        if (message is not null) _error.Show(message, FieldMessage.Line(message, "ArcWpf.Brush.Danger"), animate);
        else _error.Hide(animate);
        if (!string.IsNullOrEmpty(Description) && message is null) _hint.Show(Description, FieldMessage.Line(Description, "ArcWpf.Brush.TextMuted"), animate);
        else _hint.Hide(animate);
        if (_number is not null) AutomationProperties.SetHelpText(_number, message ?? Description ?? "");
        PaintEdge(animate);
    }

    // .control: border-strong; hover (valid) or number focus foreground; invalid danger.
    private void PaintEdge(bool animate)
    {
        bool invalid = _error?.Key is not null;
        string key = invalid ? "Danger" : _number?.IsKeyboardFocused == true || (IsMouseOver && IsEnabled && !IsDropDownOpen) ? "Foreground" : "BorderStrong";
        Tween.Color(_edge, Tween.Resolve(this, $"ArcWpf.Brush.{key}"), Tween.Fast, animate && Full);
        if (_control is not null) _control.Opacity = IsEnabled ? 1 : .5;
    }

    /* ---------------------------------------------- Country picker ---------------------------------------------- */

    private IReadOnlyList<(string Key, string? Label, IReadOnlyList<(string Key, PhoneCountry Entry)> Rows)> Sections()
    {
        string needle = (_search?.Text ?? "").Trim().ToLowerInvariant();
        var sorted = Pool.OrderBy(entry => entry.Name, StringComparer.CurrentCulture).ToList();
        if (needle.Length > 0 && needle != "+")
        {
            var hits = sorted.Where(entry => PhoneNumbers.Matches(entry, needle)).ToList();
            string digits = PhoneNumbers.OnlyDigits(needle);
            if (digits.Length > 0) hits = hits.OrderByDescending(entry => entry.Dial == digits).ThenBy(entry => entry.Dial.Length).ToList();
            return [("results", null, hits.Select(entry => ($"r-{entry.Iso}", entry)).ToList())];
        }
        var preferred = (PreferredCountries ?? ["US", "CA", "GB"]).Select(code => Pool.FirstOrDefault(entry => entry.Iso == code)).OfType<PhoneCountry>().ToList();
        var all = ("all", preferred.Count > 0 ? "All countries" : null, (IReadOnlyList<(string, PhoneCountry)>)sorted.Select(entry => ($"a-{entry.Iso}", entry)).ToList());
        return preferred.Count > 0 ? [("preferred", "Suggested", preferred.Select(entry => ($"p-{entry.Iso}", entry)).ToList()), all] : [all];
    }

    private void BuildRows()
    {
        if (_options is null) return;
        _rows.Clear();
        foreach (var child in _options.Children.OfType<UIElement>().Where(child => child != _highlight).ToList()) _options.Children.Remove(child);
        var stack = new StackPanel();
        var sections = Sections();
        foreach (var (key, label, rows) in sections)
        {
            var group = new StackPanel { Margin = new Thickness(0, stack.Children.Count == 0 ? 0 : 4, 0, 0) };
            if (label is not null)
            {
                var heading = new TextBlock { Text = label, FontSize = 12, Padding = new Thickness(10, 8, 10, 4) };
                heading.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
                group.Children.Add(heading);
                AutomationProperties.SetName(group, label);
            }
            foreach (var (rowKey, entry) in rows)
            {
                var row = OptionRow(rowKey, entry);
                row.Margin = new Thickness(0, group.Children.Count == 0 ? 0 : 2, 0, 0);
                group.Children.Add(row);
                _rows.Add((rowKey, entry, row));
            }
            stack.Children.Add(group);
        }
        if (_rows.Count == 0)
        {
            var empty = new TextBlock { Text = $"No countries match “{(_search?.Text ?? "").Trim()}”", Padding = new Thickness(10, 12, 10, 14), TextWrapping = TextWrapping.Wrap };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            stack.Children.Add(empty);
        }
        _options.Children.Add(stack);
        if (_active is null || _rows.All(row => row.Key != _active)) _active = _rows.FirstOrDefault().Key;
    }

    // .option: 36 high, radius 16, secondary ink → foreground when active or selected; flag, name 500 (ellipsis), +dial xs muted, check.
    private Border OptionRow(string key, PhoneCountry entry)
    {
        var grid = new Grid();
        foreach (var width in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, new GridLength(16) }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        var name = new TextBlock { Text = entry.Name, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var meta = new TextBlock { Text = $"+{entry.Dial}", FontSize = 12, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        meta.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        var check = Lucide.Glyph(Lucide.Check, 16);
        check.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.Foreground");
        bool selected = entry.Iso == _iso;
        check.Opacity = selected ? 1 : 0;
        check.RenderTransformOrigin = new Point(.5, .5);
        check.RenderTransform = new ScaleTransform(selected ? 1 : .6, selected ? 1 : .6);
        var flag = Flag(entry.Iso);
        Grid.SetColumn(name, 1);
        Grid.SetColumn(meta, 2);
        Grid.SetColumn(check, 3);
        grid.Children.Add(flag);
        grid.Children.Add(name);
        grid.Children.Add(meta);
        grid.Children.Add(check);
        var row = new Border { MinHeight = 36, Padding = new Thickness(10, 0, 10, 0), CornerRadius = new CornerRadius(16), Background = Brushes.Transparent, Child = grid, Cursor = Cursors.Hand };
        row.SetValue(TextElement.ForegroundProperty, new SolidColorBrush(Tween.Resolve(this, selected ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextSecondary")));
        row.MouseMove += (_, _) => { if (_active != key) { _active = key; PaintRows(true, false); } };
        row.MouseLeftButtonDown += (_, e) => e.Handled = true;
        row.MouseLeftButtonUp += (_, e) => { e.Handled = true; Pick(entry); };
        AutomationProperties.SetName(row, $"{entry.Name} +{entry.Dial}");
        return row;
    }

    private void PaintRows(bool animate, bool scroll)
    {
        animate &= Full;
        foreach (var (key, entry, row) in _rows)
            if (row.GetValue(TextElement.ForegroundProperty) is SolidColorBrush ink)
                Tween.Color(ink, Tween.Resolve(this, key == _active || entry.Iso == _iso ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextSecondary"), Tween.Fast, animate);
        var target = _rows.FirstOrDefault(row => row.Key == _active).Row;
        if (target is null || _options is null) { Tween.To(_highlight, OpacityProperty, 0, animate ? .12 : 0.001, ArcEasing.Standard); return; }
        _options.UpdateLayout();
        double y = target.TranslatePoint(new Point(), _options).Y, height = target.ActualHeight;
        var move = (TranslateTransform)_highlight.RenderTransform;
        // The highlight glides between rows on its own spring (GLIDE .28) and jumps into place when it first appears.
        if (_highlight.Opacity < .05 || !animate) { Tween.Set(move, TranslateTransform.YProperty, y); Tween.Set(_highlight, HeightProperty, height); }
        else { Tween.Spring(move, TranslateTransform.YProperty, y, .28); Tween.Spring(_highlight, HeightProperty, height, .28); }
        Tween.To(_highlight, OpacityProperty, 1, animate ? .12 : 0.001, ArcEasing.Enter);
        if (scroll && _scroll is not null)
        {
            const double pad = 6;
            double top = y + 6;
            if (top < _scroll.VerticalOffset + pad) _scroll.ScrollToVerticalOffset(top - pad);
            else if (top + height > _scroll.VerticalOffset + _scroll.ViewportHeight - pad) _scroll.ScrollToVerticalOffset(top + height - _scroll.ViewportHeight + pad);
        }
    }

    /// <summary>Opens the country list, optionally seeding the search (as typing "+" in the number does).</summary>
    public void OpenList(string seed)
    {
        if (!IsEnabled || IsDropDownOpen || _popup is null || _shape is null || _search is null) return;
        SetValue(IsDropDownOpenPropertyKey, true);
        _search.Text = seed;
        _search.CaretIndex = seed.Length;
        BuildRows();
        _active = seed.Length > 0 ? _rows.FirstOrDefault().Key : _rows.FirstOrDefault(row => row.Entry.Iso == _iso && !row.Key.StartsWith("p-", StringComparison.Ordinal)).Key ?? _rows.FirstOrDefault().Key;
        _popup.IsOpen = true;
        _popup.Child?.UpdateLayout();
        PaintRows(false, true);
        _search.Focus();
        Keyboard.Focus(_search);
        AttachOutside();
        Morph(true);
        PaintEdge(true);
    }

    private void Close(string? focus, bool animate)
    {
        if (!IsDropDownOpen) return;
        SetValue(IsDropDownOpenPropertyKey, false);
        DetachOutside();
        if (focus == "trigger") _trigger?.Focus();
        if (focus == "number" && _number is not null) { _number.Focus(); _number.CaretIndex = _number.Text.Length; }
        Morph(false, animate);
        PaintEdge(true);
    }

    // One surface whose width, height, and corners spring between the country button and the open list (GROW .4 open, SHRINK .32 closed); the
    // search row and list face fade in with a short delay, the closed button's face hides while the list is open.
    private void Morph(bool open, bool animate = true)
    {
        if (_shape is null || _popup is null || _anchor is null || _control is null) return;
        bool full = animate && Full;
        double trigger = _anchor.ActualWidth + 1, panel = Math.Min(PanelMax, _control.ActualWidth);
        if (_listFace is not null) { _listFace.Width = panel; _listFace.Measure(new Size(panel, double.PositiveInfinity)); }
        double list = _listFace?.DesiredSize.Height ?? 0;
        double width = open ? Math.Max(trigger, panel) : trigger, height = open ? Lid + 2 + list : Lid + 2, radius = open ? OpenRadius : ClosedRadius, vd = open ? Tween.Smooth : .32;
        if (_slot is not null) Tween.To(_slot, OpacityProperty, open ? 0 : 1, full ? Tween.Instant : 0.001, ArcEasing.Standard);
        if (full) Tween.Spring(_turn, RotateTransform.AngleProperty, open ? 180 : 0, .28);
        else Tween.Set(_turn, RotateTransform.AngleProperty, open ? 180 : 0);
        if (open)
        {
            if (full)
            {
                Tween.Set(_shape, WidthProperty, trigger);
                Tween.Set(_shape, HeightProperty, Lid + 2);
                Tween.Set(this, ShapeRadiusProperty, ClosedRadius);
                Tween.Spring(_shape, WidthProperty, width, vd);
                Tween.Spring(_shape, HeightProperty, height, vd);
                Tween.Spring(this, ShapeRadiusProperty, radius, vd);
                if (_searchRow is not null) Tween.Present(_searchRow, true, 0, 2, .18, ArcEasing.Enter, .05);
                if (_listFace is not null) { Tween.Present(_listFace, true, 0, 2, .22, ArcEasing.Enter, .04, opacitySeconds: .2); _listFace.RenderTransform = new TranslateTransform(0, -6); Tween.Spring((TranslateTransform)_listFace.RenderTransform, TranslateTransform.YProperty, 0, Tween.Smooth); }
            }
            else
            {
                Tween.Set(_shape, WidthProperty, width);
                Tween.Set(_shape, HeightProperty, height);
                Tween.Set(this, ShapeRadiusProperty, radius);
                foreach (var face in new[] { _searchRow, _listFace }) if (face is not null) { face.Opacity = 0; Tween.To(face, OpacityProperty, 1, Tween.Instant, null); }
            }
            return;
        }
        if (!full)
        {
            _popup.IsOpen = false;
            return;
        }
        foreach (var face in new FrameworkElement?[] { _searchRow, _listFace }) if (face is not null) Tween.Present(face, false, face == _listFace ? -6 : 0, 2, .1, ArcEasing.Standard);
        Tween.Spring(_shape, WidthProperty, width, vd);
        Tween.Spring(this, ShapeRadiusProperty, radius, vd);
        Tween.Spring(_shape, HeightProperty, height, vd, done: () => { if (!IsDropDownOpen && _popup is not null) _popup.IsOpen = false; });
    }

    private void Pick(PhoneCountry? entry)
    {
        if (entry is null) return;
        if (entry.Iso != _iso)
        {
            Roll(entry);
            SetCountry(entry.Iso);
            _digits = PhoneNumbers.CapDigits(entry, _digits);
            Emit();
            Render(true);
            Announce($"{entry.Name}, +{entry.Dial}");
        }
        Close("number", true);
    }

    private void OnQuery()
    {
        if (!IsDropDownOpen || _search is null) return;
        if (_searchPlaceholder is not null) _searchPlaceholder.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _active = null;
        BuildRows();
        _scroll?.ScrollToTop();
        PaintRows(false, false);
        SetClear(_search.Text.Length > 0);
        string text = _search.Text.Trim().ToLowerInvariant();
        int count = text.Length > 0 && text != "+" ? Pool.Count(entry => PhoneNumbers.Matches(entry, text)) : Pool.Count;
        Announce(text.Length > 0 ? count > 0 ? $"{count} {(count == 1 ? "country" : "countries")}" : "No matches" : "");
        Morph(true, false);
    }

    private void SetClear(bool show)
    {
        if (_searchRow is null) return;
        if (show && _clear is null)
        {
            _clear = new FieldIconButton(Lucide.X, 14, 24, 12) { RestFill = "FieldClear", HoverFill = "FieldClear", Margin = new Thickness(0, 0, 4, 0), Focusable = false };
            AutomationProperties.SetName(_clear, "Clear search");
            _clear.Click += (_, _) => { _search!.Clear(); _search.Focus(); };
            Grid.SetColumn(_clear, 2);
            _searchRow.Children.Add(_clear);
            if (Full) { _clear.RenderTransformOrigin = new Point(.5, .5); var scale = new ScaleTransform(.6, .6); _clear.RenderTransform = scale; _clear.Opacity = 0; Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, .28); Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, .28); Tween.Spring(_clear, OpacityProperty, 1, .28); }
        }
        else if (!show && _clear is not null) { _searchRow.Children.Remove(_clear); _clear = null; }
    }

    private void SearchKeyDown(object sender, KeyEventArgs e)
    {
        int at = _rows.FindIndex(row => row.Key == _active);
        void Move(int index) { e.Handled = true; if (_rows.Count == 0) return; _active = _rows[Math.Clamp(index, 0, _rows.Count - 1)].Key; PaintRows(true, true); }
        bool query = (_search?.Text.Length ?? 0) > 0;
        switch (e.Key)
        {
            case Key.Down: Move(at + 1); return;
            case Key.Up: Move(at - 1); return;
            case Key.PageDown: Move(at + Page); return;
            case Key.PageUp: Move(at - Page); return;
            case Key.Home when !query: Move(0); return;
            case Key.End when !query: Move(_rows.Count - 1); return;
            case Key.Enter: e.Handled = true; Pick(at >= 0 ? _rows[at].Entry : null); return;
            case Key.Escape: e.Handled = true; Close("trigger", true); return;
            case Key.Tab: Close(null, true); return;
        }
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

    private void OutsideDown(object sender, MouseButtonEventArgs e) { if (!Outside.IsWithin(e.OriginalSource, _trigger, _popup?.Child)) Close(null, true); }
    private void OutsideDeactivated(object? sender, EventArgs e) => Close(null, true);

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
