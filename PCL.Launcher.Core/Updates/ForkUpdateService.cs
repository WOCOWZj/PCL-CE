using System.Text.Json.Nodes;
using PCL.Core.Logging;
using PCL.Network;

namespace PCL.Launcher.Core.Updates;

/// <summary>本 fork（WOCOWZj 改版）的发布信息。</summary>
/// <param name="Tag">发布标签，例如 <c>v2.15.1-wocowzj.1</c>。</param>
/// <param name="Name">发布标题。</param>
/// <param name="Body">发布说明（Markdown）。</param>
/// <param name="PublishedAt">发布时间（UTC）。</param>
/// <param name="HtmlUrl">发布页地址。</param>
/// <param name="ExeUrl">单文件自包含版下载地址，没有时为 <c>null</c>。</param>
/// <param name="ZipUrl">框架依赖版压缩包下载地址，没有时为 <c>null</c>。</param>
public sealed record ForkReleaseInfo(
    string Tag,
    string Name,
    string Body,
    DateTimeOffset PublishedAt,
    string HtmlUrl,
    string? ExeUrl,
    string? ZipUrl);

/// <summary>
/// 检查本 fork 的 GitHub Releases，用于在上游更新检查之外额外提示「改版」的新版本。
/// 只读取公开 API，不需要凭据；失败时返回 <c>null</c> 而不是抛异常，
/// 以免改版更新检查失败影响上游更新检查。
/// </summary>
public static class ForkUpdateService
{
    private const string Owner = "WOCOWZj";
    private const string Repository = "PCL-CE";

    private const string ApiUrl =
        "https://api.github.com/repos/" + Owner + "/" + Repository + "/releases?per_page=20";

    private const string ReleasesPageUrl =
        "https://github.com/" + Owner + "/" + Repository + "/releases";

    /// <summary>发布页地址，供「打开全部发布」使用。</summary>
    public static string AllReleasesUrl => ReleasesPageUrl;

    /// <summary>
    /// 获取本 fork 最新的正式发布（忽略草稿与预发布）。
    /// 网络失败、限流或返回结构异常时返回 <c>null</c>。
    /// </summary>
    public static async Task<ForkReleaseInfo?> GetLatestReleaseAsync()
    {
        try
        {
            var json = await Requester
                .FetchJsonAsync(
                    ApiUrl,
                    new RequestParam
                    {
                        Accept = "application/vnd.github+json",
                        UseBrowserUserAgent = true,
                        Timeout = 15000,
                        Retries = 2
                    })
                .ConfigureAwait(false);

            if (json is not JsonArray list) return null;

            ForkReleaseInfo? latest = null;
            foreach (var node in list)
            {
                if (node is not JsonObject release) continue;
                if (release["draft"]?.GetValue<bool>() ?? false) continue;
                if (release["prerelease"]?.GetValue<bool>() ?? false) continue;

                var tag = release["tag_name"]?.ToString();
                if (string.IsNullOrWhiteSpace(tag)) continue;

                var publishedAt = DateTimeOffset.TryParse(
                    release["published_at"]?.ToString() ?? release["created_at"]?.ToString(),
                    out var parsed)
                    ? parsed
                    : DateTimeOffset.MinValue;

                var info = new ForkReleaseInfo(
                    tag,
                    release["name"]?.ToString() ?? tag,
                    release["body"]?.ToString() ?? string.Empty,
                    publishedAt,
                    release["html_url"]?.ToString() ?? ReleasesPageUrl,
                    _FindAssetUrl(release, ".exe"),
                    _FindAssetUrl(release, ".zip"));

                if (latest is null || info.PublishedAt > latest.PublishedAt)
                    latest = info;
            }

            return latest;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "ForkUpdate", "获取改版更新信息失败");
            return null;
        }
    }

    /// <summary>
    /// 抽取版本号本身：<c>v2.15.1-wocowzj.1</c> → <c>2.15.1</c>，
    /// <c>v2.15.1-wocowzj.6300bfe</c> → <c>2.15.1</c>。
    /// </summary>
    public static string ParseVersion(string tag)
    {
        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V')) text = text[1..];
        var dash = text.IndexOf('-');
        return dash > 0 ? text[..dash] : text;
    }

    /// <summary>
    /// 判断是否有比 <paramref name="currentVersion" /> 更新的改版发布。
    /// 版本相同时按「发布时间晚于安装时间」判定，即同版本重新构建过也算更新。
    /// </summary>
    public static bool IsUpdateAvailable(
        ForkReleaseInfo release,
        string? currentVersion,
        DateTimeOffset? installedAt)
    {
        if (string.IsNullOrWhiteSpace(currentVersion)) return true;

        var comparison = CompareVersions(ParseVersion(release.Tag), currentVersion);
        if (comparison > 0) return true;
        if (comparison < 0) return false;

        // 版本号相同：只有明确知道安装时间、且发布更晚时才提示，
        // 避免用户在用本地自编译版本时被反复提示「有更新」。
        return installedAt is { } installed && release.PublishedAt > installed.AddMinutes(1);
    }

    private static int CompareVersions(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        var length = Math.Max(leftParts.Length, rightParts.Length);
        for (var i = 0; i < length; i++)
        {
            var leftValue = i < leftParts.Length && int.TryParse(leftParts[i], out var l) ? l : 0;
            var rightValue = i < rightParts.Length && int.TryParse(rightParts[i], out var r) ? r : 0;
            if (leftValue != rightValue) return leftValue.CompareTo(rightValue);
        }

        return 0;
    }

    private static string? _FindAssetUrl(JsonObject release, string extension)
    {
        if (release["assets"] is not JsonArray assets) return null;
        foreach (var asset in assets)
        {
            if (asset is not JsonObject item) continue;
            var name = item["name"]?.ToString();
            if (name?.EndsWith(extension, StringComparison.OrdinalIgnoreCase) != true) continue;
            var url = item["browser_download_url"]?.ToString();
            if (!string.IsNullOrWhiteSpace(url)) return url;
        }

        return null;
    }
}
