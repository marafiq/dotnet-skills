using Webhooks.Events;
using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Application;

/// <summary>
/// Role-shaped public surface for dashboards. Read-only — separate from
/// admin (no mutations) and from dispatch (no internal delivery types).
/// </summary>
public interface IOutboundEndpointReads
{
    Task<EndpointSummary?> GetByIdAsync(EndpointId id, CancellationToken ct);

    IAsyncEnumerable<EndpointSummary> ListAsync(TenantId tenant, CancellationToken ct);
}
