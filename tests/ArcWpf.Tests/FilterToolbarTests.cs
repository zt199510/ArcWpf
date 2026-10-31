using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass]
public sealed class FilterToolbarTests
{
    private static FilterChip Chip(string id, string label, string? value = null) => new() { Id = id, Label = label, Value = value };

    [TestMethod]
    public void DescribeFollowsSource()
    {
        var status = Chip("status", "Status", "In review");
        var owner = Chip("owner", "Owner", "Emma Collins");
        Assert.AreEqual("Added Status: In review", FilterToolbar.Describe([], [status]));
        Assert.AreEqual("Status changed to Done", FilterToolbar.Describe([status], [Chip("status", "Status", "Done")]));
        Assert.AreEqual("Removed Owner: Emma Collins", FilterToolbar.Describe([status, owner], [status]));
        Assert.AreEqual("All filters cleared", FilterToolbar.Describe([status, owner], []));
    }

    [TestMethod]
    public void DefaultsFollowSource() => Sta.Run(() =>
    {
        var toolbar = new FilterToolbar();
        Assert.AreEqual("Active filters", toolbar.Label);
        Assert.AreEqual("Add filter", toolbar.AddFilterLabel);
        Assert.AreEqual(FilterMenuAlignment.End, toolbar.AddFilterAlignment);
        Assert.IsEmpty(toolbar.Filters);
    });

    [TestMethod]
    public void MenuPickReplacesSameFieldAndKeepsFieldOrder() => Sta.Run(() =>
    {
        var toolbar = new FilterToolbar();
        toolbar.Fields.Add(new FilterField { Id = "status", Label = "Status", Options = ["Todo", "Done"] });
        toolbar.Fields.Add(new FilterField { Id = "owner", Label = "Owner", Options = ["Emma"] });
        toolbar.Filters.Add(Chip("owner", "Owner", "Emma"));
        var menu = toolbar.Fields.Count > 0 ? (FilterMenu)typeof(FilterToolbar).GetField("_menu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(toolbar)! : null!;
        menu.RaiseEvent(new FilterChipEventArgs(FilterMenu.FilterSelectedEvent, menu, Chip("status", "Status", "Todo"), toolbar.Fields[0]));
        CollectionAssert.AreEqual(new[] { "status", "owner" }, toolbar.Filters.Select(filter => filter.Id).ToArray());
        menu.RaiseEvent(new FilterChipEventArgs(FilterMenu.FilterSelectedEvent, menu, Chip("status", "Status", "Done"), toolbar.Fields[0]));
        Assert.HasCount(2, toolbar.Filters);
        Assert.AreEqual("Done", toolbar.Filters[0].Value);
    });
}
