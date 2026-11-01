using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class UsageMeterTests
{
    [TestMethod] public void PercentageUsesUsedAndLimit() => Sta.Run(() => { var meter = new UsageMeter { Used = 75, Limit = 100 }; Assert.AreEqual(.75, meter.Percentage, .001); });
    [TestMethod] public void WarningThresholdIsApplied() => Sta.Run(() => { var meter = new UsageMeter { Used = 90, Limit = 100 }; Assert.IsTrue(meter.IsWarning); });
    [TestMethod] public void ZeroLimitProducesZeroPercentage() => Sta.Run(() => { var meter = new UsageMeter { Used = 10, Limit = 0 }; Assert.AreEqual(0d, meter.Percentage); });
}
