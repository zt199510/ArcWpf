using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class BarChartPage : UserControl
{
    public BarChartPage()
    {
        InitializeComponent();
        Chart.Data = ChartDemoData.Week;
    }

    private void OnRange(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) { return; }
        bool week = Range.SelectedValue as string == "week";
        Chart.Period = week ? "Sep 15–21, 2026" : "Aug 23 – Sep 21, 2026";
        Chart.Data = week ? ChartDemoData.Week : ChartDemoData.Month;
    }
}
