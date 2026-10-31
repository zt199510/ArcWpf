using System.Collections.ObjectModel;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ReorderableListTests
{
    [TestMethod] public void ItemsSourceAndCanReorderAreBindable() => Sta.Run(() => { var list = new ReorderableList { ItemsSource = new ObservableCollection<string>(["A", "B"]) }; Assert.HasCount(2, list.Items); list.CanReorder = false; Assert.IsFalse(list.CanReorder); });
    [TestMethod] public void EmptyListStartsWithNoSelection() => Sta.Run(() => Assert.AreEqual(-1, new ReorderableList().SelectedIndex));
    [TestMethod] public void LabelItemsCanBeDisplayed() => Sta.Run(() => { var list = new ReorderableList { ItemsSource = new ObservableCollection<string>(["Release"]) }; Assert.AreEqual("Release", list.Items[0]); });
}
