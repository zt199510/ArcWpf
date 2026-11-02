using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class SortableDataTablePage : UserControl
{
    public SortableDataTablePage()
    {
        InitializeComponent();
        // Arc's demo uses browser auto table layout; these are the tracks it resolved to at this width.
        Table.Columns.Add(new DataTableColumn { Key = "Project", Header = "Project", Path = "Project", Width = new DataGridLength(125.4) });
        Table.Columns.Add(new DataTableColumn { Key = "Owner", Header = "Owner", Path = "Owner", Width = new DataGridLength(123.2) });
        Table.Columns.Add(new DataTableColumn { Key = "Status", Header = "Status", Path = "Status", Width = new DataGridLength(96.5), CellTemplate = (DataTemplate)Resources["StatusCell"] });
        Table.Columns.Add(new DataTableColumn { Key = "Due", Header = "Due", Path = "Due", Width = new DataGridLength(79.6) });
        Table.Columns.Add(new DataTableColumn { Key = "Score", Header = "Score", Path = "Score", Numeric = true, Width = new DataGridLength(91.3) });
        Table.Rows = new object[]
        {
            new ProjectRow("Aurora redesign", "Emma Collins", "In review", BadgeTone.Info, "Oct 2", 92),
            new ProjectRow("Billing migration", "Noah Williams", "On track", BadgeTone.Success, "Oct 14", 86),
            new ProjectRow("Docs refresh", "Marcus Johnson", "Done", BadgeTone.Neutral, "Sep 18", 97),
            new ProjectRow("Mobile onboarding", "Ari Singh", "On track", BadgeTone.Success, "Nov 6", 78),
            new ProjectRow("Cinder analytics", "Inez Park", "At risk", BadgeTone.Warning, "Sep 28", 64),
        };
        Empty.Columns.Add(new DataTableColumn { Key = "Project", Header = "Project", Path = "Project" });
        Empty.Columns.Add(new DataTableColumn { Key = "Owner", Header = "Owner", Path = "Owner" });
        Empty.Rows = System.Array.Empty<object>();
    }

    public sealed record ProjectRow(string Project, string Owner, string Status, BadgeTone Tone, string Due, int Score);
}
