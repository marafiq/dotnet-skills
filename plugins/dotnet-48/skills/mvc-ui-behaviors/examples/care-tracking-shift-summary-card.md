---
# ──────────────── Identity ────────────────
id: care-tracking-shift-summary-card
title: "Care Tracking — Shift Summary Card"
view: "unknown — fill when source arrives"
control_type: card

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
    # Per-card payload observed in the DOM
    shift_name: "string (e.g. 'NOC', 'ShiftThree', 'Shift Two', 'Shift One')"
    shift_label: "PREVIOUS | CURRENT | NEXT (relative to the current time and selected date)"
    shift_time_range: "string (e.g. '11:00 PM - 6:59 AM')"
    items_unassigned: "int (e.g. 4)"
    total_estimated_minutes: "int (e.g. 30)"
    tasks_recorded: "int (advances via SignalR push, see business_logic.user_visible_side_effects + reactivity)"
    tasks_total: "int (e.g. 4)"
    time_remaining_minutes: "int (e.g. 15)"
    tasks_remaining: "int (e.g. 3)"
    category_badges: "list<string> (observed: 'CARE')"
  default_value: null
  pre_filled_from_server: true

# ──────────────── Server-side business logic ────────────────
# Mode B: largely unknown — fill when source arrives. Observed evidence in the body.
business_logic:
  selection:
    rules:
      - "Returns the shift cards for the community in `/Care/Tracking/{communityId}` for the selected date."
      - "Multiple shifts visible at once (observed: PREVIOUS, CURRENT, NEXT — at least three on this view)."
      - "Selection rule for which shifts appear (current-day? next 24h? configured per community?) — unknown — fill when source arrives."
    code_refs: ["unknown — fill when source arrives"]

  authorization_filters:
    - rule: "Scoped to the community in the URL. The community selector at top-right also reflects scoping. Authorization rule for which communities the current user can see — unknown — fill when source arrives."
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
    default: "Likely shift start time ascending; observed left-to-right on the page as 'Shift Two (PREVIOUS), ShiftThree (CURRENT), Shift One (NEXT)' — confirm rule when source arrives."
    user_changeable: false

  paging:
    default_size: null    # all shifts shown inline; no paging observed
    server_side: false

  soft_delete: "unknown — fill when source arrives"
  temporal_scoping: "Page is 'Care for Today' by default; a 'Change' link suggests date selection. Temporal selection mechanism — unknown — fill when source arrives."

  user_visible_side_effects:
    - kind: "audit_entry"
      description: "Each task recorded in the editor (downstream slice) likely writes an audit entry. Whether the entry is surfaced anywhere on this card is unknown."
      code_ref: "unknown"

# ──────────────── Configuration ────────────────
configuration:
  required_indicator_convention: null   # this is a display card, no required-field convention applies
  presence_condition: null
  states:
    - "PREVIOUS"
    - "CURRENT"
    - "NEXT"

  inline_indicators:
    - kind: shift_label
      color: "blue (PREVIOUS), green (CURRENT), yellow (NEXT)"
      semantics: "Position of this shift relative to current time on the selected date."
    - kind: category
      color: "neutral pill"
      semantics: "The care-program category this shift belongs to (observed: CARE)."

  empty_state: "unknown — what does the user see if no shifts exist for the selected day in the selected community?"

# ──────────────── Validation ────────────────
validation: []   # display card; no input

# ──────────────── Reactivity ────────────────
reactivity:
  - event: click
    targets: ["care-tracking-record-editor (downstream slice — per-task editor at /Care/Tracking/{communityId}/Record/{date}/List/{shiftId})"]
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
    targets: ["self (this card's tasks_recorded counter and the X-of-Y progress bar)"]
    action: replace_partial
    endpoint:
      method: null
      url: "SignalR `stafftaskshub` (connection observed; method name unknown — fill when source arrives)"
      request_payload: "Push frame from server when any task is recorded for this shift+date"
      response_handling: "Counter text advances by 1; tasks_remaining decrements by 1; progress bar fills proportionally."
    settle_ms: 2500
    immediate_response: "When the editor's POST `/Care/Tracking/{communityId}/Record/{date}` returns 200, the editor shows a success toast — but this card's counter does NOT update synchronously."
    final_response: "Within ~1–3 seconds of the AJAX response, the counter on this card advances via SignalR push. Verified: counter went from '0 of 4' to '1 of 4' after one record action."

# ──────────────── Cross-slice ────────────────
related_controls:
  - id: care-tracking-record-editor
    relation: child   # navigated-to
  - id: dashboard-community-selector
    relation: scope_provider

scoped_by:
  - dashboard-community-selector

signal_sources:
  - kind: signalr
    detail: "Connects to `stafftaskshub` on page load (observed: SignalR negotiate request to `alis-sigr-prd-usc.service.signalr.net`). Receives push frames when any task is recorded for any shift in this community on the selected date."

on_close: null   # this is a primary view, not a modal/drawer

# ──────────────── Endpoints ────────────────
endpoints:
  - method: GET
    url: "/Care/Tracking/{communityId}"
    purpose: "Return the shift summary cards for the community on the selected date."
    requires_anti_forgery: false
    response_kind: html_full
    unverified: true     # observed but not source-confirmed

  - method: GET
    url: "/Care/Tracking/{communityId}/Record/{date}/List/{shiftId}"
    purpose: "Navigate into the per-task editor for a specific shift on a specific date."
    requires_anti_forgery: false
    response_kind: html_full
    unverified: true

  - method: "SignalR (negotiate + WebSocket)"
    url: "stafftaskshub"
    purpose: "Receive push notifications when any task is recorded; updates the tasks_recorded counter in real time."
    requires_anti_forgery: false
    response_kind: json    # frame payloads
    unverified: true

# ──────────────── Authorization ────────────────
authorization:
  presence_condition: "User must be authenticated and have access to the community in the URL. Observed: page renders for the current user; permission rule — unknown — fill when source arrives."
  action_authorization: []
  re_auth_required: false

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
  - "endpoints[*].requires_anti_forgery — currently assumed false; confirm"
  - "stafftaskshub method names + payload schemas"
---

# Care Tracking — Shift Summary Card

## Behavior summary

The shift summary card surfaces, for each shift on the selected date in the current community, an at-a-glance progress view: shift name, position label (PREVIOUS / CURRENT / NEXT), time range, unassigned-task count, total estimated time, a progress bar with *"X of Y Tasks Recorded"*, time remaining, tasks remaining, and a primary action (*Record Care*) that navigates to the per-task editor. The card's progress counter is **not** updated by the user's local actions on this view — it advances via a SignalR push from `stafftaskshub` after any task is recorded in the editor (typically within 1–3 s of the editor's POST returning 200).

## Code references

(For human reviewers cross-checking the artifact. The downstream LLM does not need these.)

- View: unknown — fill when source arrives.
- Controller action: unknown — fill when source arrives. URL pattern is `GET /Care/Tracking/{communityId}`.
- SignalR Hub: `stafftaskshub` (negotiate URL observed; method names unknown).

## Edge cases

- **Empty state** — what does the user see if no shifts exist for the selected day in the selected community? Not observed.
- **Stale community** — switching the community selector reloads the page (full reload propagation observed); no stale-data risk on this view itself.
- **Stale date** — *"Change"* link on the page suggests changing the date; behavior on date change unknown.
- **Counter not advancing** — if SignalR connection drops, the counter stops updating; the user has to refresh to see correct progress. Observed: connection re-negotiates after page navigation; refresh-recovery behavior on disconnect is not explicit.
- **Concurrent recording by another user** — the counter would advance from another user's record action via the same SignalR push. Confirmed by the "any task recorded for any shift in this community" framing of the hub. The card displays totals, not the recording user.
- **Permission denied to view a community** — would presumably return a 403 / unauthorized full-page response (consistent with the legacy 403 pattern observed elsewhere). Not explicitly tested.

## Verification claims

Each claim is testable against the running app. Step 3 of the workflow exercises these.

1. **Initial render** — navigating to `/Care/Tracking/{communityId}` shows N shift cards (one per shift active for the community on the selected date). Each card has the labels above. *Observed: 3 cards on `/Care/Tracking/1` (Shift Two, ShiftThree, Shift One).*
2. **Counter format** — the progress text reads exactly *"X of Y Tasks Recorded"* (current/total) with a horizontal progress bar showing the same proportion.
3. **Shift labels** — at most one card shows `CURRENT` at a time on a given visit; cards before/after carry `PREVIOUS` / `NEXT` accordingly.
4. **Record Care navigation** — clicking the **Record Care** button on a card navigates to `/Care/Tracking/{communityId}/Record/{currentDate}/List/{shiftId}`, displays a brief loading screen, then renders the per-task editor.
5. **SignalR-driven counter advance** — after a task is recorded in the editor and its POST returns 200, this card's counter advances by 1 within ~1–3 s without user action on this view. *Observed: counter went from "0 of 4" to "1 of 4" after one record action.*
6. **Settle window is non-zero** — the counter does not advance synchronously with the editor's POST response; there is an observable 1–3 s settle window between the response and the counter update on this view.
7. **Tasks remaining** = `tasks_total − tasks_recorded`, updated in lock-step with the counter.
8. **Community scoping** — switching the community selector at top-right reloads the page (full-page reload propagation), and the cards reflect the new community's shifts.

## Verification log

- 2026-05-02 — initial Mode-B artifact drafted from browser observation; many fields marked `unknown — fill when source arrives`. Claims 1, 2, 5, 6 verified during exploration.
