using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using VpnTraffic.Localization;
using VpnTraffic.Services;

namespace VpnTraffic.Pages;

public sealed class QuotaListPage : ListPage
{
    private readonly VpnTrafficCommandsProvider _provider;
    private readonly List<IListItem> _items = [];

    public QuotaListPage(VpnTrafficCommandsProvider provider)
    {
        _provider = provider;
        Name = Localizer.AppName;
        Title = Localizer.AppName;
        Icon = new IconInfo("\uE968");
        ShowDetails = true;
        Rebuild();
    }

    public override IListItem[] GetItems()
    {
        lock (_items)
        {
            return _items.ToArray();
        }
    }

    public void Rebuild()
    {
        lock (_items)
        {
            _items.Clear();
            var runtimes = _provider.Runtimes;
            var global = _provider.GlobalSettings;

            if (runtimes.Count == 0)
            {
                _items.Add(StaticItem(
                    Localizer.DockSubtitleNoConfig,
                    Localizer.ConfigureHint));
            }
            else
            {
                foreach (var rt in runtimes)
                {
                    var snap = rt.Service.Current;
                    AddSubscriptionItems(rt.Entry, snap, global, rt);
                }
            }
        }

        try
        {
            RaiseItemsChanged();
        }
        catch
        {
            // host may not be attached
        }
    }

    private void AddSubscriptionItems(
        SubscriptionEntry entry,
        QuotaSnapshot snap,
        AppSettings global,
        SubscriptionRuntime rt)
    {
        _items.Add(StaticItem(entry.Name, TrimUrl(entry.Url)));

        if (snap.Error is not null)
        {
            var errText = snap.Error switch
            {
                "no-userinfo" => Localizer.NoQuotaHeader,
                "empty-url" => Localizer.ConfigureHint,
                _ => snap.Error,
            };
            _items.Add(StaticItem(
                $"{Localizer.ErrorPrefix}: {errText}",
                snap.FetchedAt == default
                    ? string.Empty
                    : $"{Localizer.LastUpdated} {snap.FetchedAt.ToLocalTime():HH:mm:ss}"));
        }

        if (snap.HasUsed)
        {
            _items.Add(StaticItem(
                $"{Localizer.UsedLabel}: {Localizer.FormatBytes(snap.UsedBytes)}",
                snap.Percent is { } p ? $"{ChartRenderer.MiniBar(p)} {p:0}%" : Localizer.Unknown));
        }

        if (snap.HasTotal)
        {
            _items.Add(StaticItem(
                $"{Localizer.TotalLabel}: {Localizer.FormatBytes(snap.TotalBytes)}",
                $"{Localizer.LeftLabel}: {Localizer.FormatBytes(snap.LeftBytes)}"));
        }

        if (snap.ExpireLocal is { } expire)
        {
            var days = (expire - DateTimeOffset.Now).Days;
            _items.Add(StaticItem(
                $"{Localizer.ExpireLabel}: {expire:yyyy-MM-dd}",
                Localizer.ExpireInDays(Math.Max(0, days))));
        }

        _items.Add(new ListItem(new AnonymousCommand(() =>
        {
            _ = rt.Service.RefreshOnceAsync();
        })
        {
            Name = $"{Localizer.RefreshNow} ({entry.Name})",
            Result = CommandResult.KeepOpen(),
        })
        {
            Title = $"{Localizer.RefreshNow} · {entry.Name}",
            Subtitle = $"{Localizer.LastUpdated}: {(snap.FetchedAt == default ? Localizer.Never : snap.FetchedAt.ToLocalTime().ToString("HH:mm:ss"))}",
            Details = BuildDetails(entry, snap),
        });

        if (global.ShowHistoryChart)
        {
            var points = rt.Service.History.Snapshot();
            var chart = ChartRenderer.Render(points);
            _items.Add(new ListItem(new AnonymousCommand(() => { })
            {
                Name = $"{Localizer.HistoryTitle} · {entry.Name}",
                Result = CommandResult.KeepOpen(),
            })
            {
                Title = $"{Localizer.HistoryTitle} · {entry.Name}",
                Subtitle = points.Count == 0 ? Localizer.Never : $"{points.Count} pts",
                Details = new Details
                {
                    Title = entry.Name,
                    Body = "```\n" + chart + "\n```",
                },
            });
        }
    }

    private static string TrimUrl(string url)
    {
        if (url.Length <= 48)
        {
            return url;
        }

        return url[..45] + "...";
    }

    private static Details BuildDetails(SubscriptionEntry entry, QuotaSnapshot snap)
    {
        var body = new System.Text.StringBuilder();
        body.Append(entry.Name).Append('\n').Append(entry.Url).Append("\n\n");
        if (snap.Percent is { } p)
        {
            body.Append(ChartRenderer.MiniBar(p, 20)).Append(' ').Append(p.ToString("0.#")).Append("%\n");
        }

        body.Append(Localizer.UsedLabel).Append(": ")
            .Append(Localizer.FormatBytes(Math.Max(0, snap.UsedBytes))).Append('\n');
        if (snap.HasTotal)
        {
            body.Append(Localizer.TotalLabel).Append(": ")
                .Append(Localizer.FormatBytes(snap.TotalBytes)).Append('\n');
            body.Append(Localizer.LeftLabel).Append(": ")
                .Append(Localizer.FormatBytes(snap.LeftBytes)).Append('\n');
        }

        return new Details
        {
            Title = entry.Name,
            Body = body.ToString(),
        };
    }

    private static ListItem StaticItem(string title, string subtitle) =>
        new(new AnonymousCommand(() => { })
        {
            Name = title,
            Result = CommandResult.KeepOpen(),
        })
        {
            Title = title,
            Subtitle = subtitle,
        };
}
