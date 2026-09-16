using System.Text;
using VpnTraffic.Localization;

namespace VpnTraffic.Services;

public static class ChartRenderer
{
    public static string Render(IReadOnlyList<HistoryPoint> points, int width = 16)
    {
        if (points.Count == 0)
        {
            return Localizer.Never;
        }

        // Use percentages against the latest total when available, else against max used.
        var lastTotal = points[^1].TotalBytes > 0
            ? points[^1].TotalBytes
            : points.Max(static p => Math.Max(p.UsedBytes, 1));

        if (lastTotal <= 0)
        {
            lastTotal = 1;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var pct = Math.Clamp(p.UsedBytes * 100.0 / lastTotal, 0, 100);
            var filled = (int)Math.Round(pct / 100.0 * width);
            filled = Math.Clamp(filled, 0, width);
            var bar = new string('█', filled) + new string('░', width - filled);
            var time = p.Timestamp.ToLocalTime().ToString("HH:mm");
            sb.Append(time).Append(' ').Append(bar).Append(' ').Append(pct.ToString("0")).Append('%');
            if (i < points.Count - 1)
            {
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>Compact single-line bar for dock/list summary (default + compact docks).</summary>
    public static string MiniBar(double percent, int width = 10)
    {
        var filled = (int)Math.Round(Math.Clamp(percent, 0, 100) / 100.0 * width);
        filled = Math.Clamp(filled, 0, width);
        return new string('█', filled) + new string('░', width - filled);
    }
}
