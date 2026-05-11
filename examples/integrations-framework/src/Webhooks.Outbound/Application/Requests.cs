using Webhooks.Events;
using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Application;

public sealed record RegisterEndpointRequest(
    TenantId TenantId,
    string Name,
    string Url,
    AuthScheme Auth,
    RetryPolicy? RetryPolicy = null);

public abstract record RegisterEndpointOutcome
{
    public sealed record Registered(EndpointId Id) : RegisterEndpointOutcome;
    public sealed record InvalidUrl(UrlParseOutcome Reason) : RegisterEndpointOutcome;
    private RegisterEndpointOutcome() { }
}

/// <summary>
/// Read-side projection. Separate type from the aggregate — never expose
/// the live entity at the boundary.
/// </summary>
public sealed record EndpointSummary(
    EndpointId Id,
    TenantId TenantId,
    string Name,
    string Url,
    string AuthSchemeKind,
    EndpointStatus Status,
    int SubscriptionCount,
    long TotalDeliveries,
    DateTime CreatedAt);
