using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class RatingTests
{
    [TestMethod] public void ValueAndMaximumAreBindable() => Sta.Run(() => { var rating = new Rating { Value = 3, Maximum = 5 }; Assert.AreEqual(3d, rating.Value); Assert.AreEqual(5, rating.Maximum); });
    [TestMethod] public void MaximumIsCoerced() => Sta.Run(() => { var rating = new Rating { Maximum = 0 }; Assert.AreEqual(1, rating.Maximum); });
    [TestMethod] public void ReadOnlyCanBeEnabled() => Sta.Run(() => { var rating = new Rating { IsReadOnly = true }; Assert.IsTrue(rating.IsReadOnly); });
}
