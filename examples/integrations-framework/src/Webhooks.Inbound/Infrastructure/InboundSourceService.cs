using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Webhooks.Audit;
using Webhooks.Compliance;
using Webhooks.Events;
using Webhooks.Inbound.Application;
using Webhooks.Inbound.Application.Verification;
using Webhooks.Inbound.Domain;

namespace Webhooks.Inbound.Infrastructure;

/// <summary>
/// One internal type, three role-shaped public surfaces (admin, receiver, reads).
/// </summary>
internal sealed class InboundSourceService(
    IInboundSourceRepository repository,
    IIdempotencyStore idempotency,
    IVerificationStrategyFactory verifierFactory,
    DispatchRegistry handlers,
    IAuditWriter audit,
    IClock clock,
    ILogger<InboundSourceService> logger)
    : IInboundSourceAdmin, IInboundReceiver, IInboundSourceReads
{
    // ---- IInboundSourceAdmin ----

    public async Task<RegisterSourceOutcome> RegisterAsync(
        RegisterSourceRequest request, ActorRef actor, CancellationToken ct)
    {
        var bySlug = await repository.FindBySlugAsync(request.Slug, ct);
        if (bySlug is not null)
            return new RegisterSourceOutcome.SlugAlreadyTaken(request.Slug);

        if (handlers.Find(request.DispatchHandler) is null)
            return new RegisterSourceOutcome.HandlerNotRegistered(request.DispatchHandler);

        var id = SourceId.New();
        var source = InboundSource.Register(
            id, request.TenantId, request.Name, request.Slug,
            request.Verification, request.IdempotencyKey,
            request.DispatchHandler, clock);
        await repository.SaveAsync(source, ct);

        await WriteAudit(actor, request.TenantId, "inbound.source.registered",
            new AuditSubject.InboundSource(id.Value),
            new Dictionary<string, string>
            {
                ["name"] = request.Name,
                ["slug"] = request.Slug.ToString(),
                ["verification"] = request.Verification.GetType().Name,
                ["dispatch"] = request.DispatchHandler.ToString(),
            }, ct);
        return new RegisterSourceOutcome.Registered(id);
    }

    public async Task PauseAsync(SourceId id, string reason, ActorRef actor, CancellationToken ct)
    {
        var source = await repository.GetForUpdateAsync(id, ct)
            ?? throw new InvalidOperationException($"Source {id} not found.");
        source.Pause();
        await repository.SaveAsync(source, ct);
        await WriteAudit(actor, source.TenantId, "inbound.source.paused",
            new AuditSubject.InboundSource(id.Value),
            new Dictionary<string, string> { ["reason"] = reason }, ct);
    }

    public async Task ResumeAsync(SourceId id, ActorRef actor, CancellationToken ct)
    {
        var source = await repository.GetForUpdateAsync(id, ct)
            ?? throw new InvalidOperationException($"Source {id} not found.");
        source.Resume();
        await repository.SaveAsync(source, ct);
        await WriteAudit(actor, source.TenantId, "inbound.source.resumed",
            new AuditSubject.InboundSource(id.Value),
            new Dictionary<string, string>(), ct);
    }

    // ---- IInboundReceiver ----

    public async Task<ReceiveOutcome> ReceiveAsync(
        ReceiveSlug slug, IncomingRequest request, CancellationToken ct)
    {
        var source = await repository.FindBySlugAsync(slug, ct);
        if (source is null) return new ReceiveOutcome.SlugUnknown();
        if (source.Status != SourceStatus.Active)
            return new ReceiveOutcome.SourceNotActive(source.Status);

        var verifier = verifierFactory.For(source.Verification);
        var verifyOutcome = await verifier.VerifyAsync(request, source, ct);
        if (verifyOutcome is not VerificationOutcome.Verified verified)
        {
            source.RecordRejected();
            await repository.SaveAsync(source, ct);
            await WriteAudit(ActorRef.System("inbound"), source.TenantId,
                "inbound.receipt.rejected",
                new AuditSubject.InboundReceipt(source.Id.Value, "<unverified>"),
                new Dictionary<string, string>
                {
                    ["reason"] = verifyOutcome.GetType().Name,
                    ["sourceIp"] = request.SourceIp,
                }, ct);
            return new ReceiveOutcome.Rejected(verifyOutcome);
        }

        var first = await idempotency.RecordIfFirstAsync(source.Id, verified.Key, ct);
        if (!first)
        {
            source.RecordDuplicate();
            await repository.SaveAsync(source, ct);
            await WriteAudit(ActorRef.System("inbound"), source.TenantId,
                "inbound.receipt.duplicate",
                new AuditSubject.InboundReceipt(source.Id.Value, verified.Key.Value),
                new Dictionary<string, string>(), ct);
            return new ReceiveOutcome.Duplicate(verified.Key);
        }

        var handler = handlers.Find(source.DispatchHandler);
        if (handler is null)
            return new ReceiveOutcome.HandlerMissing(source.DispatchHandler);

        var inboundEvent = new InboundEvent(
            source.Id, source.Name, verified.Key, request.Body,
            request.Headers, clock.UtcNow);

        ProcessingOutcome processOutcome;
        try
        {
            processOutcome = await handler.HandleAsync(inboundEvent, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Handler {Handler} threw while processing {EventKey}",
                source.DispatchHandler, verified.Key);
            return new ReceiveOutcome.DispatchFailed(ex.Message);
        }

        source.RecordVerified();
        await repository.SaveAsync(source, ct);
        await WriteAudit(ActorRef.System("inbound"), source.TenantId,
            processOutcome switch
            {
                ProcessingOutcome.Processed       => "inbound.receipt.processed",
                ProcessingOutcome.IgnoredByDesign => "inbound.receipt.ignored",
                ProcessingOutcome.Failed          => "inbound.receipt.failed",
                _ => "inbound.receipt.unknown",
            },
            new AuditSubject.InboundReceipt(source.Id.Value, verified.Key.Value),
            new Dictionary<string, string>
            {
                ["handler"] = source.DispatchHandler.ToString(),
                ["outcome"] = processOutcome.GetType().Name,
            }, ct);

        return processOutcome switch
        {
            ProcessingOutcome.Processed         => new ReceiveOutcome.Accepted(verified.Key),
            ProcessingOutcome.IgnoredByDesign i => new ReceiveOutcome.IgnoredByHandler(i.Reason),
            ProcessingOutcome.Failed f          => new ReceiveOutcome.DispatchFailed(f.Reason),
            _                                   => new ReceiveOutcome.DispatchFailed("Unknown"),
        };
    }

    // ---- IInboundSourceReads ----

    public async Task<SourceSummary?> GetByIdAsync(SourceId id, CancellationToken ct)
    {
        var s = await repository.GetForUpdateAsync(id, ct);
        return s is null ? null : Project(s);
    }

    public async IAsyncEnumerable<SourceSummary> ListAsync(
        TenantId tenant, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var s in repository.ListForTenantAsync(tenant, ct))
            yield return Project(s);
    }

    private static SourceSummary Project(InboundSource s)
        => new(s.Id, s.TenantId, s.Name, s.Slug,
               s.Verification.GetType().Name, s.DispatchHandler,
               s.Status, s.VerifiedCount, s.RejectedCount, s.DuplicateCount, s.CreatedAt);

    private async Task WriteAudit(
        ActorRef actor, TenantId tenant, string action,
        AuditSubject subject, IReadOnlyDictionary<string, string> fields,
        CancellationToken ct)
    {
        await audit.WriteAsync(new AuditEntry(
            AuditEntryId.New(), tenant, actor, action, subject,
            new AuditPayload.Structured(fields),
            clock.UtcNow, SourceIp: null,
            PhiSensitivity.None), ct);
    }
}
