using VpnTraffic.Localization;
using VpnTraffic.Services;

namespace VpnTraffic;

/// <summary>
/// CmdPal Title is the large primary line. Always put quota metrics first;
/// airport name goes to Subtitle.
/// </summary>
public static class DisplayFormat
{
    public static (string Title, string Subtitle) Dock(SubscriptionEntry entry, QuotaSnapshot snap)
    {
        if (snap.Percent is { } pct)
        {
            // Compact dock hides Subtitle — Title must carry the key numbers.
            var title = $"{pct:0}% · {Localizer.FormatBytes(snap.UsedBytes)}";
            var parts = new List<string> { entry.Name };
            if (snap.HasTotal)
            {
                parts.Add($"{Localizer.FormatBytes(snap.LeftBytes)} {Localizer.LeftLabel}");
            }

            if (snap.ExpireLocal is { } exp)
            {
                parts.Add(Localizer.ExpireInDays(Math.Max(0, (exp - DateTimeOffset.Now).Days)));
            }

            if (snap.Error is not null)
            {
                parts.Add("⚠ " + snap.Error);
            }

            return (title, string.Join(" · ", parts));
        }

        if (snap.Error is not null)
        {
            var shortErr = ShortError(snap.Error);
            return ($"⚠ {shortErr}", $"{entry.Name} · {ErrorHint(snap.Error)}");
        }

        return (entry.Name, Localizer.DockSubtitleNoConfig);
    }

    public static (string Title, string Subtitle) ListRow(SubscriptionEntry entry, QuotaSnapshot snap)
    {
        var (dockTitle, dockSub) = Dock(entry, snap);
        if (snap.Percent is { } pct && snap.HasTotal)
        {
            // List can show used/total on the large line.
            return (
                $"{pct:0}% · {Localizer.FormatBytes(snap.UsedBytes)} / {Localizer.FormatBytes(snap.TotalBytes)}",
                dockSub);
        }

        return (dockTitle, dockSub);
    }

    public static string ShortError(string error) => error switch
    {
        "http-403" => "403",
        "no-userinfo" => "no-userinfo",
        "empty-url" => "—",
        "invalid-url" => "bad-url",
        "timeout" => "timeout",
        _ when error.StartsWith("http-") => error["http-".Length..],
        _ when error.Length <= 18 => error,
        _ => error[..15] + "…",
    };

    public static string ErrorHint(string error) => error switch
    {
        "http-403" => Localizer.Http403Hint,
        "no-userinfo" => Localizer.NoQuotaHeader,
        "empty-url" => Localizer.ConfigureHint,
        _ => error,
    };
}
