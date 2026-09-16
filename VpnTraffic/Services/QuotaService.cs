namespace VpnTraffic.Services;

public sealed class AppSettings
{
    public string SubscriptionUrl { get; set; } = string.Empty;
    public int RefreshIntervalSeconds { get; set; } = 60;
    public bool ShowHistoryChart { get; set; } = true;
    public bool PersistHistory { get; set; } = true;
    public int MaxHistoryPoints { get; set; } = 48;
}

public sealed class QuotaService : IDisposable
{
    public const string SettingsFolderName = "VpnTraffic";

    private readonly SubscriptionClient _client = new();
    private readonly HistoryStore _history = new();
    private readonly object _gate = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private AppSettings _settings = new();
    private QuotaSnapshot _current = QuotaSnapshot.Empty;
    private bool _disposed;

    public event EventHandler<QuotaSnapshot>? Updated;

    public HistoryStore History => _history;

    public QuotaSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public AppSettings Settings
    {
        get
        {
            lock (_gate)
            {
                return _settings;
            }
        }
    }

    public void Start(AppSettings settings, string historyFilePath)
    {
        ApplySettings(settings, historyFilePath, reloadHistory: true);
        RestartLoop();
    }

    public void ApplySettings(AppSettings settings, string historyFilePath, bool reloadHistory)
    {
        lock (_gate)
        {
            _settings = settings;
        }

        _history.Configure(historyFilePath, settings.PersistHistory, settings.MaxHistoryPoints);
        if (reloadHistory)
        {
            _history.Load();
        }
    }

    public void RestartLoop()
    {
        var previous = _cts;
        var previousLoop = _loop;
        previous?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _loop = Task.Run(async () =>
        {
            if (previousLoop is not null)
            {
                try
                {
                    await previousLoop.ConfigureAwait(false);
                }
                catch
                {
                    // previous loop cancelled/faulted
                }
            }

            previous?.Dispose();
            await RunLoopAsync(cts.Token).ConfigureAwait(false);
        });
    }

    public async Task RefreshOnceAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts?.Token ?? CancellationToken.None);
        if (!await _refreshLock.WaitAsync(0, linked.Token).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            linked.Token.ThrowIfCancellationRequested();
            AppSettings settings;
            lock (_gate)
            {
                settings = _settings;
            }

            var snapshot = await _client
                .FetchAsync(settings.SubscriptionUrl, linked.Token)
                .ConfigureAwait(false);
            if (snapshot.IsSuccess)
            {
                _history.Add(snapshot);
            }
            else if (!string.IsNullOrWhiteSpace(settings.SubscriptionUrl) && Current.IsSuccess)
            {
                // Keep last good data; only annotate error.
                snapshot = Current with { Error = snapshot.Error, FetchedAt = snapshot.FetchedAt };
            }

            lock (_gate)
            {
                _current = snapshot;
            }

            Updated?.Invoke(this, snapshot);
        }
        catch (OperationCanceledException)
        {
            // shutdown / restart
        }
        finally
        {
            try
            {
                _refreshLock.Release();
            }
            catch (ObjectDisposedException)
            {
                // already tearing down
            }
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        // First fetch immediately.
        await Task.Yield();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RefreshOnceAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Swallow to keep the loop alive.
            }

            int delaySeconds;
            lock (_gate)
            {
                delaySeconds = Math.Clamp(_settings.RefreshIntervalSeconds, 10, 3600);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Cancel();
        try
        {
            // HttpClient timeout is 15s; join the loop so in-flight work can observe cancel.
            _loop?.Wait(TimeSpan.FromSeconds(20));
        }
        catch
        {
            // ignore
        }

        _cts?.Dispose();
        _client.Dispose();
        try
        {
            _refreshLock.Dispose();
        }
        catch
        {
            // ignore
        }
    }
}
