using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class PasswordStrengthTests
{
    [TestMethod] public void StrongPasswordScoresFour() => Sta.Run(() => { var strength = new PasswordStrength { Password = "Abc123!@" }; Assert.AreEqual(4, strength.Score); Assert.AreEqual("Strong", strength.StrengthLabel); });
    [TestMethod] public void EmptyPasswordScoresZero() => Sta.Run(() => Assert.AreEqual(0, new PasswordStrength().Score));
    [TestMethod] public void RulesCanBeHidden() => Sta.Run(() => { var strength = new PasswordStrength { ShowRules = false }; Assert.IsFalse(strength.ShowRules); });
}
