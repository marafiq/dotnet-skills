using Webhooks.Audit;
using Webhooks.Events;
using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Application;

/// <summary>
/// Role-shaped public surface for admin controllers.
/// Mutations only — no reads, no dispatch.
/// </summary>
public interface IOutboundEndpointAdmin
{
    Task<RegisterEndpointOutcome> RegisterAsync(
        RegisterEndpointRequest request, ActorRef actor, CancellationToken ct);

    Task<SubscribeOutcome> SubscribeAsync(
        EndpointId id, EventTypeRef eventType, ActorRef actor, CancellationToken ct);

    Task PauseAsync(EndpointId id, string reason, ActorRef actor, CancellationToken ct);
    Task ResumeAsync(EndpointId id, ActorRef actor, CancellationToken ct);
    Task DisableAsync(EndpointId id, ActorRef actor, CancellationToken ct);
}
