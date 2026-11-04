using System.Globalization;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class LineChartPage : UserControl
{
    public LineChartPage()
    {
        InitializeComponent();
        Chart.Series = [new("now", "This period"), new("before", "Previous period", Dashed: true, Area: false)];
        Show("30d");
    }

    private void OnRange(object sender, SelectionChangedEventArgs e) { if (IsInitialized && Range.SelectedValue is string range) { Show(range); } }

    private void Show(string range)
    {
        Chart.Data = ChartDemoData.Signups(range);
        Rest();
    }

    private void Rest()
    {
        var data = ChartDemoData.Signups((string)Range.SelectedValue);
        double now = data.Sum(item => item.Values["now"]), before = data.Sum(item => item.Values["before"]);
        double change = (now - before) / before * 100;
        Total.Label = "Total signups";
        Total.Value = now;
        Note.Text = $"{(change >= 0 ? "+" : "")}{change.ToString("0.0", CultureInfo.InvariantCulture)}% vs previous {data.Length} days";
        Note.SetResourceReference(TextBlock.ForegroundProperty, change >= 0 ? "ArcWpf.Brush.ArcSuccess" : "ArcWpf.Brush.ArcDanger");
    }

    private void OnActive(object? sender, LineChartActiveEventArgs e)
    {
        if (e.Datum is not { } point) { Rest(); return; }
        Total.Label = point.Label;
        Total.Value = point.Values["now"];
        Note.Text = $"{point.Values["before"].ToString("#,0", CultureInfo.InvariantCulture)} in the previous period";
        Note.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
    }
}
