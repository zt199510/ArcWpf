using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class MoneyInputTests
{
    [TestMethod] public void ValueIsClampedToBounds() => Sta.Run(() => { var input = new MoneyInput { Minimum = 0, Maximum = 100 }; input.Value = 150; Assert.AreEqual(150m, input.Value); });
    [TestMethod] public void CurrencyAndDigitsAreBindable() => Sta.Run(() => { var input = new MoneyInput { CurrencySymbol = "€", DecimalDigits = 0 }; Assert.AreEqual("€", input.CurrencySymbol); Assert.AreEqual(0, input.DecimalDigits); });
    [TestMethod] public void DefaultValueIsNull() => Sta.Run(() => Assert.IsNull(new MoneyInput().Value));
}
