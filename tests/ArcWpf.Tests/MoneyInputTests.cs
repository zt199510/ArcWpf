using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class MoneyInputTests
{
    [TestMethod] public void DraftConvertsToMinorUnits()
    {
        Assert.AreEqual(1250L, MoneyMath.ToMinor("12.5", 2));
        Assert.AreEqual("60.00", MoneyMath.FromMinor(6000, 2));
        Assert.AreEqual("88000", MoneyMath.FromMinor(88000, 0));
        Assert.IsNull(MoneyMath.ToMinor("", 2));
    }

    [TestMethod] public void SanitizeStripsLeadingZerosAndKeepsCaret()
    {
        var result = MoneyMath.Sanitize("0012", 4, 2);
        Assert.AreEqual("12", result!.Value.Draft);
        Assert.AreEqual(2, result.Value.Before);
        Assert.AreEqual("0.5", MoneyMath.Sanitize(".5", 2, 2)!.Value.Draft);
        Assert.IsNull(MoneyMath.Sanitize("1.234", 5, 2));
    }

    [TestMethod] public void DisplayGroupsAndPartsKeepGhostCents()
    {
        Assert.AreEqual("1,234.5", MoneyMath.Display("1234.5"));
        var parts = MoneyMath.Parts("12", 2);
        CollectionAssert.AreEqual(new[] { "i1", "i0", "d", "f0", "f1" }, parts.Select(p => p.Key).ToArray());
        Assert.IsTrue(parts[2].Ghost && parts[4].Ghost);
    }

    [TestMethod] public void CurrencyFormatsMatchEnUs()
    {
        Assert.AreEqual("$2,480.00", MoneyMath.Currency("USD").Format(248000));
        Assert.AreEqual("¥88,000", MoneyMath.Currency("JPY").Format(88000));
        Assert.AreEqual("+$10", "+" + MoneyMath.Currency("USD").FormatWhole(10));
    }

    [TestMethod] public void PasteReadsBothSeparatorStyles()
    {
        Assert.AreEqual("1234.56", MoneyMath.ParsePaste("1.234,56 €", 2));
        Assert.AreEqual("1234.5", MoneyMath.ParsePaste("$1,234.5", 2));
        Assert.AreEqual("1234", MoneyMath.ParsePaste("1,234.56", 0));
    }

    [TestMethod] public void ConvertDraftKeepsMajorAmount()
    {
        Assert.AreEqual("60", MoneyMath.ConvertDraft("60.00", 0));
        Assert.AreEqual("60.00", MoneyMath.Settle(MoneyMath.ConvertDraft("60", 2), 2));
    }
}
