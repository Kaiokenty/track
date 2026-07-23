using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Track.Core.Cursor;

public sealed class CursorApiClient : IDisposable
{
    public const string ClientId = "KbZUR41cY7W6zRSdpSUJ7I7mLYBKOCmB";
    private const string BaseUrl = "https://api2.cursor.sh";

    private readonly HttpClient _http;
    private string _accessToken;
    private string? _refreshToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public CursorApiClient(string accessToken, string? refreshToken = null, HttpClient? http = null)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public string AccessToken => _accessToken;

    public async Task<CursorPeriodUsage?> GetCurrentPeriodUsageAsync(CancellationToken ct = default)
    {
        var response = await SendDashboardAsync(
            "/aiserver.v1.DashboardService/GetCurrentPeriodUsage",
            new { },
            ct).ConfigureAwait(false);

        if (response is null) return null;
        return JsonSerializer.Deserialize<CursorPeriodUsage>(response, JsonOptions);
    }

    public async Task<CursorPlanInfoResponse?> GetPlanInfoAsync(CancellationToken ct = default)
    {
        var response = await SendDashboardAsync(
            "/aiserver.v1.DashboardService/GetPlanInfo",
            new { },
            ct).ConfigureAwait(false);

        if (response is null) return null;
        return JsonSerializer.Deserialize<CursorPlanInfoResponse>(response, JsonOptions);
    }

    public async Task<bool> RefreshAccessTokenAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_refreshToken)) return false;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/oauth/token")
        {
            Content = JsonContent.Create(new
            {
                grant_type = "refresh_token",
                client_id = ClientId,
                refresh_token = _refreshToken
            })
        };

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return false;

        var payload = await resp.Content.ReadFromJsonAsync<CursorTokenRefreshResponse>(JsonOptions, ct)
            .ConfigureAwait(false);
        if (payload is null || payload.ShouldLogout || string.IsNullOrWhiteSpace(payload.AccessToken))
            return false;

        _accessToken = payload.AccessToken;
        return true;
    }

    public void Dispose() => _http.Dispose();

    private async Task<string?> SendDashboardAsync(string path, object body, CancellationToken ct)
    {
        async Task<HttpResponseMessage> Once()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path)
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            req.Headers.TryAddWithoutValidation("Connect-Protocol-Version", "1");
            return await _http.SendAsync(req, ct).ConfigureAwait(false);
        }

        using var first = await Once().ConfigureAwait(false);
        if (first.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            if (!await RefreshAccessTokenAsync(ct).ConfigureAwait(false))
                return null;

            using var retry = await Once().ConfigureAwait(false);
            if (!retry.IsSuccessStatusCode) return null;
            return await retry.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }

        if (!first.IsSuccessStatusCode) return null;
        return await first.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }
}

public sealed class CursorTokenRefreshResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("shouldLogout")]
    public bool ShouldLogout { get; set; }
}

public sealed class CursorPeriodUsage
{
    public string? BillingCycleStart { get; set; }
    public string? BillingCycleEnd { get; set; }
    public CursorPlanUsage? PlanUsage { get; set; }
    public CursorSpendLimitUsage? SpendLimitUsage { get; set; }
    public string? DisplayMessage { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class CursorPlanUsage
{
    public double TotalSpend { get; set; }
    public double IncludedSpend { get; set; }
    public double BonusSpend { get; set; }
    public double? Remaining { get; set; }
    public double Limit { get; set; }
    public double? AutoPercentUsed { get; set; }
    public double? ApiPercentUsed { get; set; }
    public double? TotalPercentUsed { get; set; }
    public string? BonusTooltip { get; set; }
}

public sealed class CursorSpendLimitUsage
{
    public double? TotalSpend { get; set; }
    public double? IndividualLimit { get; set; }
    public double? IndividualUsed { get; set; }
    public double? IndividualRemaining { get; set; }
    public double? PooledLimit { get; set; }
    public double? PooledUsed { get; set; }
    public string? LimitType { get; set; }
}

public sealed class CursorPlanInfoResponse
{
    public CursorPlanInfo? PlanInfo { get; set; }
}

public sealed class CursorPlanInfo
{
    public string? PlanName { get; set; }
    public double? IncludedAmountCents { get; set; }
    public string? Price { get; set; }
    public string? BillingCycleEnd { get; set; }
}
