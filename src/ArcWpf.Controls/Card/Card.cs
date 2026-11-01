using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

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
    public static readonly DependencyProperty TrendProperty = DependencyProperty.Register(nameof(Trend), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty TrendIsPositiveProperty = DependencyProperty.Register(nameof(TrendIsPositive), typeof(bool), typeof(MetricCard), new PropertyMetadata(true));
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Trend { get => (string)GetValue(TrendProperty); set => SetValue(TrendProperty, value); }
    public bool TrendIsPositive { get => (bool)GetValue(TrendIsPositiveProperty); set => SetValue(TrendIsPositiveProperty, value); }
    static MetricCard() { DefaultStyleKeyProperty.OverrideMetadata(typeof(MetricCard), new FrameworkPropertyMetadata(typeof(MetricCard))); }
}
