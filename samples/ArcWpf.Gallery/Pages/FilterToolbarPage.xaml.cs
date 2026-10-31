using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class FilterToolbarPage : UserControl
{
    // Arc filter-toolbar demo fields: lucide circle-dashed, user-round, calendar-days and signal-high.
    private static readonly Geometry Status = Geometry.Parse("M10.1,2.182 a10,10 0 0 1 3.8,0 M13.9,21.818 a10,10 0 0 1 -3.8,0 M17.609,3.721 a10,10 0 0 1 2.69,2.7 M2.182,13.9 a10,10 0 0 1 0,-3.8 M20.279,17.609 a10,10 0 0 1 -2.7,2.69 M21.818,10.1 a10,10 0 0 1 0,3.8 M3.721,6.391 a10,10 0 0 1 2.7,-2.69 M6.391,20.279 a10,10 0 0 1 -2.69,-2.7");
    private static readonly Geometry Owner = Geometry.Parse("M7,8 a5,5 0 1 0 10,0 a5,5 0 1 0 -10,0 M20,21 a8,8 0 0 0 -16,0");
    private static readonly Geometry Due = Geometry.Parse("M8,2 v3 M16,2 v3 M5,3 h14 a2,2 0 0 1 2,2 v14 a2,2 0 0 1 -2,2 h-14 a2,2 0 0 1 -2,-2 v-14 a2,2 0 0 1 2,-2 z M3,9 h18 M8,13 h0.01 M12,13 h0.01 M16,13 h0.01 M8,17 h0.01 M12,17 h0.01 M16,17 h0.01");
    private static readonly Geometry Priority = Geometry.Parse("M2,20 h0.01 M7,20 v-4 M12,20 v-8 M17,20 V8");

    public FilterToolbarPage()
    {
        InitializeComponent();
        Toolbar.Fields.Add(new FilterField { Id = "status", Label = "Status", Icon = Status, Options = [new() { Value = "Backlog", Hint = 8 }, new() { Value = "Todo", Hint = 14 }, new() { Value = "In progress", Hint = 6 }, new() { Value = "In review", Hint = 3 }, new() { Value = "Done", Hint = 21 }] });
        Toolbar.Fields.Add(new FilterField { Id = "owner", Label = "Owner", Icon = Owner, Options = [Person("Emma Collins"), Person("Marcus Johnson"), Person("Ava Mitchell"), Person("Sofia Ramirez")] });
        Toolbar.Fields.Add(new FilterField { Id = "due", Label = "Due date", Icon = Due, Options = ["Overdue", "Today", "This week", "Next week", "No due date"] });
        Toolbar.Fields.Add(new FilterField { Id = "priority", Label = "Priority", Icon = Priority, Options = ["Urgent", "High", "Medium", "Low"] });
        Toolbar.Filters.Add(new FilterChip { Id = "status", Label = "Status", Value = "In review" });
        Toolbar.Filters.Add(new FilterChip { Id = "owner", Label = "Owner", Value = "Emma Collins" });
    }

    // Owner options show a 20 DIP avatar (.filterDemoAvatar); photos are replaced by initials.
    private static FilterOption Person(string name) => new() { Value = name, Icon = new Avatar { DisplayName = name, Size = AvatarSize.Small, Width = 20, Height = 20 } };
}
