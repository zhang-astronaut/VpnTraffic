using System.Globalization;
using System.Net.Http.Headers;

namespace VpnTraffic.Services;

/// <summary>
/// Fetches airport subscription quota from the <c>subscription-userinfo</c> response header.
/// Most airports only emit that header for Clash-like User-Agents.
/// </summary>
public sealed class SubscriptionClient : IDisposable
{
    // Common client UAs that make airport panels return subscription-userinfo.
    private static readonly string[] UserAgents =
    [
        "clash.meta/1.18.0",
        "ClashforWindows/0.20.39",
        "ClashforAndroid/2.5.12",
        "v2rayN/6.23",
        "Shadowrocket/2.2.0",
        "Stash/2.5.0",
        "Quantumult%20X/1.0.30",
        "VpnTraffic/0.1 (+cmdpal)",
    ];

    private readonly HttpClient _http;

    public SubscriptionClient()
    {
        _http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            UseCookies = false,
        })
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
    }

    public async Task<QuotaSnapshot> FetchAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return QuotaSnapshot.Empty with { Error = "empty-url", FetchedAt = DateTimeOffset.Now };
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return QuotaSnapshot.Empty with { Error = "invalid-url", FetchedAt = DateTimeOffset.Now };
        }

        QuotaSnapshot last = QuotaSnapshot.Empty with { Error = "no-userinfo", FetchedAt = DateTimeOffset.Now };

        foreach (var ua in UserAgents)
        {
            ct.ThrowIfCancellationRequested();
            var attempt = await FetchOnceAsync(uri, ua, ct).ConfigureAwait(false);
            if (attempt.IsSuccess)
            {
                return attempt;
            }

            // Prefer a successful HTTP response with missing header over network errors for next attempt.
            last = attempt;
            // Network / HTTP hard failures: try next UA anyway (some panels filter by UA with 403).
        }

        return last with { FetchedAt = DateTimeOffset.Now };
    }

    private async Task<QuotaSnapshot> FetchOnceAsync(Uri uri, string userAgent, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            // Some panels check these as well.
            request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");

            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            var fetchedAt = DateTimeOffset.Now;
            if (!response.IsSuccessStatusCode)
            {
                return QuotaSnapshot.Empty with
                {
                    Error = $"http-{(int)response.StatusCode}",
                    FetchedAt = fetchedAt,
                };
            }

            string? userInfo = FirstHeader(response, "subscription-userinfo")
                ?? FirstHeader(response, "Subscription-Userinfo");
            string? profileTitle = FirstHeader(response, "profile-title")
                ?? FirstHeader(response, "Profile-Title");

            if (string.IsNullOrWhiteSpace(userInfo))
            {
                return QuotaSnapshot.Empty with
                {
                    Error = "no-userinfo",
                    FetchedAt = fetchedAt,
                    ProfileTitle = DecodeProfileTitle(profileTitle),
                };
            }

            var parsed = ParseUserInfo(userInfo, fetchedAt, DecodeProfileTitle(profileTitle));
            if (!parsed.IsSuccess && parsed.Error == "parse-failed")
            {
                // Header present but unparsable — try next UA; keep last as this error.
                return parsed;
            }

            return parsed;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return QuotaSnapshot.Empty with { Error = "timeout", FetchedAt = DateTimeOffset.Now };
        }
        catch (HttpRequestException ex)
        {
            return QuotaSnapshot.Empty with
            {
                Error = $"network:{ex.Message}",
                FetchedAt = DateTimeOffset.Now,
            };
        }
        catch (Exception ex)
        {
            return QuotaSnapshot.Empty with
            {
                Error = $"unexpected:{ex.GetType().Name}",
                FetchedAt = DateTimeOffset.Now,
            };
        }
    }

    private static string? FirstHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values))
        {
            return string.Join(" ", values);
        }

        if (response.Content.Headers.TryGetValues(name, out var contentValues))
        {
            return string.Join(" ", contentValues);
        }

        return null;
    }

    public static QuotaSnapshot ParseUserInfo(string userInfo, DateTimeOffset fetchedAt, string? profileTitle = null)
    {
        long upload = -1, download = -1, total = -1;
        long? expire = null;

        foreach (var rawPart in userInfo.Split([';', ' ', '\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.Trim();
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = part[..eq].Trim().ToLowerInvariant();
            var value = part[(eq + 1)..].Trim();
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                continue;
            }

            switch (key)
            {
                case "upload":
                    upload = number;
                    break;
                case "download":
                    download = number;
                    break;
                case "total":
                    total = number;
                    break;
                case "expire":
                    expire = number;
                    break;
            }
        }

        var hasUser = upload >= 0 && download >= 0;
        return new QuotaSnapshot
        {
            UploadBytes = upload,
            DownloadBytes = download,
            TotalBytes = total,
            ExpireUnix = expire is > 0 ? expire : null,
            FetchedAt = fetchedAt,
            ProfileTitle = profileTitle,
            HasUserInfo = hasUser,
            Error = hasUser ? null : "parse-failed",
        };
    }

    private static string? DecodeProfileTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim().Trim('"');
        if (text.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var b64 = text["base64:".Length..].Trim();
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            }
            catch
            {
                return text;
            }
        }

        try
        {
            return Uri.UnescapeDataString(text);
        }
        catch
        {
            return text;
        }
    }

    public void Dispose() => _http.Dispose();
}
