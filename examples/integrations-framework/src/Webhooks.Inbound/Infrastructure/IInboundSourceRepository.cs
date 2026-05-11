using Webhooks.Events;
using Webhooks.Inbound.Domain;

namespace Webhooks.Inbound.Infrastructure;

internal interface IInboundSourceRepository
{
    Task<InboundSource?> GetForUpdateAsync(SourceId id, CancellationToken ct);
    Task<InboundSource?> FindBySlugAsync(ReceiveSlug slug, CancellationToken ct);
    Task SaveAsync(InboundSource source, CancellationToken ct);
    IAsyncEnumerable<InboundSource> ListForTenantAsync(TenantId tenant, CancellationToken ct);
}

internal interface IIdempotencyStore
{
    /// <summary>Returns true if first-seen; false if already recorded for this source.</summary>
    Task<bool> RecordIfFirstAsync(SourceId source, IdempotencyKey key, CancellationToken ct);
}
