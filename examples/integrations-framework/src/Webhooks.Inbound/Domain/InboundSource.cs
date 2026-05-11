using Webhooks.Events;

namespace Webhooks.Inbound.Domain;

internal sealed class InboundSource
{
    public SourceId Id { get; }
    public TenantId TenantId { get; }
    public string Name { get; private set; }
    public ReceiveSlug Slug { get; }
    public VerificationScheme Verification { get; private set; }
    public IdempotencyKeyExtractor IdempotencyKey { get; private set; }
    public DispatchHandlerKey DispatchHandler { get; private set; }
    public SourceStatus Status { get; private set; }
    public DateTime CreatedAt { get; }

    private long _verifiedCount;
    private long _rejectedCount;
    private long _duplicateCount;
    public long VerifiedCount => _verifiedCount;
    public long RejectedCount => _rejectedCount;
    public long DuplicateCount => _duplicateCount;

    private InboundSource(
        SourceId id, TenantId tenant, string name, ReceiveSlug slug,
        VerificationScheme verification, IdempotencyKeyExtractor idem,
        DispatchHandlerKey handler, DateTime createdAt)
    {
        Id = id;
        TenantId = tenant;
        Name = name;
        Slug = slug;
        Verification = verification;
        IdempotencyKey = idem;
        DispatchHandler = handler;
        Status = SourceStatus.Active;
        CreatedAt = createdAt;
    }

    public static InboundSource Register(
        SourceId id, TenantId tenant, string name, ReceiveSlug slug,
        VerificationScheme verification, IdempotencyKeyExtractor idem,
        DispatchHandlerKey handler, IClock clock)
        => new(id, tenant, name, slug, verification, idem, handler, clock.UtcNow);

    public void Pause() => Status = SourceStatus.Paused;
    public void Resume() { if (Status == SourceStatus.Paused) Status = SourceStatus.Active; }
    public void Disable() => Status = SourceStatus.Disabled;

    internal void RecordVerified() => Interlocked.Increment(ref _verifiedCount);
    internal void RecordRejected() => Interlocked.Increment(ref _rejectedCount);
    internal void RecordDuplicate() => Interlocked.Increment(ref _duplicateCount);
}
