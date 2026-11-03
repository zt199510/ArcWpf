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

    private static void Shown(PasswordStrength field, Action body)
    {
        var window = new System.Windows.Window { Width = 400, Height = 500, Content = field, ShowInTaskbar = false, WindowStyle = System.Windows.WindowStyle.None };
        try { window.Show(); window.UpdateLayout(); Sta.Pump(); body(); }
        finally { window.Close(); }
    }

    private static List<System.Windows.Controls.TextBlock> Words(PasswordStrength field) =>
        ((System.Windows.Controls.Grid)field.Template.FindName("PART_Word", field)!).Children.OfType<System.Windows.Controls.TextBlock>().ToList();

    // .word is flex 0 0 4.5rem beside a 12 DIP gap. The gap once came out of the 72 DIP column, leaving 60 for the word.
    [TestMethod] public void WordSlotKeepsTheLongestWordClearOfTheMeter() => Sta.Run(() =>
    {
        var field = new PasswordStrength { Width = 360, Password = "Ab1!" };
        Shown(field, () =>
        {
            var meter = (System.Windows.Controls.Grid)field.Template.FindName("PART_Meter", field)!;
            var slot = (System.Windows.Controls.Grid)field.Template.FindName("PART_Word", field)!;
            var word = Words(field)[^1];
            double Left(System.Windows.FrameworkElement part) => part.TransformToAncestor(field).Transform(new System.Windows.Point()).X;
            double meterEnd = Left(meter) + meter.ActualWidth;
            Assert.AreEqual("Too short", word.Text);
            Assert.AreEqual(72, slot.ActualWidth, .01, "slot width");
            Assert.AreEqual(12, Left(slot) - meterEnd, .01, "gap");
            Assert.AreEqual(360, Left(slot) + slot.ActualWidth, .01, "the word stays flush with the field's edge");
            Assert.IsGreaterThanOrEqualTo(meterEnd + 12, Left(word), $"word {word.ActualWidth:F2} wide starts at {Left(word):F2}, meter ends at {meterEnd:F2}");
        });
    });

    // PART_Word disables hit testing, which WPF forces onto its children, so looking the current word up by IsHitTestVisible never found it and each word stayed under the next.
    [TestMethod] public void OnlyTheCurrentWordRemains() => Sta.Run(() =>
    {
        var field = new PasswordStrength { Width = 360 };
        ArcMotion.SetLevel(field, ArcMotionLevel.Off);
        Shown(field, () =>
        {
            foreach (var (password, label) in new[] { ("Ab1!", "Too short"), ("abcdefgh", "Weak"), ("Abcdefgh1", "Fair"), ("Abcdefgh1!", "Good"), ("Juniper-Harbor-48", "Strong") })
            {
                field.Password = password;
                Sta.Pump();
                CollectionAssert.AreEqual(new[] { label }, Words(field).Select(word => word.Text).ToList(), password);
            }
            field.Password = "";
            Sta.Pump();
            Assert.IsEmpty(Words(field));
        });
    });

    [TestMethod] public void ControlExposesLevel() => Sta.Run(() =>
    {
        var field = new PasswordStrength { Password = "Copper-Lantern-73" };
        Assert.AreEqual(4, field.Level);
        Assert.AreEqual("Strong", field.StrengthLabel);
    });
}
