using Track.Core.Models;

namespace Track.Core.Adapters;

/// <summary>Phase 2: OpenAI Organization Usage + Costs Admin API.</summary>
public sealed class OpenAIAdminAdapter : IProviderAdapter
{
    public string Id => "openai";
    public string DisplayName => "OpenAI API";
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
            StatusMessage = "Add an OpenAI Admin API key in Settings (Phase 2).",
            FetchedAt = DateTimeOffset.UtcNow
        });
}
