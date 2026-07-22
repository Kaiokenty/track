using Track.Core.Models;

namespace Track.Core.Adapters;

/// <summary>
/// Phase 1: read local Cursor session + unofficial dashboard RPC.
/// Scaffold returns a NotLinked / placeholder until implemented.
/// </summary>
public sealed class CursorSessionAdapter : IProviderAdapter
{
    public string Id => "cursor";
    public string DisplayName => "Cursor";
    public bool CanAutoDetect => true;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Phase 1: probe %APPDATA%\Cursor\User\globalStorage\state.vscdb (or equivalent).
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cursor"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "cursor")
        };

        return Task.FromResult(candidates.Any(Directory.Exists));
    }

    public async Task<ProviderSnapshot> FetchAsync(CancellationToken cancellationToken = default)
    {
        var available = await IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        if (!available)
        {
            return new ProviderSnapshot
            {
                Id = Id,
                DisplayName = DisplayName,
                Mode = ProviderMode.Subscription,
                Status = ProviderStatus.NotLinked,
                StatusMessage = "Cursor not detected. Install and sign in, then reconnect.",
                FetchedAt = DateTimeOffset.UtcNow
            };
        }

        // Placeholder until Phase 1 wires GetCurrentPeriodUsage.
        var periodStart = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1);

        return new ProviderSnapshot
        {
            Id = Id,
            DisplayName = DisplayName,
            PlanLabel = "Detected (not linked yet)",
            Mode = ProviderMode.Subscription,
            Status = ProviderStatus.NotLinked,
            StatusMessage = "Local Cursor install found. Phase 1 will sync session usage.",
            FetchedAt = DateTimeOffset.UtcNow,
            Meters =
            [
                new UsageMeter
                {
                    Id = "total",
                    Label = "Total",
                    Kind = MeterKind.Monthly,
                    Start = periodStart,
                    End = periodEnd,
                    ResetsAt = periodEnd,
                    Used = 0,
                    Limit = 100,
                    Unit = MeterUnit.Percent
                }
            ]
        };
    }
}
