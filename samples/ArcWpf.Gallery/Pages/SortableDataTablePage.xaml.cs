using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class SortableDataTablePage : UserControl
{
    public SortableDataTablePage()
    {
        InitializeComponent();
        Table.Columns.Add(new DataTableColumn { Key = "Name", Header = "Name", Path = "Name" });
        Table.Columns.Add(new DataTableColumn { Key = "Owner", Header = "Owner", Path = "Owner" });
        Table.Columns.Add(new DataTableColumn { Key = "Count", Header = "Count", Path = "Count", Numeric = true });
        Table.Rows = new object[] { new { Name = "Harbour", Owner = "Maya", Count = 12 }, new { Name = "Atlas", Owner = "Leo", Count = 7 } };
    }
}
