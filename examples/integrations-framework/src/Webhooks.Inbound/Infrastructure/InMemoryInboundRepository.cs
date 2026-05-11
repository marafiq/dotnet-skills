using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Webhooks.Events;
using Webhooks.Inbound.Domain;

namespace Webhooks.Inbound.Infrastructure;

internal sealed class InMemoryInboundRepository : IInboundSourceRepository
{
    private readonly ConcurrentDictionary<SourceId, InboundSource> _byId = new();

    public Task<InboundSource?> GetForUpdateAsync(SourceId id, CancellationToken ct)
        => Task.FromResult(_byId.TryGetValue(id, out var s) ? s : null);

    public Task<InboundSource?> FindBySlugAsync(ReceiveSlug slug, CancellationToken ct)
        => Task.FromResult(_byId.Values.FirstOrDefault(s => s.Slug == slug));

    public Task SaveAsync(InboundSource source, CancellationToken ct)
    {
        _byId[source.Id] = source;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<InboundSource> ListForTenantAsync(
        TenantId tenant, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        foreach (var s in _byId.Values)
            if (s.TenantId == tenant) yield return s;
    }
}

internal sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<(SourceId, IdempotencyKey), byte> _seen = new();

    public Task<bool> RecordIfFirstAsync(SourceId source, IdempotencyKey key, CancellationToken ct)
        => Task.FromResult(_seen.TryAdd((source, key), 1));
}

public interface IInboundSecretSeeder
{
    void Seed(SecretRef reference, byte[] value);
}

internal sealed class InMemoryInboundSecretReader : ISecretReader, IInboundSecretSeeder
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
