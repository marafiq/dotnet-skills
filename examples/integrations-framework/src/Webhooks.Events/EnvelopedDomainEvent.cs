namespace Webhooks.Events;

/// <summary>
/// The wire-shape of an event flowing through the system.
/// Outbound consumes envelopes; producing modules wrap their typed events into this shape.
/// </summary>
public sealed record EnvelopedDomainEvent(
    Guid EventId,
    EventTypeRef Type,
    TenantId TenantId,
    DateTime OccurredUtc,
    byte[] PayloadJson,
    IReadOnlyDictionary<string, string> Metadata);
