using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class GaugePage : UserControl
{
    public GaugePage()
    {
        InitializeComponent();
        Show(72);
    }

    private void OnLower(object sender, RoutedEventArgs e) => Show(Health.Value - 12);
    private void OnRaise(object sender, RoutedEventArgs e) => Show(Health.Value + 12);

    private void Show(double value)
    {
        value = Math.Clamp(value, 0, 100);
        Health.Value = value;
        Health.Detail = $"{Math.Round(value / 5, MidpointRounding.AwayFromZero)} of 20 checks passing";
        Lower.IsEnabled = value > 0;
        Raise.IsEnabled = value < 100;
    }
}
