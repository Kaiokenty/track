using Track.Core.Models;

namespace Track.Core.Adapters;

public interface IProviderAdapter
{
    string Id { get; }
    string DisplayName { get; }
    bool CanAutoDetect { get; }

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<ProviderSnapshot> FetchAsync(CancellationToken cancellationToken = default);
}
