using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class RulerSliderPage : UserControl
{
    public RulerSliderPage()
    {
        InitializeComponent();
        ShowImperial(Tall.Value);
    }

    private void OnTall(object sender, RoutedPropertyChangedEventArgs<double> e) => ShowImperial(e.NewValue);

    private void ShowImperial(double centimeters)
    {
        if (Imperial is null) { return; }
        int inches = (int)Math.Round(centimeters / 2.54, MidpointRounding.AwayFromZero);
        Imperial.Text = $"{inches / 12} ft {inches % 12} in";
    }
}
