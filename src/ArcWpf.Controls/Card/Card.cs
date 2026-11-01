using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

public class Card : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Card), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(Card), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(nameof(Footer), typeof(object), typeof(Card));
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    static Card() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(typeof(Card))); }
    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);
    private sealed class CardPeer(Card owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group; protected override string GetNameCore() => owner.Title; }
}

public class MetricCard : Card
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
    public MetricCard() { RefreshMetric(); Unloaded += (_, _) => _valueMotion.Stop(); }
    public override void OnApplyTemplate()
    {
        _valueMotion.Stop();
        base.OnApplyTemplate();
        _valueBlock = GetTemplateChild("PART_Value") as TextBlock;
        _valueMove = _valueBlock is null ? null : new TranslateTransform();
        if (_valueBlock is not null) _valueBlock.RenderTransform = _valueMove;
    }
    private static void OnMetricChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (MetricCard)d;
        if (e.Property == NumericValueProperty && e.OldValue is double oldValue && double.IsFinite(oldValue))
            card._valueDirection = (double)e.NewValue < oldValue ? -1 : 1;
        card.RefreshMetric();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TitleProperty || e.Property == ValueProperty) RefreshMetric();
        if (e.Property == DisplayValueProperty && _valueBlock is not null && _valueMove is not null && IsLoaded)
        {
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
