using Track.Core.Adapters;
using Track.Core.Models;
using Track.Core.Storage;

namespace Track.Core.Services;

/// <summary>
/// Stale-while-revalidate: callers always get the last good snapshot immediately;
/// refresh runs in the background on an interval.
/// </summary>
public sealed class UsageService : IDisposable
{
    private readonly IReadOnlyList<IProviderAdapter> _adapters;
    private readonly SnapshotStore? _store;
    private readonly TimeSpan _pollInterval;
    private readonly Dictionary<string, ProviderSnapshot> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public UsageService(
        IEnumerable<IProviderAdapter> adapters,
        TimeSpan? pollInterval = null,
        SnapshotStore? store = null)
    {
        _adapters = adapters.ToList();
        _pollInterval = pollInterval ?? TimeSpan.FromMinutes(5);
        _store = store;
    }

    public event EventHandler? SnapshotsChanged;

    public IReadOnlyList<ProviderSnapshot> GetCachedSnapshots()
    {
        lock (_gate)
        {
            return _adapters
                .Select(a => _cache.TryGetValue(a.Id, out var snap)
                    ? snap
                    : Placeholder(a))
                .ToList();
        }
    }

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = RunLoopAsync(_cts.Token);
    }

    public async Task RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        foreach (var adapter in _adapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var snap = await adapter.FetchAsync(cancellationToken).ConfigureAwait(false);
                lock (_gate) _cache[adapter.Id] = snap;
                RecordHistory(snap);
            }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    if (_cache.TryGetValue(adapter.Id, out var previous))
                    {
                        _cache[adapter.Id] = previous with
                        {
                            Status = ProviderStatus.Stale,
                            StatusMessage = $"Refresh failed: {ex.Message}"
                        };
                    }
                    else
                    {
                        _cache[adapter.Id] = new ProviderSnapshot
                        {
                            Id = adapter.Id,
                            DisplayName = adapter.DisplayName,
                            Mode = ProviderMode.Subscription,
                            Status = ProviderStatus.Error,
                            StatusMessage = ex.Message,
                            FetchedAt = DateTimeOffset.UtcNow
                        };
                    }
                }
            }
        }

        SnapshotsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        await RefreshNowAsync(cancellationToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(_pollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await RefreshNowAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void RecordHistory(ProviderSnapshot snap)
    {
        if (_store is null || snap.Status is not (ProviderStatus.Ok or ProviderStatus.Stale))
            return;

        foreach (var meter in snap.Meters)
        {
            try
            {
                _store.AppendMeterSample(
                    snap.Id,
                    meter.Id,
                    meter.Used,
                    meter.Limit,
                    snap.FetchedAt);
            }
            catch
            {
                // History is best-effort.
            }
        }
    }

    private static ProviderSnapshot Placeholder(IProviderAdapter adapter) => new()
    {
        Id = adapter.Id,
        DisplayName = adapter.DisplayName,
        Mode = ProviderMode.Subscription,
        Status = ProviderStatus.NotLinked,
        StatusMessage = "Waiting for first refresh…",
        FetchedAt = DateTimeOffset.UtcNow
    };
}
