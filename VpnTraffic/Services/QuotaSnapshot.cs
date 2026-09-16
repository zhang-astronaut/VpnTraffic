namespace VpnTraffic.Services;

public sealed record QuotaSnapshot
{
    public static QuotaSnapshot Empty { get; } = new();

    public long UploadBytes { get; init; } = -1;
    public long DownloadBytes { get; init; } = -1;
    public long TotalBytes { get; init; } = -1;
    public long? ExpireUnix { get; init; }
    public DateTimeOffset FetchedAt { get; init; }
    public string? Error { get; init; }
    public string? ProfileTitle { get; init; }
    public bool HasUserInfo { get; init; }

    public bool IsSuccess => Error is null && HasUserInfo;
    public long UsedBytes => UploadBytes >= 0 && DownloadBytes >= 0 ? UploadBytes + DownloadBytes : -1;
    public bool HasTotal => TotalBytes > 0;
    public bool HasUsed => UsedBytes >= 0;

    public double? Percent
    {
        get
        {
            if (!HasTotal || !HasUsed || TotalBytes <= 0)
            {
                return null;
            }

            return Math.Clamp(UsedBytes * 100.0 / TotalBytes, 0, 100);
        }
    }

    public long LeftBytes => HasTotal && HasUsed ? Math.Max(0, TotalBytes - UsedBytes) : -1;

    public DateTimeOffset? ExpireLocal =>
        ExpireUnix is > 0 ? DateTimeOffset.FromUnixTimeSeconds(ExpireUnix.Value).ToLocalTime() : null;
}

public sealed record HistoryPoint(
    DateTimeOffset Timestamp,
    long UsedBytes,
    long TotalBytes);
