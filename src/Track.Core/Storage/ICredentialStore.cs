namespace Track.Core.Storage;

/// <summary>
/// Phase 1+: store Admin API keys / tokens via Windows Credential Manager.
/// Scaffold keeps an in-memory stub so Core stays free of Win32 deps.
/// </summary>
public interface ICredentialStore
{
    void SetSecret(string key, string value);
    string? GetSecret(string key);
    void RemoveSecret(string key);
}

public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

    public void SetSecret(string key, string value) => _secrets[key] = value;

    public string? GetSecret(string key) =>
        _secrets.TryGetValue(key, out var value) ? value : null;

    public void RemoveSecret(string key) => _secrets.Remove(key);
}
