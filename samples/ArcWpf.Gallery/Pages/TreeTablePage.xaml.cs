using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class TreeTablePage : UserControl
{
    private bool _legacyFailedOnce;

    public TreeTablePage()
    {
        InitializeComponent();
        Table.Columns.Add(new TreeTableColumn { Key = "Name", Header = "Name", Path = "Name", CellTemplate = (DataTemplate)Resources["NameCell"] });
        Table.Columns.Add(new TreeTableColumn { Key = "Resources", Header = "Resources", Path = "Resources", Numeric = true, Width = new GridLength(112) });
        Table.Columns.Add(new TreeTableColumn { Key = "Monthly", Header = "Monthly", Path = "Monthly", Numeric = true, Width = new GridLength(96), CellTemplate = (DataTemplate)Resources["MonthlyCell"] });
        Table.LoadChildren = LoadLegacy;
        Table.Rows.Add(Row("platform", "Platform", null, 48, 4450,
            Row("api-gateway", "API gateway", null, 16, 2150,
                Row("api-prod", "api-prod", "us-east-1", 12, 1840),
                Row("api-staging", "api-staging", "us-east-1", 4, 310)),
            Row("auth", "Auth service", null, 8, 1180,
                Row("auth-prod", "auth-prod", "us-east-1", 6, 920),
                Row("session-cache", "Session cache", "eu-west-1", 2, 260)),
            Row("runners", "Build runners", null, 24, 1120)));
        Table.Rows.Add(Row("data", "Data", null, 21, 4150,
            Row("warehouse", "Warehouse", null, 11, 3300),
            Row("pipelines", "Pipelines", null, 10, 850,
                Row("ingest", "Ingest workers", "us-east-1", 9, 710),
                Row("dbt", "dbt scheduler", "us-east-1", 1, 140))));
        Table.Rows.Add(Row("growth", "Growth", null, 7, 600,
            Row("marketing", "Marketing site", "Edge", 3, 220),
            Row("experiments", "Experiment service", "us-west-2", 4, 380)));
        var legacy = Row("legacy", "Legacy services", null, 4, 635);
        legacy.HasChildren = true;
        Table.Rows.Add(legacy);
        Table.ExpandedIds.Add("platform");
        Table.ExpandedIds.Add("api-gateway");
    }

    // Arc's demo fails the first load on purpose, so its retry row can be seen.
    private async Task<IEnumerable<TreeTableRow>> LoadLegacy(TreeTableRow row)
    {
        await Task.Delay(1200);
        if (!_legacyFailedOnce) { _legacyFailedOnce = true; throw new InvalidOperationException("Demo load failure"); }
        return [Row("billing-v1", "Billing v1", "us-east-1", 3, 540), Row("cron-box", "Cron box", "us-east-1", 1, 95)];
    }

    private static TreeTableRow Row(string id, string name, string? detail, int resources, int monthly, params TreeTableRow[] children)
    {
        var row = new TreeTableRow { Id = id, Data = new Resource(name, detail, resources, monthly) };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }

    public sealed record Resource(string Name, string? Detail, int Resources, int Monthly);
}
