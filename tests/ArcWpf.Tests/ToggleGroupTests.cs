using ArcWpf.Controls;
using System.Windows;
namespace ArcWpf.Tests;
[TestClass] public sealed class ToggleGroupTests
{
    [TestMethod] public void SingleGroupKeepsOneSelection() => Sta.Run(() => { var g = new ToggleGroup { Type = ToggleGroupType.Single }; var a = new ToggleGroupItem { Value = "a" }; var b = new ToggleGroupItem { Value = "b" }; g.Items.Add(a); g.Items.Add(b); a.IsSelected = true; b.IsSelected = true; Assert.IsFalse(a.IsSelected); Assert.IsTrue(b.IsSelected); });
    [TestMethod] public void MultipleGroupAllowsIndependentSelection() => Sta.Run(() => { var g = new ToggleGroup { Type = ToggleGroupType.Multiple }; var a = new ToggleGroupItem { Value = "a" }; var b = new ToggleGroupItem { Value = "b" }; g.Items.Add(a); g.Items.Add(b); a.IsSelected = true; b.IsSelected = true; Assert.IsTrue(a.IsSelected); Assert.IsTrue(b.IsSelected); });
}
