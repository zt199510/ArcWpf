using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class PhoneInputTests
{
    private static PhoneCountry Iso(string iso) => PhoneNumbers.ByIso(iso)!;

    [TestMethod] public void FormatsAsTypedWithoutDanglingSeparators()
    {
        Assert.AreEqual("(201) 5", PhoneNumbers.FormatNational(Iso("US"), "2015"));
        Assert.AreEqual("(201) 555-0123", PhoneNumbers.FormatNational(Iso("US"), "2015550123"));
        Assert.AreEqual("07400 123456", PhoneNumbers.FormatNational(Iso("GB"), "07400123456"));
    }

    [TestMethod] public void StatusFollowsPatternLengths()
    {
        Assert.AreEqual(PhoneStatus.Empty, PhoneNumbers.StatusOf(Iso("US"), ""));
        Assert.AreEqual(PhoneStatus.Incomplete, PhoneNumbers.StatusOf(Iso("US"), "201555"));
        Assert.AreEqual(PhoneStatus.Valid, PhoneNumbers.StatusOf(Iso("US"), "2015550123"));
        Assert.AreEqual("10 or 11", PhoneNumbers.LengthCopy(Iso("DE")));
    }

    [TestMethod] public void ParsesInternationalNumbers()
    {
        var uk = PhoneNumbers.Parse("+44 (0)7911 123456")!.Value;
        Assert.AreEqual("GB", uk.Country.Iso);
        Assert.AreEqual("7911123456", uk.National);
        Assert.AreEqual("CA", PhoneNumbers.Parse("+14165550123")!.Value.Country.Iso);
        Assert.AreEqual("US", PhoneNumbers.Parse("0012015550123")!.Value.Country.Iso);
        Assert.IsNull(PhoneNumbers.Parse("2015550123"));
    }

    [TestMethod] public void E164AndGuide()
    {
        Assert.AreEqual("+12015550123", PhoneNumbers.ToE164(Iso("US"), "12015550123"));
        Assert.AreEqual("+1 (201) 555-0123", PhoneNumbers.Format("+12015550123"));
        Assert.AreEqual("01) 555-0123", PhoneNumbers.Guide(Iso("US"), "2"));
    }

    [TestMethod] public void ControlReadsValueIntoCountry() => Sta.Run(() =>
    {
        var input = new PhoneInput { Value = "+447400123456" };
        input.ApplyTemplate();
        Assert.AreEqual("+447400123456", input.Value);
    });
}
