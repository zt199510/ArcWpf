using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum ConfirmPopoverPhase { Idle, Confirming, Working, Success, Error }
public enum ConfirmPopoverTone { Danger, Neutral }
public enum ConfirmPopoverAlign { Start, Center, End }
public enum ConfirmPopoverSide { Auto, Bottom, Top }

/// <summary>What a running action can use: report progress from 0 to 1, and stop if the control goes away.</summary>
public sealed class ConfirmPopoverContext(Action<double> setProgress, CancellationToken cancellation)
{
    public CancellationToken Cancellation { get; } = cancellation;
    public void SetProgress(double value) => setProgress(value);
}

public sealed class ConfirmPopoverPhaseEventArgs(ConfirmPopoverPhase phase) : EventArgs { public ConfirmPopoverPhase Phase { get; } = phase; }
public sealed class ConfirmPopoverErrorEventArgs(Exception error) : EventArgs { public Exception Error { get; } = error; }

internal static class ConfirmPhrase
{
    public static bool Matches(string typed, string phrase) => string.Equals(typed.Trim(), phrase.Trim(), StringComparison.OrdinalIgnoreCase);
    /// <summary>The share of the phrase matched so far, from its start.</summary>
    public static double Share(string typed, string phrase)
    {
        string a = typed.TrimStart(), b = phrase.Trim();
        if (b.Length == 0) { return 1; }
        int at = 0;
        while (at < a.Length && at < b.Length && char.ToLowerInvariant(a[at]) == char.ToLowerInvariant(b[at])) { at++; }
        return (double)at / b.Length;
    }
}

/// <summary>
/// A destructive button that asks in place: the button itself grows, as one surface, into a small card with the consequence, Cancel and Confirm,
/// and an optional phrase to type first. Confirm is also the progress bar. Success folds back into the button's shape with a check; failure stays open with the error.
/// Independently implemented from the public Arc preview (see docs/references/components/ConfirmPopover.md).
/// </summary>
public class ConfirmPopover : Control
{
    public static readonly DependencyProperty LabelProperty = Prop(nameof(Label), "");
    public static readonly DependencyProperty TitleProperty = Prop(nameof(Title), "");
    public static readonly DependencyProperty DescriptionProperty = Prop<string?>(nameof(Description), null);
    public static readonly DependencyProperty IconProperty = Prop<string?>(nameof(Icon), null);
    public static readonly DependencyProperty ConfirmTextProperty = Prop<string?>(nameof(ConfirmText), null);
    public static readonly DependencyProperty ConfirmLabelProperty = Prop(nameof(ConfirmLabel), "Delete");
    public static readonly DependencyProperty CancelLabelProperty = Prop(nameof(CancelLabel), "Cancel");
    public static readonly DependencyProperty PendingLabelProperty = Prop(nameof(PendingLabel), "Deleting");
    public static readonly DependencyProperty SuccessLabelProperty = Prop(nameof(SuccessLabel), "Deleted");
    public static readonly DependencyProperty ErrorTitleProperty = Prop(nameof(ErrorTitle), "That didn't work");
    public static readonly DependencyProperty RetryLabelProperty = Prop(nameof(RetryLabel), "Try again");
    public static readonly DependencyProperty ToneProperty = Prop(nameof(Tone), ConfirmPopoverTone.Danger);
    public static readonly DependencyProperty AlignProperty = Prop(nameof(Align), ConfirmPopoverAlign.Start);
    public static readonly DependencyProperty SideProperty = Prop(nameof(Side), ConfirmPopoverSide.Auto);
    public static readonly DependencyProperty CardWidthProperty = Prop(nameof(CardWidth), 320d);
    public static readonly DependencyProperty ResultDurationProperty = Prop(nameof(ResultDuration), TimeSpan.FromMilliseconds(1600));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ConfirmPopover), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ConfirmPopover)d).OnIsOpen((bool)e.NewValue)));

    private static DependencyProperty Prop<T>(string name, T value) => DependencyProperty.Register(name, typeof(T), typeof(ConfirmPopover), new PropertyMetadata(value, (d, _) => ((ConfirmPopover)d).RefreshTrigger()));

    private const double Radius = 14, CardRadius = 22, ShakeSeconds = .42;
    private readonly OwnedMotion _motion = new();
    private readonly SpringDriver _driver;
    private readonly ControlSpring _w, _h, _r, _fillShare, _cancelShare;
    private readonly DispatcherTimer _hold = new();
    private readonly List<(SolidColorBrush Brush, string Role)> _inks = [];
    private ConfirmPopoverPhase _phase;
    private Decorator? _root;
    private ArcChromeButton? _trigger;
    private ArcDataGridIcon? _triggerIcon;
    private TextBlock? _triggerText;
    private AdornerLayerHost? _host;
    private Window? _window;
    private Border? _surface, _plate;
    private Grid? _surfaceContent, _restFace, _successFace;
    private DropShadowEffect? _shadow;
    private ThemedBrush? _surfaceFill, _surfaceEdge;
    private TranslateTransform? _shake, _panelMove;
    private StackPanel? _panel;
    private TextBox? _field;
    private Border? _fieldEdge;
    private Border? _errorBlock;
    private TextBlock? _errorText;
    private Grid? _actions;
    private ColumnDefinition? _cancelColumn, _gapColumn;
    private ArcChromeButton? _cancelButton, _confirmButton;
    private Border? _confirmTrack, _fillLayer, _sweep;
    private RectangleGeometry? _fillClip;
    private TextBlock? _labelText, _labelPercent, _fillText, _fillPercent;
    private TextBlock? _live;
    private CancellationTokenSource? _run;
    private double _anchorX, _shift;
    private bool _up, _progressReported, _fold, _focusBack, _removing;

    static ConfirmPopover() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ConfirmPopover), new FrameworkPropertyMetadata(typeof(ConfirmPopover)));

    public ConfirmPopover()
    {
        _driver = new SpringDriver(ApplyGeometry) { Settled = OnSettled };
        _w = _driver.Add(0, Tween.Morph);
        _h = _driver.Add(0, Tween.Morph);
        _r = _driver.Add(Radius, Tween.Morph);
        _fillShare = _driver.Add(0, Tween.Snappy);
        _cancelShare = _driver.Add(1, Tween.Smooth);
        _hold.Tick += (_, _) => { _hold.Stop(); FinishSuccess(); };
        Loaded += (_, _) => { RefreshTrigger(); if (IsOpen && _phase == ConfirmPopoverPhase.Idle) { Dispatcher.BeginInvoke(() => OpenCard(true), DispatcherPriority.Loaded); } };
        ThemeWatch.Add(this, () => { foreach (var (brush, role) in _inks) { brush.Color = Tween.Resolve(this, "ArcWpf.Brush." + role); } });
        Unloaded += (_, _) => { _run?.Cancel(); _hold.Stop(); _driver.Stop(); RemoveCard(); _phase = ConfirmPopoverPhase.Idle; };
    }

    /// <summary>The trigger label, such as "Delete project".</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>The question. It names the dialog.</summary>
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Icon name before the trigger label.</summary>
    public string? Icon { get => (string?)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>A phrase to type before Confirm unlocks. Matching ignores case and outer spaces.</summary>
    public string? ConfirmText { get => (string?)GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }
    public string ConfirmLabel { get => (string)GetValue(ConfirmLabelProperty); set => SetValue(ConfirmLabelProperty, value); }
    public string CancelLabel { get => (string)GetValue(CancelLabelProperty); set => SetValue(CancelLabelProperty, value); }
    public string PendingLabel { get => (string)GetValue(PendingLabelProperty); set => SetValue(PendingLabelProperty, value); }
    public string SuccessLabel { get => (string)GetValue(SuccessLabelProperty); set => SetValue(SuccessLabelProperty, value); }
    public string ErrorTitle { get => (string)GetValue(ErrorTitleProperty); set => SetValue(ErrorTitleProperty, value); }
    public string RetryLabel { get => (string)GetValue(RetryLabelProperty); set => SetValue(RetryLabelProperty, value); }
    public ConfirmPopoverTone Tone { get => (ConfirmPopoverTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    /// <summary>Which edge of the button the card keeps.</summary>
    public ConfirmPopoverAlign Align { get => (ConfirmPopoverAlign)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }
    public ConfirmPopoverSide Side { get => (ConfirmPopoverSide)GetValue(SideProperty); set => SetValue(SideProperty, value); }
    public double CardWidth { get => (double)GetValue(CardWidthProperty); set => SetValue(CardWidthProperty, value); }
    public TimeSpan ResultDuration { get => (TimeSpan)GetValue(ResultDurationProperty); set => SetValue(ResultDurationProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public ConfirmPopoverPhase Phase => _phase;
    /// <summary>Runs the action. A thrown exception shows the failure, with its message under the error title.</summary>
    public Func<ConfirmPopoverContext, Task>? ConfirmAction { get; set; }

    public event EventHandler? Cancelled;
    public event EventHandler? Done;
    public event EventHandler<ConfirmPopoverErrorEventArgs>? Failed;
    public event EventHandler<ConfirmPopoverPhaseEventArgs>? PhaseChanged;

    internal ArcChromeButton? TriggerForTest => _trigger;
    internal TextBox? FieldForTest => _field;
    internal Border? SurfaceForTest => _surface;
    internal ArcChromeButton? ConfirmButtonForTest => _confirmButton;
    internal ArcChromeButton? CancelButtonForTest => _cancelButton;
    internal double FillTargetForTest => _fillShare.Target;
    internal double CardTargetWidthForTest => _w.Target;
    internal double CardTargetHeightForTest => _h.Target;
    internal double LeftForTest => _surface is null ? 0 : Canvas.GetLeft(_surface);
    internal double TopForTest => _surface is null ? 0 : Canvas.GetTop(_surface);
    internal bool ErrorVisibleForTest => _errorBlock is { Visibility: Visibility.Visible };
    internal string ConfirmTextForTest => _labelText?.Text ?? string.Empty;
    internal string PercentForTest => _labelPercent?.Text ?? string.Empty;
    internal bool HasCardForTest => _host is not null;
    internal void FinishMotionForTest() { _driver.Finish(); OnSettled(); }
    internal void PressOutsideForTest() => Dismiss();
    internal void SubmitForTest() => Submit();
    internal void OpenForTest() => OpenCard(true);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _trigger = new ArcChromeButton(Radius, 1) { Height = 40, Padding = new Thickness(16, 0, 16, 0), FontSize = 14, FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Left };
        _trigger.Roles = button => (button.IsMouseOver ? TrackRole : TintRole, EdgeRole, InkRole);
        _trigger.Click += (_, _) => { if (_phase == ConfirmPopoverPhase.Idle) { SetCurrentValue(IsOpenProperty, true); } };
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        _triggerIcon = ArcText.Icon(Icon ?? "x", 16, _trigger.Ink);
        _triggerIcon.Margin = new Thickness(0, 0, 8, 0);
        _triggerText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(_triggerIcon);
        content.Children.Add(_triggerText);
        _trigger.Content = content;
        _live = new TextBlock { Width = 1, Height = 1, Opacity = 0, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetLiveSetting(_live, AutomationLiveSetting.Polite);
        _root.Child = new Grid { Children = { _live, _trigger } };
        RefreshTrigger();
    }

    private string TintRole => Tone == ConfirmPopoverTone.Danger ? "ConfirmTint" : "ConfirmNeutralTint";
    private string TrackRole => Tone == ConfirmPopoverTone.Danger ? "ConfirmTrack" : "ConfirmNeutralTrack";
    private string EdgeRole => Tone == ConfirmPopoverTone.Danger ? "ConfirmEdge" : "Border";
    private string InkRole => Tone == ConfirmPopoverTone.Danger ? "ArcDanger" : "Foreground";
    private string FillRole => Tone == ConfirmPopoverTone.Danger ? "ArcDanger" : "Foreground";
    private string OnFillRole => Tone == ConfirmPopoverTone.Danger ? "ArcDangerOnFill" : "Background";
    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private bool HasPhrase => !string.IsNullOrEmpty(ConfirmText);
    private bool Unlocked => !HasPhrase || ConfirmPhrase.Matches(_field?.Text ?? string.Empty, ConfirmText!);

    private void RefreshTrigger()
    {
        if (_trigger is null || _triggerText is null || _triggerIcon is null) { return; }
        _triggerText.Text = Label;
        _triggerIcon.Icon = Icon ?? "x";
        _triggerIcon.Visibility = string.IsNullOrEmpty(Icon) ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(_trigger, Label);
        _trigger.Refresh(false);
    }

    private void SetPhase(ConfirmPopoverPhase phase)
    {
        if (_phase == phase) { return; }
        _phase = phase;
        PhaseChanged?.Invoke(this, new ConfirmPopoverPhaseEventArgs(phase));
    }

    private void OnIsOpen(bool open)
    {
        if (open && _phase == ConfirmPopoverPhase.Idle) { if (IsLoaded) { OpenCard(true); } }
        else if (!open && _phase is ConfirmPopoverPhase.Confirming or ConfirmPopoverPhase.Error) { FoldBack(true); }
    }

    private void OpenCard(bool focus)
    {
        if (_trigger is null || !IsLoaded || _host is not null && !_removing) { return; }
        var layer = OutermostLayer(_trigger);
        if (layer is null) { return; }
        RemoveCard();
        _window = Window.GetWindow(this);
        BuildCard();
        _host = new AdornerLayerHost(_trigger, _surface!.Parent as UIElement ?? _surface);
        layer.Add(_host);
        _window?.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnWindowPress), true);
        // The card's size: never wider than the window less 32, shifted to stay 16 from either edge.
        double available = _window is { Content: FrameworkElement body } ? body.ActualWidth : double.PositiveInfinity;
        double width = Math.Min(CardWidth, available - 32);
        _panel!.Width = width;
        _panel.Measure(new Size(width, double.PositiveInfinity));
        double height = _panel.DesiredSize.Height;
        double tw = _trigger.ActualWidth, th = _trigger.ActualHeight;
        var at = _window?.Content is UIElement content ? _trigger.TranslatePoint(new Point(0, 0), content) : new Point();
        double windowHeight = _window?.Content is FrameworkElement frame ? frame.ActualHeight : double.PositiveInfinity;
        _up = Side == ConfirmPopoverSide.Top || Side == ConfirmPopoverSide.Auto && at.Y + height > windowHeight - 16 && at.Y + th - height >= 16;
        double left = Align switch { ConfirmPopoverAlign.End => tw - width, ConfirmPopoverAlign.Center => (tw - width) / 2, _ => 0 };
        _anchorX = left;
        double screenLeft = at.X + left;
        _shift = double.IsInfinity(available) ? 0 : screenLeft < 16 ? 16 - screenLeft : screenLeft + width > available - 16 ? available - 16 - width - screenLeft : 0;
        // At rest the surface is drawn exactly over the button.
        _w.Snap(tw); _h.Snap(th); _r.Snap(Radius);
        _fillShare.Snap(HasPhrase ? 0 : 1);
        _cancelShare.Snap(1);
        _trigger.Opacity = 0;
        _fold = false;
        _focusBack = true;
        SetPhase(ConfirmPopoverPhase.Confirming);
        if (!IsOpen) { SetCurrentValue(IsOpenProperty, true); }
        bool full = Full;
        ApplyGeometry();
        // Tint, border and floating shadow crossfade from the button's look to the card's.
        _surfaceFill!.To(TintRole, false); _surfaceEdge!.To(EdgeRole, false);
        _surfaceFill.To("SurfaceRaised", true, Tween.Standard); _surfaceEdge.To("Border", true, Tween.Standard);
        if (full)
        {
            _driver.Animate(_w, width, Tween.Morph); _driver.Animate(_h, height, Tween.Morph); _driver.Animate(_r, CardRadius, Tween.Morph);
            _shadow!.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0, FloatingOpacity, TimeSpan.FromSeconds(Tween.Standard)));
            // The button face fades out in 100ms as the card content fades up 6px from a 4px blur, 60ms later.
            Tween.To(_restFace!, OpacityProperty, 0, .1, ArcEasing.Standard);
            _panel.Opacity = 0;
            Tween.Present(_panel, true, 6, 4 * 2, Tween.Standard, ArcEasing.Enter, delay: .06);
        }
        else
        {
            _w.Snap(width); _h.Snap(height); _r.Snap(CardRadius);
            _restFace!.Opacity = 0;
            _shadow!.Opacity = FloatingOpacity;
            ApplyGeometry();
        }
        if (focus) { Dispatcher.BeginInvoke(FocusFirst, DispatcherPriority.Input); }
    }

    private double FloatingOpacity => Tween.Resolve(this, "ArcWpf.Brush.Surface").R < 128 ? .34 : .105;

    private void FocusFirst()
    {
        if (_field is not null && HasPhrase) { _field.Focus(); _field.SelectAll(); }
        else { _cancelButton?.Focus(); }
    }

    private void BuildCard()
    {
        _surfaceFill = new ThemedBrush(_trigger!, _motion);
        _surfaceEdge = new ThemedBrush(_trigger!, _motion);
        _shake = new TranslateTransform();
        _shadow = new DropShadowEffect { Color = Colors.Black, BlurRadius = 48, ShadowDepth = 20, Direction = 270, Opacity = 0 };
        _surfaceContent = new Grid { ClipToBounds = true };
        // The shadow sits on its own plate: an effect on the surface itself would rasterize the text inside it.
        _plate = new Border { Background = _surfaceFill.Brush, Effect = _shadow, RenderTransform = _shake, IsHitTestVisible = false };
        _surface = new Border { BorderThickness = new Thickness(1), Background = _surfaceFill.Brush, BorderBrush = _surfaceEdge.Brush, RenderTransform = _shake, Child = _surfaceContent };
        TextElement.SetFontFamily(_surface, FontFamily);
        TextElement.SetFontSize(_surface, 14);
        AutomationProperties.SetName(_surface, Title);
        AutomationProperties.SetHelpText(_surface, Description ?? string.Empty);
        KeyboardNavigation.SetTabNavigation(_surface, KeyboardNavigationMode.Cycle);
        _surface.PreviewKeyDown += OnCardKey;

        var hAlign = Align switch { ConfirmPopoverAlign.End => HorizontalAlignment.Right, ConfirmPopoverAlign.Center => HorizontalAlignment.Center, _ => HorizontalAlignment.Left };
        _restFace = Face(Icon, Label, InkRole, hAlign);
        _successFace = Face("check", SuccessLabel, Tone == ConfirmPopoverTone.Danger ? "ArcSuccess" : "Foreground", hAlign);
        _successFace.Opacity = 0;
        _panel = BuildPanel();
        _panel.HorizontalAlignment = hAlign;
        _panel.VerticalAlignment = _up ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _surfaceContent.Children.Add(_panel);
        _surfaceContent.Children.Add(_restFace);
        _surfaceContent.Children.Add(_successFace);
        _surface.IsHitTestVisible = true;
        var canvas = new Canvas { Children = { _plate, _surface } };
        canvas.Width = 0; canvas.Height = 0;
    }

    private Grid Face(string? icon, string label, string role, HorizontalAlignment align)
    {
        var ink = Res(role);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(icon)) { var glyph = ArcText.Icon(icon, 16, ink); glyph.Margin = new Thickness(0, 0, 8, 0); row.Children.Add(glyph); }
        row.Children.Add(new TextBlock { Text = label, Foreground = ink, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
        return new Grid { Width = _trigger!.ActualWidth - 2, Height = _trigger.ActualHeight - 2, HorizontalAlignment = align, VerticalAlignment = _up ? VerticalAlignment.Bottom : VerticalAlignment.Top, Children = { row }, IsHitTestVisible = false };
    }

    private StackPanel BuildPanel()
    {
        var panel = new StackPanel { Margin = new Thickness(0) };
        var pad = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(pad);
        pad.Children.Add(new TextBlock { Text = Title, FontSize = 16, FontWeight = FontWeights.Medium, LineHeight = 17.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap, Foreground = Res("Foreground") });
        if (!string.IsNullOrEmpty(Description))
        {
            pad.Children.Add(new TextBlock { Text = Description, Margin = new Thickness(0, 4, 0, 0), LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap, Foreground = Res("TextSecondary") });
        }
        _field = null; _fieldEdge = null;
        if (HasPhrase)
        {
            var label = new TextBlock { Margin = new Thickness(0, 14, 0, 5.9), FontSize = 12, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Foreground = Res("TextSecondary") };
            label.Inlines.Add(new Run("Type "));
            var phrase = new Run(ConfirmText) { FontSize = 11.4, FontWeight = FontWeights.Medium, Foreground = Res("Foreground") };
            phrase.SetResourceReference(TextElement.FontFamilyProperty, "ArcWpf.Font.Mono");
            label.Inlines.Add(phrase);
            label.Inlines.Add(new Run(" to confirm"));
            pad.Children.Add(label);
            _field = ThreadUi.PlainTextBox();
            _field.FontSize = 14;
            _field.SetResourceReference(TextElement.FontFamilyProperty, "ArcWpf.Font.Mono");
            _field.VerticalContentAlignment = VerticalAlignment.Center;
            _field.Padding = new Thickness(12, 0, 12, 0);
            AutomationProperties.SetName(_field, label.Text.Length > 0 ? $"Type {ConfirmText} to confirm" : ConfirmText!);
            var hint = new TextBlock { Text = ConfirmText, FontSize = 14, Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false, Foreground = Res("TextMuted") };
            hint.SetResourceReference(TextElement.FontFamilyProperty, "ArcWpf.Font.Mono");
            _fieldEdge = new Border { Height = 40, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = new Grid { Children = { hint, _field } } };
            _fieldEdge.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
            _field.TextChanged += (_, _) => { hint.Visibility = _field.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible; OnTyped(); };
            _field.GotKeyboardFocus += (_, _) => RefreshFieldEdge();
            _field.LostKeyboardFocus += (_, _) => RefreshFieldEdge();
            RefreshFieldEdge();
            pad.Children.Add(_fieldEdge);
        }
        // The failure opens above the actions.
        _errorText = new TextBlock { LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap, Foreground = Res("TextSecondary") };
        var errorCopy = new StackPanel { Children = { new TextBlock { Text = ErrorTitle, FontWeight = FontWeights.Medium, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap, Foreground = Res(InkRole) }, _errorText } };
        var errorIcon = ArcText.Icon("circle-alert", 16, Res(InkRole));
        errorIcon.VerticalAlignment = VerticalAlignment.Top; errorIcon.Margin = new Thickness(0, 1, 8, 0);
        DockPanel.SetDock(errorIcon, Dock.Left);
        _errorBlock = new Border { Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(12), Visibility = Visibility.Collapsed, Child = new DockPanel { Children = { errorIcon, errorCopy } } };
        _errorBlock.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.ConfirmErrorFill");
        AutomationProperties.SetLiveSetting(_errorBlock, AutomationLiveSetting.Assertive);
        pad.Children.Add(_errorBlock);
        pad.Children.Add(BuildActions());
        panel.SizeChanged += (_, _) => OnPanelResized();
        return panel;
    }

    private Brush Res(string role)
    {
        var brush = new SolidColorBrush(Tween.Resolve(this, "ArcWpf.Brush." + role));
        _inks.Add((brush, role));
        return brush;
    }

    private UIElement BuildActions()
    {
        _cancelColumn = new ColumnDefinition();
        _gapColumn = new ColumnDefinition { Width = new GridLength(8) };
        _actions = new Grid { Height = 40, Margin = new Thickness(0, 14, 0, 0) };
        _actions.ColumnDefinitions.Add(_cancelColumn);
        _actions.ColumnDefinitions.Add(_gapColumn);
        _actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _cancelButton = new ArcChromeButton(12) { FontWeight = FontWeights.Medium, FontSize = 14, Content = new TextBlock { Text = CancelLabel } };
        _cancelButton.Roles = button => (button.IsMouseOver ? "Border" : "SurfaceMuted", null, "Foreground");
        _cancelButton.Click += (_, _) => Dismiss();
        _cancelButton.MinWidth = 0;
        _confirmButton = new ArcChromeButton(12) { FontWeight = FontWeights.Medium, FontSize = 14, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        _confirmButton.Click += (_, _) => Submit();
        _confirmButton.Roles = _ => (null, null, InkRole);
        (_labelText, _labelPercent, var baseRow) = ConfirmRow(Res(InkRole));
        (_fillText, _fillPercent, var fillRow) = ConfirmRow(Res(OnFillRole));
        _fillClip = new RectangleGeometry();
        _fillLayer = new Border { Clip = _fillClip, Child = new Grid { Children = { fillRow } } };
        _fillLayer.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush." + FillRole);
        _sweep = new Border { Width = 96, HorizontalAlignment = HorizontalAlignment.Left, Opacity = 0, IsHitTestVisible = false, RenderTransform = new TranslateTransform() };
        _sweep.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush." + FillRole);
        _confirmTrack = new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, Child = new Grid { Children = { baseRow, _sweep, _fillLayer } } };
        _confirmTrack.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush." + TrackRole);
        _confirmTrack.SizeChanged += (_, e) => { _confirmTrack.Clip = new RectangleGeometry(new Rect(e.NewSize), 12, 12); ApplyGeometry(); };
        _confirmButton.Content = _confirmTrack;
        Grid.SetColumn(_cancelButton, 0);
        Grid.SetColumn(_confirmButton, 2);
        _actions.Children.Add(_cancelButton);
        _actions.Children.Add(_confirmButton);
        SetConfirmLabel(ConfirmLabel);
        AutomationProperties.SetName(_confirmButton, ConfirmLabel);
        return _actions;
    }

    private static (TextBlock Label, TextBlock Percent, UIElement Row) ConfirmRow(Brush ink)
    {
        var label = new TextBlock { Foreground = ink, VerticalAlignment = VerticalAlignment.Center };
        var percent = new TextBlock { Foreground = ink, Opacity = .82, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        return (label, percent, new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Children = { label, percent } });
    }

    private void SetConfirmLabel(string text, string? percent = null)
    {
        foreach (var block in new[] { _labelText, _fillText }) { if (block is not null) { block.Text = text; } }
        foreach (var block in new[] { _labelPercent, _fillPercent })
        {
            if (block is null) { continue; }
            block.Text = percent ?? string.Empty;
            block.Visibility = percent is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void RefreshFieldEdge()
    {
        if (_fieldEdge is null || _field is null) { return; }
        bool wrong = _field.Text.Trim().Length > 0 && !Unlocked;
        _fieldEdge.SetResourceReference(Border.BorderBrushProperty, wrong ? "ArcWpf.Brush.ConfirmEdge" : _field.IsKeyboardFocused ? "ArcWpf.Brush.BorderStrong" : "ArcWpf.Brush.Border");
        AutomationProperties.SetHelpText(_field, wrong ? "Does not match yet" : string.Empty);
    }

    private void OnTyped()
    {
        RefreshFieldEdge();
        if (_phase is not (ConfirmPopoverPhase.Confirming or ConfirmPopoverPhase.Error) || _field is null) { return; }
        double share = ConfirmPhrase.Share(_field.Text, ConfirmText ?? string.Empty);
        if (Full) { _driver.Animate(_fillShare, share, Tween.Snappy); } else { _fillShare.Snap(share); ApplyGeometry(); }
    }

    private void OnPanelResized()
    {
        if (_panel is null || _surface is null || _phase is ConfirmPopoverPhase.Idle or ConfirmPopoverPhase.Success || _fold) { return; }
        double next = _panel.ActualHeight;
        if (Math.Abs(next - _h.Target) < .01) { return; }
        if (Full) { _driver.Animate(_h, next, Tween.Morph); } else { _h.Snap(next); ApplyGeometry(); }
    }

    private void OnCardKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; if (_phase is ConfirmPopoverPhase.Confirming or ConfirmPopoverPhase.Error) { Dismiss(); } return; }
        if (e.Key == Key.Enter && ReferenceEquals(e.OriginalSource, _field)) { e.Handled = true; Submit(); }
    }

    private void OnWindowPress(object sender, MouseButtonEventArgs e)
    {
        if (_phase is not (ConfirmPopoverPhase.Confirming or ConfirmPopoverPhase.Error) || Outside.IsWithin(e.OriginalSource, _surface)) { return; }
        _focusBack = false;
        Dismiss();
    }

    private void Dismiss()
    {
        if (_phase is not (ConfirmPopoverPhase.Confirming or ConfirmPopoverPhase.Error)) { return; }
        Cancelled?.Invoke(this, EventArgs.Empty);
        FoldBack(true);
    }

    private void FoldBack(bool toRest)
    {
        if (_surface is null || _trigger is null) { SetPhase(ConfirmPopoverPhase.Idle); return; }
        _fold = true;
        _surface.IsHitTestVisible = false;
        if (toRest) { SetPhase(ConfirmPopoverPhase.Idle); }
        if (IsOpen) { SetCurrentValue(IsOpenProperty, false); }
        double tw = _trigger.ActualWidth, th = _trigger.ActualHeight;
        if (Full)
        {
            _driver.Animate(_w, tw, Tween.Smooth); _driver.Animate(_h, th, Tween.Smooth); _driver.Animate(_r, Radius, Tween.Smooth);
            Tween.To(_panel!, OpacityProperty, 0, .1, ArcEasing.Standard);
            _surfaceFill!.To(TintRole, true, Tween.Standard); _surfaceEdge!.To(EdgeRole, true, Tween.Standard);
            _shadow!.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(Tween.Standard)));
            Tween.To(_restFace!, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter, delay: .06);
        }
        else { OnSettled(true); }
    }

    // Submitting: a locked or wrong phrase shakes the card; otherwise Confirm runs the action.
    private void Submit()
    {
        if (_phase is not (ConfirmPopoverPhase.Confirming or ConfirmPopoverPhase.Error)) { return; }
        if (!Unlocked) { Shake(); _field?.Focus(); return; }
        _ = RunAction();
    }

    private void Shake()
    {
        if (_shake is null || !Full) { return; }
        var keys = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(ShakeSeconds) };
        double[] offsets = [-7, 6, -4, 3, -1.5, 0];
        for (int i = 0; i < offsets.Length; i++) { keys.KeyFrames.Add(new EasingDoubleKeyFrame(offsets[i], KeyTime.FromPercent((i + 1d) / offsets.Length), new SineEase { EasingMode = EasingMode.EaseInOut })); }
        _shake.BeginAnimation(TranslateTransform.XProperty, keys);
    }

    private async Task RunAction()
    {
        if (_cancelButton is { IsKeyboardFocused: true }) { _confirmButton?.Focus(); }
        SetPhase(ConfirmPopoverPhase.Working);
        _progressReported = false;
        _errorBlock!.Visibility = Visibility.Collapsed;
        if (_field is not null) { _field.IsReadOnly = true; }
        AutomationProperties.SetItemStatus(_surface!, "Busy");
        // Confirming folds Cancel away while Confirm widens to the full row; the fill drops to zero, then follows progress.
        if (Full) { _driver.Animate(_cancelShare, 0, Tween.Smooth); } else { _cancelShare.Snap(0); }
        _fillShare.Snap(0);
        SetConfirmLabel(PendingLabel);
        ArcEffects.Announce(_live, PendingLabel);
        StartSweep();
        ApplyGeometry();
        _run?.Dispose();
        _run = new CancellationTokenSource();
        var context = new ConfirmPopoverContext(ReportProgress, _run.Token);
        try
        {
            if (ConfirmAction is { } action) { await action(context); }
        }
        catch (OperationCanceledException) when (_run.IsCancellationRequested) { return; }
        catch (Exception error)
        {
            if (_phase == ConfirmPopoverPhase.Working) { ShowError(error); }
            return;
        }
        if (_phase == ConfirmPopoverPhase.Working) { await ShowSuccess(); }
    }

    private void ReportProgress(double value)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => ReportProgress(value)); return; }
        if (_phase != ConfirmPopoverPhase.Working) { return; }
        double share = Math.Clamp(value, 0, 1);
        _progressReported = true;
        StopSweep();
        string percent = $"{Math.Round(share * 100)}%";
        SetConfirmLabel(PendingLabel, percent);
        ArcEffects.Announce(_live, $"{PendingLabel} {percent}");
        if (Full) { _driver.Animate(_fillShare, share, Tween.Snappy); } else { _fillShare.Snap(share); ApplyGeometry(); }
    }

    private void StartSweep()
    {
        if (_sweep is null) { return; }
        if (!Full) { _sweep.Width = double.NaN; _sweep.HorizontalAlignment = HorizontalAlignment.Stretch; _sweep.Opacity = .2; return; }
        _sweep.Opacity = .28;
        ((TranslateTransform)_sweep.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-96, _actions!.ActualWidth, TimeSpan.FromSeconds(1.2)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ArcEasing.InOut });
    }

    private void StopSweep()
    {
        if (_sweep is null) { return; }
        ((TranslateTransform)_sweep.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);
        _sweep.Opacity = 0;
    }

    private void ShowError(Exception error)
    {
        StopSweep();
        SetPhase(ConfirmPopoverPhase.Error);
        if (_field is not null) { _field.IsReadOnly = false; }
        AutomationProperties.SetItemStatus(_surface!, string.Empty);
        if (Full) { _driver.Animate(_cancelShare, 1, Tween.Smooth); _driver.Animate(_fillShare, 1, Tween.Snappy); } else { _cancelShare.Snap(1); _fillShare.Snap(1); }
        SetConfirmLabel(RetryLabel);
        AutomationProperties.SetName(_confirmButton!, RetryLabel);
        _errorText!.Text = error.Message;
        _errorText.Visibility = string.IsNullOrEmpty(error.Message) ? Visibility.Collapsed : Visibility.Visible;
        _errorBlock!.Visibility = Visibility.Visible;
        ArcEffects.Announce(_live, $"{ErrorTitle}. {error.Message}");
        Shake();
        ApplyGeometry();
        _confirmButton!.Focus();
        Failed?.Invoke(this, new ConfirmPopoverErrorEventArgs(error));
    }

    private async Task ShowSuccess()
    {
        StopSweep();
        if (Full) { _driver.Animate(_fillShare, 1, Tween.Snappy); await Task.Delay(TimeSpan.FromSeconds(Tween.Snappy)); } else { _fillShare.Snap(1); ApplyGeometry(); }
        if (_phase != ConfirmPopoverPhase.Working || _surface is null) { return; }
        SetPhase(ConfirmPopoverPhase.Success);
        ArcEffects.Announce(_live, SuccessLabel);
        _fold = true;
        if (IsOpen) { SetCurrentValue(IsOpenProperty, false); }
        double tw = _trigger!.ActualWidth, th = _trigger.ActualHeight;
        string tint = Tone == ConfirmPopoverTone.Danger ? "ConfirmSuccessTint" : "ConfirmNeutralTint", edge = Tone == ConfirmPopoverTone.Danger ? "ConfirmSuccessEdge" : "Border";
        var check = (_successFace!.Children[0] as Panel)?.Children.OfType<ArcDataGridIcon>().FirstOrDefault();
        if (Full)
        {
            _driver.Animate(_w, tw, Tween.Smooth); _driver.Animate(_h, th, Tween.Smooth); _driver.Animate(_r, Radius, Tween.Smooth);
            Tween.To(_panel!, OpacityProperty, 0, .1, ArcEasing.Standard);
            _surfaceFill!.To(tint, true, Tween.Standard); _surfaceEdge!.To(edge, true, Tween.Standard);
            _shadow!.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(Tween.Standard)));
            Tween.To(_successFace, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter, delay: .08);
            if (check is not null)
            {
                var turn = new RotateTransform(-90, 8, 8);
                check.RenderTransform = turn;
                Tween.Spring(turn, RotateTransform.AngleProperty, 0, Tween.Snappy, delay: .08);
            }
        }
        else
        {
            _w.Snap(tw); _h.Snap(th); _r.Snap(Radius);
            _panel!.Opacity = 0; _successFace.Opacity = 1;
            _surfaceFill!.To(tint, false); _surfaceEdge!.To(edge, false);
            _shadow!.Opacity = 0;
            ApplyGeometry();
        }
        _hold.Interval = ResultDuration + TimeSpan.FromSeconds(Full ? Tween.Smooth : 0);
        _hold.Start();
    }

    // The check crossfades back to the button's own face, then the real button takes over.
    private void FinishSuccess()
    {
        if (_surface is null) { return; }
        bool full = Full;
        _surfaceFill!.To(TintRole, full, Tween.Standard); _surfaceEdge!.To(EdgeRole, full, Tween.Standard);
        if (full)
        {
            Tween.To(_successFace!, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard);
            Tween.To(_restFace!, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter, done: CompleteSuccess);
        }
        else { CompleteSuccess(); }
    }

    private void CompleteSuccess()
    {
        if (_phase != ConfirmPopoverPhase.Success) { return; }
        bool hadFocus = _surface?.IsKeyboardFocusWithin == true;
        RemoveCard();
        if (hadFocus) { _trigger?.Focus(); }
        SetPhase(ConfirmPopoverPhase.Idle);
        Done?.Invoke(this, EventArgs.Empty);
    }

    // Every ScrollViewer has its own layer that would clip the card; the outermost one is bounded only by the window, as Arc's card is.
    private static AdornerLayer? OutermostLayer(Visual start)
    {
        AdornerLayer? found = null;
        for (var at = AdornerLayer.GetAdornerLayer(start); at is not null; at = VisualTreeHelper.GetParent(at) is Visual parent ? AdornerLayer.GetAdornerLayer(parent) : null) { found = at; }
        return found;
    }

    private void OnSettled() => OnSettled(false);

    private void OnSettled(bool immediate)
    {
        if (!_fold || _phase != ConfirmPopoverPhase.Idle || _surface is null) { return; }
        _ = immediate;
        // The trigger takes focus back only after the card has folded into it, so focus is never lost to the page.
        bool focus = _focusBack && _surface.IsKeyboardFocusWithin;
        RemoveCard();
        if (focus) { _trigger?.Focus(); }
    }

    private void RemoveCard()
    {
        _removing = false;
        _window?.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnWindowPress));
        _window = null;
        if (_host is not null) { (VisualTreeHelper.GetParent(_host) as AdornerLayer)?.Remove(_host); _host.Release(); _host = null; }
        _inks.Clear();
        _surface = null;
        _plate = null;
        _panel = null; _field = null; _fieldEdge = null;
        _fold = false;
        _driver.Stop();
        if (_trigger is not null) { _trigger.Opacity = 1; }
    }

    private void ApplyGeometry()
    {
        if (_surface is null || _trigger is null) { return; }
        double tw = _trigger.ActualWidth, th = _trigger.ActualHeight;
        double w = Math.Max(0, _w.Position), h = Math.Max(0, _h.Position), r = Math.Max(0, _r.Position);
        _surface.Width = w; _surface.Height = h;
        _surface.CornerRadius = new CornerRadius(r);
        _plate!.Width = w; _plate.Height = h;
        _plate.CornerRadius = new CornerRadius(r);
        double target = Math.Max(1, _panel?.Width ?? CardWidth) - tw;
        double t = target <= 0 ? 1 : Math.Clamp((w - tw) / target, 0, 1.2);
        double left = Align switch { ConfirmPopoverAlign.End => tw - w, ConfirmPopoverAlign.Center => (tw - w) / 2, _ => 0 };
        Canvas.SetLeft(_surface, left + _shift * t);
        Canvas.SetTop(_surface, _up ? th - h : 0);
        Canvas.SetLeft(_plate, left + _shift * t);
        Canvas.SetTop(_plate, _up ? th - h : 0);
        if (_surfaceContent is not null) { _surfaceContent.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, w - 2), Math.Max(0, h - 2)), Math.Max(0, r - 1), Math.Max(0, r - 1)); }
        if (_actions is { ActualWidth: > 0 } && _cancelColumn is not null && _gapColumn is not null && _cancelButton is not null)
        {
            double share = Math.Clamp(_cancelShare.Position, 0, 1), rowWidth = _actions.ActualWidth;
            double half = (rowWidth - 8) / 2;
            _cancelColumn.Width = new GridLength(half * share);
            _gapColumn.Width = new GridLength(8 * share);
            _cancelButton.Opacity = share;
            _cancelButton.IsEnabled = _phase != ConfirmPopoverPhase.Working;
        }
        if (_fillClip is not null && _confirmTrack is { ActualWidth: > 0 }) { _fillClip.Rect = new Rect(0, 0, _confirmTrack.ActualWidth * Math.Clamp(_fillShare.Position, 0, 1), _confirmTrack.ActualHeight); }
    }

    // A non-modal layer over the page: the card floats without reflowing anything around it.
    private sealed class AdornerLayerHost : System.Windows.Documents.Adorner
    {
        private UIElement? _child;
        public AdornerLayerHost(UIElement adorned, UIElement child) : base(adorned) { _child = child; AddVisualChild(child); IsClipEnabled = false; }
        protected override int VisualChildrenCount => _child is null ? 0 : 1;
        protected override Visual GetVisualChild(int index) => _child!;
        protected override Size MeasureOverride(Size constraint) { _child?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); return AdornedElement.RenderSize; }
        protected override Size ArrangeOverride(Size finalSize) { _child?.Arrange(new Rect(0, 0, 0, 0)); return finalSize; }
        public void Release() { if (_child is not null) { RemoveVisualChild(_child); _child = null; } }
    }
}
