# ALIS Integrations — Behavior Specification

This spec describes *what the Integrations subsystem of the ALIS senior-living platform does* and *what observable properties hold*. It deliberately avoids implementation choices (frameworks, persistence, patterns, transport details) so that any implementer can satisfy it on any reasonable stack.

When this spec uses words like "must," "shall," "is required to," it means a property the implementation must hold under all paths. When it uses "should," it means a default that may be overridden with explicit, recorded reason.

---

## 1. Purpose and audience

**Problem.** ALIS is a multi-tenant SaaS platform for senior-living operators. The platform must exchange events with partner systems — billing systems, EHRs, pharmacies, payment processors, regional health information exchanges, internal admin tools. The exchange must be reliable, secure, observable, and HIPAA-defensible.

**Audience.** This spec is for an engineer or team building the Integrations subsystem from scratch. It is not for end-users; it is the contract between product and implementation.

**Why this exists in one paragraph.** Operators (a senior-living facility's billing, clinical, and administrative staff) and the partner systems they integrate with both need to know that when something happens in ALIS — a resident is approved, an invoice is issued, a payment posts — the right partner systems hear about it once and only once, on time, with proof. They also need to know that when partner systems push events to ALIS — a payment succeeded, a clinical record changed — those events are authenticated, deduplicated, and routed to the right place. Both directions must leave an immutable audit trail that satisfies a HIPAA reviewer.

---

## 2. Glossary

These terms have precise meanings in this spec. Implementations may rename them but must preserve the distinctions.

- **Tenant.** One senior-living operator (e.g., "Brookside Living Group"). All data is tenant-isolated.
- **Operator user.** A human at the tenant who configures and observes integrations through ALIS dashboards.
- **Partner system.** An external system the tenant integrates with — billing, EHR, payment processor, etc.
- **Event type.** A versioned name for a kind of business event (e.g., `billing.invoice.issued@v2`). Defined by ALIS, consumed by partners (outbound) or sent to ALIS (inbound).
- **Domain event.** A specific occurrence of an event type for a specific tenant at a specific time, with a payload. Has a unique identifier.
- **Outbound endpoint.** A configuration that says: "when events of these types happen for this tenant, push them to this URL using this authentication." Owned by an operator user; lives until disabled.
- **Inbound source.** A configuration that says: "when this partner posts to this URL, verify them this way and dispatch to this handler." Owned by an operator user; lives until disabled.
- **Delivery.** A single attempt to push one domain event to one outbound endpoint. Has an outcome: delivered, retrying, dead-lettered, configuration-error, skipped.
- **Receipt.** A single instance of an inbound source receiving a request from a partner. Has an outcome: accepted, duplicate, rejected, dispatch-failed, ignored-by-handler.
- **Idempotency key.** A value extracted from an inbound request that uniquely identifies the partner's "logical" event. Two receipts with the same idempotency key represent the same logical event from the partner's perspective.
- **Verification.** The process of proving that an inbound request actually came from the configured partner and was not modified in transit.
- **Authentication scheme** (outbound). The method ALIS uses to prove its identity to a partner — credential type and how it is presented on each request.
- **Verification scheme** (inbound). The method ALIS uses to verify a partner's identity — what header, signature, or other proof is required.
- **Dispatch handler.** Code inside ALIS that processes verified inbound events. Each inbound source is bound to exactly one handler.
- **PHI.** Protected Health Information as defined under HIPAA. Resident identifiers, clinical data, payer information when joined to a resident.
- **PII.** Personally Identifiable Information that is not PHI (e.g., a partner's contact email).
- **Audit entry.** An immutable record of an action: actor, action, subject, timestamp, structured fields, source IP. Append-only.
- **Reveal.** An audited, role-gated action where an operator user temporarily sees normally-redacted data (e.g., the original PHI payload of a delivery).

---

## 3. Personas

**Operator-Admin.** Configures integrations. Creates outbound endpoints and inbound sources, subscribes events, rotates secrets, pauses misbehaving integrations. Does not see PHI by default.

**Operator-Compliance.** Reviews audit logs, performs PHI reveals when investigations require it, exports audit trails for HIPAA reviews.

**Operator-Dashboard.** Reads dashboards: integration health, delivery success rates, recent failures. May or may not be the same person as the admin.

**Partner-Developer.** Works at the partner system. Receives a URL from the operator, configures their system to POST to it, possibly reveals or rotates a shared secret on their side. Does not have direct access to ALIS.

**System.** Background processes that dispatch events, retry failed deliveries, project read models, age out old audit data per retention policy.

**HIPAA reviewer.** External auditor who, given access to the audit log export, can reconstruct what happened in any time window.

---

## 4. Scope

### In scope

- Configuring outbound endpoints: destination, authentication, retry policy, event subscriptions.
- Configuring inbound sources: receive URL generation, verification scheme, idempotency rule, dispatch handler binding.
- Reliable outbound delivery with retry and dead-letter.
- Inbound verification, deduplication, and dispatch.
- Secret rotation with grace windows for outbound endpoints.
- Pause/resume/disable for both directions.
- Multi-tenant isolation.
- PHI redaction in logs and dashboards by default; audited reveal protocol.
- Tamper-evident, append-only audit log.
- Read-side dashboards and exports for operators and HIPAA reviewers.

### Out of scope (for this spec)

- The catalog of business event *types* themselves and their schemas. (Defined separately by the domains that emit them.)
- The dispatch handlers' business logic. (Owned by the consuming domain modules.)
- The specific persistence technology, message broker, secret store, or HTTP framework used.
- Partner-side configuration tooling. (We provide a URL and verification details; the partner-side wiring is theirs.)

---

## 5. Functional requirements

Each requirement has an ID (FR-N), a statement, optional rationale, and acceptance criteria expressed in observable terms.

### 5.1 Outbound — endpoint lifecycle

**FR-OUT-1. Register an outbound endpoint.**
An Operator-Admin can register a new outbound endpoint by providing: a human-readable name, a destination URL, an authentication scheme, and (optionally) a retry policy. The system shall return an outcome that either confirms registration with a stable identifier, or names the precise reason for refusal.

*Acceptance.* Given a valid request, the endpoint appears in the dashboard with the supplied properties, and the audit log records who created it. Given a malformed URL or a destination that the system can confidently identify as unreachable or unsafe (e.g., a non-public address that the security policy disallows), the system rejects the request with a typed reason and creates no endpoint.

**FR-OUT-2. Subscribe an endpoint to event types.**
An Operator-Admin can add or remove event-type subscriptions on an existing endpoint. Subscriptions can pin a specific schema version. The system shall be idempotent on subscribe (subscribing twice is a no-op) and shall reject subscription to event types that do not exist in the catalog.

**FR-OUT-3. Pause, resume, disable.**
An Operator-Admin can pause an endpoint (no future deliveries; configuration retained), resume a paused endpoint, or disable it (no future deliveries; cannot be re-enabled, but historic records remain searchable). The system shall record reason for pause and disable.

*Acceptance.* While paused, no deliveries are dispatched; queued retries are held. On resume, retry attempts continue from where they were. On disable, queued retries are cancelled and surfaced in the dead-letter view.

**FR-OUT-4. Rotate secret with grace window.**
For endpoints whose authentication uses a secret, an Operator-Admin can begin a rotation that produces a new secret while keeping the old one valid for a configurable grace window (default 7 days). The system shall accept either secret on outbound deliveries during the grace window; the new secret shall be revealable exactly once after rotation is initiated; after the grace window expires, only the new secret is used.

*Acceptance.* During the grace window, the system can be observed using either secret on outbound requests (operationally — the partner accepts both). After the window, only the new secret. The old secret cannot be retrieved after rotation begins. The reveal of the new secret is recorded in the audit log; subsequent reveals require another rotation.

### 5.2 Outbound — delivery behavior

**FR-OUT-5. Deliver a domain event to all subscribed endpoints.**
When a domain event is published, the system shall, for each endpoint subscribed to that event type and active, attempt delivery. Delivery is to the endpoint's configured URL with the configured authentication applied to each request. Each delivery is recorded with its outcome.

**FR-OUT-6. Typed delivery outcomes.**
Every delivery attempt produces exactly one of these outcomes:
- **Delivered** — partner returned a 2xx response within the per-attempt timeout.
- **Retrying** — transient failure (5xx, timeout, connection error); next attempt scheduled per the retry policy.
- **Dead-lettered** — all retry attempts exhausted without success.
- **Configuration error** — failure that retrying cannot fix (expired credential, IDP rejected our credentials, certificate invalid, vault secret missing, destination removed from allow-list).
- **Skipped** — endpoint became inactive between event publication and attempt.

The system shall not collapse these into a generic "failed" — the dashboard, the dead-letter handling, and the operator's diagnosis depend on the distinction.

**FR-OUT-7. Configurable retry policy.**
Each endpoint may carry a retry policy that names a sequence of delays for each attempt after the first. The system shall provide named defaults (a "standard" policy that is sufficient for typical receivers without configuration) and shall let operators override per endpoint. The system shall stop retrying when the schedule is exhausted *or* when the failure is classified as a configuration error (configuration errors do not retry).

**FR-OUT-8. Honor partner backpressure signals.**
When a partner returns `Retry-After` (header) or a 429 status, the system shall delay the next attempt by at least the duration the partner requested, up to a configurable cap.

**FR-OUT-9. Auto-disable on sustained failure.**
After N consecutive failures on the same endpoint (default N = 50), the system shall auto-pause the endpoint and surface this prominently. An Operator-Admin must explicitly resume.

**FR-OUT-10. Replay a dead-lettered delivery.**
An Operator-Admin can replay a dead-lettered delivery, which begins a fresh attempt sequence using the current endpoint configuration. The replay is itself audited; the original dead-lettered record is preserved.

### 5.3 Inbound — source lifecycle

**FR-IN-1. Register an inbound source.**
An Operator-Admin can register a new inbound source by providing: a human-readable name, a path slug (lowercase, hyphens), a verification scheme, an idempotency-key extraction rule, and the dispatch handler to bind. The system shall generate a stable URL combining the tenant identity and the slug; this URL shall not change for the lifetime of the source. The system shall refuse to register if the slug is already taken within the tenant or if the named dispatch handler is not registered with the system.

**FR-IN-2. Provider templates.**
The registration UI shall offer pre-configured starting points for common partners (e.g., the partners listed in the dashboard mockup): each template prefills the verification scheme, header conventions, and idempotency-key rule appropriate to that partner's published webhook contract. Custom configuration is also available.

**FR-IN-3. Pause, resume.**
An Operator-Admin can pause an inbound source (the URL still exists; received requests are rejected with a typed "source paused" outcome) or resume it. The dashboard shows the paused state.

**FR-IN-4. Rotate the verification credential.**
For sources whose verification scheme uses a shared secret (HMAC family), the operator can replace the secret. Unlike outbound rotation, inbound rotation is single-step (the partner is the source of the secret; ALIS just stores what they give us). The audit log records the rotation.

### 5.4 Inbound — receive behavior

**FR-IN-5. Verify every incoming request.**
When a request arrives at the receive URL of an active source, the system shall verify it according to the source's verification scheme *before* any handler runs. Verification produces exactly one of these outcomes:
- **Verified** — the request matches the verification scheme; an idempotency key is extracted.
- **Signature mismatch** — the cryptographic check failed.
- **Timestamp out of tolerance** — the request's timestamp is outside the configured window (replay defense).
- **IP not allowed** — the source IP is not in the allow-list (when configured).
- **Configuration problem** — the configured secret is missing or unreadable.

The system shall return an HTTP status that distinguishes acceptance (2xx) from rejection (4xx for partner-fault, 5xx for our-fault).

**FR-IN-6. Deduplicate by idempotency key.**
After successful verification, the system shall check whether the idempotency key has been processed before for this source. If yes, dispatch is skipped, but the partner receives a 2xx response (so they do not retry). If no, the key is recorded as seen and dispatch proceeds.

*Acceptance.* The same partner sending the same logical event N times results in the dispatch handler being invoked exactly once.

**FR-IN-7. Dispatch verified, deduplicated events to the bound handler.**
The bound dispatch handler runs in the worker pool, not on the HTTP request. The HTTP response is acked to the partner promptly (under the configured ack-deadline budget); handler work happens after.

**FR-IN-8. Typed receipt outcomes.**
Every incoming request produces exactly one of:
- **Accepted** — verified, unique, dispatched, handler completed without error.
- **Duplicate** — verified, but the idempotency key was already processed.
- **Rejected** — verification failed; carries the verification outcome as its reason.
- **Dispatch failed** — verified, unique, but the handler reported a failure or threw.
- **Ignored by handler** — verified, unique, but the handler chose not to process (e.g., event type the handler does not subscribe to internally).
- **Slug unknown** — request arrived at a URL that no source is bound to.
- **Source not active** — source exists but is paused or disabled.

**FR-IN-9. Tampering and replay defenses are first-class.**
The system shall behave correctly under hostile traffic:
- Bytes that look almost-but-not-quite right (one bit flipped in body, signature, or timestamp) shall produce **Rejected** with a typed reason — never **Accepted**.
- A captured-and-replayed valid request from beyond the timestamp tolerance shall produce **Rejected** with **TimestampOutOfTolerance**.
- A valid request replayed within the tolerance window with the same idempotency key shall produce **Duplicate** (handler not re-invoked).

### 5.5 Cross-cutting — audit

**FR-AUD-1. Every state change recorded.**
The system shall record an audit entry for: every endpoint/source registration, every config change, every pause/resume/disable, every secret rotation, every PHI reveal, every delivery outcome, every receipt outcome, and every administrative action.

**FR-AUD-2. Audit entries carry full context.**
Each entry shall include: tenant, actor (with kind: user/system, identifier, display name), action name (in domain vocabulary, e.g. `outbound.endpoint.secret.rotated`), subject (typed identifier of what was acted upon), structured payload, UTC timestamp, source IP for human actors.

**FR-AUD-3. Append-only.**
The audit log shall be append-only at the API level (no update, no delete). Entries shall be written before the operation that produced them is acknowledged to the caller; if the audit write fails, the operation fails too. Retention is governed by a separate policy (typically 7 years for HIPAA-relevant entries).

**FR-AUD-4. Exportable for compliance.**
An Operator-Compliance shall be able to export the audit log for a date range and tenant in two formats: CSV (for spreadsheets) and structured JSON (for SIEM ingestion).

### 5.6 Cross-cutting — compliance

**FR-COMP-1. PHI redaction by default.**
Any payload that flows through the system and may contain PHI shall be stored in a form where PHI fields are masked (per a per-event-type sensitivity classification: None / Limited / Standard / Restricted) when displayed in dashboards, exports, or logs. The original payload may be retained encrypted for replay/diagnosis.

**FR-COMP-2. Reveal is gated and audited.**
An Operator-Compliance with the appropriate role and a second factor may reveal the original payload of a specific delivery or receipt. Reveal requires a free-text justification (e.g., "vendor support ticket #4419"). The reveal is itself an audit entry that records what was revealed, by whom, why, when, from what IP.

**FR-COMP-3. Restricted event types require additional approval.**
Some event types (e.g., `resident.deceased`) are classified Restricted. Subscribing an endpoint to a Restricted event requires an explicit compliance approval reference recorded with the subscription.

**FR-COMP-4. Auth schemes that bypass cryptographic verification require approval.**
Inbound sources or outbound endpoints that use IP-allowlist-only (no signature, no token) require a compliance approval reference. The approval reference is part of the configuration; the system shall refuse to activate without it.

### 5.7 Cross-cutting — operability

**FR-OPS-1. Health visible at a glance.**
The dashboard shall surface, per tenant: total deliveries (24h), total receipts (24h), failed delivery count (24h), dead-lettered count, per-endpoint success rate, per-source verification rate.

**FR-OPS-2. Diagnostic affordances at the boundary.**
For any individual delivery or receipt, an operator shall be able to determine the outcome, the time, the attempt count (for outbound), the verification outcome (for inbound), the latency, and the partner's response (status, reason). PHI is redacted by default per FR-COMP-1.

**FR-OPS-3. Search and filter.**
The dashboard shall support filtering by status, time range, endpoint/source, and full-text search across visible (non-PHI) fields.

**FR-OPS-4. Recent activity feed.**
A unified, near-real-time feed shall show the most recent significant events (registrations, failures, reveals, rotations) across all integrations the operator has access to.

---

## 6. Behavioral scenarios

These read as test cases. They overlap with FRs above; they exist to make the contracts concrete with specific data.

### Scenario: a typical happy-path outbound delivery
**Given** an active outbound endpoint at `https://billing.partner.example/hooks/alis`,
authenticated by an HMAC-family signature scheme with a current secret,
subscribed to `billing.invoice.issued@v2`.
**When** a domain event of type `billing.invoice.issued@v2` is published for this tenant.
**Then** within seconds the partner receives one HTTP POST whose body is the event payload, whose headers include the configured signature, and the partner's 2xx response is recorded as outcome `Delivered` with attempt = 1, observed latency, and the response status code. An audit entry `outbound.delivery.delivered` is written, naming the endpoint and the event id.

### Scenario: transient receiver failure with retry recovery
**Given** the same endpoint, but the partner is returning 503 for the next minute.
**When** an event is published.
**Then** attempt 1 fails with HTTP 503 → outcome `Retrying(attempt=1, next=2)`. The next attempt is scheduled per the policy. When the partner recovers and returns 2xx on attempt 3, outcome `Delivered(attempt=3)` is recorded, and the previous `Retrying` records remain in history. The handler at the partner's side observes exactly one delivery (because of attempt 3's success); attempts 1 and 2 produced no successful processing on their side.

### Scenario: dead-letter after exhausting retries
**Given** the partner is returning 503 indefinitely.
**When** the retry schedule is exhausted.
**Then** the final attempt's outcome is `DeadLettered(finalAttempt=N, reason=HttpStatus(503,...))`. The dead-letter view surfaces this entry. An audit entry `outbound.delivery.dead_lettered` is written. The dashboard's dead-letter count increments. The operator can replay (FR-OUT-10), which starts a fresh attempt sequence and records that fact.

### Scenario: configuration error short-circuits retry
**Given** an outbound endpoint whose OAuth2 client credentials have been revoked at the partner's IDP.
**When** an event is published.
**Then** the IDP rejects ALIS's token request; outcome is `ConfigurationError(IdpRejectedCredentials)` on the first attempt, *not* `Retrying`. No retries are scheduled. The configuration-error queue surfaces this for operator action.

### Scenario: secret rotation grace window
**Given** an active outbound endpoint with secret v3 currently in use.
**When** an Operator-Admin begins rotation with a 7-day grace window.
**Then** secret v4 is generated. Both v3 and v4 are valid for outbound deliveries (the system may use either; the partner accepts either while they update their side). The new secret v4 is revealable exactly once. After 7 days, v3 is automatically retired and only v4 is used. The operator can also explicitly retire v3 early.

### Scenario: legitimate inbound POST is verified, deduped, dispatched
**Given** an active inbound source bound to dispatch handler `BillingProcessor`,
verification scheme = HMAC family with current shared secret,
idempotency-key extraction = the partner's `Stripe-Event-Id` header (or equivalent).
**When** the partner POSTs a valid request with a fresh idempotency key.
**Then** verification succeeds, the key is recorded as seen, the partner gets a 2xx response within the ack budget, and `BillingProcessor.HandleAsync(InboundEvent)` is invoked on the worker pool with the verified key, raw body, and headers.

### Scenario: replay of the same logical event is deduplicated
**Given** the same source.
**When** the partner POSTs the same valid request twice within the timestamp tolerance window with the same idempotency key.
**Then** the first POST is `Accepted`; the second is `Duplicate`. The handler is invoked exactly once. Both POSTs receive a 2xx response.

### Scenario: tampered request is rejected
**Given** the same source.
**When** an attacker POSTs a body with a forged signature.
**Then** outcome is `Rejected(SignatureMismatch)`. HTTP 4xx is returned with a body that names the rejection reason. The dispatch handler is *not* invoked. An audit entry `inbound.receipt.rejected` is written including the source IP.

### Scenario: stale captured request is rejected
**Given** the same source with timestamp tolerance = 5 minutes.
**When** an attacker captures a valid request and replays it 1 hour later.
**Then** outcome is `Rejected(TimestampOutOfTolerance(skew=1h))`. HTTP 4xx is returned. Handler not invoked.

### Scenario: source paused mid-flight
**Given** a source receiving 100 requests/minute.
**When** an Operator-Admin pauses the source.
**Then** subsequent requests receive HTTP 5xx with `SourceNotActive`. The configuration is retained and the source can be resumed. No requests are silently dropped.

### Scenario: PHI reveal
**Given** a delivery whose payload contained PHI (redacted in storage).
**When** an Operator-Compliance with the appropriate role attempts to reveal the original payload.
**Then** the system requires a second factor and a justification. Once provided, the original payload is shown once; the reveal action is recorded as an audit entry naming the actor, IP, time, justification, and the specific delivery revealed.

### Scenario: HIPAA reviewer reconstructs a 7-day window
**Given** access to the audit log export for tenant T over a 7-day window.
**When** the reviewer asks "what changed in our outbound endpoint configurations during this window?"
**Then** the export contains every config change, with actor, time, before/after where applicable, and the IP of the human actor for each. The reviewer does not need source code or live system access to answer the question.

---

## 7. Non-functional requirements

### 7.1 Security

- **NFR-SEC-1.** Secrets shall never appear in plaintext in any log, audit entry, dashboard, error response, or telemetry. References to secrets (vault paths or equivalents) are acceptable; the secret value is not.
- **NFR-SEC-2.** All inbound and outbound HTTP must be TLS-protected by default. Loopback and explicit local-development exceptions may exist but must be flagged in dashboards.
- **NFR-SEC-3.** Outbound destinations must be validated against an SSRF policy (no internal addresses, no metadata endpoints) at registration and re-validated at delivery time.
- **NFR-SEC-4.** Cryptographic comparisons (signature verification, secret comparison) must use constant-time equality.
- **NFR-SEC-5.** Authentication of operator users to the dashboards is out of scope of this spec but assumed: every operator action carries a verified user identity.

### 7.2 Performance and scale

- **NFR-PERF-1.** Inbound ack target: P50 under 100ms, P99 under 1s, measured from request receipt to response sent (handler dispatch is asynchronous and excluded).
- **NFR-PERF-2.** Outbound dispatch latency target: P50 under 5s from event publication to first delivery attempt (under nominal load).
- **NFR-PERF-3.** The system shall sustain 1,000 outbound deliveries/minute and 1,000 inbound receipts/minute per tenant without degradation, on commodity hardware. Scale beyond is an open question, not promised by this spec.
- **NFR-PERF-4.** A single misbehaving endpoint or source shall not block deliveries to other endpoints (queue isolation per endpoint is required).

### 7.3 Reliability

- **NFR-REL-1.** The system shall not lose events: a published domain event is either delivered to its subscribed endpoints (per their retry policies, possibly ending in dead-letter) or remains visible in the dead-letter view. There is no "silently dropped" outcome.
- **NFR-REL-2.** The system shall not double-process inbound events: the same idempotency key is processed at most once per source, even under handler restarts or partial failures.
- **NFR-REL-3.** The audit log write is part of the success criterion of every operation: if audit fails, the operation fails.

### 7.4 Observability

- **NFR-OBS-1.** Per-endpoint and per-source metrics (volume, success rate, latency percentiles, retry depth) are continuously available with at most 1-minute lag.
- **NFR-OBS-2.** Distributed traces tie an inbound request to the dispatch handler invocation, and an outbound delivery to the partner's HTTP response, with stable trace identifiers searchable from the dashboard.
- **NFR-OBS-3.** All log statements that name an entity use stable, opaque identifiers (e.g., the endpoint id, the event id), not PHI or secrets.

### 7.5 Compliance

- **NFR-COMP-1.** The system is designed to be HIPAA-defensible: PHI handling, access logging, and reveal protocols satisfy the technical safeguards expected of a Business Associate.
- **NFR-COMP-2.** Audit retention defaults to 7 years for tenant data and is configurable per tenant.

### 7.6 Multi-tenant isolation

- **NFR-ISO-1.** No tenant's configuration, deliveries, receipts, audit entries, or PHI shall be visible to another tenant by any path through the system, including dashboards, exports, and accidental cross-references.
- **NFR-ISO-2.** Operator-user-to-tenant access is enforced at every read and write surface. The system fails closed: an unauthenticated or unscoped request returns 4xx, never 200-with-empty.

---

## 8. Invariants (always-true properties)

These shall hold across all execution paths and shall be the basis of property-based testing where feasible.

- **INV-1.** Every state transition produces exactly one audit entry.
- **INV-2.** A request that fails verification is never dispatched to a handler.
- **INV-3.** A request whose idempotency key has been seen before is not dispatched to a handler again.
- **INV-4.** A delivery to a paused or disabled endpoint never occurs.
- **INV-5.** A secret is never logged, never exported, never returned in an API response. The value is revealable only through the explicit reveal protocol.
- **INV-6.** A delivery's recorded outcome corresponds to what actually happened on the wire. No outcome is fabricated, defaulted, or smoothed.
- **INV-7.** The audit log is append-only. There is no API surface that allows updating or deleting an entry.
- **INV-8.** Configuration changes are visible to subsequent reads from the same operator session within a bounded time (target: under 1 second).
- **INV-9.** Tenant T's configuration changes have no effect on tenant T'. There is no mechanism by which a configuration write to tenant T can change observable behavior for tenant T'.
- **INV-10.** A single endpoint's misbehavior cannot stall delivery to other endpoints.

---

## 9. Failure-mode catalog

What the system does when various things break. Not exhaustive; the named failures are the ones the spec commits to handling explicitly.

| Failure | What the system does |
|---|---|
| Partner returns 5xx on outbound delivery | Classify as transient; retry per policy; eventually dead-letter if retries exhaust |
| Partner returns 4xx (not 401/403) on outbound delivery | Classify as transient (assume their fault); retry per policy |
| Partner returns 401/403 on outbound delivery | Classify as configuration error; do not retry; surface for operator |
| Outbound IDP returns "credentials revoked" | Configuration error; surface for operator; endpoint flagged |
| Vault returns "secret missing" on outbound | Configuration error; surface for operator |
| Outbound TLS handshake fails | Classify as transient (network or partner cert change); retry; if all fail → dead-letter |
| Outbound DNS resolution fails | Same as TLS handshake fail |
| Inbound request signature does not verify | `Rejected(SignatureMismatch)`; HTTP 4xx; audit |
| Inbound request timestamp out of tolerance | `Rejected(TimestampOutOfTolerance)`; HTTP 4xx; audit |
| Inbound request from disallowed IP | `Rejected(IpNotAllowed)`; HTTP 4xx; audit |
| Inbound request to unknown slug | HTTP 404; audit (defense against scanning) |
| Inbound request to paused/disabled source | HTTP 5xx with `SourceNotActive`; audit |
| Inbound dispatch handler throws | `DispatchFailed`; HTTP 5xx (so partner retries per their policy); audit; the verified-and-deduped record is preserved so future processing has context |
| Inbound dispatch handler reports `IgnoredByDesign` | HTTP 2xx (the partner shouldn't retry); audit; not a failure |
| Audit log write fails | The originating operation fails (atomic with audit) |
| Database unavailable | Operations fail with explicit configuration/infrastructure error (typed); not "silent retry forever" |

---

## 10. What the spec deliberately does not say

The implementer chooses, with explicit recorded rationale:

- The persistence technology (relational, document, event-sourced, mixed).
- The HTTP framework (any one will do; the spec's HTTP behaviors are what matter).
- The retry-scheduler shape (background service, durable queue with delays, scheduled jobs — the observable behavior is what the spec constrains).
- The secret store implementation (any vault that supports the required revealability semantics).
- The inter-module communication topology (in-process, message bus, etc.).
- The DI container, the testing framework, the CI shape.
- Patterns or methodologies. The spec does not require or forbid any specific pattern (Hexagonal, CQRS, Strangler Fig, etc.). Implementers should choose what genuinely serves the requirements; named patterns are useful when their pressures match the system's pressures, and noise otherwise.

The spec also does not address:

- The product UX in pixel detail (treat the supplied HTML mockup, if any, as illustrative — the underlying behavior is the contract).
- Disaster recovery specifics (RPO/RTO targets are tenant-policy, not framework-policy).
- Internationalization (an open question, not addressed here).

---

## 11. Open questions for the implementer

These are real ambiguities the spec acknowledges. The implementer should resolve them with an explicit decision and record the rationale.

1. **Outbound at-least-once vs at-most-once.** The spec assumes at-least-once delivery (retries on transient failure). Is there any event type for which at-most-once is required (e.g., "send email")? If yes, those event types need a separate delivery shape that does not retry — or partners must be expected to be idempotent. Resolve before shipping any non-idempotent partner integration.
2. **Cross-tenant analytics rollup.** The spec is strictly tenant-isolated. If product later requires cross-tenant aggregation (e.g., "industry benchmark of average invoice issuance time"), the spec must be extended to define what is permissible.
3. **Inbound replay storage limit.** How long do we keep raw inbound bodies (encrypted) for replay/diagnosis? Default proposal: 90 days. Confirm with compliance.
4. **Outbound replay storage limit.** Same question for outbound payloads after dead-letter. Default proposal: 30 days post-dead-letter.
5. **Secret rotation initiated by partner.** Inbound rotation is single-step (partner sends a new secret, operator updates ALIS). Should we offer the partner a self-service rotation surface, or is that always operator-mediated?
6. **Webhook ordering.** Does any partner require strictly-ordered delivery within a tenant? The spec assumes best-effort ordering. If strict is needed, the per-endpoint shape changes (single-flight, no parallelism).
7. **Schema migrations of event types.** When `billing.invoice.issued` advances from v2 to v3, what is the operator workflow? Does the system auto-subscribe v2 subscribers to v3, or require explicit migration? Default proposal: explicit migration with a deprecation grace window of 90 days.

---

## 12. Acceptance — how to know the spec is satisfied

A reasonable acceptance suite consists of:

- **Property-based tests** for the invariants in section 8 (especially INV-2, INV-3, INV-4, INV-9).
- **Scenario tests** for every behavioral scenario in section 6, written in the same Given/When/Then form.
- **Adversarial tests** for FR-IN-9 (tampering, replay, header injection, body truncation, header/body mismatch, oversized payload, slow-loris).
- **Multi-tenant isolation tests** that exercise tenant T's surfaces while tenant T' has overlapping data, asserting nothing crosses.
- **A blind-review pass.** Hand the public surface (the configuration API, the dashboards' read APIs, the dispatch-handler interface, the typed outcomes) to someone who has not seen the implementation. Ask them to:
  1. Register a new outbound endpoint to a new partner.
  2. Diagnose a dead-lettered delivery to that endpoint.
  3. Implement a new dispatch handler for a new inbound source.
  4. Audit-trail the last 24 hours of activity for a specific tenant.

  If they can do these from the public surface alone, in less than an hour each, and report the surface "felt right," the spec is satisfied. If they are guessing, asking what null means, or wondering whether order matters, the gap is at the surface — fix the surface, not the docs.

---

## 13. Suggested implementation hints (non-binding)

These are observations from prior thinking on this domain. They are not requirements; the implementer may adopt or refuse.

- The "outbound" side and the "inbound" side have very different shapes (push vs pull, latency budgets, failure modes). Treating them as one module with a shared mental model is usually wrong; treating them as two modules with separate aggregates and separate operational concerns usually scales better.
- Authentication and verification both have many variants. Encoding each variant as a distinct closed value with a distinct strategy implementation is usually cheaper than encoding it as flags or branches.
- Persistence interfaces should be designed around the writes the aggregates make, not around generic CRUD. A generic `IRepository<T>` exposed at module boundaries tends to leak persistence semantics across the boundary; per-aggregate, internal repositories tend not to.
- "Outcome" types — discriminated values with one case per legitimate result — replace exception-driven control flow for *expected* outcomes. Reserve exceptions for genuinely unexpected conditions (DI mis-wired, database unreachable, programming-error invariant violations).
- Cross-module communication should prefer direct method calls on consumer-defined interfaces over reflective dispatch through bus/mediator infrastructure, unless the bus shape is genuinely earned (e.g., across process boundaries).
- The audit log's append-only invariant is easier to enforce if the API surface has no update or delete method to begin with, rather than enforced at runtime.
- Secrets should be referenced (vault path, key id) at the type level — the bytes never live in domain types — so accidental serialization cannot leak them.

These hints serve the spec; they are not the spec. If the implementer disagrees, they should pick what serves the requirements better and record the rationale.

---

## 14. Reference artifact

A working interactive HTML mockup of the operator dashboards exists separately (see the project's design-resources area). The mockup illustrates the *shape* of the operator experience — the screens, the structure of choices the operator makes, the way outcomes are surfaced — but is not authoritative on details of behavior. Where the mockup and this spec disagree, the spec wins; the mockup may be updated to follow.

End of spec.
