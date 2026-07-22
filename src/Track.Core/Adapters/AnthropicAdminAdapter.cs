using Track.Core.Models;

namespace Track.Core.Adapters;

/// <summary>Phase 2: Anthropic Usage &amp; Cost Admin API.</summary>
public sealed class AnthropicAdminAdapter : IProviderAdapter
{
    public string Id => "anthropic";
    public string DisplayName => "Claude API";
    public bool CanAutoDetect => false;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<ProviderSnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProviderSnapshot
        {
            Id = Id,
            DisplayName = DisplayName,
            Mode = ProviderMode.ApiCost,
            Status = ProviderStatus.NotLinked,
            StatusMessage = "Add an Anthropic Admin API key in Settings (Phase 2).",
            FetchedAt = DateTimeOffset.UtcNow
        });
}
