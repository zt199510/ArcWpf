using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ChipGroupTests
{
    private static ChipGroup Topics(bool multiple) => new() { Multiple = multiple, ItemsSource = new[] { "Typography", "Color", "Motion" } };

    [TestMethod] public void MultipleToggleAddsInOptionOrder() => Sta.Run(() =>
    {
        var group = Topics(true);
        group.Toggle("Motion");
        group.Toggle("Typography");
        CollectionAssert.AreEqual(new[] { "Typography", "Motion" }, group.SelectedValues!.ToArray());
    });

    [TestMethod] public void SingleToggleReplacesAndClears() => Sta.Run(() =>
    {
        var group = Topics(false);
        group.Toggle("Color");
        group.Toggle("Motion");
        CollectionAssert.AreEqual(new[] { "Motion" }, group.SelectedValues!.ToArray());
        group.Toggle("Motion");
        Assert.HasCount(0, group.SelectedValues!);
    });

    [TestMethod] public void ToggleRaisesSelectedValuesChanged() => Sta.Run(() =>
    {
        var group = Topics(true);
        int raised = 0;
        group.SelectedValuesChanged += (_, _) => raised++;
        group.Toggle("Color");
        Assert.AreEqual(1, raised);
    });
}
