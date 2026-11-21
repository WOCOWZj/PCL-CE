using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Region;

namespace PCL.Core.Test;

[TestClass]
public class RegionPolicyTest
{
    [TestMethod]
    public void ChinaTimeZoneAndChinaRegionDoNotRestrict()
    {
        var result = RegionPolicy.Evaluate(Signals("CN", "China Standard Time"));
        Assert.AreEqual(RegionClassification.Other, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    [TestMethod]
    public void AnyRegionAndTimeZoneCombinationDoesNotRestrict()
    {
        var cases = new[]
        {
            ("CN", "Tokyo Standard Time"),
            ("US", "China Standard Time"),
            ("JP", "Tokyo Standard Time"),
            ("156", "Asia/Shanghai"),
            ("CN", "PRC")
        };

        foreach (var (geo, timeZone) in cases)
        {
            var result = RegionPolicy.Evaluate(Signals(geo, timeZone));
            Assert.AreEqual(RegionClassification.Other, result.Classification, $"geo={geo}, tz={timeZone}");
            Assert.IsFalse(result.IsRestricted, $"geo={geo}, tz={timeZone}");
        }
    }

    [TestMethod]
    public void MissingSignalsDoNotRestrict()
    {
        var result = RegionPolicy.Evaluate(new RegionSignals(null, false, null, false));
        Assert.IsFalse(result.IsRestricted);
    }

    [TestMethod]
    public void UnavailableRegionDoesNotRestrictEvenWithChinaTimeZone()
    {
        var result = RegionPolicy.Evaluate(new RegionSignals(null, false, "China Standard Time", true));
        Assert.IsFalse(result.IsRestricted);
    }

    private static RegionSignals Signals(string? geo, string? timeZone) =>
        new(geo, true, timeZone, true);
}
