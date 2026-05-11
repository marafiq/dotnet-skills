# integrations-framework — modular-solid skill, made executable

A working .NET 10 webhooks framework that *uses* the [`modular-solid`](../../plugins/dotnet-10/skills/modular-solid/SKILL.md) skill end-to-end. Five modules + a sample app that round-trips a signed event from the outbound dispatcher to the inbound receiver and back.

**A skill is only as good as the design it produces under load.** This is the load test.

## Run it

```bash
cd examples/integrations-framework
dotnet run --project samples/Webhooks.SampleApp
```

Output is annotated by step (1 → 8). The demo registers an inbound source, registers an outbound endpoint pointing at the inbound source's URL on `localhost:5099`, subscribes the endpoint to `billing.invoice.issued@v2`, then dispatches one envelope through the framework. You should see:

- HMAC SHA-256 signature applied by Outbound, verified by Inbound
- Idempotency enforced (re-dispatching the same event id → handler fires once)
- Tampering rejected (a manually-crafted POST with a bogus signature → `HTTP 401 SignatureMismatch`)
- Read-side projections (`IOutboundEndpointReads`, `IInboundSourceReads`) returning summary DTOs
- Audit log capturing every config change and every delivery outcome

## Module topology

```
src/
├── Webhooks.Events/          # EventTypeRef, TenantId, IClock — shared value types
├── Webhooks.Compliance/      # PHI sensitivity, payload redactor (small shared kernel)
├── Webhooks.Audit/           # IAuditWriter (foundational module), AuditEntry value types
├── Webhooks.Outbound/        # outbound bounded context (push to partners)
└── Webhooks.Inbound/         # inbound bounded context (receive from partners)

samples/
└── Webhooks.SampleApp/       # composition root + HttpListener host + DemoLoopbackProcessor
```

The compiler enforces the topology — `Webhooks.Outbound` cannot import `Webhooks.Inbound` because there is no project reference between them, and vice versa. `csproj` references are the load-bearing wall.

## Where the skill's frame shows up in the code

| Skill move | Where to look |
|---|---|
| **Encapsulation** at the aggregate boundary | `OutboundEndpoint.cs`, `InboundSource.cs` — private setters, intent-revealing methods, atomic state transitions |
| **SRP** at module boundary → bounded contexts | Two separate aggregates (Outbound vs Inbound), each with its own `SecretRef` value type — no cross-module dependency |
| **OCP** via strategy + sealed discriminated value type | `AuthScheme.cs` + `IAuthSchemeApplicator.cs` (Outbound); `VerificationScheme.cs` + `IVerificationSchemeStrategy.cs` (Inbound) |
| **LSP** at adapter boundary | `IInboundEventProcessor` honored by `DemoLoopbackProcessor`; failure semantics named in `ProcessingOutcome` |
| **ISP** with role-shaped interfaces per consumer | Three interfaces per module: admin / dispatcher (or receiver) / reads — see `IOutboundEndpointAdmin`, `IOutboundEventDispatcher`, `IOutboundEndpointReads` |
| **DIP** with consumer-owned port | `IInboundEventProcessor` lives in `Webhooks.Inbound.Application`; `DemoLoopbackProcessor` (in the sample app) implements it. The sample app is the consumer of the inbound module's contract; the framework *receives the implementation* via DI |
| **Foundational-module exception** | `IAuditWriter` lives in `Webhooks.Audit` (producer-owned) — Audit is a stable sink that does not flip under consumers |
| **Per-aggregate repository, internal, around writes** | `IOutboundEndpointRepository`, `IInboundSourceRepository` — both `internal`, never leak out |
| **No `Common.Abstractions`** | Each interface lives in its owning module's namespace; cross-module sharing is value objects (`EventTypeRef`, `TenantId`) directly imported |
| **Discriminated outcomes, no exceptions for expected outcomes** | `DeliveryOutcome` (Delivered / Retrying / DeadLettered / ConfigurationError / EndpointNotActive); `VerificationOutcome` (Verified / SignatureMismatch / TimestampOutOfTolerance / IpNotAllowed / ConfigurationProblem); `ReceiveOutcome`; `ProcessingOutcome` |
| **Strong-typed identifiers** | `EndpointId`, `SourceId`, `TenantId`, `IdempotencyKey`, `SecretRef`, `EventTypeRef` — all readonly record structs |

## Anti-patterns refused

What the framework deliberately *does not* contain:

- ❌ `Common.Abstractions` library — each interface in its owning module
- ❌ `IRepository<T>` exposed across modules — per-aggregate, `internal`
- ❌ `IEntity { Guid Id }` reflexive interface — strongly-typed `*Id` per aggregate
- ❌ `bool` flag parameters on the public surface — separate methods per intent
- ❌ Public setters on entities — every mutation goes through a method
- ❌ Exceptions for expected outcomes (declined, mismatch, duplicate, paused) — typed outcome records
- ❌ Author citations in code comments — pattern names earn their own weight

## What the demo proves

| Property | Validated by |
|---|---|
| Outbound signing matches Inbound verification | Round-trip POST → verified → handler runs |
| Idempotency enforced | Second dispatch of same `EventId` → DemoProcessor invocation count stays 1 |
| Tampering rejected at the boundary | Bogus signature → HTTP 401 with typed `SignatureMismatch` outcome |
| Module isolation | `Webhooks.Outbound` and `Webhooks.Inbound` have no project reference between them; both compile and link without it |
| Audit completeness | Every state change and every delivery outcome shows up in the audit log |

## Limits of this demo (honest scope)

- **In-memory everything.** No EF Core, no real database, no real vault. Production would swap `InMemoryOutboundRepository` for an EF Core repository, `InMemorySecretReader` for an Azure Key Vault adapter, etc. The role-shaped interfaces would not change.
- **Retry policy is `NoRetry` for the demo.** The `RetryPolicy` value object supports `Standard` (~40 hours over 7 attempts) and `Aggressive`, but the dispatcher does not yet schedule retries through a hosted background service — that wiring is a real-system concern, separate from the design demonstration.
- **Only HMAC + Bearer auth schemes are wired.** OAuth2 and Azure AD are present in `AuthScheme.cs` as cases (closed core) but their applicators are stubbed. Adding them is one new class each — `AuthSchemeApplicatorFactory` will route automatically.
- **HTTP host uses `HttpListener`, not ASP.NET Core.** Keeps the demo dependency-light. A production deployment would front the receiver with Kestrel + ASP.NET Core minimal API; the `IInboundReceiver` interface would not change.
- **Single tenant in the demo.** The framework is tenant-aware end-to-end (`TenantId` flows through every aggregate, every outcome, every audit entry).

## Why this demo matters for the skill

The skill's central claim is that designing under its frame produces shippable shapes. The framework is that claim being cashed:

- The aggregates compile because they have *real* invariants worth encapsulating.
- The strategy interfaces work because the closed core is genuinely closed.
- The role-shaped interfaces compose because each consumer plays one role.
- The DIP rotation works because the inbound module's port `IInboundEventProcessor` is implemented by an outside module (`DemoLoopbackProcessor` in the sample) without the framework knowing about that module.
- The audit trail is honest because audit is a foundational module with a single narrow public surface.

Run it. Read the code. Trace any one flow end-to-end. The blind-review pressure test from the skill applies here too: hand a teammate `IOutboundEndpointAdmin` and `IInboundReceiver` and the typed outcome records, and ask them to wire a new partner integration. They should not need to read the internals.
