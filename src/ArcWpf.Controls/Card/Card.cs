using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Data;

namespace ArcWpf.Controls;

public class Card : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Card), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(Card), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(nameof(Footer), typeof(object), typeof(Card));
    public static readonly DependencyProperty MediaProperty = DependencyProperty.Register(nameof(Media), typeof(object), typeof(Card), new PropertyMetadata(null, OnVisualContentChanged));
    public static readonly DependencyProperty MediaTemplateProperty = DependencyProperty.Register(nameof(MediaTemplate), typeof(DataTemplate), typeof(Card), new PropertyMetadata(null, OnVisualContentChanged));
    public static readonly DependencyProperty AvatarProperty = DependencyProperty.Register(nameof(Avatar), typeof(object), typeof(Card), new PropertyMetadata(null, OnVisualContentChanged));
    public static readonly DependencyProperty MetaProperty = DependencyProperty.Register(nameof(Meta), typeof(string), typeof(Card), new PropertyMetadata(string.Empty, OnVisualContentChanged));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(string), typeof(Card), new PropertyMetadata(string.Empty, OnVisualContentChanged));
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(nameof(Action), typeof(object), typeof(Card), new PropertyMetadata(null, OnVisualContentChanged));
    public static readonly DependencyProperty DetailsProperty = DependencyProperty.Register(nameof(Details), typeof(object), typeof(Card), new PropertyMetadata(null, OnVisualContentChanged));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Card), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOpenChanged));
    public static readonly DependencyProperty DefaultOpenProperty = DependencyProperty.Register(nameof(DefaultOpen), typeof(bool), typeof(Card), new PropertyMetadata(false));
    private static readonly DependencyPropertyKey HasDetailsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasDetails), typeof(bool), typeof(Card), new PropertyMetadata(false));
    public static readonly DependencyProperty HasDetailsProperty = HasDetailsPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey HasBylinePropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasByline), typeof(bool), typeof(Card), new PropertyMetadata(false));
    public static readonly DependencyProperty HasBylineProperty = HasBylinePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsPointerHoverPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPointerHover), typeof(bool), typeof(Card), new PropertyMetadata(false));
    public static readonly DependencyProperty IsPointerHoverProperty = IsPointerHoverPropertyKey.DependencyProperty;
    private ButtonBase? _titleButton;
    private Border? _surface;
    private FrameworkElement? _mediaHost;
    private FrameworkElement? _actionHost;
    private TextBlock? _statusText;
    private TranslateTransform? _lift;
    private TranslateTransform? _statusMove;
    private ScaleTransform? _zoom;
    private readonly OwnedMotion _motion = new();
    private Dialog? _quickLook;
    private bool _initialized;
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    public object? Media { get => GetValue(MediaProperty); set => SetValue(MediaProperty, value); }
    public DataTemplate? MediaTemplate { get => (DataTemplate?)GetValue(MediaTemplateProperty); set => SetValue(MediaTemplateProperty, value); }
    public object? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
    public string Meta { get => (string)GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public string Status { get => (string)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }
    public object? Details { get => GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public bool DefaultOpen { get => (bool)GetValue(DefaultOpenProperty); set => SetValue(DefaultOpenProperty, value); }
    public bool HasDetails => (bool)GetValue(HasDetailsProperty);
    public bool HasByline => (bool)GetValue(HasBylineProperty);
    public bool IsPointerHover => (bool)GetValue(IsPointerHoverProperty);
    static Card() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(typeof(Card))); }
    public Card()
    {
        Loaded += CardLoaded;
        Unloaded += (_, _) => { _motion.Stop(); _quickLook?.Close(); };
    }
    private void CardLoaded(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
        {
            _initialized = true;
            if (DefaultOpen && ReadLocalValue(IsOpenProperty) == DependencyProperty.UnsetValue)
                SetCurrentValue(IsOpenProperty, true);
        }
        if (IsOpen) OpenQuickLook();
    }
    public override void OnApplyTemplate()
    {
        ReleaseParts();
        base.OnApplyTemplate();
        _titleButton = GetTemplateChild("PART_TitleButton") as ButtonBase;
        _surface = GetTemplateChild("PART_Surface") as Border;
        _mediaHost = GetTemplateChild("PART_MediaHost") as FrameworkElement;
        _actionHost = GetTemplateChild("PART_ActionHost") as FrameworkElement;
        _statusText = GetTemplateChild("PART_StatusText") as TextBlock;
        _lift = new TranslateTransform();
        _zoom = new ScaleTransform(1, 1);
        if (_surface is not null && this is not MetricCard)
        {
            _surface.RenderTransform = _lift;
            _surface.MouseEnter += SurfaceEnter;
            _surface.MouseLeave += SurfaceLeave;
            _surface.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(SurfaceClicked), true);
        }
        if (_mediaHost is not null) { _mediaHost.RenderTransformOrigin = new Point(.5, .5); _mediaHost.RenderTransform = _zoom; }
        if (_titleButton is not null) _titleButton.Click += TitleClicked;
        if (_statusText is not null)
        {
            AutomationProperties.SetLiveSetting(_statusText, AutomationLiveSetting.Polite);
            _statusMove = new TranslateTransform();
            _statusText.RenderTransform = _statusMove;
        }
    }
    private void ReleaseParts()
    {
        _motion.Stop();
        if (_titleButton is not null) _titleButton.Click -= TitleClicked;
        if (_surface is not null)
        {
            _surface.MouseEnter -= SurfaceEnter;
            _surface.MouseLeave -= SurfaceLeave;
            _surface.RemoveHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(SurfaceClicked));
        }
        _titleButton = null; _surface = null; _mediaHost = null; _actionHost = null; _statusText = null; _statusMove = null; _lift = null; _zoom = null;
    }
    private static void OnVisualContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (Card)d;
        card.SetValue(HasDetailsPropertyKey, card.Details is not null);
        card.SetValue(HasBylinePropertyKey, card.Avatar is not null || card.Action is not null || !string.IsNullOrEmpty(card.Meta) || !string.IsNullOrEmpty(card.Status));
        if (e.Property == StatusProperty && card._statusText is not null && card.IsLoaded)
        {
            UIElementAutomationPeer.FromElement(card._statusText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            card.AnimateStatus();
        }
        if (card.Details is null && card.IsOpen) card.SetCurrentValue(IsOpenProperty, false);
        card.UpdateQuickLook();
    }
    private static void OnOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (Card)d;
        if ((bool)e.NewValue)
        {
            if (card.Details is null) { card.SetCurrentValue(IsOpenProperty, false); return; }
            if (card.IsLoaded) card.OpenQuickLook();
        }
        else card._quickLook?.Close();
    }
    private void TitleClicked(object sender, RoutedEventArgs e) { SetCurrentValue(IsOpenProperty, true); e.Handled = true; }
    private void SurfaceClicked(object sender, MouseButtonEventArgs e)
    {
        if (!HasDetails || e.ChangedButton != MouseButton.Left) return;
        for (DependencyObject? node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, _surface); node = DialogHost.GetParentNode(node))
        {
            if (ReferenceEquals(node, _actionHost) || node is ButtonBase button && !ReferenceEquals(button, _titleButton)) return;
        }
        SetCurrentValue(IsOpenProperty, true);
    }
    private void OpenQuickLook()
    {
        if (Details is null || _quickLook?.IsOpen == true) return;
        if (DialogHost.Find(this) is null) throw new InvalidOperationException("Card details require a DialogHost around the window content.");
        _quickLook ??= CreateQuickLook();
        UpdateQuickLook();
        _quickLook.Show(this);
        _quickLook.MorphFrom((FrameworkElement?)_surface ?? this);
    }
    private Dialog CreateQuickLook()
    {
        var dialog = new Dialog();
        BindingOperations.SetBinding(dialog, DataContextProperty, new Binding(nameof(DataContext)) { Source = this });
        BindingOperations.SetBinding(dialog, FlowDirectionProperty, new Binding(nameof(FlowDirection)) { Source = this });
        BindingOperations.SetBinding(dialog, ArcMotion.LevelProperty, new Binding { Source = this, Path = new PropertyPath(ArcMotion.LevelProperty) });
        dialog.Closed += (_, _) => { if (IsOpen) SetCurrentValue(IsOpenProperty, false); };
        return dialog;
    }
    private void UpdateQuickLook()
    {
        if (_quickLook is null) return;
        _quickLook.Title = Title;
        _quickLook.Description = Description;
        var body = new StackPanel();
        if (Media is not null && MediaTemplate is not null)
            body.Children.Add(new ContentPresenter { Content = Media, ContentTemplate = MediaTemplate, MinHeight = 112 });
        body.Children.Add(new ContentPresenter { Content = Details });
        _quickLook.Content = body;
    }
    private void SurfaceEnter(object sender, MouseEventArgs e)
    {
        if (e.StylusDevice is not null) return;
        SetValue(IsPointerHoverPropertyKey, true);
        SetHover(true);
    }
    private void SurfaceLeave(object sender, MouseEventArgs e) { SetValue(IsPointerHoverPropertyKey, false); SetHover(false); }
    private void SetHover(bool hovered)
    {
        if (_lift is null || _zoom is null) return;
        bool animate = ArcMotion.GetLevel(this) == ArcMotionLevel.Full && IsLoaded;
        _motion.Double(_lift, TranslateTransform.YProperty, hovered ? -2 : 0, hovered ? .26 : .18, animate, spring: hovered);
        _motion.Double(_zoom, ScaleTransform.ScaleXProperty, hovered ? 1.04 : 1, hovered ? .6 : .3, animate);
        _motion.Double(_zoom, ScaleTransform.ScaleYProperty, hovered ? 1.04 : 1, hovered ? .6 : .3, animate);
    }
    private void AnimateStatus()
    {
        if (_statusText is null || _statusMove is null) return;
        var level = ArcMotion.GetLevel(this);
        bool animate = level != ArcMotionLevel.Off;
        _statusText.Opacity = animate ? 0 : 1;
        _statusMove.Y = level == ArcMotionLevel.Full ? 4 : 0;
        _motion.Double(_statusText, OpacityProperty, 1, level == ArcMotionLevel.Full ? .24 : .12, animate, enter: true);
        _motion.Double(_statusMove, TranslateTransform.YProperty, 0, .24, animate && level == ArcMotionLevel.Full, enter: true);
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TitleProperty || e.Property == DescriptionProperty) UpdateQuickLook();
        if (e.Property == ArcMotion.LevelProperty && ArcMotion.GetLevel(this) != ArcMotionLevel.Full)
        {
            _motion.Stop();
            if (_lift is not null) _lift.Y = 0;
            if (_zoom is not null) _zoom.ScaleX = _zoom.ScaleY = 1;
            if (ArcMotion.GetLevel(this) == ArcMotionLevel.Off && _statusText is not null) _statusText.Opacity = 1;
            if (ArcMotion.GetLevel(this) == ArcMotionLevel.Off && _statusMove is not null) _statusMove.Y = 0;
        }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);
    private sealed class CardPeer(Card owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group; protected override string GetNameCore() => owner.Title; }
}

public partial class MetricCard : Card
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty TrendProperty = DependencyProperty.Register(nameof(Trend), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty, OnMetricChanged));
    public static readonly DependencyProperty TrendIsPositiveProperty = DependencyProperty.Register(nameof(TrendIsPositive), typeof(bool), typeof(MetricCard), new PropertyMetadata(true, OnMetricChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty, OnMetricChanged));
    public static readonly DependencyProperty NumericValueProperty = DependencyProperty.Register(nameof(NumericValue), typeof(double), typeof(MetricCard), new PropertyMetadata(double.NaN, OnMetricChanged), value => value is double number && (double.IsNaN(number) || double.IsFinite(number)));
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty, OnMetricChanged));
    public static readonly DependencyProperty ContextProperty = DependencyProperty.Register(nameof(Context), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ChangeProperty = DependencyProperty.Register(nameof(Change), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty, OnMetricChanged));
    private static readonly DependencyPropertyKey DisplayLabelPropertyKey = DependencyProperty.RegisterReadOnly(nameof(DisplayLabel), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DisplayLabelProperty = DisplayLabelPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey DisplayValuePropertyKey = DependencyProperty.RegisterReadOnly(nameof(DisplayValue), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DisplayValueProperty = DisplayValuePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey DisplayChangePropertyKey = DependencyProperty.RegisterReadOnly(nameof(DisplayChange), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DisplayChangeProperty = DisplayChangePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsNegativeChangePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsNegativeChange), typeof(bool), typeof(MetricCard), new PropertyMetadata(false));
    public static readonly DependencyProperty IsNegativeChangeProperty = IsNegativeChangePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsPositiveChangePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPositiveChange), typeof(bool), typeof(MetricCard), new PropertyMetadata(false));
    public static readonly DependencyProperty IsPositiveChangeProperty = IsPositiveChangePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsCompactPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsCompact), typeof(bool), typeof(MetricCard), new PropertyMetadata(false));
    public static readonly DependencyProperty IsCompactProperty = IsCompactPropertyKey.DependencyProperty;
    private TextBlock? _valueBlock;
    private Grid? _numberHost;
    private OdometerNumber? _ticker;
    private TranslateTransform? _valueMove;
    private readonly OwnedMotion _valueMotion = new();
    private int _valueDirection = 1;
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Trend { get => (string)GetValue(TrendProperty); set => SetValue(TrendProperty, value); }
    public bool TrendIsPositive { get => (bool)GetValue(TrendIsPositiveProperty); set => SetValue(TrendIsPositiveProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double NumericValue { get => (double)GetValue(NumericValueProperty); set => SetValue(NumericValueProperty, value); }
    public string Suffix { get => (string)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public string Context { get => (string)GetValue(ContextProperty); set => SetValue(ContextProperty, value); }
    public string Change { get => (string)GetValue(ChangeProperty); set => SetValue(ChangeProperty, value); }
    public string DisplayLabel => (string)GetValue(DisplayLabelProperty);
    public string DisplayValue => (string)GetValue(DisplayValueProperty);
    public string DisplayChange => (string)GetValue(DisplayChangeProperty);
    public bool IsNegativeChange => (bool)GetValue(IsNegativeChangeProperty);
    public bool IsPositiveChange => (bool)GetValue(IsPositiveChangeProperty);
    public bool IsCompact => (bool)GetValue(IsCompactProperty);
    static MetricCard() { DefaultStyleKeyProperty.OverrideMetadata(typeof(MetricCard), new FrameworkPropertyMetadata(typeof(MetricCard))); }
    public MetricCard() { RefreshMetric(); Unloaded += (_, _) => { _valueMotion.Stop(); _ticker?.Stop(); StopChangeMotion(); }; }
    public override void OnApplyTemplate()
    {
        _valueMotion.Stop();
        _ticker?.Stop();
        StopChangeMotion();
        base.OnApplyTemplate();
        _changeChip = GetTemplateChild("ChangeChip") as Border;
        _changeText = GetTemplateChild("TrendText") as TextBlock;
        _changeWidthMotion.Attach(_changeChip);
        _changeScale = null; _changeMove = null; _changeBlur = null;
        if (_changeChip is not null)
        {
            _changeScale = new ScaleTransform(1, 1);
            _changeChip.RenderTransformOrigin = new Point(.5, .5);
            _changeChip.RenderTransform = _changeScale;
        }
        if (_changeText is not null)
        {
            _changeMove = new TranslateTransform();
            _changeBlur = new System.Windows.Media.Effects.BlurEffect();
            _changeText.RenderTransform = _changeMove;
            _changeText.Effect = _changeBlur;
        }
        UpdateChangeMotion(false);
        _valueBlock = GetTemplateChild("PART_Value") as TextBlock;
        _numberHost = GetTemplateChild("PART_NumberHost") as Grid;
        _ticker = null;
        _valueMove = _valueBlock is null ? null : new TranslateTransform();
        if (_valueBlock is not null) _valueBlock.RenderTransform = _valueMove;
        if (_numberHost is not null)
        {
            _ticker = new OdometerNumber { FontSize = 36, FontWeight = FontWeights.Medium, Visibility = double.IsFinite(NumericValue) ? Visibility.Visible : Visibility.Collapsed };
            _ticker.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
            _ticker.SetResourceReference(FontFamilyProperty, "ArcWpf.Font.Body");
            _numberHost.Children.Add(_ticker);
            if (double.IsFinite(NumericValue)) _ticker.Set(NumericValue, DisplayValue, false);
        }
        if (_valueBlock is not null) _valueBlock.Visibility = double.IsFinite(NumericValue) ? Visibility.Collapsed : Visibility.Visible;
    }
    private static void OnMetricChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (MetricCard)d;
        if (e.Property == NumericValueProperty && e.OldValue is double oldValue && double.IsFinite(oldValue))
            card._valueDirection = (double)e.NewValue < oldValue ? -1 : 1;
        card.RefreshMetric();
        card.UpdateChangeMotion(true);
        bool numeric = double.IsFinite(card.NumericValue);
        if (card._valueBlock is not null) card._valueBlock.Visibility = numeric ? Visibility.Collapsed : Visibility.Visible;
        if (card._ticker is not null) card._ticker.Visibility = numeric ? Visibility.Visible : Visibility.Collapsed;
        if (double.IsFinite(card.NumericValue)) card._ticker?.Set(card.NumericValue, card.DisplayValue, true);
        else card._ticker?.Stop();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TitleProperty || e.Property == ValueProperty) RefreshMetric();
        if (e.Property == DisplayValueProperty && _valueBlock is not null && _valueMove is not null && IsLoaded)
        {
            bool numeric = double.IsFinite(NumericValue);
            _valueBlock.Visibility = numeric ? Visibility.Collapsed : Visibility.Visible;
            if (_ticker is not null)
            {
                _ticker.Visibility = numeric ? Visibility.Visible : Visibility.Collapsed;
                if (numeric) _ticker.Set(NumericValue, DisplayValue, true);
                else _ticker.Stop();
            }
            if (numeric) { _valueMotion.Stop(); return; }
            bool animate = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
            _valueMotion.Stop();
            _valueBlock.Opacity = animate ? 0 : 1;
            _valueMove.Y = animate ? _valueDirection * 8 : 0;
            _valueMotion.Double(_valueBlock, OpacityProperty, 1, .22, animate, enter: true);
            _valueMotion.Double(_valueMove, TranslateTransform.YProperty, 0, .24, animate, enter: true);
        }
        if (e.Property == ArcMotion.LevelProperty && ArcMotion.GetLevel(this) != ArcMotionLevel.Full)
        {
            _valueMotion.Stop();
            _ticker?.Stop();
            StopChangeMotion();
            if (_valueBlock is not null) _valueBlock.Opacity = 1;
            if (_valueMove is not null) _valueMove.Y = 0;
        }
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        SetValue(IsCompactPropertyKey, ActualWidth > 0 && ActualWidth < 380);
    }
    private void RefreshMetric()
    {
        SetValue(DisplayLabelPropertyKey, string.IsNullOrWhiteSpace(Label) ? Title : Label);
        SetValue(DisplayValuePropertyKey, double.IsNaN(NumericValue) ? Value : $"{NumericValue.ToString("#,0.##", System.Globalization.CultureInfo.CurrentCulture)}{Suffix}");
        string change = string.IsNullOrWhiteSpace(Change) ? Trend : Change;
        SetValue(DisplayChangePropertyKey, change);
        SetValue(IsNegativeChangePropertyKey, !TrendIsPositive || change.TrimStart().StartsWith('-') || change.TrimStart().StartsWith('−'));
        SetValue(IsPositiveChangePropertyKey, TrendIsPositive && change.TrimStart().StartsWith('+'));
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new MetricCardPeer(this);
    private sealed class MetricCardPeer(MetricCard owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.DisplayLabel;
        protected override string GetItemStatusCore() => $"{owner.DisplayValue} {owner.DisplayChange}".Trim();
    }
}
