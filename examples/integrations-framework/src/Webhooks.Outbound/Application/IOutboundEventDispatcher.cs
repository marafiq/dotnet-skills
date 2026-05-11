using Webhooks.Events;
using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Application;

/// <summary>
/// Role-shaped public surface for the worker that consumes domain events
/// from the outbox and dispatches deliveries.
/// </summary>
public interface IOutboundEventDispatcher
{
    /// <summary>
    /// Fan out to every subscribed endpoint for this event. Returns one outcome per endpoint.
    /// </summary>
    Task<IReadOnlyList<DispatchResult>> DispatchAsync(
        EnvelopedDomainEvent envelope, CancellationToken ct);
}

public sealed record DispatchResult(EndpointId EndpointId, DeliveryOutcome Outcome);
