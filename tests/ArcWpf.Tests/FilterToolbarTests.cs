using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class FilterToolbarTests
{
    [TestMethod] public void QueryAndFiltersAreBindable() => Sta.Run(() => { var toolbar = new FilterToolbar { Query = "open", HasActiveFilters = true }; toolbar.Filters.Add(new FilterToolbarFilter { Label = "Status", Value = "Open" }); Assert.AreEqual("open", toolbar.Query); Assert.HasCount(1, toolbar.Filters); });
    [TestMethod] public void ClearStateCanBeReset() => Sta.Run(() => { var toolbar = new FilterToolbar { HasActiveFilters = true }; toolbar.HasActiveFilters = false; Assert.IsFalse(toolbar.HasActiveFilters); });
    [TestMethod] public void EmptyToolbarStartsWithoutActiveFilters() => Sta.Run(() => Assert.IsFalse(new FilterToolbar().HasActiveFilters));
}
