using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class PasswordStrengthTests
{
    [TestMethod] public void EmptyPasswordIsLevelZero() => Assert.AreEqual(0, PasswordStrength.EstimateStrength("").Level);

    [TestMethod] public void ShortPasswordHoldsAtFirstStep()
    {
        var result = PasswordStrength.EstimateStrength("Ab1!");
        Assert.AreEqual(1, result.Level);
        Assert.AreEqual("Too short", result.Label);
        Assert.IsTrue(result.Short);
    }

    [TestMethod] public void LevelsFollowShareOfRulesMet()
    {
        Assert.AreEqual("Weak", PasswordStrength.EstimateStrength("abcdefgh").Label);
        Assert.AreEqual("Weak", PasswordStrength.EstimateStrength("abcdefgh1").Label);
        Assert.AreEqual("Fair", PasswordStrength.EstimateStrength("Abcdefgh1").Label);
        Assert.AreEqual("Good", PasswordStrength.EstimateStrength("Abcdefgh1!").Label);
        Assert.AreEqual("Strong", PasswordStrength.EstimateStrength("Juniper-Harbor-48").Label);
    }

    [TestMethod] public void LengthRuleCountsCodePointsAndRemaining()
    {
        var length = PasswordStrength.DefaultRules[0];
        Assert.AreEqual(11, length.Remaining!("😀"));
        Assert.AreEqual(1, PasswordStrength.CountCharacters("😀"));
    }

    // The editors once pointed at PasswordInput's string-keyed styles through DynamicResource, which a theme dictionary cannot resolve,
    // so both fell back to WPF's bordered default boxes inside the shell.
    [TestMethod] public void EditorsUseTheBareTemplate() => Sta.Run(() =>
    {
        var field = new PasswordStrength();
        Assert.IsTrue(field.ApplyTemplate());
        foreach (var name in new[] { "PART_PasswordBox", "PART_RevealBox" })
        {
            var editor = (System.Windows.Controls.Control)field.Template.FindName(name, field)!;
            Assert.AreEqual(new System.Windows.Thickness(0), editor.BorderThickness, name);
            Assert.AreEqual(System.Windows.Media.Colors.Transparent, ((System.Windows.Media.SolidColorBrush)editor.Background).Color, name);
        }
    });

    [TestMethod] public void ControlExposesLevel() => Sta.Run(() =>
    {
        var field = new PasswordStrength { Password = "Copper-Lantern-73" };
        Assert.AreEqual(4, field.Level);
        Assert.AreEqual("Strong", field.StrengthLabel);
    });
}
