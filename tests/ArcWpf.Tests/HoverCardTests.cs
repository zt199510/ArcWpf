using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class HoverCardTests
{
    [TestMethod] public void DelayPropertiesAreBindable() => Sta.Run(() => { var card = new HoverCard { OpenDelay = 200, CloseDelay = 100 }; Assert.AreEqual(200, card.OpenDelay); Assert.AreEqual(100, card.CloseDelay); });
    [TestMethod] public void DefaultSideIsBottom() => Sta.Run(() => Assert.AreEqual(HoverCardSide.Bottom, new HoverCard().Side));
    [TestMethod] public void IsOpenDefaultsFalse() => Sta.Run(() => Assert.IsFalse(new HoverCard().IsOpen));
}
