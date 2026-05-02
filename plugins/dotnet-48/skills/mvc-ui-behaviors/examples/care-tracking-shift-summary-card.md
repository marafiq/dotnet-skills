---
# ──────────────── Identity ────────────────
id: care-tracking-shift-summary-card
title: "Care Tracking — Shift Summary Card"
view: "unknown — fill when source arrives"
control_type: card

# ──────────────── Contract status ────────────────
contract_status: incomplete
contract_status_reason: >
  Read-only slice scoped by community. Tenant tamper_matrix
  scenarios untested (route-path tamper to other community,
  revoked-grant). Endpoint verification still has error_shape
  and authorization at unknown for the GET. Push-disconnect
  failure-matrix cell unobserved. Source-fill or testing
  required before contract-complete.
contract_status_exceptions: []

routes:
  - "/Care/Tracking/{communityId}"

binding:
  view_model: "unknown"
  property: "unknown"

# ──────────────── Population ────────────────
data_source:
  kind: api
  reference: "GET /Care/Tracking/{communityId} (server-rendered HTML; data embedded in the view)"
  populated_by: "unknown — fill when source arrives"
  fields:
    shift_name: "string"
    shift_label: "PREVIOUS | CURRENT | NEXT (relative to the current time and selected date)"
    shift_time_range: "string (start–end time of the shift)"
    items_unassigned: "int"
    total_estimated_minutes: "int"
    tasks_recorded: "int (advances via SignalR push)"
    tasks_total: "int"
    time_remaining_minutes: "int"
    tasks_remaining: "int"
    category_badges: "list<string>"
  default_value: null
  pre_filled_from_server: true

# ──────────────── Server-side business logic ────────────────
business_logic:
  selection:
    rules:
      - "Returns the shift cards for the community in `/Care/Tracking/{communityId}` for the selected date."
      - "Multiple shifts visible at once (PREVIOUS / CURRENT / NEXT relative to current time on the selected date)."
      - "Selection rule for which shifts appear (current-day? next 24h? configured per community?) — unknown — fill when source arrives."
    code_refs: ["unknown — fill when source arrives"]

  authorization_filters:
    - rule: "Scoped to the community in the URL. Authorization rule for which communities the current user can see — unknown — fill when source arrives."
      code_ref: "unknown"

  computed_fields:
    - name: tasks_recorded
      derivation: "Count of completed care tasks for the shift on the selected date. Updated via SignalR push from `stafftaskshub` after each record action."
      code_ref: "unknown — fill when source arrives"
    - name: time_remaining_minutes
      derivation: "Estimated remaining time for unrecorded tasks. Likely = total_estimated_minutes − recorded_minutes. Exact rule unknown — fill when source arrives."
      code_ref: "unknown"
    - name: tasks_remaining
      derivation: "tasks_total − tasks_recorded."
      code_ref: "unknown"
    - name: shift_label
      derivation: "Position relative to the current time on the selected date (PREVIOUS / CURRENT / NEXT)."
      code_ref: "unknown"

  ordering:
    default: "Likely shift start time ascending; observed left-to-right as PREVIOUS → CURRENT → NEXT — confirm rule when source arrives."
    user_changeable: false

  paging:
    default_size: null
    server_side: false

  soft_delete: "unknown — fill when source arrives"
  temporal_scoping: "Page is 'Care for Today' by default; a 'Change' link suggests date selection. Mechanism unknown — fill when source arrives."

  user_visible_side_effects:
    - kind: audit_entry
      description: "Each task recorded in the editor (downstream slice) likely writes an audit entry. Whether the entry surfaces anywhere on this card is unknown."
      code_ref: "unknown"

# ──────────────── Configuration ────────────────
configuration:
  required_indicator_convention: null
  presence_condition: null
  states:
    - PREVIOUS
    - CURRENT
    - NEXT

  inline_indicators:
    - kind: shift_label
      color: "blue (PREVIOUS), green (CURRENT), yellow (NEXT)"
      semantics: "Position of this shift relative to current time on the selected date."
    - kind: category
      color: "neutral pill"
      semantics: "The care-program category this shift belongs to."

  empty_state: "unknown — what does the user see if no shifts exist for the selected day in the selected community?"

# ──────────────── Validation ────────────────
validation: []

# ──────────────── Reactivity ────────────────
reactivity:
  - event: click
    targets: ["care-tracking-record-editor"]
    action: navigate
    endpoint:
      method: GET
      url: "/Care/Tracking/{communityId}/Record/{date}/List/{shiftId}"
      request_payload: null
      response_handling: "Full-page navigation; loading screen ('Please wait while we load Care Tracking data') displayed during initial fetch, then replaced by the editor."
    settle_ms: null
    immediate_response: "Navigates to the per-task editor for this shift on the selected date."
    final_response: "Editor view rendered with task list grouped by time, filter bar, sort controls, toolbar with Record Care button."

  - event: server_push
    targets: [self]
    action: replace_partial
    endpoint:
      method: null
      url: "SignalR `stafftaskshub` (connection observed; method name unknown — fill when source arrives)"
      request_payload: "Push frame from server when any task is recorded for this shift+date"
      response_handling: "Counter text advances by 1; tasks_remaining decrements by 1; progress bar fills proportionally."
    settle_ms: 2500
    immediate_response: "When the editor's POST `/Care/Tracking/{communityId}/Record/{date}` returns 200, the editor shows a success toast — but this card's counter does NOT update synchronously."
    final_response: "Within ~1–3 seconds of the AJAX response, the counter on this card advances via SignalR push."

# ──────────────── Cross-slice ────────────────
related_controls:
  - id: care-tracking-record-editor
    relation: child
  - id: dashboard-community-selector
    relation: scope_provider

scoped_by:
  - dashboard-community-selector

signal_sources:
  - kind: signalr
    detail: "Connects to `stafftaskshub` on page load. Receives push frames when any task is recorded for any shift in this community on the selected date."

on_close: null

# ──────────────── Endpoints ────────────────
endpoints:
  - id: shift_list_get
    method: GET
    url: "/Care/Tracking/{communityId}"
    purpose: "Return the shift summary cards for the community on the selected date."
    response_kind: html_full
    verification:
      method:           observed
      route:            observed
      payload_schema:   n/a       # GET returns rendered HTML, not a structured payload
      response_shape:   observed_partial
      error_shape:      unknown
      anti_forgery:     n/a       # GET, no anti-forgery
      authorization:    observed_partial

  - id: editor_navigation
    method: GET
    url: "/Care/Tracking/{communityId}/Record/{date}/List/{shiftId}"
    purpose: "Navigate into the per-task editor for a specific shift on a specific date."
    response_kind: html_full
    verification:
      method:           observed
      route:            observed
      payload_schema:   n/a
      response_shape:   observed_partial
      error_shape:      unknown
      anti_forgery:     n/a
      authorization:    unknown

  - id: signalr_stafftaskshub
    method: "SignalR"
    url: "stafftaskshub"
    purpose: "Receive push notifications when any task is recorded; updates the tasks_recorded counter in real time."
    response_kind: json
    verification:
      method:           observed
      route:            observed
      payload_schema:   unknown   # frame schema not source-confirmed
      response_shape:   unknown
      error_shape:      unknown
      anti_forgery:     n/a
      authorization:    unknown

# ──────────────── Authorization ────────────────
authorization:
  presence_condition: "User must be authenticated and have access to the community in the URL."
  action_authorization: []
  re_auth_required: false

  tenant_boundary:
    context_sources:
      - "url_path: /Care/Tracking/{communityId}"
      - "session: CurrentCommunityId (assumed; legacy MVC default — verify when source arrives)"
    tamper_matrix:
      - endpoint_id: shift_list_get
        scenarios:
          - kind: route_tenant_mismatch
            baseline_context: "Authenticated user with grant for community A."
            tampered_input: "Manually navigate to /Care/Tracking/{B} where the user has no grant for B."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested — likely the legacy 'Unauthorized' HTML page (observed pattern elsewhere)"
            source_refs: ["unknown"]
            status: unknown
          - kind: body_tenant_mismatch
            baseline_context: "GET endpoint."
            tampered_input: "n/a — GET has no body."
            expected_status: "n/a"
            expected_shape: n/a
            observed_result: "n/a — GET takes no body"
            source_refs: []
            status: n/a
          - kind: foreign_key_ownership
            baseline_context: "GET endpoint."
            tampered_input: "n/a — community id is the only tenant reference; covered by route_tenant_mismatch."
            expected_status: "n/a"
            expected_shape: n/a
            observed_result: "n/a — no nested foreign keys for this read"
            source_refs: []
            status: n/a
          - kind: revoked_grant
            baseline_context: "User initially has grant for community A; admin revokes mid-session."
            tampered_input: "User refreshes /Care/Tracking/A or navigates back to it."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown
          - kind: read_vs_write
            baseline_context: "User has read-only grant for community A (can view, not record)."
            tampered_input: "User views the summary card; observation: are tasks_recorded counts visible without write permission?"
            expected_status: "allow (read), deny on attempted Record Care navigation"
            expected_shape: n/a
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

      - endpoint_id: editor_navigation
        scenarios:
          - kind: route_tenant_mismatch
            baseline_context: "Authenticated user with grant for community A."
            tampered_input: "Click Record Care, then manually edit the URL communityId in the editor route to community B."
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
            observed_result: "n/a"
            source_refs: []
            status: n/a
          - kind: foreign_key_ownership
            baseline_context: "GET navigation; URL contains shiftId."
            tampered_input: "Manually edit shiftId in the URL to a shiftId belonging to a different community."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown
          - kind: revoked_grant
            baseline_context: "User had grant; admin revokes."
            tampered_input: "User clicks Record Care from a stale page."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown
          - kind: read_vs_write
            baseline_context: "User has read grant but not write."
            tampered_input: "User reaches the editor (read view); writes are gated separately on the editor's POST."
            expected_status: "allow read; deny write"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

      - endpoint_id: signalr_stafftaskshub
        scenarios:
          - kind: route_tenant_mismatch
            baseline_context: "User authenticated for community A; SignalR connection negotiated for that user."
            tampered_input: "Inspect the negotiate request to see if the channel scopes by community; attempt to subscribe to community B's channel."
            expected_status: "deny"
            expected_shape: n/a
            observed_result: "untested — must inspect SignalR connection scoping"
            source_refs: ["unknown"]
            status: unknown
          - kind: body_tenant_mismatch
            baseline_context: "Push channel; client receives, doesn't send."
            tampered_input: "n/a — push frames are server-emitted."
            expected_status: "n/a"
            expected_shape: n/a
            observed_result: "n/a"
            source_refs: []
            status: n/a
          - kind: foreign_key_ownership
            baseline_context: "Push frames carry task ids."
            tampered_input: "n/a from the client; the concern is whether the server restricts which tasks it pushes per connection."
            expected_status: "deny push of cross-tenant frames"
            expected_shape: n/a
            observed_result: "untested — must verify server-side filter on which frames go to which connections"
            source_refs: ["unknown"]
            status: unknown
          - kind: revoked_grant
            baseline_context: "User had grant; admin revokes."
            tampered_input: "Open SignalR connection persists with stale identity."
            expected_status: "Connection should be closed or further frames suppressed."
            expected_shape: n/a
            observed_result: "untested — verify SignalR Hub OnDisconnected / authorization filter behavior on grant revocation"
            source_refs: ["unknown"]
            status: unknown
          - kind: read_vs_write
            baseline_context: "Push channel is read-only from the client's perspective."
            tampered_input: "n/a"
            expected_status: "n/a"
            expected_shape: n/a
            observed_result: "n/a — push channel is read-only"
            source_refs: []
            status: n/a

# ──────────────── Failure matrix ────────────────
# Read-only slice — most cells are n/a. SignalR-related cells matter because
# the counter depends on it.
failure_matrix:
  http_4xx:
    status:   unknown
    behavior: "If the GET returns 403, the user sees the legacy 'Unauthorized' page. If 404, behavior unverified."
    evidence: "untested"
  http_5xx:
    status:   unknown
    behavior: "Standard ASP.NET error page; legacy YSOD or generic error view."
    evidence: "untested"
  network_timeout:
    status:   n/a
    behavior: "Page fails to render; browser standard error. n/a for behavioral contract."
    evidence: "n/a"
  double_click_or_resubmit:
    status:   n/a
    behavior: "n/a — read-only view."
    evidence: "n/a"
  retry_after_failure:
    status:   n/a
    behavior: "Browser refresh."
    evidence: "n/a"
  partial_success:
    status:   n/a
    behavior: "n/a — read-only."
    evidence: "n/a"
  refresh_mid_flight:
    status:   n/a
    behavior: "n/a — initial GET; refresh restarts the request."
    evidence: "n/a"
  context_switch_mid_edit:
    status:   n/a
    behavior: "n/a — no edit state on this slice."
    evidence: "n/a"
  push_disconnect:
    status:   unknown
    behavior: "If SignalR connection drops, the counter stops advancing. Page refresh re-establishes; the displayed count then reflects current server state. Worth flagging for rewrite — the user has no in-page indicator that the live counter is stale."
    evidence: "untested — must exercise by killing the WebSocket and recording a task in the editor"
  idempotency_strategy:
    status:   n/a
    behavior: "n/a — no mutation."
    evidence: "n/a"
  queue_retention:
    status:   n/a
    behavior: "n/a — no mutation."
    evidence: "n/a"

# ──────────────── Schema extensions ────────────────
# All values used in this artifact are sanctioned. Empty.
extensions: []

# ──────────────── Mode B helpers ────────────────
url_conventions_observed:
  - "/Care/Tracking/{communityId} → list view of shift cards"
  - "/Care/Tracking/{communityId}/Record/{date}/List/{shiftId} → per-task recording editor"
  - "stafftaskshub (SignalR) → push channel for task-recording updates"

unknowns_to_fill_when_source_arrives:
  - "view: which Razor view file renders this card?"
  - "binding.view_model + binding.property"
  - "data_source.populated_by — controller action that returns this view"
  - "business_logic.selection.rules — which shifts are returned and why"
  - "business_logic.authorization_filters — exact rule"
  - "business_logic.computed_fields[*].derivation — exact formulas (especially time_remaining_minutes)"
  - "business_logic.temporal_scoping — date-selection mechanism (Change link)"
  - "endpoints[*].verification — promote unknowns to source_confirmed"
  - "stafftaskshub method names + payload schemas"
  - "tenant_boundary.tamper_test_evidence — exercise URL-path tampering"
---

# Care Tracking — Shift Summary Card

## Behavior summary

The shift summary card surfaces, for each shift on the selected date in the current community, an at-a-glance progress view: shift name, position label (PREVIOUS / CURRENT / NEXT), time range, unassigned-task count, total estimated time, a progress bar with *"X of Y Tasks Recorded"*, time remaining, tasks remaining, category badge, and a primary action (*Record Care*) that navigates to the per-task editor. The progress counter advances via a SignalR push from `stafftaskshub` after any task is recorded in the editor (typically within 1–3 s of the editor's POST returning 200).

## Code references

- View: unknown — fill when source arrives.
- Controller action: unknown — fill when source arrives. URL pattern is `GET /Care/Tracking/{communityId}`.
- SignalR Hub: `stafftaskshub` (negotiate URL observed; method names unknown).

## Edge cases

- **Empty state** — what does the user see if no shifts exist for the selected day in the selected community? Not observed.
- **Stale community** — switching the community selector reloads the page (full reload propagation observed); no stale-data risk on this view itself.
- **Stale date** — *"Change"* link on the page suggests changing the date; behavior on date change unknown.
- **Counter not advancing (SignalR drop)** — if the SignalR connection drops, the counter stops updating and the user has no in-page indicator that it's stale; only a refresh recovers. Flag for rewrite.
- **Concurrent recording by another user** — counters advance from any user's record action via the same push channel.
- **Permission denied** — would presumably return a 403 / Unauthorized full-page response (consistent with the legacy 403 pattern observed elsewhere). Not explicitly tested.
- **URL tampering** — visiting `/Care/Tracking/{otherCommunity}` for a community the user lacks access to should yield 403; untested.

## Verification claims

1. **Initial render** — navigating to `/Care/Tracking/{communityId}` shows N shift cards (one per shift active for the community on the selected date), each with the labels above.
2. **Counter format** — the progress text reads exactly *"X of Y Tasks Recorded"* (current/total) with a horizontal progress bar showing the same proportion.
3. **Shift labels** — at most one card shows `CURRENT` at a time on a given visit; cards before/after carry `PREVIOUS` / `NEXT` accordingly.
4. **Record Care navigation** — clicking the **Record Care** button on a card navigates to `/Care/Tracking/{communityId}/Record/{currentDate}/List/{shiftId}`, displays a brief loading screen, then renders the per-task editor.
5. **SignalR-driven counter advance** — after a task is recorded in the editor and its POST returns 200, this card's counter advances by 1 within ~1–3 s without user action on this view.
6. **Settle window is non-zero** — the counter does not advance synchronously with the editor's POST response; there is an observable 1–3 s settle window.
7. **Tasks remaining** = `tasks_total − tasks_recorded`, updated in lock-step with the counter.
8. **Community scoping** — switching the community selector at top-right reloads the page (full-page reload propagation), and the cards reflect the new community's shifts.
9. **Tamper boundary** — visiting `/Care/Tracking/{otherCommunity}` for a community without a user grant should yield 403; not silent data leakage.
10. **Stale-counter under SignalR disconnect** — if the SignalR connection is dropped (verifiable by killing the WebSocket and recording a task in the editor), this card's counter stays stale; the user has no in-page indication.

## Verification log

- 2026-05-02 — initial Mode-B artifact drafted from browser observation. Claims 1, 2, 5, 6 verified during exploration.
- 2026-05-02 — Codex review: redacted observed tenant data; added `tenant_boundary` block; split endpoint `unverified` into per-aspect `verification`; added `failure_matrix` covering SignalR-disconnect and tamper-boundary scenarios; added claims 9 and 10 as required-but-unverified.
