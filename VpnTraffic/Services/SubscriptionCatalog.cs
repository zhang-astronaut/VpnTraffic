using System.Text.Json;
using System.Text.Json.Serialization;
using VpnTraffic;

namespace VpnTraffic.Services;

public sealed class SubscriptionEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "Subscription";
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    public bool IsValid => !string.IsNullOrWhiteSpace(Url) &&
                           Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var u) &&
                           (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}

public sealed class SubscriptionCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();
    private List<SubscriptionEntry> _entries = [];

    public IReadOnlyList<SubscriptionEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.Select(Clone).ToList();
        }
    }

    public IReadOnlyList<SubscriptionEntry> EnabledSnapshot() =>
        Snapshot().Where(static e => e.Enabled && e.IsValid).ToList();

    public bool Add(string name, string url) => Add(name, url, out _);

    public bool Add(string name, string url, out string error)
    {
        url = (url ?? string.Empty).Trim();
        name = (name ?? string.Empty).Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "invalid-url";
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = GuessName(url);
        }

        lock (_gate)
        {
            if (_entries.Any(e => string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase)))
            {
                error = "duplicate";
                return false;
            }

            _entries.Add(new SubscriptionEntry
            {
                Name = name,
                Url = url,
                Enabled = true,
            });
            error = string.Empty;
            return true;
        }
    }

    public bool Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        lock (_gate)
        {
            var before = _entries.Count;
            _entries.RemoveAll(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
            return _entries.Count < before;
        }
    }

    public void Load()
    {
        lock (_gate)
        {
            try
            {
                AppPaths.EnsureRoot();
                if (File.Exists(AppPaths.SubscriptionsFile))
                {
                    var json = File.ReadAllText(AppPaths.SubscriptionsFile);
                    var loaded = JsonSerializer.Deserialize<List<SubscriptionEntry>>(json, JsonOptions);
                    if (loaded is { Count: > 0 })
                    {
                        _entries = Normalize(loaded);
                        return;
                    }
                }
            }
            catch
            {
                // fall through to empty / migration
            }

            _entries ??= [];
        }
    }

    public void Save()
    {
        List<SubscriptionEntry> copy;
        lock (_gate)
        {
            copy = _entries.Select(Clone).ToList();
        }

        try
        {
            AppPaths.EnsureRoot();
            var temp = AppPaths.SubscriptionsFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(copy, JsonOptions));
            File.Move(temp, AppPaths.SubscriptionsFile, overwrite: true);
            Diag.Log($"subscriptions saved count={copy.Count} path={AppPaths.SubscriptionsFile}");
        }
        catch (Exception ex)
        {
            Diag.Log("subscriptions save failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Replace catalog from settings multiline text. Preserves existing Id/Enabled by URL
    /// so CmdPal Dock band pins and history-{id}.json stay stable across settings saves.
    /// </summary>
    public void ReplaceFromMultiline(string text)
    {
        var parsed = ParseMultiline(text);
        lock (_gate)
        {
            var byNameUrl = new Dictionary<string, SubscriptionEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in _entries)
            {
                byNameUrl[Key(e.Url, e.Name)] = e;
            }

            var merged = new List<SubscriptionEntry>();
            foreach (var e in parsed)
            {
                if (byNameUrl.TryGetValue(Key(e.Url, e.Name), out var existing))
                {
                    merged.Add(new SubscriptionEntry
                    {
                        Id = existing.Id,
                        Name = e.Name,
                        Url = e.Url,
                        Enabled = existing.Enabled,
                    });
                }
                else if (_entries.FirstOrDefault(x =>
                             string.Equals(x.Url, e.Url, StringComparison.OrdinalIgnoreCase)) is { } urlMatch
                         && _entries.Count(x => string.Equals(x.Url, e.Url, StringComparison.OrdinalIgnoreCase)) == 1)
                {
                    // Unique URL match: keep Id even if renamed.
                    merged.Add(new SubscriptionEntry
                    {
                        Id = urlMatch.Id,
                        Name = e.Name,
                        Url = e.Url,
                        Enabled = urlMatch.Enabled,
                    });
                }
                else
                {
                    merged.Add(e);
                }
            }

            _entries = Normalize(merged);
        }

        static string Key(string url, string name) =>
            (url ?? string.Empty).Trim() + "\n" + (name ?? string.Empty).Trim();
    }

    public string ToMultiline()
    {
        lock (_gate)
        {
            return string.Join("\n", _entries.Select(static e => $"{e.Name}|{e.Url}"));
        }
    }

    /// <summary>Migrate a single legacy URL into the catalog if the catalog is empty.</summary>
    public bool MigrateLegacyUrl(string url, string name = "Default")
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        lock (_gate)
        {
            if (_entries.Any())
            {
                return false;
            }

            _entries =
            [
                new SubscriptionEntry
                {
                    Name = string.IsNullOrWhiteSpace(name) ? "Default" : name.Trim(),
                    Url = url.Trim(),
                    Enabled = true,
                },
            ];
            return true;
        }
    }

    public static List<SubscriptionEntry> ParseMultiline(string? text)
    {
        var list = new List<SubscriptionEntry>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return list;
        }

        foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string name;
            string url;
            var pipe = line.IndexOf('|');
            if (pipe > 0)
            {
                name = line[..pipe].Trim();
                url = line[(pipe + 1)..].Trim();
            }
            else
            {
                url = line;
                name = GuessName(url);
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            list.Add(new SubscriptionEntry
            {
                Name = string.IsNullOrWhiteSpace(name) ? GuessName(url) : name,
                Url = url,
                Enabled = true,
            });
        }

        return list;
    }

    private static string GuessName(string url)
    {
        try
        {
            var host = new Uri(url).Host;
            return string.IsNullOrWhiteSpace(host) ? "Subscription" : host;
        }
        catch
        {
            return "Subscription";
        }
    }

    private static List<SubscriptionEntry> Normalize(List<SubscriptionEntry> source)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SubscriptionEntry>();
        foreach (var e in source)
        {
            if (e is null || string.IsNullOrWhiteSpace(e.Url))
            {
                continue;
            }

            var url = e.Url.Trim();
            // Same subscription URL is one entry (one Dock band / one quota).
            if (!seen.Add(url))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(e.Id))
            {
                e.Id = Guid.NewGuid().ToString("N")[..12];
            }

            if (string.IsNullOrWhiteSpace(e.Name))
            {
                e.Name = GuessName(url);
            }

            e.Url = url;
            result.Add(e);
        }

        return result;
    }

    private static SubscriptionEntry Clone(SubscriptionEntry e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Url = e.Url,
        Enabled = e.Enabled,
    };
}
