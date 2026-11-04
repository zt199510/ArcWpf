// Demo figures read from the public preview pages on uiarc.dev (2026-11-04), so the Gallery can be compared with them side by side.
using System.Windows.Media;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

internal static class ChartDemoData
{
    public static ActivityDay[] Days(string year) => year switch
    {
        "2023" => Run(new DateOnly(2023, 1, 1), [0, 8, 5, 3, 5, 4, 2, 0, 4, 4, 5, 0, 2, 2, 0, 2, 7, 4, 8, 3, 0, 0, 0, 3, 6, 3, 5, 0, 0, 6, 8, 0, 7, 0, 0, 4, 6, 3, 3, 3, 5, 0, 3, 4, 8, 5, 4, 6, 3, 6, 0, 4, 3, 3, 5, 0, 0, 2, 8, 3, 4, 3, 7, 9, 12, 15, 11, 3, 13, 3, 0, 7, 15, 5, 15, 14, 0, 9, 2, 0, 3, 4, 6, 0, 0, 3, 3, 0, 4, 0, 0, 0, 3, 3, 6, 7, 3, 0, 0, 7, 3, 6, 5, 2, 0, 0, 4, 3, 6, 0, 6, 6, 0, 3, 3, 4, 2, 4, 0, 0, 7, 6, 6, 6, 0, 0, 4, 2, 3, 3, 2, 5, 0, 0, 3, 3, 5, 2, 4, 0, 0, 8, 8, 0, 7, 2, 0, 0, 4, 8, 3, 7, 7, 0, 0, 6, 5, 15, 5, 13, 0, 3, 10, 5, 5, 8, 10, 15, 0, 3, 6, 12, 5, 6, 0, 0, 2, 5, 4, 6, 0, 0, 0, 0, 8, 3, 0, 8, 0, 0, 8, 4, 3, 2, 6, 0, 0, 2, 3, 3, 7, 7, 0, 4, 7, 0, 0, 2, 8, 0, 0, 8, 3, 0, 4, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 9, 4, 2, 2, 2, 0, 6, 3, 6, 2, 2, 0, 0, 6, 7, 3, 6, 6, 0, 0, 2, 0, 5, 5, 2, 0, 0, 2, 3, 4, 6, 2, 0, 0, 6, 7, 16, 4, 10, 0, 4, 11, 7, 11, 3, 14, 10, 0, 3, 14, 4, 7, 2, 0, 0, 5, 3, 7, 2, 4, 2, 0, 3, 3, 3, 2, 2, 4, 0, 0, 3, 5, 4, 0, 0, 0, 2, 4, 7, 2, 7, 0, 7, 2, 6, 8, 2, 0, 5, 0, 6, 3, 3, 4, 7, 0, 3, 4, 8, 6, 4, 2, 0, 0, 6, 5, 6, 4, 7, 2, 0, 2, 0, 3, 6, 3, 0, 3, 7, 3, 0, 3, 7, 0, 4, 2, 0, 0, 0, 0, 0, 0]),
        "2024" => Run(new DateOnly(2024, 1, 1), [0, 6, 0, 6, 2, 0, 0, 3, 4, 4, 8, 8, 0, 0, 3, 8, 3, 0, 5, 0, 0, 6, 5, 7, 0, 0, 0, 0, 8, 0, 0, 2, 2, 0, 0, 0, 6, 6, 0, 2, 8, 0, 0, 0, 3, 4, 2, 0, 0, 2, 5, 4, 2, 3, 4, 0, 5, 0, 7, 2, 6, 0, 3, 5, 6, 10, 5, 6, 0, 6, 3, 0, 9, 4, 9, 6, 0, 13, 4, 8, 0, 3, 0, 0, 5, 0, 5, 5, 3, 0, 0, 2, 5, 4, 0, 5, 0, 0, 2, 3, 3, 2, 3, 0, 0, 2, 0, 5, 8, 2, 0, 0, 4, 0, 0, 8, 7, 0, 0, 2, 3, 0, 6, 3, 0, 0, 3, 8, 4, 0, 0, 0, 0, 2, 0, 5, 0, 5, 0, 0, 5, 7, 5, 2, 3, 0, 0, 0, 7, 3, 2, 2, 0, 0, 6, 3, 7, 6, 5, 0, 0, 14, 12, 5, 4, 7, 0, 9, 12, 17, 4, 15, 4, 4, 12, 9, 5, 3, 3, 4, 3, 0, 3, 0, 6, 6, 2, 0, 0, 3, 6, 5, 4, 6, 0, 0, 7, 0, 3, 3, 6, 2, 0, 5, 6, 3, 5, 2, 0, 2, 3, 3, 7, 4, 0, 0, 4, 2, 3, 4, 5, 3, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 2, 0, 4, 2, 2, 0, 0, 2, 3, 0, 7, 8, 0, 6, 7, 0, 5, 6, 2, 0, 0, 0, 5, 8, 2, 0, 0, 0, 4, 0, 3, 5, 9, 13, 5, 6, 6, 15, 7, 12, 0, 5, 14, 4, 7, 3, 10, 0, 0, 5, 3, 0, 3, 2, 0, 0, 2, 3, 5, 2, 0, 0, 0, 3, 8, 5, 5, 7, 0, 0, 0, 3, 8, 3, 3, 0, 0, 4, 6, 5, 4, 8, 2, 0, 2, 6, 7, 0, 3, 0, 0, 2, 6, 9, 7, 4, 0, 0, 0, 0, 4, 3, 2, 0, 0, 2, 8, 4, 6, 3, 0, 0, 6, 3, 3, 5, 2, 5, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0]),
        "2025" => Run(new DateOnly(2025, 1, 1), [0, 8, 8, 2, 0, 3, 3, 6, 0, 4, 0, 0, 3, 3, 4, 7, 2, 0, 0, 4, 4, 5, 5, 7, 2, 0, 4, 0, 0, 0, 5, 0, 0, 2, 3, 8, 7, 5, 0, 0, 4, 9, 7, 4, 4, 0, 0, 4, 3, 9, 5, 4, 2, 0, 0, 5, 4, 7, 2, 0, 0, 3, 7, 10, 13, 8, 5, 0, 15, 9, 16, 3, 3, 3, 4, 9, 8, 5, 2, 0, 0, 0, 5, 8, 7, 7, 2, 0, 0, 5, 7, 9, 2, 2, 0, 0, 6, 4, 3, 5, 0, 3, 0, 0, 4, 3, 3, 2, 0, 0, 8, 5, 4, 2, 6, 7, 0, 7, 4, 6, 2, 7, 0, 0, 3, 0, 0, 4, 2, 0, 5, 7, 8, 0, 3, 5, 0, 0, 2, 3, 8, 2, 2, 0, 0, 8, 4, 4, 2, 2, 0, 0, 3, 0, 0, 0, 8, 0, 0, 0, 5, 4, 4, 7, 3, 0, 13, 15, 9, 8, 4, 0, 0, 11, 7, 0, 2, 2, 0, 0, 0, 3, 4, 8, 6, 0, 0, 4, 5, 7, 6, 3, 0, 0, 0, 0, 8, 0, 2, 0, 0, 0, 3, 4, 4, 2, 0, 2, 2, 5, 3, 2, 7, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 3, 3, 0, 3, 0, 4, 8, 3, 8, 2, 2, 0, 0, 5, 0, 3, 0, 4, 0, 0, 2, 0, 7, 2, 2, 0, 0, 2, 8, 6, 0, 3, 0, 0, 4, 8, 13, 6, 10, 6, 13, 4, 10, 9, 3, 6, 10, 3, 4, 5, 13, 2, 5, 0, 0, 3, 3, 4, 6, 7, 0, 0, 3, 8, 8, 2, 4, 0, 5, 6, 4, 3, 0, 4, 0, 0, 3, 3, 3, 2, 6, 6, 0, 3, 3, 3, 3, 7, 2, 4, 5, 0, 3, 3, 0, 0, 0, 3, 0, 3, 2, 6, 0, 0, 8, 7, 3, 5, 6, 0, 0, 8, 7, 7, 6, 4, 0, 0, 0, 3, 8, 4, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
        _ => [],
    };
    private static ActivityDay[] Run(DateOnly start, int[] counts) => [.. counts.Select((count, index) => new ActivityDay(start.AddDays(index), count))];
    public static BarChartDatum[] Week => [
        new("Sep 15", "Tue, Sep 15", 46, "T"),
        new("Sep 16", "Wed, Sep 16", 58, "W"),
        new("Sep 17", "Thu, Sep 17", 24, "T"),
        new("Sep 18", "Fri, Sep 18", 41, "F"),
        new("Sep 19", "Sat, Sep 19", 86, "S"),
        new("Sep 20", "Sun, Sep 20", 67, "S"),
        new("Sep 21", "Today, Sep 21", 18, "M"),
    ];
    public static BarChartDatum[] Month => [
        new("Aug 23", "Sun, Aug 23", 42),
        new("Aug 24", "Mon, Aug 24", 38, "Aug 24"),
        new("Aug 25", "Tue, Aug 25", 30),
        new("Aug 26", "Wed, Aug 26", 22),
        new("Aug 27", "Thu, Aug 27", 38),
        new("Aug 28", "Fri, Aug 28", 33),
        new("Aug 29", "Sat, Aug 29", 52),
        new("Aug 30", "Sun, Aug 30", 34),
        new("Aug 31", "Mon, Aug 31", 29, "Aug 31"),
        new("Sep 1", "Tue, Sep 1", 34),
        new("Sep 2", "Wed, Sep 2", 31),
        new("Sep 3", "Thu, Sep 3", 12),
        new("Sep 4", "Fri, Sep 4", 36),
        new("Sep 5", "Sat, Sep 5", 118),
        new("Sep 6", "Sun, Sep 6", 44),
        new("Sep 7", "Mon, Sep 7", 48, "Sep 7"),
        new("Sep 8", "Tue, Sep 8", 44),
        new("Sep 9", "Wed, Sep 9", 38),
        new("Sep 10", "Thu, Sep 10", 36),
        new("Sep 11", "Fri, Sep 11", 27),
        new("Sep 12", "Sat, Sep 12", 64),
        new("Sep 13", "Sun, Sep 13", 49),
        new("Sep 14", "Mon, Sep 14", 31, "Sep 14"),
        new("Sep 15", "Tue, Sep 15", 46),
        new("Sep 16", "Wed, Sep 16", 58),
        new("Sep 17", "Thu, Sep 17", 24),
        new("Sep 18", "Fri, Sep 18", 41),
        new("Sep 19", "Sat, Sep 19", 86),
        new("Sep 20", "Sun, Sep 20", 67),
        new("Sep 21", "Today, Sep 21", 18, "Sep 21"),
    ];
    public static LineChartDatum[] Signups(string range) => range switch
    {
        "7d" => [P("Sep 15", "Tue, Sep 15", 268, 211, "Sep 15"), P("Sep 16", "Wed, Sep 16", 262, 230), P("Sep 17", "Thu, Sep 17", 260, 226, "Sep 17"), P("Sep 18", "Fri, Sep 18", 268, 232), P("Sep 19", "Sat, Sep 19", 154, 143, "Sep 19"), P("Sep 20", "Sun, Sep 20", 176, 147), P("Sep 21", "Today, Sep 21", 248, 183, "Sep 21")],
        "30d" => [P("Aug 23", "Sun, Aug 23", 128, 121), P("Aug 24", "Mon, Aug 24", 198, 202, "Aug 24"), P("Aug 25", "Tue, Aug 25", 192, 198), P("Aug 26", "Wed, Aug 26", 211, 190), P("Aug 27", "Thu, Aug 27", 206, 182), P("Aug 28", "Fri, Aug 28", 199, 172), P("Aug 29", "Sat, Aug 29", 130, 121), P("Aug 30", "Sun, Aug 30", 159, 134), P("Aug 31", "Mon, Aug 31", 254, 218, "Aug 31"), P("Sep 1", "Tue, Sep 1", 262, 217), P("Sep 2", "Wed, Sep 2", 247, 226), P("Sep 3", "Thu, Sep 3", 260, 223), P("Sep 4", "Fri, Sep 4", 253, 231), P("Sep 5", "Sat, Sep 5", 169, 146), P("Sep 6", "Sun, Sep 6", 158, 121), P("Sep 7", "Mon, Sep 7", 231, 184, "Sep 7"), P("Sep 8", "Tue, Sep 8", 239, 203), P("Sep 9", "Wed, Sep 9", 437, 200), P("Sep 10", "Thu, Sep 10", 338, 181), P("Sep 11", "Fri, Sep 11", 240, 187), P("Sep 12", "Sat, Sep 12", 139, 118), P("Sep 13", "Sun, Sep 13", 134, 148), P("Sep 14", "Mon, Sep 14", 250, 235, "Sep 14"), P("Sep 15", "Tue, Sep 15", 268, 211), P("Sep 16", "Wed, Sep 16", 262, 230), P("Sep 17", "Thu, Sep 17", 260, 226), P("Sep 18", "Fri, Sep 18", 268, 232), P("Sep 19", "Sat, Sep 19", 154, 143), P("Sep 20", "Sun, Sep 20", 176, 147), P("Sep 21", "Today, Sep 21", 248, 183, "Sep 21")],
        "90d" => [P("Jun 24", "Wed, Jun 24", 184, 179), P("Jun 25", "Thu, Jun 25", 170, 166), P("Jun 26", "Fri, Jun 26", 168, 161), P("Jun 27", "Sat, Jun 27", 107, 109), P("Jun 28", "Sun, Jun 28", 105, 109), P("Jun 29", "Mon, Jun 29", 162, 180), P("Jun 30", "Tue, Jun 30", 168, 168), P("Jul 1", "Wed, Jul 1", 163, 184), P("Jul 2", "Thu, Jul 2", 205, 207), P("Jul 3", "Fri, Jul 3", 215, 205), P("Jul 4", "Sat, Jul 4", 120, 123), P("Jul 5", "Sun, Jul 5", 123, 119), P("Jul 6", "Mon, Jul 6", 216, 205), P("Jul 7", "Tue, Jul 7", 200, 202), P("Jul 8", "Wed, Jul 8", 214, 212), P("Jul 9", "Thu, Jul 9", 171, 171), P("Jul 10", "Fri, Jul 10", 166, 180), P("Jul 11", "Sat, Jul 11", 106, 113), P("Jul 12", "Sun, Jul 12", 113, 112), P("Jul 13", "Mon, Jul 13", 179, 179), P("Jul 14", "Tue, Jul 14", 178, 178), P("Jul 15", "Wed, Jul 15", 181, 171, "Jul 15"), P("Jul 16", "Thu, Jul 16", 177, 189), P("Jul 17", "Fri, Jul 17", 221, 216), P("Jul 18", "Sat, Jul 18", 127, 123), P("Jul 19", "Sun, Jul 19", 128, 135), P("Jul 20", "Mon, Jul 20", 204, 209), P("Jul 21", "Tue, Jul 21", 231, 205), P("Jul 22", "Wed, Jul 22", 208, 220), P("Jul 23", "Thu, Jul 23", 212, 170), P("Jul 24", "Fri, Jul 24", 179, 165), P("Jul 25", "Sat, Jul 25", 114, 107), P("Jul 26", "Sun, Jul 26", 113, 111), P("Jul 27", "Mon, Jul 27", 191, 169), P("Jul 28", "Tue, Jul 28", 205, 177), P("Jul 29", "Wed, Jul 29", 190, 183), P("Jul 30", "Thu, Jul 30", 196, 184), P("Jul 31", "Fri, Jul 31", 205, 217), P("Aug 1", "Sat, Aug 1", 134, 131), P("Aug 2", "Sun, Aug 2", 138, 134), P("Aug 3", "Mon, Aug 3", 218, 197), P("Aug 4", "Tue, Aug 4", 219, 209), P("Aug 5", "Wed, Aug 5", 233, 219), P("Aug 6", "Thu, Aug 6", 226, 222), P("Aug 7", "Fri, Aug 7", 244, 196), P("Aug 8", "Sat, Aug 8", 128, 117), P("Aug 9", "Sun, Aug 9", 118, 119), P("Aug 10", "Mon, Aug 10", 199, 167), P("Aug 11", "Tue, Aug 11", 217, 195), P("Aug 12", "Wed, Aug 12", 206, 174), P("Aug 13", "Thu, Aug 13", 213, 189), P("Aug 14", "Fri, Aug 14", 203, 190), P("Aug 15", "Sat, Aug 15", 158, 136, "Aug 15"), P("Aug 16", "Sun, Aug 16", 160, 135), P("Aug 17", "Mon, Aug 17", 244, 201), P("Aug 18", "Tue, Aug 18", 231, 224), P("Aug 19", "Wed, Aug 19", 240, 208), P("Aug 20", "Thu, Aug 20", 236, 204), P("Aug 21", "Fri, Aug 21", 252, 203), P("Aug 22", "Sat, Aug 22", 163, 121), P("Aug 23", "Sun, Aug 23", 128, 121), P("Aug 24", "Mon, Aug 24", 198, 202), P("Aug 25", "Tue, Aug 25", 192, 198), P("Aug 26", "Wed, Aug 26", 211, 190), P("Aug 27", "Thu, Aug 27", 206, 182), P("Aug 28", "Fri, Aug 28", 199, 172), P("Aug 29", "Sat, Aug 29", 130, 121), P("Aug 30", "Sun, Aug 30", 159, 134), P("Aug 31", "Mon, Aug 31", 254, 218), P("Sep 1", "Tue, Sep 1", 262, 217), P("Sep 2", "Wed, Sep 2", 247, 226), P("Sep 3", "Thu, Sep 3", 260, 223), P("Sep 4", "Fri, Sep 4", 253, 231), P("Sep 5", "Sat, Sep 5", 169, 146), P("Sep 6", "Sun, Sep 6", 158, 121), P("Sep 7", "Mon, Sep 7", 231, 184), P("Sep 8", "Tue, Sep 8", 239, 203), P("Sep 9", "Wed, Sep 9", 437, 200), P("Sep 10", "Thu, Sep 10", 338, 181), P("Sep 11", "Fri, Sep 11", 240, 187), P("Sep 12", "Sat, Sep 12", 139, 118), P("Sep 13", "Sun, Sep 13", 134, 148), P("Sep 14", "Mon, Sep 14", 250, 235), P("Sep 15", "Tue, Sep 15", 268, 211, "Sep 15"), P("Sep 16", "Wed, Sep 16", 262, 230), P("Sep 17", "Thu, Sep 17", 260, 226), P("Sep 18", "Fri, Sep 18", 268, 232), P("Sep 19", "Sat, Sep 19", 154, 143), P("Sep 20", "Sun, Sep 20", 176, 147), P("Sep 21", "Today, Sep 21", 248, 183)],
        _ => [],
    };
    private static LineChartDatum P(string key, string label, double now, double before, string? axis = null) => new(key, label, new Dictionary<string, double> { ["now"] = now, ["before"] = before }, axis);
    public static DonutChartDatum[] Visits(string range) => range switch
    {
        "week" => [new("search", "Search", 4210), new("direct", "Direct", 2380), new("newsletter", "Newsletter", 1940), new("referrals", "Referrals", 1120), new("social", "Social", 310), new("podcasts", "Podcasts", 180), new("partners", "Partners", 95)],
        "month" => [new("search", "Search", 16840), new("direct", "Direct", 8120), new("newsletter", "Newsletter", 5260), new("referrals", "Referrals", 6430), new("social", "Social", 2910), new("podcasts", "Podcasts", 640), new("partners", "Partners", 410)],
        "year" => [new("search", "Search", 168300), new("direct", "Direct", 71900), new("newsletter", "Newsletter", 38400), new("referrals", "Referrals", 52700), new("social", "Social", 41200), new("podcasts", "Podcasts", 19600), new("partners", "Partners", 6900)],
        _ => [],
    };
    public static string VisitsPeriod(string range) => range switch { "week" => "Sep 15–21, 2026", "month" => "Aug 23 – Sep 21, 2026", "year" => "Oct 2025 – Sep 2026", _ => "" };
    public static FunnelSegment[] Devices => [new("mobile", "Mobile"), new("desktop", "Desktop")];
    public static FunnelStage[] Checkout => [
        new("visit", "Visited store", Values: new Dictionary<string, double> { ["mobile"] = 68420, ["desktop"] = 31580 }),
        new("product", "Viewed product", Values: new Dictionary<string, double> { ["mobile"] = 39710, ["desktop"] = 21460 }),
        new("cart", "Added to cart", Values: new Dictionary<string, double> { ["mobile"] = 9140, ["desktop"] = 7280 }),
        new("checkout", "Started checkout", Values: new Dictionary<string, double> { ["mobile"] = 4310, ["desktop"] = 4090 }),
        new("purchase", "Purchased", Values: new Dictionary<string, double> { ["mobile"] = 1540, ["desktop"] = 2050 }),
    ];
}
