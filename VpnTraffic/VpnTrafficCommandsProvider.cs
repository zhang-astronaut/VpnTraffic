using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using VpnTraffic.Localization;
using VpnTraffic.Pages;
using VpnTraffic.Services;

namespace VpnTraffic;

public sealed class SubscriptionRuntime : IDisposable
{
    public SubscriptionEntry Entry { get; private set; }
    public QuotaService Service { get; } = new();
    public WrappedDockItem Dock { get; }
    public event EventHandler<QuotaSnapshot>? SnapshotUpdated;

    public SubscriptionRuntime(SubscriptionEntry entry, AppSettings global)
    {
        Entry = entry;
        var settings = global with { SubscriptionUrl = entry.Url };
        Service.ApplySettings(settings, AppPaths.HistoryFile(entry.Id), reloadHistory: true);
        Service.Updated += OnUpdated;
        Service.StartLoop();

        Dock = new WrappedDockItem(
            [BuildItem(QuotaSnapshot.Empty)],
            entry.Name,
            "quota:" + entry.Id)
        {
            Icon = new IconInfo("\uE968"),
        };
    }

    public void ApplyEntry(SubscriptionEntry entry, AppSettings global)
    {
        Entry = entry;
        Service.ApplySettings(
            global with { SubscriptionUrl = entry.Url },
            AppPaths.HistoryFile(entry.Id),
            reloadHistory: false);
        Dock.Items = [BuildItem(Service.Current)];
    }

    private void OnUpdated(object? sender, QuotaSnapshot snap)
    {
        Dock.Items = [BuildItem(snap)];
        SnapshotUpdated?.Invoke(this, snap);
    }

    private ListItem BuildItem(QuotaSnapshot snap)
    {
        string title;
        string subtitle;
        if (snap.Percent is { } pct)
        {
            title = $"{Entry.Name} · {pct:0}% · {Localizer.FormatBytes(snap.UsedBytes)}";
            var baseSubtitle = snap.ExpireLocal is { } exp
                ? $"{Localizer.FormatBytes(snap.LeftBytes)} · {Localizer.ExpireInDays(Math.Max(0, (exp - DateTimeOffset.Now).Days))}"
                : $"{Localizer.FormatBytes(snap.LeftBytes)} left";
            subtitle = snap.Error is not null ? $"{baseSubtitle} · {snap.Error}" : baseSubtitle;
        }
        else if (snap.Error is not null)
        {
            title = $"{Entry.Name} · ⚠";
            subtitle = snap.Error;
        }
        else
        {
            title = Entry.Name;
            subtitle = Localizer.DockSubtitleNoConfig;
        }

        return new ListItem(new AnonymousCommand(() => { })
        {
            Name = title,
            Result = CommandResult.KeepOpen(),
        })
        {
            Title = title,
            Subtitle = subtitle,
        };
    }

    public void Dispose()
    {
        Service.Updated -= OnUpdated;
        Service.Dispose();
    }
}

public sealed class VpnTrafficCommandsProvider : CommandProvider
{
    private readonly VpnJsonSettingsManager _settingsManager;
    private readonly SubscriptionCatalog _catalog = new();
    private readonly object _runtimeGate = new();
    private readonly TextSetting _subscriptionsSetting;
    private List<SubscriptionRuntime> _runtimes = [];
    private QuotaListPage? _homePage;
    private CommandItem _topLevel;
    private bool _disposed;

    public VpnTrafficCommandsProvider()
    {
        Diag.Log("provider ctor begin");
        Id = VpnTrafficConstants.ExtensionId;
        DisplayName = Localizer.AppName;
        Icon = new IconInfo("\uE968");

        AppPaths.EnsureRoot();
        var settingsPath = AppPaths.SettingsFile;
        Diag.Log("settings path=" + settingsPath);
        _settingsManager = new VpnJsonSettingsManager(settingsPath);

        _subscriptionsSetting = new TextSetting(
            "subscriptions",
            "Subscriptions",
            "One per line: Name|https://subscription-url",
            string.Empty)
        {
            Multiline = true,
            Placeholder = "Airport A|https://example.com/sub\nAirport B|https://example.com/sub2",
            Label = "Subscriptions (Name|URL per line)",
            Description = "Each enabled subscription gets its own Dock band you can pin independently.",
        };
        _settingsManager.Settings.Add(_subscriptionsSetting);

        // Keep legacy key so old installs can migrate once.
        _settingsManager.Settings.Add(new TextSetting(
            "subscriptionUrl",
            "Legacy URL (migrated)",
            "Deprecated single URL; prefer Subscriptions.",
            string.Empty));

        _settingsManager.Settings.Add(new ChoiceSetSetting(
            "refreshIntervalSeconds",
            "Refresh interval",
            "How often to query each subscription.",
            [
                new("15", "15s"),
                new("30", "30s"),
                new("60", "60s (default)"),
                new("300", "5m"),
                new("900", "15m"),
            ])
        {
            Value = "60",
        });
        _settingsManager.Settings.Add(new ToggleSetting(
            "showHistoryChart",
            "Show usage timeline",
            "Render time-point usage bars in the list page.",
            true));
        _settingsManager.Settings.Add(new ToggleSetting(
            "persistHistory",
            "Persist history (sparse)",
            "Write history only every ≥15 minutes or ≥64 MiB change.",
            true));
        _settingsManager.Settings.Add(new ChoiceSetSetting(
            "maxHistoryPoints",
            "History points",
            "Maximum in-memory samples.",
            [
                new("24", "24"),
                new("48", "48 (default)"),
                new("96", "96"),
            ])
        {
            Value = "48",
        });

        try
        {
            _settingsManager.LoadSettings();
            Diag.Log("LoadSettings ok fileExists=" + File.Exists(settingsPath));
        }
        catch (Exception ex)
        {
            Diag.Log("LoadSettings failed: " + ex.Message);
        }

        Settings = _settingsManager.Settings;
        _settingsManager.Settings.SettingsChanged += OnSettingsChanged;

        _catalog.Load();
        MigrateLegacyIfNeeded();
        EnsureSubscriptionsSettingFilled();

        _homePage = new QuotaListPage(this);
        _topLevel = new CommandItem(_homePage)
        {
            Title = Localizer.AppName,
            Subtitle = Localizer.DockSubtitleNoConfig,
            Icon = new IconInfo("\uE968"),
        };

        RebuildRuntimes();
        Diag.Log("provider ctor end runtimes=" + _runtimes.Count);
    }

    public IReadOnlyList<SubscriptionRuntime> Runtimes
    {
        get
        {
            lock (_runtimeGate)
            {
                return _runtimes.ToList();
            }
        }
    }

    public SubscriptionCatalog Catalog => _catalog;

    public AppSettings GlobalSettings => new()
    {
        RefreshIntervalSeconds = ReadInt("refreshIntervalSeconds", 60),
        ShowHistoryChart = ReadBool("showHistoryChart", true),
        PersistHistory = ReadBool("persistHistory", true),
        MaxHistoryPoints = ReadInt("maxHistoryPoints", 48),
    };

    private void MigrateLegacyIfNeeded()
    {
        var legacy = ReadString("subscriptionUrl");
        if (string.IsNullOrWhiteSpace(legacy))
        {
            return;
        }

        if (_catalog.MigrateLegacyUrl(legacy, "Default"))
        {
            Diag.Log("migrated legacy subscriptionUrl");
            _catalog.Save();
        }
    }

    private void EnsureSubscriptionsSettingFilled()
    {
        try
        {
            var current = _subscriptionsSetting.Value ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(current))
            {
                return;
            }

            var text = _catalog.ToMultiline();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _subscriptionsSetting.Value = text;
            _settingsManager.SaveSettings();
            Diag.Log("seeded subscriptions setting from catalog");
        }
        catch (Exception ex)
        {
            Diag.Log("seed subscriptions setting: " + ex.Message);
        }
    }

    private void OnSettingsChanged(object? sender, Settings args)
    {
        try
        {
            _settingsManager.SaveSettings();
            Diag.Log("SaveSettings done exists=" + File.Exists(AppPaths.SettingsFile));
        }
        catch (Exception ex)
        {
            Diag.Log("SaveSettings failed: " + ex.Message);
        }

        var multiline = ReadString("subscriptions");
        var previous = _catalog.ToMultiline();
        if (!string.IsNullOrWhiteSpace(multiline))
        {
            // Only rewrite catalog when the text actually changed — preserves Ids on unrelated setting edits.
            if (!MultilineEquals(previous, multiline))
            {
                _catalog.ReplaceFromMultiline(multiline);
                _catalog.Save();
            }
        }
        else
        {
            var legacy = ReadString("subscriptionUrl");
            if (!string.IsNullOrWhiteSpace(legacy) && _catalog.Snapshot().Count == 0)
            {
                _catalog.ReplaceFromMultiline(legacy);
                _catalog.Save();
            }
        }

        RebuildRuntimes();
    }

    private static bool MultilineEquals(string a, string b)
    {
        static string Norm(string s) => string.Join('\n',
            s.Replace("\r\n", "\n").Split('\n', StringSplitOptions.None)
                .Select(static l => l.Trim())
                .Where(static l => l.Length > 0));
        return string.Equals(Norm(a), Norm(b), StringComparison.Ordinal);
    }

    private void RebuildRuntimes()
    {
        var global = GlobalSettings;
        var enabled = _catalog.EnabledSnapshot();
        Diag.Log("rebuild runtimes count=" + enabled.Count);

        List<SubscriptionRuntime> next = [];
        List<SubscriptionRuntime> old;
        lock (_runtimeGate)
        {
            old = _runtimes;
        }

        foreach (var entry in enabled)
        {
            var existing = old.FirstOrDefault(r =>
                string.Equals(r.Entry.Id, entry.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Entry.Url, entry.Url, StringComparison.OrdinalIgnoreCase));
            if (existing is not null &&
                string.Equals(existing.Entry.Url, entry.Url, StringComparison.Ordinal))
            {
                existing.ApplyEntry(entry, global);
                next.Add(existing);
            }
            else if (existing is not null &&
                     string.Equals(existing.Entry.Id, entry.Id, StringComparison.OrdinalIgnoreCase))
            {
                // Same stable Id but URL changed — refresh in place so Dock pin stays valid.
                existing.ApplyEntry(entry, global);
                next.Add(existing);
            }
            else
            {
                if (existing is not null)
                {
                    existing.SnapshotUpdated -= OnRuntimeSnapshot;
                    existing.Dispose();
                }

                var rt = new SubscriptionRuntime(entry, global);
                next.Add(rt);
            }
        }

        foreach (var r in old)
        {
            if (!next.Contains(r))
            {
                r.SnapshotUpdated -= OnRuntimeSnapshot;
                r.Dispose();
            }
        }

        foreach (var r in next)
        {
            r.SnapshotUpdated -= OnRuntimeSnapshot;
            r.SnapshotUpdated += OnRuntimeSnapshot;
        }

        lock (_runtimeGate)
        {
            _runtimes = next;
        }

        if (_homePage is not null)
        {
            _homePage.Rebuild();
        }
    }

    private void OnRuntimeSnapshot(object? sender, QuotaSnapshot snap)
    {
        _homePage?.Rebuild();
        try
        {
            var first = Runtimes.FirstOrDefault();
            if (first is not null)
            {
                _topLevel.Title = first.Entry.Name;
                if (first.Service.Current.Percent is { } pct)
                {
                    _topLevel.Title = $"{first.Entry.Name} · {pct:0}%";
                    _topLevel.Subtitle = $"{Localizer.FormatBytes(first.Service.Current.UsedBytes)} / {Localizer.FormatBytes(Math.Max(0, first.Service.Current.TotalBytes))}";
                }
            }
        }
        catch
        {
            // ignore UI update failures
        }
    }

    public override ICommandItem[] TopLevelCommands() => [_topLevel];

    public override ICommandItem[] GetDockBands()
    {
        lock (_runtimeGate)
        {
            if (_runtimes.Count == 0)
            {
                // Always expose one placeholder band so users can open settings from Dock if needed.
                return
                [
                    new WrappedDockItem(
                        [new ListItem(new AnonymousCommand(() => { })
                        {
                            Name = Localizer.DockSubtitleNoConfig,
                            Result = CommandResult.KeepOpen(),
                        })
                        {
                            Title = Localizer.AppName,
                            Subtitle = Localizer.DockSubtitleNoConfig,
                        }],
                        Localizer.AppName,
                        "quota:empty")
                    {
                        Icon = new IconInfo("\uE968"),
                    },
                ];
            }

            return _runtimes.Select(static r => (ICommandItem)r.Dock).ToArray();
        }
    }

    private string ReadString(string key)
    {
        if (_settingsManager.Settings.TryGetSetting<string>(key, out var value) && value is not null)
        {
            return value;
        }

        return string.Empty;
    }

    private bool ReadBool(string key, bool fallback)
    {
        if (_settingsManager.Settings.TryGetSetting<bool>(key, out var value))
        {
            return value;
        }

        return fallback;
    }

    private int ReadInt(string key, int fallback)
    {
        if (_settingsManager.Settings.TryGetSetting<string>(key, out var text) &&
            int.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settingsManager.Settings.SettingsChanged -= OnSettingsChanged;
        lock (_runtimeGate)
        {
            foreach (var r in _runtimes)
            {
                r.Dispose();
            }

            _runtimes = [];
        }

        base.Dispose();
    }
}
