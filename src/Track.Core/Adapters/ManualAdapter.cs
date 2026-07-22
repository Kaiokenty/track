using Track.Core.Models;

namespace Track.Core.Adapters;

/// <summary>Manual limits when unofficial APIs break or a provider has no API.</summary>
public sealed class ManualAdapter : IProviderAdapter
{
    private readonly ManualProviderConfig _config;

    public ManualAdapter(ManualProviderConfig config)
    {
        _config = config;
    }

    public string Id => _config.Id;
    public string DisplayName => _config.DisplayName;
    public bool CanAutoDetect => false;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<ProviderSnapshot> FetchAsync(CancellationToken cancellationToken = default)
    {
        var meter = new UsageMeter
        {
            Id = "manual",
            Label = _config.MeterLabel,
            Kind = _config.Kind,
            Start = _config.Start,
            End = _config.End,
            ResetsAt = _config.End,
            Used = _config.Used,
            Limit = _config.Limit,
            Unit = _config.Unit
        };

        return Task.FromResult(new ProviderSnapshot
        {
            Id = Id,
            DisplayName = DisplayName,
            PlanLabel = "Manual",
            Mode = ProviderMode.Manual,
            Status = ProviderStatus.Ok,
            FetchedAt = DateTimeOffset.UtcNow,
            Meters = [meter]
        });
    }
}

public sealed class ManualProviderConfig
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string MeterLabel { get; init; } = "Usage";
    public MeterKind Kind { get; init; } = MeterKind.Monthly;
    public MeterUnit Unit { get; init; } = MeterUnit.Percent;
    public double Used { get; init; }
    public double Limit { get; init; } = 100;
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}
