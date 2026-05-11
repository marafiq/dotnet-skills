using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Webhooks.Audit;
using Webhooks.Events;
using Webhooks.Outbound.Application;
using Webhooks.Outbound.Application.Auth;
using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Infrastructure;

/// <summary>
/// One internal type, three role-shaped public surfaces (admin, dispatcher, reads).
/// Composition over duplication; encapsulation preserved.
/// </summary>
internal sealed class OutboundEndpointService(
    IOutboundEndpointRepository repository,
    IAuthSchemeApplicatorFactory authFactory,
    HttpClient http,
    IAuditWriter audit,
    IClock clock,
    ILogger<OutboundEndpointService> logger)
    : IOutboundEndpointAdmin, IOutboundEventDispatcher, IOutboundEndpointReads
{
    // ---- IOutboundEndpointAdmin ----

    public async Task<RegisterEndpointOutcome> RegisterAsync(
        RegisterEndpointRequest request, ActorRef actor, CancellationToken ct)
    {
        var urlOutcome = EndpointUrl.Create(request.Url);
        if (urlOutcome is not UrlParseOutcome.Created urlOk)
            return new RegisterEndpointOutcome.InvalidUrl(urlOutcome);

        var id = EndpointId.New();
        var endpoint = OutboundEndpoint.Register(
            id, request.TenantId, request.Name, urlOk.Url,
            request.Auth, request.RetryPolicy ?? RetryPolicy.Standard, clock);

        await repository.SaveAsync(endpoint, ct);
        await WriteAudit(actor, request.TenantId, "outbound.endpoint.registered",
            new AuditSubject.OutboundEndpoint(id.Value),
            new Dictionary<string, string>
            {
                ["name"] = request.Name,
                ["url"] = urlOk.Url.ToString(),
                ["auth"] = request.Auth.GetType().Name,
            }, ct);

        return new RegisterEndpointOutcome.Registered(id);
    }

    public async Task<SubscribeOutcome> SubscribeAsync(
        EndpointId id, EventTypeRef eventType, ActorRef actor, CancellationToken ct)
    {
        var endpoint = await repository.GetForUpdateAsync(id, ct)
            ?? throw new InvalidOperationException($"Endpoint {id} not found.");

        var outcome = endpoint.Subscribe(eventType, clock);
        await repository.SaveAsync(endpoint, ct);

        await WriteAudit(actor, endpoint.TenantId, "outbound.endpoint.subscribed",
            new AuditSubject.OutboundEndpoint(id.Value),
            new Dictionary<string, string> { ["eventType"] = eventType.ToString() }, ct);
        return outcome;
    }

    public async Task PauseAsync(EndpointId id, string reason, ActorRef actor, CancellationToken ct)
    {
        var endpoint = await repository.GetForUpdateAsync(id, ct)
            ?? throw new InvalidOperationException($"Endpoint {id} not found.");
        endpoint.Pause();
        await repository.SaveAsync(endpoint, ct);
        await WriteAudit(actor, endpoint.TenantId, "outbound.endpoint.paused",
            new AuditSubject.OutboundEndpoint(id.Value),
            new Dictionary<string, string> { ["reason"] = reason }, ct);
    }

    public async Task ResumeAsync(EndpointId id, ActorRef actor, CancellationToken ct)
    {
        var endpoint = await repository.GetForUpdateAsync(id, ct)
            ?? throw new InvalidOperationException($"Endpoint {id} not found.");
        endpoint.Resume();
        await repository.SaveAsync(endpoint, ct);
        await WriteAudit(actor, endpoint.TenantId, "outbound.endpoint.resumed",
            new AuditSubject.OutboundEndpoint(id.Value),
            new Dictionary<string, string>(), ct);
    }

    public async Task DisableAsync(EndpointId id, ActorRef actor, CancellationToken ct)
    {
        var endpoint = await repository.GetForUpdateAsync(id, ct)
            ?? throw new InvalidOperationException($"Endpoint {id} not found.");
        endpoint.Disable();
        await repository.SaveAsync(endpoint, ct);
        await WriteAudit(actor, endpoint.TenantId, "outbound.endpoint.disabled",
            new AuditSubject.OutboundEndpoint(id.Value),
            new Dictionary<string, string>(), ct);
    }

    // ---- IOutboundEventDispatcher ----

    public async Task<IReadOnlyList<DispatchResult>> DispatchAsync(
        EnvelopedDomainEvent envelope, CancellationToken ct)
    {
        var results = new List<DispatchResult>();
        await foreach (var endpoint in repository.ListSubscribersAsync(
            envelope.TenantId, envelope.Type, ct))
        {
            var outcome = await DeliverOnceAsync(endpoint, envelope, attempt: 1, ct);
            endpoint.RecordDelivery(envelope.Type);
            await repository.SaveAsync(endpoint, ct);
            results.Add(new DispatchResult(endpoint.Id, outcome));

            await WriteAudit(ActorRef.System("dispatcher"), endpoint.TenantId,
                outcome switch
                {
                    DeliveryOutcome.Delivered          => "outbound.delivery.delivered",
                    DeliveryOutcome.Retrying           => "outbound.delivery.retrying",
                    DeliveryOutcome.DeadLettered       => "outbound.delivery.dead_lettered",
                    DeliveryOutcome.ConfigurationError => "outbound.delivery.config_error",
                    DeliveryOutcome.EndpointNotActive  => "outbound.delivery.skipped_inactive",
                    _ => "outbound.delivery.unknown",
                },
                new AuditSubject.OutboundDelivery(endpoint.Id.Value, envelope.EventId),
                new Dictionary<string, string>
                {
                    ["eventType"] = envelope.Type.ToString(),
                    ["outcome"] = outcome.GetType().Name,
                }, ct);
        }
        return results;
    }

    private async Task<DeliveryOutcome> DeliverOnceAsync(
        OutboundEndpoint endpoint, EnvelopedDomainEvent envelope, int attempt, CancellationToken ct)
    {
        if (endpoint.Status != EndpointStatus.Active)
            return new DeliveryOutcome.EndpointNotActive(endpoint.Status);

        var body = envelope.PayloadJson;
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url.Uri)
        {
            Content = new ByteArrayContent(body)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") },
            },
        };
        request.Headers.TryAddWithoutValidation("X-Webhook-Event-Id", envelope.EventId.ToString());
        request.Headers.TryAddWithoutValidation("X-Webhook-Event-Type", envelope.Type.ToString());
        request.Headers.TryAddWithoutValidation("X-Webhook-Tenant", envelope.TenantId.Value);

        var applicator = authFactory.For(endpoint.Auth);
        var applyOutcome = await applicator.ApplyAsync(request, body, ct);
        if (applyOutcome is ApplyOutcome.Failed failed)
            return new DeliveryOutcome.ConfigurationError(failed.Problem);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var response = await http.SendAsync(request, ct);
            sw.Stop();
            if (response.IsSuccessStatusCode)
                return new DeliveryOutcome.Delivered(attempt, sw.Elapsed, (int)response.StatusCode);

            var failureReason = new FailureReason.HttpStatus((int)response.StatusCode, response.ReasonPhrase ?? "");
            return ScheduleNextOrDeadLetter(endpoint, attempt, failureReason);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return ScheduleNextOrDeadLetter(endpoint, attempt, new FailureReason.Timeout(sw.Elapsed));
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException)
        {
            return ScheduleNextOrDeadLetter(endpoint, attempt, new FailureReason.ConnectionRefused(ex.Message));
        }
        catch (HttpRequestException ex)
        {
            return ScheduleNextOrDeadLetter(endpoint, attempt, new FailureReason.InvalidResponse(ex.Message));
        }
    }

    private DeliveryOutcome ScheduleNextOrDeadLetter(
        OutboundEndpoint endpoint, int attempt, FailureReason reason)
    {
        var policy = endpoint.RetryPolicy;
        if (attempt >= policy.MaxAttempts)
            return new DeliveryOutcome.DeadLettered(attempt, reason);

        var nextDelay = policy.Schedule[attempt - 1];
        return new DeliveryOutcome.Retrying(attempt, attempt + 1, clock.UtcNow.Add(nextDelay), reason);
    }

    // ---- IOutboundEndpointReads ----

    public async Task<EndpointSummary?> GetByIdAsync(EndpointId id, CancellationToken ct)
    {
        var endpoint = await repository.GetForUpdateAsync(id, ct);
        return endpoint is null ? null : Project(endpoint);
    }

    public async IAsyncEnumerable<EndpointSummary> ListAsync(
        TenantId tenant, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var ep in repository.ListForTenantAsync(tenant, ct))
            yield return Project(ep);
    }

    private static EndpointSummary Project(OutboundEndpoint e)
        => new(
            e.Id, e.TenantId, e.Name, e.Url.ToString(),
            e.Auth.GetType().Name,
            e.Status,
            e.Subscriptions.Count,
            e.Subscriptions.Values.Sum(s => s.DeliveryCount),
            e.CreatedAt);

    // ---- helpers ----

    private async Task WriteAudit(
        ActorRef actor, TenantId tenant, string action,
        AuditSubject subject, IReadOnlyDictionary<string, string> fields,
        CancellationToken ct)
    {
        await audit.WriteAsync(new AuditEntry(
            AuditEntryId.New(), tenant, actor, action, subject,
            new AuditPayload.Structured(fields),
            clock.UtcNow, SourceIp: null,
            Compliance.PhiSensitivity.None), ct);
    }
}
