using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class TreeTablePage : UserControl
{
    public TreeTablePage()
    {
        InitializeComponent();
        Table.Columns.Add(new TreeTableColumn { Key = "Name", Header = "Name", Path = "Name" });
        Table.Columns.Add(new TreeTableColumn { Key = "Owner", Header = "Owner", Path = "Owner" });
        Table.Rows.Add(new TreeTableRow { Id = "design", Data = new Item("Design", "Maya"), Children = { new TreeTableRow { Id = "tokens", Data = new Item("Tokens", "Leo") } } });
        Table.Rows.Add(new TreeTableRow { Id = "engineering", Data = new Item("Engineering", "Sam") });
    }
    private sealed record Item(string Name, string Owner);
}
