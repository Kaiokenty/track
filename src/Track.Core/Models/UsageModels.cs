namespace Track.Core.Models;

public enum ProviderMode
{
    Subscription,
    ApiCost,
    Manual
}

public enum MeterKind
{
    Monthly,
    Weekly,
    Rolling5h,
    Custom
}

public enum MeterUnit
{
    Percent,
    UsdCents,
    Requests
}

public enum ProviderStatus
{
    Ok,
    NeedsReauth,
    Error,
    Stale,
    NotLinked
}

public enum PaceVerdict
{
    Under,
    OnTrack,
    Over
}

public sealed record UsagePoint(DateTimeOffset Timestamp, double Used);

public sealed record UsageMeter
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required MeterKind Kind { get; init; }
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public DateTimeOffset? ResetsAt { get; init; }
    public double Used { get; init; }
    public double Limit { get; init; }
    public MeterUnit Unit { get; init; } = MeterUnit.Percent;
    public IReadOnlyList<UsagePoint> Series { get; init; } = [];

    public double Remaining => Math.Max(0, Limit - Used);
    public double PercentUsed => Limit <= 0 ? 0 : Math.Clamp(Used / Limit * 100, 0, 100);
    public double PercentRemaining => 100 - PercentUsed;
}

public sealed record ExtraStat(string Label, string Value);

public sealed record ProviderSnapshot
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? PlanLabel { get; init; }
    public required ProviderMode Mode { get; init; }
    public IReadOnlyList<UsageMeter> Meters { get; init; } = [];
    public IReadOnlyList<ExtraStat> Extras { get; init; } = [];
    public ProviderStatus Status { get; init; } = ProviderStatus.NotLinked;
    public string? StatusMessage { get; init; }
    public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PaceSnapshot
{
    public required UsageMeter Meter { get; init; }
    public double RecommendedPercent { get; init; }
    public double ActualPercent { get; init; }
    public double DeltaPercent => ActualPercent - RecommendedPercent;
    public PaceVerdict Verdict { get; init; }
    public DateTimeOffset? ProjectedExhaustion { get; init; }
}
