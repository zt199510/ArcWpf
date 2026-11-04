using System.Globalization;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class DonutChartPage : UserControl
{
    public DonutChartPage()
    {
        InitializeComponent();
        Chart.FormatValue = value => value >= 100000 ? (value / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "K" : value.ToString("#,0", CultureInfo.InvariantCulture);
        Show("month");
    }

    private void OnRange(object sender, SelectionChangedEventArgs e) { if (IsInitialized && Range.SelectedValue is string range) { Show(range); } }

    private void Show(string range)
    {
        Chart.Data = ChartDemoData.Visits(range);
        Period.Text = ChartDemoData.VisitsPeriod(range);
    }
}
