using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class FunnelChartPage : UserControl
{
    public FunnelChartPage()
    {
        InitializeComponent();
        Chart.Segments = ChartDemoData.Devices;
        Chart.Stages = ChartDemoData.Checkout;
        Show();
    }

    private void OnView(object sender, SelectionChangedEventArgs e) { if (IsInitialized) { if (Device.SelectedValue as string != "total") { Split.IsChecked = false; } Show(); } }

    private void OnSwitch(object sender, RoutedEventArgs e)
    {
        if (sender == Split && Split.IsChecked == true) { Device.SelectedValue = "total"; }
        Show();
    }

    private void Show()
    {
        string device = Device.SelectedValue as string ?? "total";
        Chart.View = Split.IsChecked == true ? "split" : device;
        Chart.Flow = Flow.IsChecked == true;
        var stages = ChartDemoData.Checkout;
        double Sum(int at) => device == "total" ? stages[at].Values!.Values.Sum() : stages[at].Values![device];
        double percent = Sum(stages.Length - 1) / Sum(0) * 100;
        Meta.Text = $"Last 30 days, {percent.ToString("0.0", CultureInfo.InvariantCulture)}% convert";
    }
}
