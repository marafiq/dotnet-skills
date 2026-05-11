using Webhooks.Audit;
using Webhooks.Events;
using Webhooks.Inbound.Domain;

namespace Webhooks.Inbound.Application;

public sealed record RegisterSourceRequest(
    TenantId TenantId,
    string Name,
    ReceiveSlug Slug,
    VerificationScheme Verification,
    IdempotencyKeyExtractor IdempotencyKey,
    DispatchHandlerKey DispatchHandler);

public abstract record RegisterSourceOutcome
{
    public sealed record Registered(SourceId Id) : RegisterSourceOutcome;
    public sealed record SlugAlreadyTaken(ReceiveSlug Slug) : RegisterSourceOutcome;
    public sealed record HandlerNotRegistered(DispatchHandlerKey Key) : RegisterSourceOutcome;
    private RegisterSourceOutcome() { }
}

public sealed record SourceSummary(
    SourceId Id,
    TenantId TenantId,
    string Name,
    ReceiveSlug Slug,
    string VerificationKind,
    DispatchHandlerKey DispatchHandler,
    SourceStatus Status,
    long Verified,
    long Rejected,
    long Duplicates,
    DateTime CreatedAt);

/// <summary>Admin-side role interface (controllers).</summary>
public interface IInboundSourceAdmin
{
    Task<RegisterSourceOutcome> RegisterAsync(
        RegisterSourceRequest request, ActorRef actor, CancellationToken ct);
    Task PauseAsync(SourceId id, string reason, ActorRef actor, CancellationToken ct);
    Task ResumeAsync(SourceId id, ActorRef actor, CancellationToken ct);
}

/// <summary>HTTP-adapter-side role interface (a request lands, this routes).</summary>
public interface IInboundReceiver
{
    Task<ReceiveOutcome> ReceiveAsync(
        ReceiveSlug slug, IncomingRequest request, CancellationToken ct);
}

public abstract record ReceiveOutcome
{
    public sealed record Accepted(IdempotencyKey Key) : ReceiveOutcome;
    public sealed record Duplicate(IdempotencyKey Key) : ReceiveOutcome;
    public sealed record Rejected(VerificationOutcome Reason) : ReceiveOutcome;
    public sealed record SlugUnknown : ReceiveOutcome;
    public sealed record SourceNotActive(SourceStatus Status) : ReceiveOutcome;
    public sealed record HandlerMissing(DispatchHandlerKey Key) : ReceiveOutcome;
    public sealed record DispatchFailed(string Reason) : ReceiveOutcome;
    public sealed record IgnoredByHandler(string Reason) : ReceiveOutcome;
    private ReceiveOutcome() { }
}

/// <summary>Dashboard-side reads.</summary>
public interface IInboundSourceReads
{
    Task<SourceSummary?> GetByIdAsync(SourceId id, CancellationToken ct);
    IAsyncEnumerable<SourceSummary> ListAsync(TenantId tenant, CancellationToken ct);
}
