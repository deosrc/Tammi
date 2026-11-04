using System.Collections.Concurrent;
using Deosrc.Tammi.Core.Credentials;

namespace Deosrc.Tammi.Core.Tests.Credentials;

/// <summary>
/// A credential store for tests that don't need the platform secret store.
/// </summary>
internal sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new();

    public IReadOnlyDictionary<string, string> Secrets => _secrets;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_secrets.TryGetValue(key, out string? secret) ? secret : null);

    public Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        _secrets[key] = secret;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_secrets.TryRemove(key, out _));
}
