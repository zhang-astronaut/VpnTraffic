using System.Text.Json;
using System.Text.Json.Serialization;

namespace VpnTraffic.Services;

/// <summary>
/// In-memory usage samples with sparse disk persistence to minimize SSD wear.
/// Writes only when at least <see cref="MinWriteInterval"/> elapsed or used-bytes delta exceeds threshold.
/// </summary>
public sealed class HistoryStore
{
    public static readonly TimeSpan MinWriteInterval = TimeSpan.FromMinutes(15);
    public const long MinWriteDeltaBytes = 64L * 1024 * 1024; // 64 MiB

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();
    private readonly List<HistoryPoint> _points = [];
    private string? _filePath;
    private bool _persistEnabled = true;
    private int _maxPoints = 48;
    private DateTimeOffset _lastPersistedAt = DateTimeOffset.MinValue;
    private long _lastPersistedUsed = -1;

    public void Configure(string filePath, bool persistEnabled, int maxPoints)
    {
        lock (_gate)
        {
            _filePath = filePath;
            _persistEnabled = persistEnabled;
            _maxPoints = Math.Clamp(maxPoints, 8, 240);
            TrimLocked();
        }
    }

    public int MaxPoints
    {
        get
        {
            lock (_gate)
            {
                return _maxPoints;
            }
        }
    }

    public IReadOnlyList<HistoryPoint> Snapshot()
    {
        lock (_gate)
        {
            return _points.ToArray();
        }
    }

    public void Load()
    {
        lock (_gate)
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<List<HistoryPoint>>(json, JsonOptions);
                if (loaded is null)
                {
                    return;
                }

                // Merge with in-memory samples so a settings reload does not wipe unpersisted points.
                var merged = new List<HistoryPoint>(_points);
                merged.AddRange(loaded.Where(static p => p.UsedBytes >= 0));
                merged.Sort(static (a, b) => a.Timestamp.CompareTo(b.Timestamp));
                // Dedupe near-identical timestamps.
                var deduped = new List<HistoryPoint>(merged.Count);
                foreach (var p in merged)
                {
                    if (deduped.Count > 0 &&
                        (p.Timestamp - deduped[^1].Timestamp).Duration() < TimeSpan.FromSeconds(1) &&
                        p.UsedBytes == deduped[^1].UsedBytes)
                    {
                        deduped[^1] = p;
                    }
                    else
                    {
                        deduped.Add(p);
                    }
                }

                _points.Clear();
                _points.AddRange(deduped);
                TrimLocked();
                if (_points.Count > 0)
                {
                    var last = _points[^1];
                    _lastPersistedAt = last.Timestamp;
                    _lastPersistedUsed = last.UsedBytes;
                }
            }
            catch
            {
                // Corrupt history is non-fatal.
            }
        }
    }

    public void Add(QuotaSnapshot snapshot)
    {
        if (!snapshot.IsSuccess || !snapshot.HasUsed)
        {
            return;
        }

        bool shouldPersist;
        lock (_gate)
        {
            var point = new HistoryPoint(snapshot.FetchedAt, snapshot.UsedBytes, snapshot.TotalBytes);
            if (_points.Count > 0 &&
                _points[^1].UsedBytes == point.UsedBytes &&
                _points[^1].TotalBytes == point.TotalBytes &&
                snapshot.FetchedAt - _points[^1].Timestamp < TimeSpan.FromMinutes(1))
            {
                // Refresh current sample instead of stacking duplicates.
                _points[^1] = point;
            }
            else
            {
                _points.Add(point);
            }

            TrimLocked();
            shouldPersist = ShouldPersistLocked(snapshot.UsedBytes, snapshot.FetchedAt);
        }

        if (shouldPersist)
        {
            Persist();
        }
    }

    public void Persist()
    {
        string? path;
        string json;
        DateTimeOffset now;
        long used;
        lock (_gate)
        {
            if (!_persistEnabled || string.IsNullOrEmpty(_filePath))
            {
                return;
            }

            path = _filePath;
            json = JsonSerializer.Serialize(_points, JsonOptions);
            now = DateTimeOffset.Now;
            used = _points.Count > 0 ? _points[^1].UsedBytes : -1;
        }

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);

            lock (_gate)
            {
                _lastPersistedAt = now;
                _lastPersistedUsed = used;
            }
        }
        catch
        {
            // Disk failures must not break quota refresh.
        }
    }

    private bool ShouldPersistLocked(long usedBytes, DateTimeOffset fetchedAt)
    {
        if (!_persistEnabled || string.IsNullOrEmpty(_filePath))
        {
            return false;
        }

        if (_lastPersistedAt == DateTimeOffset.MinValue)
        {
            return true;
        }

        if (fetchedAt - _lastPersistedAt >= MinWriteInterval)
        {
            return true;
        }

        if (_lastPersistedUsed >= 0 && usedBytes - _lastPersistedUsed >= MinWriteDeltaBytes)
        {
            return true;
        }

        return false;
    }

    private void TrimLocked()
    {
        if (_points.Count > _maxPoints)
        {
            _points.RemoveRange(0, _points.Count - _maxPoints);
        }
    }
}
