# Worked example — ALIS Integrations in .NET 10

The skill applied to a real, complex system: webhooks (outbound + inbound) for a senior-living platform. Mockup defines the surface; this document shows the design choices and the code shapes that fall out when the skill is followed end-to-end.

> **The framework this document describes exists as runnable code.** See [`examples/integrations-framework/`](../../../../../examples/integrations-framework/) — five module projects, a sample app, ~2,300 lines of C# 14 / .NET 10. Run `dotnet run --project samples/Webhooks.SampleApp` to see the whole flow round-trip. Tampering rejected, idempotency enforced, audit captured. Code snippets in this document are abridged; the executable is the canonical artifact.

## Discover before applying

Walk the cues:

- **Anemic-domain risk?** No. The entities have real invariants (a paused endpoint must not deliver; secret rotation has timeline state; a delivery's attempt count must not exceed the policy). State transitions are real domain operations. → DDD aggregate work, *not* setters.
- **Missing-value-object risk?** Yes — easy to flatten. `RetryPolicy`, `EndpointUrl`, `AuthScheme`, `VerificationScheme`, `DeliveryOutcome`, `IdempotencyKey`, `EventTypeRef` are all value objects waiting to be named. The flat alternative would be 30-property bags on `OutboundEndpoint` and `InboundSource`.
- **Class-level smells in module clothing?** Possible — the auth/verification scheme variation could be coded as nested ifs and bool flags inside `OutboundEndpoint.Deliver()`. Refuse: that is class-level OCP work (strategy), not boundary work.
- **"All coupling is bad" risk?** Yes — temptation to interface-decouple `EventTypeRef` between Outbound and the catalog module. Refuse: it is a stable value object; sharing it directly is cheaper, clearer, and correct.
- **Generic repository risk?** Yes — temptation to expose `IRepository<OutboundEndpoint>`. Refuse: per-aggregate repository, kept `internal`, designed around writes.
- **`Common.Abstractions` risk?** Yes — temptation to put `IInboundEventProcessor` and `IAuditWriter` in a shared library. Refuse: each lives in its owning module's namespace; consumers either implement (DIP) or call the producer's narrow public surface (foundational-module exception).

The problem is genuinely a contract question. Multiple bounded contexts (Outbound vs Inbound), multiple substitutable strategies (auth schemes, verification schemes), multiple consumers per module (admin UI, dispatcher worker, dashboard reads). Frame applies — proceed.

## State the problem before applying

**What is the problem?**
Two bounded contexts — Outbound (push to partners) and Inbound (receive from partners) — must support several auth/verification schemes today and admit new ones cheaply, ship reliable delivery (retry, dead-letter, secret rotation with grace), dispatch to handler code in other modules, and stay HIPAA-defensible (PHI redaction at every leak surface).

**How is it solved?**
- **Two modules** (`Webhooks.Outbound`, `Webhooks.Inbound`) — different aggregates, different invariants, different operational characteristics. Bounded contexts (SRP at boundary).
- **Strategy** for auth and verification schemes — closed core (the abstract `AuthScheme` discriminated record + `IAuthSchemeApplicator` interface), open extension (each scheme is a class). OCP at boundary.
- **Discriminated outcome types** for delivery and verification — no exceptions for expected outcomes (`Delivered`, `Retrying`, `DeadLettered`, `Rejected`, `Duplicate`). Strong domain over exception flow.
- **DIP for cross-module dispatch** — Inbound declares `IInboundEventProcessor`; Billing/Residents/etc. modules implement. Audit goes the other way (foundational module exception): `Webhooks.Audit` exposes a stable `IAuditWriter` consumed by all.
- **Three role-shaped public interfaces per module** — admin (controller), dispatcher (worker), reads (dashboard). ISP per consumer.
- **Per-aggregate repositories**, `internal sealed`, designed around writes.
- **Shared `EventTypeRef` value object** — coupling that has no cost; do not interface-decouple.
- **Small shared kernel** in `Webhooks.Compliance` for PHI tagging and redaction — justified by being narrow, stable, and genuinely cross-cutting.

**Solution review.**
- **Buys:** new auth scheme = one new class registered in DI; new inbound provider = one new template + verifier; new dispatch handler = a class implementing `IInboundEventProcessor`. Each module ships its changes independently; `EventTypeCatalog` evolves with explicit version pinning. Outcome types make every consumer's error-handling complete by compiler enforcement.
- **Costs:** discipline required to keep `Common.Abstractions` from sneaking in (the easy thing is rarely the right thing). DI wiring grows with strategy count. Outbound and Inbound each carry their own infrastructure (HTTP client, retry, persistence) — some duplication.
- **Flips:** if the system grows to deliver across tenant boundaries (one ALIS tenant pushing to another's inbound), the Outbound discriminator (in-tenant DomainEvent vs cross-system IntegrationEvent) becomes load-bearing — see `modular-monolith`. If verification schemes grow to need pluggable challenge/response (Slack url_verification, SES SubscriptionConfirmation), `IVerificationSchemeStrategy` may need a richer protocol than "verify a single request."

## Module topology

```
src/
├── Webhooks.Outbound/             # bounded context: push to partners
├── Webhooks.Inbound/              # bounded context: receive from partners
├── Webhooks.Events/               # event type catalog, version registry
├── Webhooks.Audit/                # foundational module: append-only audit
└── Webhooks.Compliance/           # shared kernel: PHI tagging + redaction
```

Five modules. The first two are aggregates with public role-shaped surfaces. The last three are foundational modules with stable, narrow surfaces consumed by the first two (and by domain modules).

## `Webhooks.Outbound` — the canonical aggregate

### The aggregate root (encapsulation, strong domain)

```csharp
namespace Webhooks.Outbound.Domain;

internal sealed class OutboundEndpoint
{
    public EndpointId Id { get; }
    public TenantId TenantId { get; }
    public string Name { get; private set; }
    public EndpointUrl Url { get; private set; }
    public AuthScheme Auth { get; private set; }
    public RetryPolicy RetryPolicy { get; private set; }
    public EndpointStatus Status { get; private set; }

    private readonly List<Subscription> _subscriptions = [];
    public IReadOnlyList<Subscription> Subscriptions => _subscriptions;

    private SecretRotation? _rotation;
    public SecretRotation? ActiveRotation => _rotation;

    private OutboundEndpoint(EndpointId id, TenantId tenantId, string name,
                             EndpointUrl url, AuthScheme auth, RetryPolicy policy)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Url = url;
        Auth = auth;
        RetryPolicy = policy;
        Status = EndpointStatus.Active;
    }

    public static OutboundEndpoint Register(EndpointId id, TenantId tenantId, string name,
                                            EndpointUrl url, AuthScheme auth, RetryPolicy policy)
        => new(id, tenantId, name, url, auth, policy);

    public RotationStarted BeginSecretRotation(GraceWindow window, IClock clock)
    {
        if (_rotation is { Status: RotationStatus.InProgress })
            return new RotationStarted.AlreadyInProgress(_rotation);
        _rotation = SecretRotation.Begin(window, clock.UtcNow);
        return new RotationStarted.Begun(_rotation);
    }

    public void RetireOldSecret(IClock clock)
    {
        if (_rotation is null) return;
        _rotation.Retire(clock.UtcNow);
    }

    public SubscribeOutcome Subscribe(EventTypeRef eventType)
    {
        if (_subscriptions.Any(s => s.EventType == eventType))
            return new SubscribeOutcome.AlreadySubscribed(eventType);
        var sub = Subscription.For(eventType);
        _subscriptions.Add(sub);
        return new SubscribeOutcome.Subscribed(sub);
    }

    public void Pause(string reason, ActorRef actor)
    {
        if (Status == EndpointStatus.Paused) return;
        Status = EndpointStatus.Paused;
        // raises a domain event; audit subscribes
    }

    public void Resume(ActorRef actor)
    {
        if (Status != EndpointStatus.Paused) return;
        Status = EndpointStatus.Active;
    }

    public bool ShouldDeliver(EventTypeRef eventType)
        => Status == EndpointStatus.Active
           && _subscriptions.Any(s => s.EventType == eventType);
}
```

What the skill produced here:

- **Encapsulation.** All state is `private set;`. Every transition is a method that names the domain operation (`Subscribe`, `Pause`, `BeginSecretRotation`). No `Order.Total = ...` style mutations from outside. The four-modules-mutating-the-same-entity smell from the discovery is impossible here.
- **SRP at the boundary.** `OutboundEndpoint` owns one thing: the contract with one partner. Subscriptions, rotations, and policy belong to this aggregate because they share its lifecycle and invariants.
- **Discriminated outcome over exception** for `BeginSecretRotation` and `Subscribe` — see `RotationStarted` / `SubscribeOutcome` below.
- **`internal` everywhere.** The aggregate is not on the module's public surface; consumers go through role-shaped interfaces.

### Auth scheme — closed core, open extension (OCP via strategy)

```csharp
namespace Webhooks.Outbound.Domain;

// The closed core: a discriminated value type. New schemes are new cases.
public abstract record AuthScheme
{
    public sealed record HmacSha256(SecretRef Secret, string SignatureHeader, string TimestampHeader,
                                     TimeSpan Tolerance) : AuthScheme;

    public sealed record OAuth2ClientCredentials(Uri TokenEndpoint, string ClientId, SecretRef ClientSecret,
                                                  string? Scope) : AuthScheme;

    public sealed record AzureAdServicePrincipal(string TenantId, string ClientId, SecretRef Credential,
                                                  string Audience) : AuthScheme;

    public sealed record MutualTls(SecretRef ClientCertificate, SecretRef ClientKey,
                                    string SubjectCn, TlsVersion MinVersion) : AuthScheme;

    public sealed record StaticBearer(SecretRef Token) : AuthScheme;

    public sealed record HttpBasic(string Username, SecretRef Password) : AuthScheme;

    // Compliance-officer approval required before activation. Stored separately so
    // the value object alone cannot bypass approval.
    public sealed record IpAllowlistOnly(IReadOnlyList<string> AllowedIps,
                                          ComplianceApprovalId ApprovalId) : AuthScheme;

    private AuthScheme() { } // close the hierarchy
}
```

```csharp
namespace Webhooks.Outbound.Application.Auth;

// One strategy per scheme — open for extension.
internal interface IAuthSchemeApplicator
{
    Task<AppliedAuth> ApplyAsync(HttpRequestMessage request, byte[] body, CancellationToken ct);
}

internal interface IAuthSchemeApplicatorFactory
{
    IAuthSchemeApplicator For(AuthScheme scheme);
}

internal sealed class AuthSchemeApplicatorFactory(IServiceProvider sp) : IAuthSchemeApplicatorFactory
{
    public IAuthSchemeApplicator For(AuthScheme scheme) => scheme switch
    {
        AuthScheme.HmacSha256 h            => new HmacSha256Applicator(h, sp.GetRequiredService<ISecretReader>()),
        AuthScheme.OAuth2ClientCredentials o => new OAuth2ClientCredsApplicator(o, sp.GetRequiredService<ITokenCache>(), sp.GetRequiredService<HttpClient>()),
        AuthScheme.AzureAdServicePrincipal a => new AzureAdApplicator(a),
        AuthScheme.MutualTls m              => new MutualTlsApplicator(m),
        AuthScheme.StaticBearer b           => new StaticBearerApplicator(b, sp.GetRequiredService<ISecretReader>()),
        AuthScheme.HttpBasic ba             => new HttpBasicApplicator(ba, sp.GetRequiredService<ISecretReader>()),
        AuthScheme.IpAllowlistOnly _        => NoOpApplicator.Instance, // auth is at network layer
        _                                   => throw new UnreachableException("AuthScheme is sealed"),
    };
}
```

What the skill produced:

- **OCP at module level done right.** `AuthScheme` is the closed core (sealed hierarchy of value types); each applicator is a strategy. Adding a new scheme is one new case in the discriminated union, one new applicator class, one DI registration. The existing applicators do not move. The `OutboundEndpoint` aggregate does not care which scheme was chosen — it just holds the value.
- **No nested ifs.** The discovery refused nested `if (scheme is "hmac") ... else if (scheme is "oauth2") ...`. The switch expression IS the dispatch, with the compiler enforcing exhaustiveness via the `_ => throw new UnreachableException` and the sealed hierarchy.
- **No bool flags.** No `bool useTimestamp`, no `bool requireMutualTls`. Each variant carries exactly the data it needs. `HmacSha256` has a `Tolerance`; `MutualTls` has a `MinVersion`. Different schemes, different data.

### Delivery outcomes (strong domain over exception flow)

```csharp
namespace Webhooks.Outbound.Domain;

public abstract record DeliveryOutcome
{
    public sealed record Delivered(int Attempt, TimeSpan Latency, int StatusCode) : DeliveryOutcome;

    public sealed record Retrying(int Attempt, int NextAttempt, DateTime NextAttemptAt,
                                   FailureReason Reason) : DeliveryOutcome;

    public sealed record DeadLettered(int FinalAttempt, FailureReason FinalReason) : DeliveryOutcome;

    public sealed record ConfigurationError(ConfigurationProblem Problem) : DeliveryOutcome;
    // e.g. SSRF target, expired client cert, IDP rejected our request — never going to succeed,
    // surface to operator without burning the retry budget.

    private DeliveryOutcome() { }
}

public abstract record FailureReason
{
    public sealed record HttpStatus(int Code, string ReasonPhrase) : FailureReason;
    public sealed record Timeout(TimeSpan Elapsed) : FailureReason;
    public sealed record ConnectionRefused(string Detail) : FailureReason;
    public sealed record TlsHandshakeFailed(string Detail) : FailureReason;
    public sealed record InvalidResponse(string Detail) : FailureReason;
    private FailureReason() { }
}
```

The dispatcher returns `DeliveryOutcome`. Callers pattern-match. There is no `try { Deliver(); } catch (HttpException) { ... } catch (TimeoutException) { ... } catch (TlsException) { ... }` — the outcome is the contract. Exceptions are reserved for things that genuinely should not happen (DI mis-wired, database down, code defect).

### Role-shaped public interfaces (ISP per consumer)

```csharp
// Webhooks/Outbound/Application/IOutboundEndpointAdmin.cs
namespace Webhooks.Outbound.Application;

// What the admin controllers need.
public interface IOutboundEndpointAdmin
{
    Task<RegisterEndpointOutcome> RegisterAsync(RegisterEndpointRequest request, ActorRef actor, CancellationToken ct);
    Task<PauseOutcome>             PauseAsync(EndpointId id, string reason, ActorRef actor, CancellationToken ct);
    Task<ResumeOutcome>            ResumeAsync(EndpointId id, ActorRef actor, CancellationToken ct);
    Task<RotateSecretOutcome>      BeginSecretRotationAsync(EndpointId id, GraceWindow window, ActorRef actor, CancellationToken ct);
    Task<RetireSecretOutcome>      RetireOldSecretAsync(EndpointId id, ActorRef actor, CancellationToken ct);
    Task<SubscribeOutcome>         SubscribeAsync(EndpointId id, EventTypeRef eventType, ActorRef actor, CancellationToken ct);
}

// Webhooks/Outbound/Application/IOutboundEventDispatcher.cs
namespace Webhooks.Outbound.Application;

// What the worker calls when a domain event lands in the outbox.
public interface IOutboundEventDispatcher
{
    Task DispatchAsync(EnvelopedDomainEvent envelope, CancellationToken ct);
    // returns void — the dispatcher schedules per-endpoint deliveries internally;
    // each delivery's outcome is recorded and observable via reads.
}

// Webhooks/Outbound/Application/IOutboundEndpointReads.cs
namespace Webhooks.Outbound.Application;

// What the dashboard reads. Separate from admin (no mutations); separate from
// dispatcher (no internal delivery types).
public interface IOutboundEndpointReads
{
    Task<EndpointSummary?>             GetByIdAsync(EndpointId id, TenantId tenant, CancellationToken ct);
    IAsyncEnumerable<EndpointSummary>  ListAsync(TenantId tenant, EndpointStatusFilter? filter, CancellationToken ct);
    Task<DeliveryStats>                GetStatsAsync(EndpointId id, TimeRange range, CancellationToken ct);
    IAsyncEnumerable<DeliveryRecord>   GetRecentDeliveriesAsync(EndpointId id, int max, CancellationToken ct);
}
```

What the skill produced:

- **Three roles, three interfaces, one canonical aggregate.** The admin controller depends on `IOutboundEndpointAdmin` and never sees `OutboundEventDispatcher`'s methods. The worker depends on `IOutboundEventDispatcher` and never sees admin operations. The dashboard depends on `IOutboundEndpointReads` and cannot mutate. ISP per consumer — each is a *role*, not a deploy target.
- **One implementation, three interfaces.** Inside `Webhooks.Outbound.Infrastructure`, a single `internal sealed class OutboundEndpointService : IOutboundEndpointAdmin, IOutboundEventDispatcher, IOutboundEndpointReads` satisfies all three. Composition over duplication; encapsulation preserved.

### The repository (per-aggregate, internal, around writes)

```csharp
// Webhooks/Outbound/Infrastructure/IOutboundEndpointRepository.cs
namespace Webhooks.Outbound.Infrastructure;

// Internal — never reachable from outside the module. No IRepository<T>.
internal interface IOutboundEndpointRepository
{
    Task<OutboundEndpoint?> GetForUpdate(EndpointId id, CancellationToken ct);
    Task Save(OutboundEndpoint endpoint, CancellationToken ct);
}
```

What the skill produced:

- **Per-aggregate.** This repository persists `OutboundEndpoint` and nothing else.
- **`internal`.** Other modules cannot reach it. They go through `IOutboundEndpointAdmin` etc., which use it.
- **Designed around writes.** `GetForUpdate` returns the live aggregate ready to mutate; `Save` commits the whole aggregate. There is no `Query<TProjection>` interface here — reads come through `IOutboundEndpointReads`, which has its own optimized projections.
- **No `IRepository<T>` exposed across modules.** Anywhere. The anti-pattern stays out.

## `Webhooks.Inbound` — verification, dedupe, dispatch

### The aggregate

```csharp
namespace Webhooks.Inbound.Domain;

internal sealed class InboundSource
{
    public SourceId Id { get; }
    public TenantId TenantId { get; }
    public string Name { get; private set; }
    public ReceiveUrl ReceiveUrl { get; }
    public VerificationScheme Verification { get; private set; }
    public IdempotencyKeyExtractor IdempotencyKey { get; private set; }
    public DispatchHandlerKey DispatchHandler { get; private set; }
    public SourceStatus Status { get; private set; }

    public ReceiveOutcome Receive(IncomingRequest request, IClock clock)
    {
        if (Status != SourceStatus.Active)
            return new ReceiveOutcome.SourcePaused(Status);
        // verification is a separate strategy concern; this just holds the scheme.
        return new ReceiveOutcome.Accepted();
    }
}
```

### Verification scheme (OCP via strategy, mirror of Outbound's auth scheme)

```csharp
namespace Webhooks.Inbound.Domain;

public abstract record VerificationScheme
{
    public sealed record StripeStyleHmac(SecretRef Secret, TimeSpan TimestampTolerance,
                                          IReadOnlyList<string>? AllowedIps) : VerificationScheme;
    public sealed record GitHubStyleHmac(SecretRef Secret) : VerificationScheme;
    public sealed record TwilioStyleHmacSha1(SecretRef AuthToken) : VerificationScheme;
    public sealed record SlackStyleHmac(SecretRef SigningSecret, TimeSpan TimestampTolerance) : VerificationScheme;
    public sealed record JwtBearerWithJwks(Uri JwksUrl, string ExpectedAudience,
                                            TimeSpan ExpirySkew, IReadOnlyList<string>? AllowedIps) : VerificationScheme;
    public sealed record IpAllowlistOnly(IReadOnlyList<string> AllowedIps,
                                          ComplianceApprovalId ApprovalId) : VerificationScheme;
    private VerificationScheme() { }
}
```

```csharp
namespace Webhooks.Inbound.Application.Verification;

internal interface IVerificationSchemeStrategy
{
    Task<VerificationOutcome> VerifyAsync(IncomingRequest request, CancellationToken ct);
}

public abstract record VerificationOutcome
{
    public sealed record Verified(IdempotencyKey Key) : VerificationOutcome;
    public sealed record SignatureMismatch(string Detail) : VerificationOutcome;
    public sealed record TimestampOutOfTolerance(TimeSpan Skew) : VerificationOutcome;
    public sealed record IpNotAllowed(string SourceIp) : VerificationOutcome;
    public sealed record ChallengeResponse(byte[] Body) : VerificationOutcome; // Slack url_verification etc.
    private VerificationOutcome() { }
}
```

What the skill produced: same shape as Outbound's `AuthScheme` — closed value-type hierarchy, open strategy implementations. The two modules independently arrive at the same pattern because the same pressure (open extension over closed core) applies on both sides.

### `IInboundEventProcessor` — DIP for cross-module dispatch

```csharp
// Webhooks/Inbound/Application/IInboundEventProcessor.cs
namespace Webhooks.Inbound.Application;

// Inbound declares this. Other modules implement. THIS is DIP done right —
// the consumer (Inbound, which dispatches) defines the contract; producers
// (Billing, Residents, etc.) adapt.
public interface IInboundEventProcessor
{
    DispatchHandlerKey HandlerKey { get; }
    Task<ProcessingOutcome> HandleAsync(InboundEvent evt, CancellationToken ct);
}

public abstract record ProcessingOutcome
{
    public sealed record Processed : ProcessingOutcome;
    public sealed record IgnoredByDesign(string Reason) : ProcessingOutcome;
    public sealed record Failed(string Reason) : ProcessingOutcome; // will be retried
    private ProcessingOutcome() { }
}
```

```csharp
// Implemented in Billing module — adapter from Stripe events to Billing domain events.
namespace Billing.Inbound;

internal sealed class StripeEventProcessor(IBillingCommandBus commands)
    : Webhooks.Inbound.Application.IInboundEventProcessor
{
    public DispatchHandlerKey HandlerKey => new("stripe-billing");

    public async Task<ProcessingOutcome> HandleAsync(InboundEvent evt, CancellationToken ct)
    {
        var stripe = JsonSerializer.Deserialize<StripeEventEnvelope>(evt.RawBody);
        return stripe?.Type switch
        {
            "payment_intent.succeeded" =>
                await commands.RecordPaymentReceived(MapToCommand(stripe), ct),
            "charge.refunded" =>
                await commands.RecordRefund(MapToRefund(stripe), ct),
            _ =>
                new ProcessingOutcome.IgnoredByDesign($"Stripe type {stripe?.Type} not subscribed"),
        };
    }
}
```

What the skill produced:

- **DIP rotation.** `IInboundEventProcessor` lives in `Webhooks.Inbound.Application` (Inbound's namespace). Billing writes the adapter that conforms to Inbound's contract. The dependency arrow points: Billing → Inbound's contract. Inbound never imports Billing.
- **No `Common.Abstractions` library.** The interface lives where the consumer lives. Adding a new processor module never requires touching a shared library.
- **Substitutability honored (LSP at the boundary).** All processors honor the contract: return a `ProcessingOutcome`, never throw on expected outcomes (`Failed` is an outcome, not an exception), respect cancellation, are safe to retry on `Failed`. The test suite has one contract test that runs against every implementation — `assert.processor.honors_contract(processor)`.

## `Webhooks.Events` — the catalog (and the value-object refusal)

```csharp
namespace Webhooks.Events;

// EventTypeRef is shared by Outbound, Inbound, and the catalog itself. The
// "all coupling is bad" myth would have us interface this. Refused — it is a
// stable, immutable value object. Sharing is cheap; decoupling buys nothing.
public readonly record struct EventTypeRef(string Name, int SchemaVersion)
{
    public override string ToString() => $"{Name}@v{SchemaVersion}";
}

// The catalog is its own aggregate — owned and updated centrally.
internal sealed class EventTypeRegistration
{
    public EventTypeRef Type { get; }
    public string Description { get; private set; }
    public CatalogStatus Status { get; private set; } // Stable, Preview, Deprecated, Restricted
    public PhiSensitivity Sensitivity { get; private set; }
    public DateTime? DeprecatedAt { get; private set; }
    public EventTypeRef? Successor { get; private set; }

    public DeprecateOutcome Deprecate(EventTypeRef successor, IClock clock)
    {
        if (Status == CatalogStatus.Deprecated)
            return new DeprecateOutcome.AlreadyDeprecated(DeprecatedAt!.Value);
        Status = CatalogStatus.Deprecated;
        DeprecatedAt = clock.UtcNow;
        Successor = successor;
        return new DeprecateOutcome.Done(this);
    }
}
```

What the skill produced:

- **Value object shared, not interfaced.** `EventTypeRef` lives in `Webhooks.Events` and is consumed by both `Webhooks.Outbound` and `Webhooks.Inbound` — directly. No `IEventTypeContract`. No interface.
- **Aggregate for the catalog itself.** `EventTypeRegistration` enforces the deprecation invariants (must name a successor, status transitions are one-way).
- **Strong domain over magic strings.** `CatalogStatus` and `PhiSensitivity` are enums with domain meaning, not strings.

## `Webhooks.Audit` — the foundational-module exception

```csharp
namespace Webhooks.Audit.Application;

// Audit is a foundational module — stable, narrow, consumed by everything.
// The skill's "DIP is noise" exception applies: producer-defined interface is fine.
public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken ct);
}

public sealed record AuditEntry(
    AuditEntryId Id,
    TenantId TenantId,
    ActorRef Actor,
    string Action,            // e.g. "outbound.endpoint.secret.rotated"
    AuditSubject Subject,     // e.g. SubjectKind.OutboundEndpoint, EndpointId
    AuditPayload Payload,     // structured, redacted
    DateTime OccurredUtc,
    string SourceIp);

public abstract record AuditPayload
{
    public sealed record Structured(IReadOnlyDictionary<string, object> Fields) : AuditPayload;
    public sealed record Redacted(string Reason) : AuditPayload; // sensitive ops where the payload itself is the secret
    private AuditPayload() { }
}
```

What the skill produced:

- **Foundational-module exception named.** Audit will never have its dispatch flipped by consumers; it is a sink. Producer-owned interface (`Webhooks.Audit.Application.IAuditWriter`) is fine. The skill's frame allows this explicitly.
- **No `Common.Abstractions` for audit.** The interface lives where it belongs: in the producing module's namespace.
- **Append-only domain.** `IAuditWriter` has only `WriteAsync`. There is no `Update`, `Delete`, or `Replace` — the audit log's invariant is immutability, and the surface enforces it. Reads come through `IAuditReads` (separate role, ISP).

## `Webhooks.Compliance` — the small justified shared kernel

```csharp
namespace Webhooks.Compliance;

// PHI sensitivity is genuinely cross-cutting — both Outbound delivery logs and
// Inbound event storage need to apply the same redaction rules. Small, stable,
// domain-defined. Justified shared kernel.

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PhiAttribute : Attribute { }

public sealed record RedactedPayload(byte[] Bytes, IReadOnlyList<string> RedactedFieldPaths);

public interface IPayloadRedactor
{
    RedactedPayload Redact(byte[] rawJson, PhiSensitivity sensitivity);
}

public enum PhiSensitivity
{
    None,        // no PHI; e.g. system events
    Limited,     // PII but not PHI; e.g. Stripe customer email
    Standard,    // typical PHI; e.g. resident name + dates
    Restricted,  // enhanced controls; e.g. resident.deceased
}
```

The skill's check on shared kernels: small, stable, domain-defined. `PhiSensitivity` rarely changes; the redactor is a single algorithm; the attribute is a marker. The alternative — duplicated redaction in Outbound and Inbound — would drift and create the very inconsistency this kernel exists to prevent.

## What was refused along the way

The skill's discovery section drove these refusals during design. Each one would have shipped without it.

- **`Common.Abstractions`** holding `IInboundEventProcessor`, `IAuditWriter`, `IRepository<T>`, and `EventTypeRef`. Refused. Each interface lives in its owning module's namespace; the value object is shared directly.
- **Generic `IRepository<TEntity>`** exposed at any module boundary. Refused. Per-aggregate, `internal`, around writes only. Reads have their own role-shaped projection interface.
- **A 30-property `OutboundEndpointDto`** for the admin API to set fields one at a time. Refused. The aggregate accepts a `RegisterEndpointRequest` and exposes named state-transition methods; the DTO is the wire-shape, not the persistence-shape.
- **Nested ifs inside `OutboundEndpoint.Deliver()`** dispatching by auth scheme. Refused. Strategy + sealed discriminated value type; the switch is exhaustive and the compiler enforces it.
- **`bool` flag parameters** on the admin interface (`SubmitEndpoint(..., bool autoActivate, bool sendTestProbe, bool requireApproval)`). Refused. Each mode is its own named operation: `RegisterAsync` (creates draft), `SendTestProbeAsync` (during draft), `ActivateAsync` (after passing review).
- **`try { Deliver(); } catch (HttpException) { ... } catch (TimeoutException) { ... }`** as the dispatcher's contract. Refused. `DeliveryOutcome` discriminated record names the cases the consumer must handle; the compiler enforces exhaustiveness.
- **An `IEntity { Guid Id }` interface** so generic infrastructure could log changes uniformly. Refused. Each aggregate has its own `Id` property of its own strongly-typed `XxxId` record struct; cross-cutting concerns (audit) take a `AuditSubject` value type, not a marker interface.
- **Author-attributed pattern names** in code comments (`// Strangler Fig pattern (Fowler 2004)`). Refused. The pattern stands on its own; if the next maintainer needs the literature, they can find it.
- **An "Architecture" section in the README** that lists every SOLID principle and claims this design "follows" all five. Refused. The skill's frame is operational, not ceremonial. The design does what it does because of the pressures it answers; SOLID is the toolkit, not the trophy case.

## Pressure-test through blind review

The central technique applied to two pieces of this surface.

### Use case A — register a new outbound endpoint, blind

Hand a teammate `IOutboundEndpointAdmin`, `RegisterEndpointRequest`, `RegisterEndpointOutcome`, `AuthScheme`, and `RetryPolicy`. Give them: *"Register an OAuth2 endpoint for a new partner billing system, configure retry, and subscribe it to `billing.invoice.issued@v2`."*

Expected stumbles in a leaked design:
- "How do I store the OAuth client secret?" → answered: `SecretRef` is a typed reference to vault storage; the interface accepts the reference, not the secret bytes.
- "Can I subscribe to events at registration time, or do I have to register first then subscribe?" → answered: the interface has separate `RegisterAsync` and `SubscribeAsync`; the request DTO surfaces this clearly.
- "What does `RegisterEndpointOutcome.UrlNotReachable` mean — did anything get persisted?" → answered: outcome variants name what happened to the persistence side too (`Registered(EndpointId)`, `UrlValidationFailed(reason, draftId)`).

If the reviewer needs to read the implementation to answer any of these, the surface has unstated semantics. Document them in the outcome types and request shapes.

### Use case B — diagnose a dead-letter at 3 a.m., blind

Hand a teammate `IOutboundEndpointReads`, `DeliveryRecord`, `DeliveryOutcome`, `FailureReason`. Give them: *"A delivery to `Brookside Billing` was dead-lettered. Form a hypothesis about why."*

The reviewer should be able to:
1. Get the delivery record and see `DeliveryOutcome.DeadLettered(FinalAttempt: 7, FinalReason: HttpStatus(503, "Service Unavailable"))`.
2. Form the hypothesis: receiver was overloaded for the entire retry budget (7 attempts over ~40 hours per the standard schedule).
3. Check whether the receiver's other deliveries were also failing (`GetStatsAsync(EndpointId, last 48h)`).
4. Decide whether to replay (their service may be back) or escalate.

If the surface only exposes "Failed" without a typed `FailureReason`, the reviewer cannot form the hypothesis from the surface alone. The diagnosis is hidden along with the implementation. The discriminated `FailureReason` exists precisely to make 3 a.m. debugging tractable from the public surface.

## Closing

The frame did three jobs in this design:

1. **Refused class-level smells before they reached the boundary.** Public defaults, bool flags, nested control, exception-driven flow, anemic entities — caught in discovery, fixed at the class level before any module surface was drawn.
2. **Answered the boundary questions through the deep three.** SRP gave the bounded contexts (Outbound vs Inbound) and the aggregates within. OCP gave the strategy patterns for auth and verification — closed core, open extension. LSP gave the contract tests that keep adapter substitutability honest.
3. **Made the mechanical moves obvious.** ISP fell out as three role-shaped interfaces per module (admin, dispatcher, reads). DIP fell out as `IInboundEventProcessor` (consumer-owned by Inbound, implemented by handler modules), with the foundational-module exception applied to `IAuditWriter`.

The design did not invent a single architectural pattern that the pressure didn't demand. No Hexagonal-as-religion, no CQRS-because-it's-fashionable, no repository-because-tutorials-say-so. The pressure summoned strategy, discriminated outcomes, role-shaped interfaces, and consumer-owned ports — and the design crystallized exactly those.
