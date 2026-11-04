using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ElasticSliderPage : UserControl
{
    public ElasticSliderPage()
    {
        InitializeComponent();
        Volume.Format = value => $"{value.ToString("0.##", CultureInfo.InvariantCulture)}%";
        Speed.Format = value => $"{value.ToString("0.##", CultureInfo.InvariantCulture)}×";
        ShowMinutes(Speed.Value);
    }

    private void OnSpeed(object sender, RoutedPropertyChangedEventArgs<double> e) => ShowMinutes(e.NewValue);
    private void ShowMinutes(double speed) { if (Minutes is not null) { Minutes.Text = $"{Math.Round(42 / speed, MidpointRounding.AwayFromZero)} min"; } }
}
