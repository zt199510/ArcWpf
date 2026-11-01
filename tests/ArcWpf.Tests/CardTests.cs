using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class CardTests
{
    [TestMethod] public void CardContentPropertiesAreBindable() => Sta.Run(() => { var card = new Card { Title = "Title", Description = "Description", Content = "Body" }; Assert.AreEqual("Title", card.Title); Assert.AreEqual("Body", card.Content); });
    [TestMethod] public void MetricValuesAreBindable() => Sta.Run(() => { var card = new MetricCard { Value = "42", Trend = "+5%" }; Assert.AreEqual("42", card.Value); Assert.AreEqual("+5%", card.Trend); });
    [TestMethod] public void MetricTrendDefaultsPositive() => Sta.Run(() => Assert.IsTrue(new MetricCard().TrendIsPositive));
}
