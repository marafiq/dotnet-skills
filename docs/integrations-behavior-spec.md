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

### 5.7 Outbound — authentication methods catalog

The framework shall support these categories of authentication for outbound deliveries. Each category is a behavioral contract describing what the operator configures, what the system does observably, how rotation works, and how it can fail. Implementations choose specific algorithms and libraries; the spec constrains *what the operator picks from* and *what the system does as a result*.

The dashboard shall present these categories grouped under headings like *Signature*, *Token*, *Transport*, *Other*. Each scheme shows a short description, a "recommended" badge where applicable, and a per-scheme configuration form when selected.

#### 5.7.1 Shared-secret signature (symmetric, HMAC family)

**Operator inputs.** A secret value, a signature header name, an optional timestamp header name, an optional timestamp tolerance (default 5 minutes), an optional template (ALIS-native, Stripe-style, GitHub-style, Slack-style, Custom) that pre-fills the headers and signature format string.

**Behavior.** Every outbound request carries a signature header computed deterministically from a canonical representation of the request (at minimum the body; for templates that include a timestamp, the timestamp is part of the canonical input). The receiver computes the same signature with the same secret to verify.

**Templates the framework shall offer.** ALIS-native (`X-Webhook-Signature`, `X-Webhook-Timestamp`, format `v1=<hex>`); Stripe-compatible (`Stripe-Signature`, single header with `t=<unix>,v1=<hex>`); GitHub-compatible (`X-Hub-Signature-256`, `sha256=<hex>`, no timestamp); Slack-compatible (`X-Slack-Signature`, `X-Slack-Request-Timestamp`, `v0=<hex>`). Templates exist so an operator integrating with a partner whose receiver-side library expects one of these can pick the matching shape without typing header conventions.

**Rotation.** Per FR-OUT-4. During the grace window, both old and new secrets produce valid signatures (the system uses one consistently per delivery; the receiver must accept either).

**Failure modes.** Vault-backed secret unreadable → ConfigurationError (no retry). Receiver returns 401/403 across attempts → ConfigurationError on the second consecutive auth-failure attempt. Computed signature does not match what receiver expects → manifests as 401/403 from receiver.

#### 5.7.2 Asymmetric signature (public-key)

**Operator inputs.** None initially — the framework generates the key pair on registration. Operator downloads the public key in PEM format and shares it with the partner. The private key is never exported, never displayed, never returned in any response.

**Behavior.** Every outbound request carries a signature computed with the private key over a canonical request representation. The receiver verifies with the public key. The framework attaches a header that names which key id signed the request (so receivers can look up the right public key during rotation).

**Rotation.** A new key pair is generated; both public keys are published (via a key id reference) for the grace window; the partner can fetch the new key on their schedule. After the window, only the new key signs.

**When to use.** Receivers who do not want to manage shared secrets (no symmetric secret to leak); receivers who want non-repudiable signatures.

**Failure modes.** Same as shared-secret, plus key-pair generation failure during rotation initiation → ConfigurationError surfaced immediately, rotation aborted.

#### 5.7.3 Static bearer token

**Operator inputs.** A token value (treated as opaque bytes), a recommended rotation cadence (default 90 days, with reminders).

**Behavior.** Every request carries `Authorization: Bearer <token>`. The framework does not parse, validate, or transform the token — it is opaque.

**Rotation.** Direct replacement, optionally with a grace window during which both old and new are accepted by the partner side (the framework alternates or sends both — implementation choice). The dashboard surfaces approaching rotation reminders.

**Failure modes.** Receiver returns 401 → ConfigurationError. Token expired at the partner's side without rotation → manifests as 401.

#### 5.7.4 Token from identity provider (OAuth 2.0 family)

**Operator inputs.** Token endpoint URL, client identifier, client credential reference (secret or certificate), optional scope, optional resource/audience, optional sub-variant selector (Standard OAuth 2.0 client credentials / Azure AD service principal / AWS Signature v4 / other supported variants).

**Behavior.** The framework requests a token from the identity provider using the configured credentials. Tokens are cached for their declared lifetime, refreshed before expiry with a safety margin (default: refresh when 60 seconds remain on the token). Each outbound request attaches the cached token as a bearer token in the Authorization header. Token refresh is invisible to the operator under normal operation; the dashboard exposes the cache state for diagnostic purposes.

**Sub-variants.** The framework distinguishes:
- *Standard OAuth 2.0 client credentials* — generic IDP, configured by URL.
- *Azure AD service principal* — adds Azure tenant ID, supports certificate-bound tokens and federated identity, has its own diagnostic surface.
- *AWS Signature v4* — does not fetch a token; signs every request with cloud credentials per the cloud provider's signing spec. Configured with region, service name, access key, secret key reference (or assumed role).
- *Other provider-specific protocols* may be added when the user-visible inputs and behavior differ enough to warrant a distinct option (e.g., GCP service account signed JWT).

The framework shall surface these as separate options because their configuration surfaces, failure modes, and operator vocabulary differ — collapsing them under one "OAuth-ish" label confuses operators and produces wrong configurations.

**Rotation.** Client credentials are rotated per FR-OUT-4. Token refresh is automatic.

**Failure modes.**
- IDP unreachable (timeout/connection): → Retrying (transient).
- IDP returns 4xx other than 401/403 (rate limit, malformed request): → Retrying with backoff.
- IDP returns 401/403 (credentials revoked, scope denied): → ConfigurationError, *not* retried, surfaced for operator action.
- Cached token expired between fetch and partner request (clock skew): → automatic refetch, transparent to operator.
- Token rejected by receiver (401/403): → distinguish from IDP failures — manifest as ConfigurationError after the second consecutive receiver-401 within a short window.

#### 5.7.5 Mutual TLS (mTLS)

**Operator inputs.** A client certificate and its private key. The framework shall accept upload in two formats: PEM-encoded certificate file plus PEM-encoded key file, or a PFX/PKCS#12 bundle with a passphrase. Optional inputs: subject Common Name (for monitoring labels), TLS version floor (default: TLS 1.2 minimum, TLS 1.3 preferred).

**Generation alternative.** The framework may offer a generate-on-server option where ALIS produces a private key and a Certificate Signing Request (CSR); the operator downloads the CSR, has it signed by their CA, and uploads the resulting certificate back. The private key never leaves the secret store.

**Behavior.** ALIS presents the client certificate during the TLS handshake with the partner. Verification happens at the partner's TLS terminator before any application-layer code runs on the partner's side. No body-level signing is required.

**Expiry tracking.** The framework shall track the certificate's `notAfter` field and surface warnings in the dashboard at 30 days, 14 days, 7 days, and daily within the final week. After expiry, deliveries fail as ConfigurationError until a new certificate is installed.

**Rotation.** A new certificate (and key, if generating fresh) is uploaded; both are valid for the configured overlap period (default 14 days for certificates, longer than for shared secrets because partner-side cert distribution often involves manual steps); after the window, only the new is used.

**Failure modes.**
- Certificate expired: → ConfigurationError, blocks all deliveries for this endpoint.
- Partner does not trust our cert chain: → TlsHandshakeFailed (a typed sub-case of FailureReason that distinguishes from generic timeouts).
- Private key unreadable from secret store: → ConfigurationError.

#### 5.7.6 HTTP Basic

**Operator inputs.** Username and password.

**Behavior.** Every request carries `Authorization: Basic <base64(username:colon:password)>`.

**Spec position.** The framework shall support HTTP Basic for partners that require it but shall mark it visibly as the weakest scheme on the list, prompt for justification at activation, and disable it by default for endpoints handling Standard or Restricted PHI sensitivity unless an explicit compliance approval is recorded.

**Rotation.** Credential change with optional grace window.

#### 5.7.7 IP allowlist only (no application-layer authentication)

**Operator inputs.** A documented compliance approval reference. The dashboard displays the framework's static egress IPs for the operator to share with the partner.

**Behavior.** Outbound requests carry no auth headers. The partner trusts ALIS's source IPs at the network layer.

**Spec position.** Requires compliance approval per FR-COMP-4. Used only when the partner explicitly mandates it (legacy systems, isolated networks). The dashboard shall surface this scheme distinctively (e.g., a warning indicator on the endpoint card).

**Rotation.** Not applicable to authentication; the egress IPs are infrastructure-level. Egress IP changes are coordinated as a separate operations concern with notice to all affected operators.

#### 5.7.8 Composite

**Operator inputs.** Configure two or more of the above schemes.

**Behavior.** Each scheme's signing or header attachment runs in turn for every outbound request. If any one fails to apply (e.g., vault secret missing for the HMAC half), the entire delivery is a ConfigurationError. The receiver is expected to validate every applied scheme independently.

**Use case.** Belt-and-suspenders configurations for high-security receivers (signature for body integrity + bearer token for principal identity).

**Failure modes.** Composite of the parts; the most-specific failure determines the typed reason.

---

### 5.8 Inbound — verification methods catalog

Mirror of §5.8 for inbound. Each verification scheme maps to a category of partner authentication; partner-specific templates pre-fill the conventions. As above, the dashboard groups schemes by category.

#### 5.8.1 Shared-secret signature verification (HMAC family)

**Operator inputs.** A shared secret (provided by the partner; ALIS stores encrypted), the signature header name, the timestamp header name (if applicable), the timestamp tolerance (default 5 minutes), an optional partner template (Stripe, GitHub, Twilio, Slack, ALIS-native, Custom).

**Behavior.** For each incoming request: extract the signature header, extract the timestamp header (if applicable), reconstruct the canonical input, compute the expected signature using the stored secret, compare in constant time. Reject if mismatch; reject if timestamp outside tolerance.

**Templates the framework shall offer.** Stripe (`Stripe-Signature: t=,v1=`), GitHub (`X-Hub-Signature-256: sha256=`, no timestamp; replay defense via idempotency only), Twilio (`X-Twilio-Signature` over URL + sorted form params, SHA-1), Slack (`X-Slack-Signature: v0=` plus `X-Slack-Request-Timestamp`).

**Rotation.** Per FR-IN-4. Single-step replacement; the dashboard surfaces the change in the audit log.

**Failure modes.**
- Signature header missing or malformed: → SignatureMismatch.
- Computed signature does not equal received: → SignatureMismatch.
- Timestamp header missing or outside tolerance: → TimestampOutOfTolerance.
- Stored secret unreadable: → ConfigurationProblem (HTTP 5xx, partner retries are appropriate).

#### 5.8.2 JWT-bearer with JWKS

**Operator inputs.** The partner's JWKS URL (where their public keys live), the expected audience (`aud` claim), the expected issuer (`iss` claim, optional), the JWT expiry skew tolerance (default 5 minutes), the JWKS refresh interval (default 1 hour).

**Behavior.** Extract the JWT from `Authorization: Bearer`. Look up the signing key from the cached JWKS using the JWT's `kid` (key id) header. If the kid is not in the cache, refresh the JWKS (at most once per minute to defend against unknown-kid spam). Verify the JWT's signature against the looked-up key. Verify expiry within the skew tolerance. Verify audience matches. Verify issuer matches if configured. If all pass, extract the idempotency key per the configured rule and proceed; otherwise reject with the most specific failure reason.

**Failure modes.**
- JWT malformed: → SignatureMismatch (we cannot parse it; treat as forgery).
- Signing key not in JWKS even after refresh: → SignatureMismatch.
- Signature invalid: → SignatureMismatch.
- Expired beyond skew: → TimestampOutOfTolerance.
- Audience mismatch: → SignatureMismatch (specifically AudienceMismatch sub-reason if the framework distinguishes).
- Issuer mismatch (when configured): → SignatureMismatch (IssuerMismatch sub-reason).
- JWKS endpoint unreachable: → ConfigurationProblem.

**Rotation.** Partner-driven. Keys rotate on the partner's side via JWKS publication; ALIS picks them up automatically through scheduled refresh and on-demand refresh when an unknown kid arrives.

#### 5.8.3 Mutual TLS verification

**Operator inputs.** The partner's expected client certificate or expected CA chain (PEM upload), expected subject Common Name (for binding the cert to the partner identity), expected key usage extensions (optional).

**Behavior.** The TLS handshake on the receive URL requires the partner to present a client certificate. The framework's TLS terminator verifies the chain against the configured trust store and matches the subject CN. If verification fails, the request never reaches application code; the framework records a verification-failure audit entry from the network layer.

**Failure modes.**
- Partner does not present a certificate: → TLS handshake failure, request rejected before reaching the framework.
- Partner's certificate chain does not match expected CA: → same.
- Subject CN does not match: → verification failure recorded, request rejected.

**Rotation.** Partner-driven. The framework supports configuring multiple expected certificates (overlap window) for partner-side rotation.

#### 5.8.4 API key in header

**Operator inputs.** The secret value (provided by the partner or generated by ALIS), the header name (default `X-API-Key`).

**Behavior.** Extract the header; compare with the stored secret using constant-time equality. Accept on match, reject on mismatch.

**Spec position.** Provides authentication of *principal* but no integrity check on the body. The framework shall warn the operator at registration if API-key-only is selected for sources that handle Standard or Restricted PHI sensitivity, and shall require composite (API key + IP allowlist or API key + signature) for those cases unless an explicit compliance approval is recorded.

**Rotation.** Direct replacement; optional grace window where both keys are accepted.

#### 5.8.5 IP allowlist only

**Operator inputs.** A list of allowed IPv4/IPv6 ranges (CIDR notation), a documented compliance approval reference, an optional X-Forwarded-For trust policy (the framework needs to know whether to trust X-Forwarded-For from a reverse proxy).

**Behavior.** Determine the source IP per the X-Forwarded-For trust policy. Reject if the source IP is not in any allowed range. No per-request signature check.

**Spec position.** Requires compliance approval per FR-COMP-4. Often paired in composite (e.g., IP allowlist + API key) for partners that cannot do signing but operate from a known network.

**Failure modes.** Source IP not in allowlist: → IpNotAllowed. X-Forwarded-For trust misconfigured (header trusted from non-proxy source): logged as ConfigurationProblem at registration if the framework can detect; otherwise as IpNotAllowed at receive time.

#### 5.8.6 Composite

**Operator inputs.** Configure two or more of the above.

**Behavior.** Every configured check must pass. If any fails, the request is Rejected with the most-specific failure reason (e.g., if signature passes but timestamp is stale, the reason is TimestampOutOfTolerance, not SignatureMismatch).

**Use case.** Strong-defense configurations: signature + IP allowlist; API key + IP allowlist; mTLS + JWT (transport identity + application identity).

#### 5.8.7 Provider-specific compatibility templates

The framework shall provide pre-configured verification templates for partners whose webhook schemes are well-known: Stripe, GitHub, Twilio, Slack, Salesforce, and others as added. Each template:

- Pre-fills the verification scheme category (HMAC, JWT, etc.) with the partner's specific conventions.
- Pre-fills header names, signature format, timestamp handling.
- Pre-fills the idempotency-key extraction rule (e.g., Stripe `$.id`, GitHub `X-GitHub-Delivery`, Twilio `MessageSid` from form body).
- Documents the partner's published verification contract inline (a short paragraph linking to the partner's docs).
- Pre-fills any partner-specific quirks (e.g., GitHub does not include a timestamp; Twilio signs over URL + sorted form params, not raw body).

Operators may select a template as a starting point and customize. The framework shall version templates and warn operators when a partner has updated their published contract since the template was last refreshed.

---

### 5.9 Cross-cutting — credential and certificate handling

The framework deals with several kinds of secret material across both directions. Behavior shall be uniform across them.

#### 5.9.1 Storage and visibility

**Storage.** All secret material is stored encrypted at rest in a vault-style store. The framework shall not write plaintext secret material to logs, audit entries, error responses, dashboards, telemetry, or exports. References (vault paths, key ids) are surfaced; values are not.

**Display.** The dashboard shall represent stored secrets by their reference path and metadata (created at, version, owner) — never by their bytes. A reveal action (5.10.4) is the only path to seeing the bytes.

#### 5.9.2 Upload formats

The framework shall accept the following upload formats. The dashboard offers each in a context-appropriate file picker that recognizes the format from extension and content.

| Format | Used for | Notes |
|---|---|---|
| Pasted text | Shared secrets, bearer tokens, API keys, IDP client secrets | UI shall mask the field; no preview after submission |
| PEM (single file) | Public certificates, public keys | Recognized by `-----BEGIN CERTIFICATE-----` / `-----BEGIN PUBLIC KEY-----` |
| PEM (paired files) | Certificate + private key for mTLS or asymmetric signing | UI accepts both files in one upload step |
| PFX / PKCS#12 (single file) | Bundled certificate + private key with passphrase | UI prompts for passphrase; passphrase is one-time use, never stored |
| JWKS endpoint URL | Partner public keys for JWT verification | Framework fetches and caches; refresh interval configurable |
| Static IP / CIDR list | IP allowlists | Comma-separated or one-per-line |

Files shall be validated synchronously on upload: parse the format, extract metadata (issuer, subject, expiry for certificates; key type and size for keys), reject malformed input with a typed reason, surface metadata in the dashboard for operator confirmation before storing.

#### 5.9.3 Generation

For asymmetric signature schemes and (optionally) for mTLS, the framework shall offer to generate key material server-side. Generated private keys are written directly to the secret store and never returned in any response. Generated public material (public key, CSR) is downloadable once and then re-derivable on demand from the stored private key.

The dashboard shall make clear at generation time which material is server-generated (private key) vs operator-provided.

#### 5.9.4 Reveal

Every secret reveal action shall be:

- **One-time per generation/rotation.** After the first reveal, the secret can only be replaced via rotation, not re-revealed.
- **Role-gated.** Only roles with explicit reveal permission may invoke. A second factor may be required by tenant policy.
- **Justification-required.** A free-text reason is mandatory; the framework shall not surface a reveal without one.
- **Audited.** Reveal generates a high-priority audit entry: actor, IP, time, justification, the specific secret reference revealed.
- **Time-limited display.** Once shown, the reveal UI exposes the value for a bounded time (default 60 seconds) before automatically masking again. Copy-to-clipboard is offered to reduce shoulder-surfing risk.

#### 5.9.5 Expiry tracking

For all material with embedded expiry (certificates, JWT-signed content if recorded, OAuth refresh tokens with declared expiry):

- The framework shall index the expiry date at storage time.
- The dashboard shall surface upcoming expiries: 30 days (notice), 14 days (warning), 7 days (alert), within 24 hours (escalation).
- The framework shall block deliveries that depend on expired material with a typed ConfigurationError, naming the expired item.

#### 5.9.6 Provenance and chain of custody

Each secret reference shall carry metadata describing how it arrived: uploaded by user X at time T, generated by system at time T, rotated from version N-1 at time T. The audit log shall be the source of truth for this chain.

#### 5.9.7 Composite credentials

When a scheme requires multiple pieces of secret material (e.g., mTLS = cert + key; OAuth 2.0 with cert-bound = cert + key + IDP config), the framework shall treat them as one logical credential for rotation purposes. Replacing the credential rotates all parts together; the dashboard shall not allow partial rotation that would leave a half-valid configuration.

---

### 5.10 Cross-cutting — operability

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
