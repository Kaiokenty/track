using Track.Core.Cursor;
using Track.Core.Models;

namespace Track.Core.Adapters;

/// <summary>
/// Reads the local Cursor session and calls unofficial dashboard RPC (same approach as OpenUsage).
/// </summary>
public sealed class CursorSessionAdapter : IProviderAdapter
{
    private CursorApiClient? _client;

    public string Id => "cursor";
    public string DisplayName => "Cursor";
    public bool CanAutoDetect => true;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CursorAuthStore.IsCursorInstalled());

    public async Task<ProviderSnapshot> FetchAsync(CancellationToken cancellationToken = default)
    {
        if (!CursorAuthStore.IsCursorInstalled())
        {
            return NotLinked("Cursor not detected. Install and sign in, then reconnect.");
        }

        CursorAuthSession? session;
        try
        {
            session = CursorAuthStore.TryReadSession();
        }
        catch (Exception ex)
        {
            return Error($"Could not read Cursor login state: {ex.Message}");
        }

        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return NotLinked("Open Cursor and sign in, then refresh Track.");
        }

        _client?.Dispose();
        _client = new CursorApiClient(session.AccessToken, session.RefreshToken);

        CursorPeriodUsage? usage;
        CursorPlanInfoResponse? plan;
        try
        {
            usage = await _client.GetCurrentPeriodUsageAsync(cancellationToken).ConfigureAwait(false);
            plan = await _client.GetPlanInfoAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Error($"Cursor API failed: {ex.Message}", session);
        }

        if (usage?.PlanUsage is null)
        {
            return new ProviderSnapshot
            {
                Id = Id,
                DisplayName = DisplayName,
                PlanLabel = plan?.PlanInfo?.PlanName ?? TitleCase(session.MembershipType),
                Mode = ProviderMode.Subscription,
                Status = ProviderStatus.NeedsReauth,
                StatusMessage = "Signed in locally, but usage API returned no data. Try signing in again in Cursor.",
                FetchedAt = DateTimeOffset.UtcNow
            };
        }

        var start = ParseUnixMs(usage.BillingCycleStart) ?? StartOfMonthUtc();
        var end = ParseUnixMs(usage.BillingCycleEnd) ?? start.AddMonths(1);
        var pu = usage.PlanUsage;

        var limitCents = pu.Limit > 0 ? pu.Limit : plan?.PlanInfo?.IncludedAmountCents ?? 0;
        var usedCents = pu.IncludedSpend;
        if (limitCents <= 0 && pu.TotalPercentUsed is > 0)
        {
            // Percent-only fallback
            limitCents = 100;
            usedCents = pu.TotalPercentUsed.Value;
        }

        var totalPercent = pu.TotalPercentUsed
            ?? (limitCents > 0 ? usedCents / limitCents * 100 : 0);

        var meters = new List<UsageMeter>
        {
            new()
            {
                Id = "total",
                Label = "Total",
                Kind = MeterKind.Monthly,
                Start = start,
                End = end,
                ResetsAt = end,
                Used = usedCents,
                Limit = Math.Max(limitCents, usedCents > 0 ? usedCents : 1),
                Unit = MeterUnit.UsdCents,
                Series = []
            },
            BuildWeeklyDerivedMeter(start, end, usedCents, limitCents, totalPercent)
        };

        if (pu.AutoPercentUsed is not null)
        {
            meters.Add(new UsageMeter
            {
                Id = "auto",
                Label = "Auto",
                Kind = MeterKind.Monthly,
                Start = start,
                End = end,
                ResetsAt = end,
                Used = pu.AutoPercentUsed.Value,
                Limit = 100,
                Unit = MeterUnit.Percent
            });
        }

        if (pu.ApiPercentUsed is not null)
        {
            meters.Add(new UsageMeter
            {
                Id = "api",
                Label = "API",
                Kind = MeterKind.Monthly,
                Start = start,
                End = end,
                ResetsAt = end,
                Used = pu.ApiPercentUsed.Value,
                Limit = 100,
                Unit = MeterUnit.Percent
            });
        }

        var extras = new List<ExtraStat>();
        if (pu.BonusSpend > 0)
            extras.Add(new ExtraStat("Bonus spend", FormatUsd(pu.BonusSpend)));
        if (usage.SpendLimitUsage?.IndividualUsed is not null
            || usage.SpendLimitUsage?.TotalSpend is > 0)
        {
            var onDemand = usage.SpendLimitUsage.IndividualUsed
                ?? usage.SpendLimitUsage.TotalSpend
                ?? 0;
            extras.Add(new ExtraStat("On-demand", FormatUsd(onDemand)));
        }

        var planLabel = plan?.PlanInfo?.PlanName
            ?? TitleCase(session.MembershipType)
            ?? "Cursor";

        return new ProviderSnapshot
        {
            Id = Id,
            DisplayName = DisplayName,
            PlanLabel = planLabel,
            Mode = ProviderMode.Subscription,
            Status = ProviderStatus.Ok,
            StatusMessage = usage.DisplayMessage,
            FetchedAt = DateTimeOffset.UtcNow,
            Meters = meters,
            Extras = extras
        };
    }

    private static UsageMeter BuildWeeklyDerivedMeter(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        double usedCents,
        double limitCents,
        double totalPercent)
    {
        var now = DateTimeOffset.UtcNow;
        var weekStart = StartOfWeek(now);
        var weekEnd = weekStart.AddDays(7);

        // Fair-share weekly budget from the billing period.
        var periodDays = Math.Max(1, (periodEnd - periodStart).TotalDays);
        var weeklyLimit = limitCents > 0
            ? limitCents * (7.0 / periodDays)
            : 100.0 / (periodDays / 7.0);

        // Without per-day history yet, approximate this week's spend by remaining
        // linear share of total used across elapsed period days in this week.
        var elapsedPeriodDays = Math.Max(0.01, (now - periodStart).TotalDays);
        var daysIntoWeek = Math.Clamp((now - weekStart).TotalDays, 0.01, 7);
        var approxWeekUsed = usedCents * (daysIntoWeek / elapsedPeriodDays);
        if (limitCents <= 0)
            approxWeekUsed = totalPercent * (daysIntoWeek / 7.0);

        return new UsageMeter
        {
            Id = "weekly",
            Label = "Weekly",
            Kind = MeterKind.Weekly,
            Start = weekStart,
            End = weekEnd,
            ResetsAt = weekEnd,
            Used = Math.Min(approxWeekUsed, weeklyLimit > 0 ? weeklyLimit * 3 : approxWeekUsed),
            Limit = Math.Max(weeklyLimit, 1),
            Unit = limitCents > 0 ? MeterUnit.UsdCents : MeterUnit.Percent
        };
    }

    private ProviderSnapshot NotLinked(string message) => new()
    {
        Id = Id,
        DisplayName = DisplayName,
        Mode = ProviderMode.Subscription,
        Status = ProviderStatus.NotLinked,
        StatusMessage = message,
        FetchedAt = DateTimeOffset.UtcNow
    };

    private ProviderSnapshot Error(string message, CursorAuthSession? session = null) => new()
    {
        Id = Id,
        DisplayName = DisplayName,
        PlanLabel = TitleCase(session?.MembershipType),
        Mode = ProviderMode.Subscription,
        Status = ProviderStatus.Error,
        StatusMessage = message,
        FetchedAt = DateTimeOffset.UtcNow
    };

    private static DateTimeOffset? ParseUnixMs(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!long.TryParse(value, out var ms)) return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(ms);
    }

    private static DateTimeOffset StartOfMonthUtc()
    {
        var now = DateTime.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private static DateTimeOffset StartOfWeek(DateTimeOffset now)
    {
        // Monday-start week
        var diff = ((int)now.DayOfWeek + 6) % 7;
        var day = now.Date.AddDays(-diff);
        return new DateTimeOffset(day, TimeSpan.Zero);
    }

    private static string FormatUsd(double cents) => $"${cents / 100.0:0.00}";

    private static string? TitleCase(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}
