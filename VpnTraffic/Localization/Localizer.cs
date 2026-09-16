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
    public static string ConfigureHint => IsZh ? "请先在设置中填写订阅链接" : "Set your subscription URL in settings";
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
