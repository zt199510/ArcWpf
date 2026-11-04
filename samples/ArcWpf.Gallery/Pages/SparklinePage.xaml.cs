using System.Globalization;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class SparklinePage : UserControl
{
    private static readonly double[] Week = [2318, 2402, 2376, 2451, 2433, 2390, 2486];
    private static readonly double[] Month = [2540, 2522, 2497, 2468, 2431, 2412, 2385, 2371, 2352, 2330, 2306, 2291, 2284, 2302, 2279, 2268, 2295, 2311, 2287, 2304, 2336, 2329, 2297, 2318, 2402, 2376, 2451, 2433, 2390, 2486];
    private static readonly double[] Year = [1612, 1688, 1654, 1731, 1802, 1869, 1948, 2012, 2098, 2187, 2301, 2486];

    public SparklinePage()
    {
        InitializeComponent();
        Users.FormatValue = (value, _) => value.ToString("N0", CultureInfo.InvariantCulture);
        Apply("w");
    }

    private void OnRange(object sender, SelectionChangedEventArgs e)
    {
        if (Range.SelectedValue is string range && Users is not null) { Apply(range); }
    }

    private void Apply(string range)
    {
        Users.Value = "2,486";
        (Users.Data, Users.Labels, Users.Change, Users.Tone) = range switch
        {
            "m" => ((IReadOnlyList<double>)Month, (IReadOnlyList<string>)[.. Enumerable.Range(23, 9).Select(day => $"Aug {day}"), .. Enumerable.Range(1, 21).Select(day => $"Sep {day}")], "−2.1%", ArcTone.Danger),
            "y" => (Year, ["Oct 2025", "Nov 2025", "Dec 2025", "Jan 2026", "Feb 2026", "Mar 2026", "Apr 2026", "May 2026", "Jun 2026", "Jul 2026", "Aug 2026", "Sep 2026"], "+54.2%", ArcTone.Success),
            _ => (Week, [.. Enumerable.Range(15, 7).Select(day => $"Sep {day}")], "+7.2%", ArcTone.Success),
        };
    }
}
