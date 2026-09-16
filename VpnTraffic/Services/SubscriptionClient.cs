using System.Globalization;
using System.Net.Http.Headers;

namespace VpnTraffic.Services;

/// <summary>Fetches airport subscription quota from the <c>subscription-userinfo</c> response header.</summary>
public sealed class SubscriptionClient : IDisposable
{
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
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("VpnTraffic/0.1 (+cmdpal)");
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

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
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

            string? userInfo = response.Headers.TryGetValues("subscription-userinfo", out var values)
                ? string.Join(" ", values)
                : response.Content.Headers.TryGetValues("subscription-userinfo", out var contentValues)
                    ? string.Join(" ", contentValues)
                    : null;

            string? profileTitle = response.Headers.TryGetValues("profile-title", out var titles)
                ? titles.FirstOrDefault()
                : null;

            if (string.IsNullOrWhiteSpace(userInfo))
            {
                return QuotaSnapshot.Empty with
                {
                    Error = "no-userinfo",
                    FetchedAt = fetchedAt,
                    ProfileTitle = DecodeProfileTitle(profileTitle),
                };
            }

            return ParseUserInfo(userInfo, fetchedAt, DecodeProfileTitle(profileTitle));
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
