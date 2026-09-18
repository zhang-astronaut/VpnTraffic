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

            var addPage = new AddSubscriptionPage(_provider);
            _items.Add(new ListItem(new AnonymousCommand(() => { })
            {
                Name = Localizer.AddSubscription,
                Result = CommandResult.GoToPage(new GoToPageArgs
                {
                    PageId = addPage.Id,
                }),
            })
            {
                Title = Localizer.AddSubscription,
                Subtitle = Localizer.UrlLabel,
            });

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
        var remove = new AnonymousCommand(() =>
        {
            _provider.TryRemoveSubscription(entry.Id, out _);
            Rebuild();
        })
        {
            Name = Localizer.RemoveSubscription,
            Result = CommandResult.KeepOpen(),
        };

        var (primary, secondary) = DisplayFormat.ListRow(entry, snap);

        _items.Add(new ListItem(new AnonymousCommand(() => { })
        {
            Name = primary,
            Result = CommandResult.KeepOpen(),
        })
        {
            Title = primary,
            Subtitle = secondary,
            MoreCommands = [new CommandContextItem(remove)],
            Details = BuildDetails(entry, snap),
        });

        if (snap.Error is not null)
        {
            // Title shows metrics when quota exists; error lives on Subtitle + this hint row.
            _items.Add(StaticItem(
                DisplayFormat.ErrorHint(snap.Error),
                $"{Localizer.LastUpdated} {(snap.FetchedAt == default ? Localizer.Never : snap.FetchedAt.ToLocalTime().ToString("HH:mm:ss"))}"));
        }

        if (snap.HasUsed && snap.Percent is { } p)
        {
            _items.Add(StaticItem(
                $"{ChartRenderer.MiniBar(p, 12)} {p:0}%",
                $"{Localizer.UsedLabel} {Localizer.FormatBytes(snap.UsedBytes)}"
                    + (snap.HasTotal ? $" / {Localizer.FormatBytes(snap.TotalBytes)}" : string.Empty)));
        }

        if (snap.ExpireLocal is { } expire)
        {
            var days = (expire - DateTimeOffset.Now).Days;
            _items.Add(StaticItem(
                $"{Localizer.ExpireLabel} {expire:yyyy-MM-dd}",
                Localizer.ExpireInDays(Math.Max(0, days))));
        }

        _items.Add(new ListItem(new AnonymousCommand(() =>
        {
            _ = rt.Service.RefreshOnceAsync();
        })
        {
            Name = $"{Localizer.RefreshNow} · {entry.Name}",
            Result = CommandResult.KeepOpen(),
        })
        {
            Title = $"{Localizer.RefreshNow} · {primary}",
            Subtitle = $"{entry.Name} · {(snap.FetchedAt == default ? Localizer.Never : snap.FetchedAt.ToLocalTime().ToString("HH:mm:ss"))}",
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
                Title = $"{Localizer.HistoryTitle} · {primary}",
                Subtitle = $"{entry.Name} · {(points.Count == 0 ? Localizer.Never : points.Count + " pts")}",
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
