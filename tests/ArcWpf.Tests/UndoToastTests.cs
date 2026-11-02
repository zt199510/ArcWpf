using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class UndoToastTests
{
    [TestMethod] public void RemoveCreatesUndoBatch() => Sta.Run(() => { var c = new UndoToast(); c.Remove("one", "archive"); c.Remove("two", "delete"); Assert.AreEqual(2, c.Pending.Count); Assert.IsTrue(c.UndoAvailable); });
    [TestMethod] public void UndoRestoresAndClearsBatch() => Sta.Run(() => { var c = new UndoToast(); var restored = 0; c.ItemsRestored += (_, e) => restored = e.Count; c.Remove("one"); c.Undo(); Assert.AreEqual(1, restored); Assert.AreEqual(0, c.Pending.Count); Assert.IsFalse(c.UndoAvailable); });
}
