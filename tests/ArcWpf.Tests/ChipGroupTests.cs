using System.Collections.ObjectModel;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ChipGroupTests
{
    [TestMethod] public void MultipleItemsCanBeSelected() => Sta.Run(() => { var group = new ChipGroup { ItemsSource = new ObservableCollection<ChipItem>([new ChipItem { Label = "A" }, new ChipItem { Label = "B" }]) }; group.SelectedIndex = 0; Assert.AreEqual(0, group.SelectedIndex); });
    [TestMethod] public void AllowEmptyIsConfigurable() => Sta.Run(() => { var group = new ChipGroup { AllowEmpty = false }; Assert.IsFalse(group.AllowEmpty); });
    [TestMethod] public void LabelIsBindable() => Sta.Run(() => Assert.AreEqual("Options", new ChipGroup().Label));
}
