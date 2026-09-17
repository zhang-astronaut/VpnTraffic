using System.Globalization;

namespace VpnTraffic.Localization;

public static class Localizer
{
    private static readonly bool IsZh = DetectZh();

    private static bool DetectZh()
    {
        try
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return lang.Equals("zh", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string AppName => IsZh ? "VPN 流量" : "VpnTraffic";
    public static string DockSubtitleNoConfig => IsZh ? "未配置订阅" : "Not configured";
    public static string UsedLabel => IsZh ? "已用" : "Used";
    public static string TotalLabel => IsZh ? "总量" : "Total";
    public static string LeftLabel => IsZh ? "剩余" : "Left";
    public static string PercentLabel => IsZh ? "占比" : "Usage";
    public static string ExpireLabel => IsZh ? "到期" : "Expires";
    public static string Never => IsZh ? "从未" : "Never";
    public static string Unknown => IsZh ? "未知" : "Unknown";
    public static string NoQuotaHeader => IsZh
        ? "响应中无 subscription-userinfo（已尝试 Clash/ClashMeta 等 UA；请确认链接为订阅地址）"
        : "No subscription-userinfo header (tried Clash-like UAs; verify this is a subscription URL)";
    public static string ConfigureHint => IsZh
        ? "选择「添加订阅」填写名称与链接"
        : "Use Add subscription to enter name and URL";
    public static string AddSubscription => IsZh ? "添加订阅" : "Add subscription";
    public static string RemoveSubscription => IsZh ? "删除订阅" : "Remove subscription";
    public static string NameLabel => IsZh ? "名称" : "Name";
    public static string UrlLabel => IsZh ? "订阅链接" : "Subscription URL";
    public static string Saved => IsZh ? "已保存" : "Saved";
    public static string Removed => IsZh ? "已删除" : "Removed";
    public static string InvalidUrl => IsZh ? "链接无效（需 http/https）" : "Invalid URL (http/https required)";
    public static string DuplicateEntry => IsZh ? "已存在相同订阅链接" : "This subscription URL already exists";
    public static string Http403Hint => IsZh
        ? "HTTP 403：站点拒绝访问。请确认这是机场面板里的「订阅链接」（不是节点/搜索页），或换用 Clash 订阅地址"
        : "HTTP 403: site refused. Use the airport subscription URL (not a node/search page)";
    public static string RefreshNow => IsZh ? "立即刷新" : "Refresh now";
    public static string HistoryTitle => IsZh ? "用量时间线" : "Usage timeline";
    public static string LastUpdated => IsZh ? "上次更新" : "Updated";
    public static string OpenSettings => IsZh ? "打开设置" : "Open settings";
    public static string ErrorPrefix => IsZh ? "错误" : "Error";
    public static string ExpireInDays(int days) => IsZh ? $"{days} 天后" : $"in {days}d";

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return IsZh ? "未知" : "unknown";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.#} {units[unit]}";
    }
}
