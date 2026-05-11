using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Webhooks.Events;
using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Infrastructure;

internal sealed class InMemoryOutboundRepository : IOutboundEndpointRepository
{
    private readonly ConcurrentDictionary<EndpointId, OutboundEndpoint> _store = new();

    public Task<OutboundEndpoint?> GetForUpdateAsync(EndpointId id, CancellationToken ct)
        => Task.FromResult(_store.TryGetValue(id, out var ep) ? ep : null);

    public Task SaveAsync(OutboundEndpoint endpoint, CancellationToken ct)
    {
        _store[endpoint.Id] = endpoint;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<OutboundEndpoint> ListForTenantAsync(
        TenantId tenant, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        foreach (var ep in _store.Values)
        {
            if (ep.TenantId == tenant)
                yield return ep;
        }
    }

    public async IAsyncEnumerable<OutboundEndpoint> ListSubscribersAsync(
        TenantId tenant, EventTypeRef type, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        foreach (var ep in _store.Values)
        {
            if (ep.TenantId == tenant && ep.ShouldDeliver(type))
                yield return ep;
        }
    }
}

/// <summary>
/// Public seeder — the only way for app code to inject demo secrets into the
/// in-memory store. Production replacements bind their own ISecretReader and
/// do not register this seeder at all.
/// </summary>
public interface IOutboundSecretSeeder
{
    void Seed(SecretRef reference, byte[] value);
}

internal sealed class InMemorySecretReader : ISecretReader, IOutboundSecretSeeder
{
    private readonly ConcurrentDictionary<string, byte[]> _secrets = new();

    public void Seed(SecretRef reference, byte[] value) => _secrets[reference.VaultPath] = value;

    public Task<byte[]> ReadAsync(SecretRef reference, CancellationToken ct)
    {
        if (_secrets.TryGetValue(reference.VaultPath, out var v))
            return Task.FromResult(v);
        throw new KeyNotFoundException($"No secret at {reference.VaultPath}");
    }
}
