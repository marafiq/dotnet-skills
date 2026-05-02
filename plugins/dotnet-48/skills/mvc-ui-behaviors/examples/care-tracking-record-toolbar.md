---
schema_version: 7

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
  retry_after_failure, partial_success, push_disconnect,
  idempotency_strategy, queue_retention on failure, concurrency_conflict,
  audit_emission). Tenant tamper_matrix scenarios untested across all
  four endpoints (record_care_post, record_prn_navigation, print_button,
  stafftaskshub_push) for route, body, foreign-key ownership,
  revoked_grant, read_vs_write. Endpoint verification has payload_schema,
  error_shape, anti_forgery, authorization at unknown for the mutating
  POST. SignalR push endpoint stafftaskshub_push has authorization
  unknown — cross-tenant fan-out behavior is the highest-risk gap.
  Source-fill or testing required before this slice is contract-complete.

# Optional exception block — empty here because we are not claiming
# observed_partial as acceptable for any aspect; everything that's
# partial is rolled forward as the work that gates `complete`.
contract_status_exceptions: []

# global-toast-region is referenced from related_controls and reactivity
# targets but has not yet been authored as its own slice. Per gate rule 7,
# this must be acknowledged here rather than silently allowed.
cross_slice_refs_pending:
  - ref: global-toast-region
    referenced_from: "related_controls[2].id; reactivity[*].targets"
    reason: "Toast region is a global emit-target shared by many slices. Not yet authored as a focused slice; behavior is documented inline here pending a dedicated artifact."
    expected_to_land: "When a second mutating slice that emits toasts is authored, the toast region earns its own artifact and slices reference it by id."

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
    # The toolbar's "X of Y recorded" counter reflects committed care
    # tasks for this shift on this date — same data source as the summary
    # card. Atomic predicates below; rules_summary is a human-reader
    # narrative, not a substitute for the structured fields (gate rule 5).
    predicates:
      - rule: "TaskRecord.CommunityId = {communityId}"
        status: unknown
        evidence: "untested — controller / repository unknown"
        rationale: "scope to current community"
      - rule: "TaskRecord.ShiftDate = {date}"
        status: unknown
        evidence: "untested — fill when source arrives"
        rationale: "scope to selected shift date"
      - rule: "TaskRecord.IsCompleted = true (for the X count) OR plan-task TOTAL (for the Y count)"
        status: unknown
        evidence: "untested — actual derivation unverified"
        rationale: "X-of-Y aggregation"
    projection:
      fields: ["X (completed count)", "Y (total count)"]
      status: observed
      evidence: { test_id: "probe_toolbar_render" }   # observed during exploration; both numbers visible in toolbar render
    ordering:
      sort_keys: []   # toolbar is a counter; no ordering applies
      user_changeable: false
      status: n/a
      evidence: "n/a — counter, not a list"
    paging:
      default_size: null
      server_side: false
      status: n/a
      evidence: "n/a — counter, not a list"
    rules_summary: >
      The toolbar's 'X of Y recorded' counter aggregates committed care
      tasks for the current community + shift + date. The X count
      advances both via SignalR push after this toolbar's own POST and
      after another user's POST in the same shift; the Y count is the
      planned-task total for that shift.
    code_refs: []   # populate when source arrives

  authorization_filters:
    - rule: "User must have permission to record care for this shift (rule unknown — fill when source arrives)."
      code_ref: "unknown"
      status: unknown

  computed_fields:
    - name: queue_count
      derivation: "Client-side: count of per-row actions (Completed / Not Completed) that have local state but haven't been committed. Reset to 0 on successful POST."
      code_ref: "unknown — likely a JS handler attached to per-row buttons"
      status: observed   # observed during browser exercise

  soft_delete: null
  temporal_scoping: "Bound to the {date} segment in the URL (the shift's date)."

  user_visible_side_effects:
    - kind: audit_entry
      description: "Each task recorded likely writes an audit entry. Whether visible elsewhere is unknown."
      code_ref: "unknown"
      status: unknown
    - kind: signalr_push
      description: "POST returns 200 → server emits SignalR push on `stafftaskshub` → summary card counter and any in-page progress indicators advance within ~1–3 s."
      code_ref: "unknown — fill when source arrives"
      status: observed   # the cause→effect was observed end-to-end during exploration; hub method/frame schema unverified is captured separately under endpoints[stafftaskshub_push]

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
    endpoint_id: stafftaskshub_push   # see endpoints[]; gate requires this anchor for tenant-scoped slices
    artifact_ref: care-tracking-shift-summary-card  # the source slice that owns the hub connection

on_close: null

# ──────────────── Endpoints ────────────────
endpoints:
  - id: record_care_post
    method: POST
    url: "/Care/Tracking/{communityId}/Record/{date}"
    purpose: "Commit queued task records for the shift on the given date."
    response_kind: json
    mutates_state: true   # writes care records; the central mutating endpoint of this slice
    verification:
      method:           observed       # POST observed in network during commit
      route:            observed       # URL captured
      payload_schema:   unknown        # request body schema not source-confirmed
      response_shape:   observed_partial   # 200 with brief JSON observed; full schema unknown
      error_shape:      unknown        # no failure path exercised
      anti_forgery:     unknown        # presence of __RequestVerificationToken not confirmed
      authorization:    unknown        # permission rule for the action not source-confirmed

  - id: record_prn_navigation
    method: GET
    url: "/Care/Tracking/{communityId}/RecordPrn/{date}/{shiftId}"
    purpose: "Navigate to the PRN-care recording editor (separate flow)."
    response_kind: html_full
    mutates_state: false   # navigation to an editor view; the editor's POST is documented in the editor slice
    verification:
      method:           observed       # link href captured
      route:            observed_partial   # pattern guessed; navigation not exercised
      payload_schema:   n/a
      response_shape:   unknown
      error_shape:      unknown
      anti_forgery:     n/a
      authorization:    unknown

  - id: print_button
    method: "unknown"
    url: "unknown — Print button target"
    purpose: "Print or export the current shift's task list."
    response_kind: "unknown"
    mutates_state: false   # presumed read; unknown until verified — flag in unknowns_to_fill if it turns out to mutate (e.g. logs a print event)
    verification:
      method:           unknown
      route:            unknown
      payload_schema:   unknown
      response_shape:   unknown
      error_shape:      unknown
      anti_forgery:     unknown
      authorization:    unknown

  - id: stafftaskshub_push
    method: "n/a — SignalR hub method (not HTTP)"
    url: "stafftaskshub.<MethodName> — exact method name unknown"
    purpose: "Server-pushed counter update after Record Care commits. Frame is consumed by both this toolbar and the summary card."
    response_kind: "n/a — push frame, not request/response"
    mutates_state: false   # consumes pushed state; doesn't write
    verification:
      method:           observed_partial   # SignalR frames observed; specific method name not confirmed
      route:            observed_partial   # hub URL observed; method name unknown
      payload_schema:   unknown            # frame body shape not confirmed
      response_shape:   n/a
      error_shape:      unknown            # disconnect handling unverified
      anti_forgery:     n/a                # SignalR connection-token bound, not anti-forgery
      authorization:    unknown            # whether tenant-filtered server-side at fan-out time is unverified

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
      - endpoint_id: record_care_post
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

      - endpoint_id: record_prn_navigation
        scenarios:
          - kind: route_tenant_mismatch
            baseline_context: "User authenticated for community A; URL has communityId=A."
            tampered_input: "Manually navigate to /Care/Tracking/{B}/RecordPrn/... where the user lacks B."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown
          - kind: body_tenant_mismatch
            baseline_context: "GET navigation."
            tampered_input: "n/a — GET has no body."
            expected_status: "n/a"
            expected_shape: n/a
            observed_result: "n/a — read-only navigation"
            source_refs: []
            status: n/a
          - kind: foreign_key_ownership
            baseline_context: "GET navigation."
            tampered_input: "n/a — GET takes no foreign-key references in body."
            expected_status: "n/a"
            expected_shape: n/a
            observed_result: "n/a — foreign-key check applies on the editor's POST flow"
            source_refs: []
            status: n/a
          - kind: revoked_grant
            baseline_context: "User had grant for community A; admin revokes."
            tampered_input: "User clicks Record PRN Care."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown
          - kind: read_vs_write
            baseline_context: "User has read grant but not write. Read endpoint."
            tampered_input: "User reaches the navigation; the linked editor's writes would gate separately."
            expected_status: "allow at this read endpoint"
            expected_shape: html_full
            observed_result: "untested — must verify read access doesn't leak data"
            source_refs: ["unknown"]
            status: unknown

      - endpoint_id: print_button
        scenarios:
          - kind: route_tenant_mismatch
            baseline_context: "Print scoped to current community + shift + date."
            tampered_input: "Print URL/method unknown — cannot exercise yet. Once known, exercise URL-tampered tenant id."
            expected_status: "deny"
            expected_shape: "unknown"
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown
          - kind: body_tenant_mismatch
            baseline_context: "Print URL/method unknown."
            tampered_input: "n/a — print mechanism unknown"
            expected_status: "unknown"
            expected_shape: "unknown"
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown
          - kind: foreign_key_ownership
            baseline_context: "Print URL/method unknown."
            tampered_input: "n/a"
            expected_status: "unknown"
            expected_shape: "unknown"
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown
          - kind: revoked_grant
            baseline_context: "Print URL/method unknown."
            tampered_input: "n/a"
            expected_status: "unknown"
            expected_shape: "unknown"
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown
          - kind: read_vs_write
            baseline_context: "Print is read-side; export must respect the same authorization as the underlying read."
            tampered_input: "n/a — print mechanism unknown"
            expected_status: "unknown"
            expected_shape: "unknown"
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

      - endpoint_id: stafftaskshub_push
        scenarios:
          - kind: route_tenant_mismatch
            baseline_context: "Subscriber connected via SignalR for community A."
            tampered_input: "n/a — SignalR has no per-frame URL/route to tamper. Equivalent test: connect a second tab as user with grant for community B and confirm that A's commits do NOT push frames into B's connection."
            expected_status: "no cross-tenant frame delivery"
            expected_shape: n/a
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: body_tenant_mismatch
            baseline_context: "Frames are server-pushed; clients cannot tamper the body. Risk is server-side fan-out leaking frames to wrong group."
            tampered_input: "n/a — server-controlled. Equivalent test: subscribe with two simultaneous browser sessions for different communities; verify hub group membership is by tenant scope, not user-only."
            expected_status: "frames scoped to tenant group"
            expected_shape: n/a
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: foreign_key_ownership
            baseline_context: "Each pushed frame references resident_id and task_id."
            tampered_input: "n/a from the client. Equivalent test: confirm that frame content does NOT include cross-tenant resident_ids when fan-out routes by community group."
            expected_status: "no foreign-tenant ids leaked into frames"
            expected_shape: n/a
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: revoked_grant
            baseline_context: "Subscriber connected; admin revokes grant for community A."
            tampered_input: "Without re-subscribing, observe whether the existing SignalR connection continues to receive frames after the grant is revoked."
            expected_status: "deny / disconnect after revocation"
            expected_shape: n/a
            observed_result: "untested — known SignalR risk: long-lived connections can outlive permission changes"
            source_refs: ["unknown"]
            status: unknown

          - kind: read_vs_write
            baseline_context: "Push is read-only from the client perspective."
            tampered_input: "Confirm that hub method invocations from the client (if any are exposed) cannot be used to inject frames or trigger writes outside the user's tenant scope."
            expected_status: "deny on any tampered hub-method invocation"
            expected_shape: n/a
            observed_result: "untested"
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
    evidence: { test_id: "probe_context_switch_discards_queue" }
  push_disconnect:
    status:   unknown
    behavior: "If SignalR drops during the 1–3 s settle window, the counter advancement may be missed entirely. The POST itself succeeds; only the live UI feedback is lost. Refresh recovers, but stale counter persists between."
    evidence: "untested — must exercise by killing the WebSocket mid-flight"
  idempotency_strategy:
    status:   unknown
    behavior: "Possibilities: natural_idempotent (server dedupes by resident_id+task_id+date), client_dedupe_token (no client evidence observed), or none. Critical to confirm before rewrite."
    evidence: "untested — must exercise via deliberate double-submit"
  queue_retention:
    status:   unknown
    behavior: "On success: queue cleared (observed during exploration). On failure: behavior unknown — failure path is the gating concern. On context switch: queue cleared without prompt (observed)."
    evidence: "Success path observed; failure path untested. Required cell for mutating slices — must be exercised by forcing a 5xx and observing whether the queue clears or persists for retry."
  concurrency_conflict:
    status:   unknown
    behavior: "Two staff members open the same shift on different devices and each commit a partially-overlapping queue. Last-write-wins assumed (legacy MVC default); whether the second writer sees a 409 or silently overwrites the first writer's records is unverified. The behavior either way is contractual — the rewrite must reproduce or improve it."
    evidence: "untested — must exercise by opening the same shift in two browser sessions, queue records on both, commit one, then commit the other; confirm whether the second commit displaces or merges."
  audit_emission:
    status:   unknown
    behavior: "Each committed task record presumably writes a row visible in the resident's Activity / Care history pane (with actor, timestamp, before/after care state). Whether the audit row is created in the same transaction as the care write, and whether the user sees the audit entry without a refresh, are both unverified."
    evidence: "untested — must exercise by recording a task and inspecting the Activity panel for a corresponding entry tied to the actor + the moment of commit."

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
