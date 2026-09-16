using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using VpnTraffic.Localization;
using VpnTraffic.Services;

namespace VpnTraffic.Pages;

public sealed class QuotaListPage : ListPage
{
    private readonly QuotaService _service;
    private readonly List<IListItem> _items = [];

    public QuotaListPage(QuotaService service)
    {
        _service = service;
        Name = Localizer.AppName;
        Title = Localizer.AppName;
        Icon = new IconInfo("\uE968");
        ShowDetails = true;
        _service.Updated += OnUpdated;
        Rebuild();
    }

    public override IListItem[] GetItems()
    {
        lock (_items)
        {
            return _items.ToArray();
        }
    }

    private void OnUpdated(object? sender, QuotaSnapshot e)
    {
        Rebuild();
        try
        {
            RaiseItemsChanged();
        }
        catch
        {
            // Host not attached.
        }
    }

    private void Rebuild()
    {
        lock (_items)
        {
            _items.Clear();
            var snap = _service.Current;
            var cfg = _service.Settings;

            if (string.IsNullOrWhiteSpace(cfg.SubscriptionUrl))
            {
                _items.Add(StaticItem(Localizer.DockSubtitleNoConfig, Localizer.ConfigureHint));
                return;
            }

            if (!string.IsNullOrEmpty(snap.ProfileTitle))
            {
                _items.Add(StaticItem(snap.ProfileTitle, "profile"));
            }

            if (snap.Error is not null)
            {
                var errText = snap.Error switch
                {
                    "no-userinfo" => Localizer.NoQuotaHeader,
                    "empty-url" => Localizer.ConfigureHint,
                    _ => snap.Error,
                };
                _items.Add(StaticItem($"{Localizer.ErrorPrefix}: {errText}",
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
                _ = _service.RefreshOnceAsync();
            })
            {
                Name = Localizer.RefreshNow,
                Result = CommandResult.KeepOpen(),
            })
            {
                Title = Localizer.RefreshNow,
                Subtitle = $"{Localizer.LastUpdated}: {(snap.FetchedAt == default ? Localizer.Never : snap.FetchedAt.ToLocalTime().ToString("HH:mm:ss"))}",
                Details = BuildDetails(snap, cfg),
            });

            if (cfg.ShowHistoryChart)
            {
                var points = _service.History.Snapshot();
                var chart = ChartRenderer.Render(points);
                _items.Add(new ListItem(new AnonymousCommand(() => { })
                {
                    Name = Localizer.HistoryTitle,
                    Result = CommandResult.KeepOpen(),
                })
                {
                    Title = Localizer.HistoryTitle,
                    Subtitle = points.Count == 0 ? Localizer.Never : $"{points.Count} pts",
                    Details = new Details
                    {
                        Title = Localizer.HistoryTitle,
                        Body = "```\n" + chart + "\n```",
                    },
                });
            }
        }
    }

    private static Details BuildDetails(QuotaSnapshot snap, AppSettings cfg)
    {
        _ = cfg;
        var body = new System.Text.StringBuilder();
        if (snap.Percent is { } p)
        {
            body.Append(ChartRenderer.MiniBar(p, 20)).Append(' ').Append(p.ToString("0.#")).Append("%\n");
        }

        body.Append(Localizer.UsedLabel).Append(": ").Append(Localizer.FormatBytes(Math.Max(0, snap.UsedBytes))).Append('\n');
        if (snap.HasTotal)
        {
            body.Append(Localizer.TotalLabel).Append(": ").Append(Localizer.FormatBytes(snap.TotalBytes)).Append('\n');
            body.Append(Localizer.LeftLabel).Append(": ").Append(Localizer.FormatBytes(snap.LeftBytes)).Append('\n');
        }

        return new Details
        {
            Title = Localizer.AppName,
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
