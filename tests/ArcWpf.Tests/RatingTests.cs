using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class RatingTests
{
    [TestMethod] public void ValueSnapsToWholeStars() => Sta.Run(() => { var rating = new Rating { Maximum = 5, Value = 3.4 }; Assert.AreEqual(3d, rating.Value); });
    [TestMethod] public void HalfStepsAreKeptWhenAllowed() => Sta.Run(() => { var rating = new Rating { Maximum = 5, AllowHalf = true, Value = 3.4 }; Assert.AreEqual(3.5, rating.Value); });
    [TestMethod] public void ValueIsClampedToMaximum() => Sta.Run(() => { var rating = new Rating { Maximum = 5, Value = 9 }; Assert.AreEqual(5d, rating.Value); });
    [TestMethod] public void LoweringMaximumRecoercesValue() => Sta.Run(() => { var rating = new Rating { Maximum = 5, Value = 5 }; rating.Maximum = 3; Assert.AreEqual(3d, rating.Value); });
}
