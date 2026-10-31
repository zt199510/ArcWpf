using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class PhoneInputTests
{
    [TestMethod] public void CountryCodeAndValueAreBindable() => Sta.Run(() => { var input = new PhoneInput { CountryCode = "+86", Value = "13800138000" }; Assert.AreEqual("+86", input.CountryCode); Assert.AreEqual("13800138000", input.Value); });
    [TestMethod] public void EmptyPhoneIsInvalid() => Sta.Run(() => Assert.IsFalse(new PhoneInput().IsValid));
    [TestMethod] public void PlaceholderIsConfigurable() => Sta.Run(() => { var input = new PhoneInput { Placeholder = "手机号" }; Assert.AreEqual("手机号", input.Placeholder); });
}
