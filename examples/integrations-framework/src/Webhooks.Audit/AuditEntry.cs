using Webhooks.Compliance;
using Webhooks.Events;

namespace Webhooks.Audit;

public readonly record struct AuditEntryId(Guid Value)
{
    public static AuditEntryId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N")[..12];
}

/// <summary>
/// What this entry is about. Discriminated value type — adding a new subject
/// kind is a new case, not a flag.
/// </summary>
public abstract record AuditSubject
{
    public sealed record OutboundEndpoint(string EndpointId) : AuditSubject;
    public sealed record InboundSource(string SourceId) : AuditSubject;
    public sealed record OutboundDelivery(string EndpointId, Guid EventId) : AuditSubject;
    public sealed record InboundReceipt(string SourceId, string IdempotencyKey) : AuditSubject;
    public sealed record SecretRotation(string EndpointId, int Version) : AuditSubject;
    private AuditSubject() { }
}

/// <summary>
/// Structured audit payload — never raw user-controlled bytes.
/// </summary>
public abstract record AuditPayload
{
    public sealed record Structured(IReadOnlyDictionary<string, string> Fields) : AuditPayload;
    public sealed record Redacted(string Reason) : AuditPayload;
    private AuditPayload() { }
}

public sealed record AuditEntry(
    AuditEntryId Id,
    TenantId TenantId,
    ActorRef Actor,
    string Action,                // domain-named, e.g. "outbound.endpoint.secret.rotated"
    AuditSubject Subject,
    AuditPayload Payload,
    DateTime OccurredUtc,
    string? SourceIp,
    PhiSensitivity Sensitivity);
