using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using VpnTraffic.Localization;
using VpnTraffic.Pages;
using VpnTraffic.Services;

namespace VpnTraffic;

public sealed class VpnTrafficCommandsProvider : CommandProvider
{
    private readonly QuotaService _service = new();
    private readonly VpnJsonSettingsManager _settingsManager;
    private readonly CommandItem _topLevel;
    private readonly WrappedDockItem _dock;
    private QuotaListPage? _page;
    private bool _disposed;

    public VpnTrafficCommandsProvider()
    {
        Id = VpnTrafficConstants.ExtensionId;
        DisplayName = Localizer.AppName;
        Icon = new IconInfo("\uE968");

        _settingsManager = new VpnJsonSettingsManager(Utilities.BaseSettingsPath(SettingsFolderName));
        _settingsManager.Settings.Add(new TextSetting(
            "subscriptionUrl",
            "Subscription URL",
            "https://example.com/subscription",
            string.Empty)
        {
            IsRequired = true,
            Label = "Subscription URL",
            Description = "Airport subscription link (subscription-userinfo).",
        });
        _settingsManager.Settings.Add(new ChoiceSetSetting(
            "refreshIntervalSeconds",
            "Refresh interval",
            "How often to query the subscription.",
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

        _settingsManager.LoadSettings();
        Settings = _settingsManager.Settings;
        _settingsManager.Settings.SettingsChanged += OnSettingsChanged;

        ApplySettings(reloadHistory: true);
        _service.Updated += OnQuotaUpdated;

        _page = new QuotaListPage(_service);
        _topLevel = new CommandItem(_page)
        {
            Title = Localizer.AppName,
            Subtitle = Localizer.DockSubtitleNoConfig,
            Icon = new IconInfo("\uE968"),
        };

        _dock = new WrappedDockItem(
            [
                new ListItem(new AnonymousCommand(() => { })
                {
                    Name = Localizer.AppName,
                    Result = CommandResult.KeepOpen(),
                })
                {
                    Title = Localizer.AppName,
                    Subtitle = Localizer.DockSubtitleNoConfig,
                },
            ],
            Localizer.AppName,
            "quota")
        {
            Icon = new IconInfo("\uE968"),
        };

        _service.Start(_service.Settings, HistoryFilePath());
    }

    private const string SettingsFolderName = QuotaService.SettingsFolderName;

    private static string HistoryFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            SettingsFolderName);
        return Path.Combine(dir, "history.json");
    }

    private void OnSettingsChanged(object? sender, Settings args)
    {
        _settingsManager.SaveSettings();
        ApplySettings(reloadHistory: true);
        _service.RestartLoop();
    }

    private void ApplySettings(bool reloadHistory)
    {
        var settings = new AppSettings
        {
            SubscriptionUrl = ReadString("subscriptionUrl"),
            RefreshIntervalSeconds = ReadInt("refreshIntervalSeconds", 60),
            ShowHistoryChart = ReadBool("showHistoryChart", true),
            PersistHistory = ReadBool("persistHistory", true),
            MaxHistoryPoints = ReadInt("maxHistoryPoints", 48),
        };
        _service.ApplySettings(settings, HistoryFilePath(), reloadHistory);
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

    private void OnQuotaUpdated(object? sender, QuotaSnapshot snap)
    {
        UpdateDock(snap);
        UpdateTopLevel(snap);
    }

    private void UpdateDock(QuotaSnapshot snap)
    {
        string title;
        string subtitle;
        var url = _service.Settings.SubscriptionUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            title = Localizer.AppName;
            subtitle = Localizer.DockSubtitleNoConfig;
        }
        else if (snap.Percent is { } pct)
        {
            // Self-contained for Compact (subtitle hidden).
            title = $"{pct:0}% · {Localizer.FormatBytes(snap.UsedBytes)}";
            var baseSubtitle = snap.ExpireLocal is { } exp
                ? $"{Localizer.FormatBytes(snap.LeftBytes)} · {Localizer.ExpireInDays(Math.Max(0, (exp - DateTimeOffset.Now).Days))}"
                : $"{Localizer.FormatBytes(snap.LeftBytes)} left";
            subtitle = snap.Error is not null
                ? $"{baseSubtitle} · {snap.Error}"
                : baseSubtitle;
        }
        else if (snap.Error is not null)
        {
            title = "⚠";
            subtitle = snap.Error;
        }
        else
        {
            title = Localizer.AppName;
            subtitle = Localizer.Unknown;
        }

        _dock.Items =
        [
            new ListItem(new AnonymousCommand(() => { })
            {
                Name = title,
                Result = CommandResult.KeepOpen(),
            })
            {
                Title = title,
                Subtitle = subtitle,
            },
        ];
    }

    private void UpdateTopLevel(QuotaSnapshot snap)
    {
        if (snap.Percent is { } pct)
        {
            _topLevel.Title = $"{Localizer.AppName} · {pct:0}%";
            _topLevel.Subtitle = $"{Localizer.FormatBytes(snap.UsedBytes)} / {Localizer.FormatBytes(Math.Max(0, snap.TotalBytes))}";
        }
        else if (!string.IsNullOrWhiteSpace(_service.Settings.SubscriptionUrl) && snap.Error is not null)
        {
            _topLevel.Title = Localizer.AppName;
            _topLevel.Subtitle = $"{Localizer.ErrorPrefix}: {snap.Error}";
        }
    }

    public override ICommandItem[] TopLevelCommands() => [_topLevel];

    public override ICommandItem[] GetDockBands() => [_dock];

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settingsManager.Settings.SettingsChanged -= OnSettingsChanged;
        _service.Updated -= OnQuotaUpdated;
        _service.Dispose();
        base.Dispose();
    }
}
