using Webhooks.Events;
using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Infrastructure;

/// <summary>
/// Per-aggregate, internal, designed around writes.
/// No IRepository&lt;T&gt;. No exposure to other modules.
/// </summary>
internal interface IOutboundEndpointRepository
{
    Task<OutboundEndpoint?> GetForUpdateAsync(EndpointId id, CancellationToken ct);
    Task SaveAsync(OutboundEndpoint endpoint, CancellationToken ct);
    IAsyncEnumerable<OutboundEndpoint> ListForTenantAsync(TenantId tenant, CancellationToken ct);
    IAsyncEnumerable<OutboundEndpoint> ListSubscribersAsync(TenantId tenant, EventTypeRef type, CancellationToken ct);
}
