---
# ──────────────── Identity ────────────────
id: care-tracking-record-toolbar
title: "Care Tracking — Record Care commit toolbar"
view: "unknown — fill when source arrives"
control_type: toolbar

# ──────────────── Contract status ────────────────
contract_status: incomplete
contract_status_reason: >
  Mutating slice with required failure_matrix cells at status: unknown
  (http_4xx, http_5xx, network_timeout, double_click_or_resubmit,
  retry_after_failure, partial_success, idempotency_strategy,
  queue_retention on failure). Tenant tamper_matrix scenarios untested
  (route, body, foreign-key ownership). Endpoint verification has
  payload_schema, error_shape, anti_forgery, authorization at unknown.
  Source-fill or testing required before this slice is contract-complete.

routes:
  - "/Care/Tracking/{communityId}/Record/{date}/List/{shiftId}"

binding:
  view_model: "unknown"
  property: "unknown"

# ──────────────── Population ────────────────
data_source:
  kind: api
  reference: "Server-rendered HTML for the editor view; toolbar state is driven by client-side queue + SignalR pushes after commit."
  populated_by: "unknown — fill when source arrives"
  fields:
    tasks_recorded: "int (advances via SignalR after commit; same value as the summary card)"
    tasks_total: "int"
    queue_count: "int (client-only; number of unsaved Completed / Not-Completed actions queued)"
  default_value: null
  pre_filled_from_server: false

# ──────────────── Server-side business logic ────────────────
business_logic:
  selection:
    rules:
      - "The toolbar's 'X of Y recorded' counter reflects the count of completed care tasks for this shift on this date — same source as the summary card on /Care/Tracking/{communityId}."
    code_refs: ["unknown — fill when source arrives"]

  authorization_filters:
    - rule: "User must have permission to record care for this shift (rule unknown — fill when source arrives)."
      code_ref: "unknown"

  computed_fields:
    - name: queue_count
      derivation: "Client-side: count of per-row actions (Completed / Not Completed) that have local state but haven't been committed. Reset to 0 on successful POST."
      code_ref: "unknown — likely a JS handler attached to per-row buttons"

  ordering: null
  paging: null
  soft_delete: null
  temporal_scoping: "Bound to the {date} segment in the URL (the shift's date)."

  user_visible_side_effects:
    - kind: audit_entry
      description: "Each task recorded likely writes an audit entry. Whether visible elsewhere is unknown."
      code_ref: "unknown"
    - kind: signalr_push
      description: "POST returns 200 → server emits SignalR push on `stafftaskshub` → summary card counter and any in-page progress indicators advance within ~1–3 s."
      code_ref: "unknown — fill when source arrives"

# ──────────────── Configuration ────────────────
configuration:
  required_indicator_convention: null
  presence_condition: null
  states:
    - queue_empty            # button disabled-grey, no count
    - queue_pending          # button active blue/green, shows '(N)' suffix
    - submitting             # POST in flight (transient)
    - post_success           # toast shown, button reverts to queue_empty

  buttons:
    - id: record_prn_care
      label: "Record PRN Care"
      role: navigate
      target: "/Care/Tracking/{communityId}/RecordPrn/{date}/{shiftId}"   # observed pattern; unverified
    - id: print
      label: "Print"
      role: export
      target: "unknown — fill when source arrives (likely opens browser print dialog or downloads PDF)"
    - id: record_care
      label: "Record Care"
      label_template: "Record Care ({queue_count})"   # when queue is non-empty
      role: commit_batch
      enabled_when: "queue_count > 0"

  empty_state: "When no tasks are queued, the Record Care button is disabled-grey with no count suffix. Counter still shows the persistent X of Y recorded."

# ──────────────── Validation ────────────────
validation: []

# ──────────────── Reactivity ────────────────
reactivity:
  - event: queue_changed
    targets: [self]
    action: visual_state_change
    endpoint: null
    settle_ms: 0
    immediate_response: "Record Care button state recomputed from queue_count: enabled blue/green when > 0, label appends '({queue_count})'; disabled grey when 0. No network."
    final_response: "Same as immediate."

  - event: click
    targets: [self, "care-tracking-shift-summary-card", "global-toast-region"]
    action: commit_batch
    endpoint:
      method: POST
      url: "/Care/Tracking/{communityId}/Record/{date}"
      request_payload: "JSON list of queued task records (resident id + task id + Completed/NotCompleted + minutes_taken + optional notes). Exact schema — unknown — fill when source arrives."
      response_handling: "On 200: success toast emitted top-right ('Successfully recorded N outcomes' — observed for N=1; plural form unverified). Local queue cleared; button reverts to queue_empty state."
    settle_ms: 2500
    immediate_response: "Toast appears; button reverts; counter does NOT update synchronously."
    final_response: "Within ~1–3 s of the POST response, SignalR push from `stafftaskshub` advances the X-of-Y counter here AND on the summary card."

  - event: click
    targets: ["care-tracking-record-prn-editor"]
    action: navigate
    endpoint:
      method: GET
      url: "/Care/Tracking/{communityId}/RecordPrn/{date}/{shiftId}"   # pattern unverified
    settle_ms: null
    immediate_response: "Full-page navigation."

  - event: click
    targets: []
    action: export
    endpoint:
      method: "unknown"
      url: "unknown — fill when source arrives"
    immediate_response: "Likely opens browser print dialog or downloads a PDF/printable view. Behavior unverified."

# ──────────────── Cross-slice ────────────────
related_controls:
  - id: care-tracking-shift-summary-card
    relation: sibling
  - id: dashboard-community-selector
    relation: scope_provider
  - id: global-toast-region
    relation: target

scoped_by:
  - dashboard-community-selector

signal_sources:
  - kind: signalr
    detail: "After a successful POST, server fan-out via `stafftaskshub` updates the X-of-Y counter both here AND on the summary card. Hub method name + frame schema — unknown — fill when source arrives."

on_close: null

# ──────────────── Endpoints ────────────────
endpoints:
  - method: POST
    url: "/Care/Tracking/{communityId}/Record/{date}"
    purpose: "Commit queued task records for the shift on the given date."
    response_kind: json
    verification:
      method:           observed       # POST observed in network during commit
      route:            observed       # URL captured
      payload_schema:   unknown        # request body schema not source-confirmed
      response_shape:   observed_partial   # 200 with brief JSON observed; full schema unknown
      error_shape:      unknown        # no failure path exercised
      anti_forgery:     unknown        # presence of __RequestVerificationToken not confirmed
      authorization:    unknown        # permission rule for the action not source-confirmed

  - method: GET
    url: "/Care/Tracking/{communityId}/RecordPrn/{date}/{shiftId}"
    purpose: "Navigate to the PRN-care recording editor (separate flow)."
    response_kind: html_full
    verification:
      method:           observed       # link href captured
      route:            observed_partial   # pattern guessed; navigation not exercised
      payload_schema:   n/a
      response_shape:   unknown
      error_shape:      unknown
      anti_forgery:     n/a
      authorization:    unknown

  - method: "unknown"
    url: "unknown — Print button target"
    purpose: "Print or export the current shift's task list."
    response_kind: "unknown"
    verification:
      method:           unknown
      route:            unknown
      payload_schema:   unknown
      response_shape:   unknown
      error_shape:      unknown
      anti_forgery:     unknown
      authorization:    unknown

# ──────────────── Authorization ────────────────
authorization:
  presence_condition: "User must have permission to view Care Tracking for this community + shift. The toolbar is visible whenever the editor is reachable."
  action_authorization:
    - action: commit_batch
      requires: "unknown — fill when source arrives (likely a CarePlan write permission)"
      on_denied:
        response_kind: html_full
        user_sees: "unknown — likely the legacy generic-error toast pattern (HTML 403 → unparseable AJAX → 'ERROR / error' fallback; observed elsewhere on +Incident click). Verify when an unauthorized user is available."
        legacy_quirk: "Generic toast on unparseable HTML 403 response."
        rewrite_intent: improve

  re_auth_required: false

  tenant_boundary:
    context_sources:
      - "url_path: /Care/Tracking/{communityId}/Record/{date}/List/{shiftId}"
      - "session: CurrentCommunityId (assumed; legacy MVC default — verify when source arrives)"
    tamper_matrix:
      - endpoint: "POST /Care/Tracking/{communityId}/Record/{date}"
        scenarios:
          - kind: route_tenant_mismatch
            baseline_context: "Authenticated user with grant for community A; queue contains 1 task record."
            tampered_input: "Manually edit URL communityId to community B (user has no grant); submit Record Care."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: body_tenant_mismatch
            baseline_context: "Authenticated user with grant for community A; URL is /Care/Tracking/A/Record/{date}."
            tampered_input: "Intercept POST body and replace communityId field with community B; submit."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: foreign_key_ownership
            baseline_context: "Authenticated user with grant for community A; URL/session both A."
            tampered_input: "POST body carries resident_id or task_id belonging to community B (different tenant)."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: revoked_grant
            baseline_context: "User initially has grant for community A; admin revokes mid-session."
            tampered_input: "User submits a queued Record Care from the (now-stale) editor in community A."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: read_vs_write
            baseline_context: "User has grant to view community A but not to record care (presence-time vs action-time auth)."
            tampered_input: "User clicks Record Care after queueing tasks they could view."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested — likely the legacy generic-error toast pattern"
            source_refs: ["unknown"]
            status: unknown

# ──────────────── Failure matrix ────────────────
# Mutating slice — failure_matrix is REQUIRED. Each cell carries
# status + behavior + evidence. Cells at status: unknown for required
# write semantics force contract_status: incomplete.
failure_matrix:
  http_4xx:
    status:   unknown
    behavior: "403 likely yields the generic-error toast (legacy quirk; observed elsewhere on +Incident click). 422 (validation) — response shape unknown. Worth flagging for rewrite to return ProblemDetails JSON with field-level errors."
    evidence: "untested"
  http_5xx:
    status:   unknown
    behavior: "Likely a generic ASP.NET error rendered into the toast slot or a full-page YSOD; client AJAX cannot parse and falls back to generic toast."
    evidence: "untested"
  network_timeout:
    status:   unknown
    behavior: "Client likely shows generic error or no feedback. Real risk: user re-clicks → potential duplicate POST without idempotency."
    evidence: "untested"
  double_click_or_resubmit:
    status:   unknown
    behavior: "No client-side debounce observed. Real risk: a fast double-click could submit the queue twice. Idempotency strategy unknown."
    evidence: "untested — must exercise with two rapid clicks under network throttling"
  retry_after_failure:
    status:   unknown
    behavior: "Queue retention on failure is unclear — does the queue stay populated so the user can re-click? Or is it cleared regardless?"
    evidence: "untested — must exercise by forcing a 500 response"
  partial_success:
    status:   unknown
    behavior: "If 3 of 5 batch items succeed and 2 fail, behavior unverified. Are the 2 retained in the queue? Toast may show ambiguous count."
    evidence: "untested"
  refresh_mid_flight:
    status:   unknown
    behavior: "POST may complete server-side; SignalR push still advances counters; client never sees the response. User's perspective: action appears to have succeeded silently. Acceptable but worth confirming there's no double-write on retry."
    evidence: "untested"
  context_switch_mid_edit:
    status:   observed
    behavior: "Switching the community selector while the queue has items silently discards the queue (observed at dashboard-community-selector). No confirmation prompt; data loss is silent. rewrite_intent: improve."
    evidence: "Observed during exploration: queue cleared without prompt on community switch."
  push_disconnect:
    status:   unknown
    behavior: "If SignalR drops during the 1–3 s settle window, the counter advancement may be missed entirely. The POST itself succeeds; only the live UI feedback is lost. Refresh recovers, but stale counter persists between."
    evidence: "untested — must exercise by killing the WebSocket mid-flight"
  idempotency_strategy:
    status:   unknown
    behavior: "Possibilities: natural_idempotent (server dedupes by resident_id+task_id+date), client_dedupe_token (no client evidence observed), or none. Critical to confirm before rewrite."
    evidence: "untested — must exercise via deliberate double-submit"
  queue_retention:
    status:   observed_partial
    behavior: "On success: queue cleared (observed). On failure: behavior unknown. On context switch: queue cleared without prompt (observed)."
    evidence: "Success path observed during exploration. Failure path untested."

# ──────────────── Mode B helpers ────────────────
url_conventions_observed:
  - "/Care/Tracking/{communityId}/Record/{date} → POST endpoint to commit batch task records (verb-on-noun; date in path)"
  - "/Care/Tracking/{communityId}/Record/{date}/List/{shiftId} → editor view URL (List/{shiftId} suffix as discriminator for the visible list)"
  - "/Care/Tracking/{communityId}/RecordPrn/{date}/{shiftId} → PRN navigation (pattern guessed; unverified)"

unknowns_to_fill_when_source_arrives:
  - "view: which Razor view file renders this toolbar?"
  - "controller action that returns the editor view"
  - "POST request payload schema"
  - "POST response shape on success and on each failure class"
  - "anti-forgery requirement on POST /Record/{date}"
  - "Print button's destination — print dialog vs PDF download vs new tab"
  - "PRN navigation URL exact form"
  - "stafftaskshub method name + payload schema for task-recorded push"
  - "Permission requirement to commit"
  - "Idempotency strategy on POST"
  - "Queue retention behavior on failure"
  - "All `failure_matrix` cells currently marked 'Unverified' must be confirmed"
  - "tenant_boundary.tamper_test_evidence — exercise tamper scenarios"

# All action and event values used in this artifact are sanctioned by the
# template; nothing to register as a proposed extension. Block left empty
# to make that explicit.
extensions: []
---

# Care Tracking — Record Care commit toolbar

## Behavior summary

The toolbar at the top of the per-task editor (`/Care/Tracking/{communityId}/Record/{date}/List/{shiftId}`) holds three actions and a counter. Two actions navigate (Record PRN Care, Print). The third — **Record Care** — commits the local queue of per-row Completed / Not-Completed actions in a single POST. The button shows the queued count `Record Care (N)` when N > 0 and stays disabled-grey when N = 0. On successful commit, a top-right success toast appears, the queue clears, and within ~1–3 s a SignalR push on `stafftaskshub` advances the X-of-Y counter both here and on the parent shift summary card.

## Code references

- View: unknown — fill when source arrives.
- Controller action handling `POST /Care/Tracking/{communityId}/Record/{date}`: unknown.
- SignalR Hub: `stafftaskshub` (negotiate URL observed; method name unknown).
- Client-side queue logic for per-row Completed / Not-Completed clicks: unknown — likely a JS handler bundled with the editor view.

## Edge cases

- **Empty queue** — button disabled-grey, no count suffix, no POST possible.
- **POST failure (network or server error)** — behavior unverified. The `failure_matrix` enumerates each class; all currently `unverified` and must be exercised.
- **Concurrent commit by another user** — both users' POSTs succeed independently; their respective summary-card counters advance via SignalR.
- **Stale community / date** — switching the global community selector reloads the page; queue is lost without confirmation. Worth flagging in `rewrite_intent`.
- **Permission denied for commit** — likely the legacy generic-error-toast pattern. `rewrite_intent: improve` to actionable message.
- **Browser refresh mid-commit** — POST may complete server-side but client never gets the response; SignalR push still advances counters, so next page load sees post-commit state.
- **URL / payload tampering** — sending a POST body with `communityId` or `resident_id` belonging to a different community than the URL suggests must be rejected at the server. Untested; **flag as required tamper test before contract-complete.**

## Verification claims

1. **Initial render with empty queue** — `Record Care` button disabled-grey, no count suffix; counter shows persistent X-of-Y for the shift.
2. **Queue advance on per-row click** — clicking Completed on a per-row form (no network) increments the toolbar's queued count; button changes to active blue/green and label reads `Record Care ({queue_count})`. No network call fires.
3. **Commit POST** — clicking `Record Care` with `queue_count > 0` fires `POST /Care/Tracking/{communityId}/Record/{date}` with a JSON payload representing the queued actions.
4. **Success toast** — on POST 200, a top-right toast appears with text matching *"Successfully recorded N outcomes"* (observed N=1; plural form unverified).
5. **Queue clears on success** — after the POST 200, the button reverts to disabled-grey and any per-row Reset link disappears.
6. **Counter advances via SignalR (settle window 1–3 s)** — within 1–3 s of the POST response, the X-of-Y counter advances by `queue_count` (verified for N=1).
7. **Cross-slice signal to summary card** — same SignalR push advances the counter on `care-tracking-shift-summary-card` at `/Care/Tracking/{communityId}`. Verified by navigating to summary after commit.
8. **PRN navigation** — clicking `Record PRN Care` navigates to a separate PRN editor URL (exact pattern unverified).
9. **Tamper boundary on POST** — submitting a POST body whose `communityId` differs from the URL communityId should yield 403 / failure; not silent success on the wrong community. **Untested — required before contract-complete.**
10. **Idempotency on double-click** — fast-clicking `Record Care` twice in succession should not submit the queue twice. Strategy (client debounce, server dedupe, or none) and observable behavior must be confirmed. **Untested — required.**

## Verification log

- 2026-05-02 — initial Mode-B artifact drafted from browser observation. Claims 1–7 verified during exploration. Claim 8 inferred from link presence.
- 2026-05-02 — Codex review: redacted any tenant-specific values; replaced coarse `unverified: false` with per-aspect `verification` (revealed POST is observed-only, not source-confirmed); added required `tenant_boundary` and `failure_matrix` blocks with multiple Unverified cells flagged for source-fill or testing; added claims 9 (tamper boundary) and 10 (idempotency) — both required before this slice is contract-complete.

## Linked artifacts in this feature

- [`care-tracking-shift-summary-card.md`](care-tracking-shift-summary-card.md) — sibling slice on `/Care/Tracking/{communityId}`. Receives the same SignalR push that advances this slice's counter.
- [`dashboard-community-selector.md`](dashboard-community-selector.md) — `scope_provider` for this slice.
- (Future) `care-tracking-per-task-row.md` — child slice; the per-row Completed / Not-Completed forms feeding this toolbar's queue.
- (Future) `care-tracking-filter-bar.md` — sibling slice on the editor view.
