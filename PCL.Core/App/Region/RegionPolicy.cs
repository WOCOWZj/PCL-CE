using System;
using PCL.Core.Platform;

namespace PCL.Core.App.Region;

public enum RegionClassification
{
    Unknown,
    Other,
    Mainland
}

public sealed record RegionSignals(
    string? GeoName,
    bool GeoNameAvailable,
    string? TimeZoneId,
    bool TimeZoneAvailable);

public sealed record RegionAssessment(RegionClassification Classification, string Reason)
{
    public bool IsRestricted => Classification == RegionClassification.Mainland;
}

public static class RegionPolicy
{
    /// <summary>
    /// 区域限制已关闭：无论时区与系统地区如何，都不再限制任何功能。
    /// 原实现会在「时区为中国大陆」且「系统地区为中国大陆（CN / 156）」时返回
    /// <see cref="RegionClassification.Mainland" />，进而禁止 Minecraft 下载、补全与整合包安装。
    /// </summary>
    public static RegionAssessment Evaluate(RegionSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        return new RegionAssessment(
            RegionClassification.Other,
            "region-restriction-disabled");
    }

    public static RegionSignals ReadWindowsSignals()
    {
        return ApplicationRuntime.Current.Platform.Region.ReadSignals();
    }
}