---
schema_version: 7

# ──────────────── Identity ────────────────
id: dashboard-community-selector
title: "Community / facility selector"
view: "unknown — fill when source arrives"
control_type: context_selector

# ──────────────── Contract status ────────────────
contract_status: incomplete
contract_status_reason: >
  Tenant tamper_matrix scenarios untested (selecting a community the
  user does not have a grant for via tampered POST body; behavior on
  zero grants; behavior when grant is revoked mid-session). Selection
  endpoint URL/method/payload schema all unknown — fill when source
  arrives. failure_matrix cells largely unverified.
contract_status_exceptions: []

# Selector has no related_controls or scoped_by — it IS the scope provider.
# No cross-slice refs to leave pending.
cross_slice_refs_pending: []

# Appears on every authenticated page in the app — global header element.
routes:
  - "*"

binding:
  view_model: "unknown"
  property: "current_community_id"

# ──────────────── Population ────────────────
data_source:
  kind: api
  reference: "Server-rendered into every page; same options across the app for a given user."
  populated_by: "unknown — fill when source arrives. Likely a layout / shared partial."
  fields:
    value: "Id (numeric)"
    text: "Name"
  default_value: "user's last selection (persisted across sessions; mechanism unknown — session, cookie, or DB-side)"
  pre_filled_from_server: true

# ──────────────── Server-side business logic ────────────────
business_logic:
  selection:
    predicates:
      - rule: "Community.Id IN (user's grant list)"
        status: observed
        evidence: { test_id: "probe_grant_filter_visible" }   # observed: grant-absent communities don't appear in the list
        rationale: "tenant-scope filter is the entire selection rule for this slice"
      - rule: "Community.IsActive = true (presumed; soft-deleted communities likely excluded)"
        status: unknown
        evidence: "untested — fill when source arrives"
        rationale: "soft-delete coverage on the read side"
    projection:
      fields: ["Id (numeric)", "Name (string)"]
      status: observed
      evidence: { test_id: "probe_grant_filter_visible" }
    ordering:
      sort_keys:
        - { field: "Name", direction: asc }
      user_changeable: false
      status: observed
      evidence: { test_id: "probe_alphabetical_order" }
    paging:
      default_size: null
      server_side: false
      status: n/a
      evidence: "n/a — option list is small and rendered all at once; no paging affordance"
    rules_summary: >
      Returns the communities the current user has been granted access to,
      ordered alphabetically by Name. Permission-driven — different users
      see different option lists. The exact authorization rule (single
      role? per-community grants? hierarchy?) is unknown.
    code_refs: []

  authorization_filters:
    - rule: "User must be authenticated and have at least one community grant."
      code_ref: "unknown"
      status: unknown

  computed_fields: []

  soft_delete: "unknown — communities deactivated or marked inactive presumably hidden, but not verified"
  temporal_scoping: null

  user_visible_side_effects:
    - kind: "scope_change"
      description: "Selection change re-renders every scoped slice across the app via full-page reload."
      code_ref: "unknown"
      status: observed

# ──────────────── Configuration ────────────────
configuration:
  required_indicator_convention: null
  presence_condition: "User is authenticated AND has at least one community grant."

  multi_select: false
  searchable: true
  grouped: false

  placeholder: null
  read_only: false
  disabled_when: null

  empty_state: "If a user has zero community grants — behavior not observed; ask owner."

  behavior_propagation:
    on_change: "full_page_reload"
    persists_across_navigation: true
    persistence_layer: "unknown — likely session (legacy MVC default) but could be cookie or DB-backed user preference. Fill when source arrives."

# ──────────────── Validation ────────────────
validation: []

# ──────────────── Reactivity ────────────────
reactivity:
  - event: click
    targets: [self]
    action: open_dropdown
    endpoint: null
    settle_ms: 0
    immediate_response: "Dropdown popover opens directly below the selector. Search input is auto-focused. Options list shows all communities the user has access to. The currently-selected community is visually highlighted."
    final_response: "Same as immediate."

  - event: type
    targets: [self]
    action: filter_options
    endpoint: null
    settle_ms: 0
    immediate_response: "Options list filters client-side to those matching the typed text (substring match against Name). All matching options remain visible; non-matching hidden. No network call."

  - event: select
    targets: ["*"]                 # every scoped slice on every page in the app
    action: scope_change
    endpoint:
      method: "unknown"
      url: "unknown — fill when source arrives. Either a POST to a session-set endpoint, or community id baked into next page URL via redirect."
      request_payload: null
      response_handling: "Full-page reload. URL may change (community id in path) or remain (community in session)."
    settle_ms: 0
    immediate_response: "Dropdown closes; brief loading state."
    final_response: "All page content re-renders scoped to the newly-selected community."

  - event: dismiss
    targets: [self]
    action: dismiss
    endpoint: null
    immediate_response: "Pressing Escape with the dropdown open closes the dropdown; no selection change."

# ──────────────── Cross-slice ────────────────
related_controls: []   # this slice has no parent or sibling; it is itself the scope provider for nearly every other slice in the app

scoped_by: []          # the selector IS the scope provider; it has no parent context

signal_sources: []     # selection change triggers a full-page reload, not SignalR

on_close:
  refresh_parent: false
  refresh_mechanism: full_reload
  refresh_target: "*"
  unsaved_changes: "Switching community discards any unsaved local state on the current page (queued task records, draft form fields, etc.) without confirmation. Verify; this is a likely UX wart worth flagging for the rewrite."

# ──────────────── Endpoints ────────────────
endpoints:
  - id: community_switch_post
    method: "unknown"
    url: "unknown — fill when source arrives"
    purpose: "Set the current community in the user's session/state and trigger page reload."
    response_kind: "unknown"
    mutates_state: true   # changes server-side tenant context; the slice IS the scope provider
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
  presence_condition: "User is authenticated."
  action_authorization:
    - action: select_community
      requires: "Community is in the user's grant list."
      on_denied:
        response_kind: "n/a"
        user_sees: "Disallowed communities don't appear in the option list — denial is presence-time, not action-time."
        legacy_quirk: null
        rewrite_intent: preserve

  re_auth_required: false

  # Tenant boundary — REQUIRED because this slice is the scope provider.
  tenant_boundary:
    context_sources:
      - "session: CurrentCommunityId (assumed; legacy MVC default — verify when source arrives)"
      - "url_path: many app routes carry community id (e.g. /Care/Tracking/{communityId}) — server presumably validates these match the session value"
    tamper_matrix:
      - endpoint_id: community_switch_post
        scenarios:
          - kind: body_tenant_mismatch
            baseline_context: "Authenticated user with grants for communities A and B."
            tampered_input: "Submit selection POST with communityId field set to community C (no grant)."
            expected_status: "deny"
            expected_shape: "unknown — fill when source arrives"
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: route_tenant_mismatch
            baseline_context: "Selector currently set to community A in session."
            tampered_input: "User manually navigates to /Care/Tracking/{B} where B is granted but session reads A."
            expected_status: "allow with implicit context update OR deny — behavior unknown"
            expected_shape: "unknown"
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: foreign_key_ownership
            baseline_context: "Selector POST accepts a community id."
            tampered_input: "Send a community id that exists in the DB but is owned by another tenant entirely (no relationship to the calling user's grants)."
            expected_status: "deny"
            expected_shape: "unknown"
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: revoked_grant
            baseline_context: "User had grant for community A; admin revokes; selector shows A as currently-selected."
            tampered_input: "User clicks selector to refresh state, or navigates to a scoped page."
            expected_status: "deny"
            expected_shape: html_full
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

          - kind: read_vs_write
            baseline_context: "User can read the selector (option list filtered to grants)."
            tampered_input: "Confirm that the option-list filter is enforced server-side on the selection POST as well, not only on the read render — i.e. submitting an id absent from the rendered list still denies."
            expected_status: "deny on write even if the read-side hides the option"
            expected_shape: "unknown"
            observed_result: "untested"
            source_refs: ["unknown"]
            status: unknown

# ──────────────── Failure matrix ────────────────
# Selection POST is mutating in the sense that it changes server-side
# tenant-context state.
failure_matrix:
  http_4xx:
    status:   unknown
    behavior: "If selection POST returns 4xx, page may stay or partially navigate; behavior unverified."
    evidence: "untested"
  http_5xx:
    status:   unknown
    behavior: "Likely the dropdown closes but no reload happens; user may not realize selection didn't take."
    evidence: "untested"
  network_timeout:
    status:   unknown
    behavior: "Likely the dropdown closes but no reload happens; user may not realize selection didn't take. Worth flagging for rewrite."
    evidence: "untested"
  double_click_or_resubmit:
    status:   unknown
    behavior: "User clicks two options quickly — last-write-wins assumed; selection POST may race with reload."
    evidence: "untested"
  retry_after_failure:
    status:   observed
    behavior: "User can re-open the dropdown and try again; no explicit retry affordance, but the dropdown is re-openable after dismissal."
    evidence: { test_id: "probe_dropdown_reopen_retries" }
  partial_success:
    status:   n/a
    behavior: "n/a — single-record state change."
    evidence: "n/a"
  refresh_mid_flight:
    status:   unknown
    behavior: "Browser refresh during selection POST — outcome depends on whether the server already wrote the new community to session."
    evidence: "untested"
  context_switch_mid_edit:
    status:   observed
    behavior: "Switching community while a per-task editor has queued local state silently discards the queue (no confirmation prompt). rewrite_intent: improve."
    evidence: { test_id: "probe_context_switch_discards_queue" }
  push_disconnect:
    status:   n/a
    behavior: "n/a — no SignalR involvement on this slice."
    evidence: "n/a"
  idempotency_strategy:
    status:   unknown
    behavior: "Likely natural_idempotent — selecting the same community twice should be a no-op (server-side state is already that community), but the endpoint's actual write semantics (does it always run a SET, or does it short-circuit?) are not verified."
    evidence: "untested — fill when source arrives; verify by inspecting controller action and any session-write side effects."
  queue_retention:
    status:   n/a
    behavior: "n/a — no client-side queue on this slice. Downstream queues are discarded on switch (see context_switch_mid_edit)."
    evidence: "n/a"
  concurrency_conflict:
    status:   n/a
    behavior: "n/a — selection POST is single-actor scope context, not a record edit. Two devices may set different communities for the same user, but each device's later requests resolve via that device's own session — no shared-record conflict."
    evidence: "n/a — single-actor session-scoped context."
  audit_emission:
    status:   unknown
    behavior: "Whether community-switch emits a user-visible audit row (in any Activity / log feed the user sees) is unknown. The cause→effect — switching scope → audit row appears somewhere — is the contract; whether the legacy app implements it is unverified."
    evidence: "untested — fill when source arrives; verify by inspecting the Activity panel or any user-facing audit feed for switch entries."

# ──────────────── Mode B helpers ────────────────
url_conventions_observed:
  - "Community id appears as the first numeric segment in many URLs: /Care/Tracking/{communityId}, /Residents/{communityId}?tab=…"
  - "Resident-routed URLs use resident id directly: /Residents/Profiles/{residentId} — community is implicit via session"

unknowns_to_fill_when_source_arrives:
  - "Layout / shared partial that renders this selector"
  - "Endpoint that handles community switching (URL, method, payload)"
  - "Persistence layer (session vs cookie vs user preference DB)"
  - "Authorization rule for community access (role-based? per-grant? hierarchical?)"
  - "Behavior when user has zero community grants"
  - "Behavior when the current session's community is revoked mid-session"
  - "Whether any unsaved-changes guard exists before discarding pending work on community switch"
  - "Whether URL-path community id is server-validated against session — and if so, the exact denial path"

# All event/action values used in this artifact are sanctioned by the
# template (open_dropdown, filter_options, scope_change, type, select,
# dismiss are all in the canonical enum lists). Block left empty.
extensions: []
---

# Community / facility selector

## Behavior summary

A global combobox in the page header (top-right) that scopes every other slice in the app to the chosen community. Click to open a dropdown with a search input and the user's accessible communities. Search filters client-side; selecting an option triggers a full-page reload where every scoped slice re-renders against the new community. Persists across navigation within the session. The currently-selected community is visible in the closed-state of the combobox.

This slice is the **scope provider** for nearly every other slice in the app — `scoped_by: dashboard-community-selector` is a near-universal frontmatter line.

## Code references

- View / shared partial: unknown — fill when source arrives. Likely a `_Layout.cshtml` or shared header partial.
- Selection endpoint: unknown.
- Authorization rule for the option list: unknown.

## Edge cases

- **Zero community grants** — behavior not observed. A user with no grants likely sees a chooser screen, an error page, or an empty selector — verify with owner.
- **Revoked community mid-session** — what happens if an admin revokes the current user's access to the active community? Likely the next request 403s; the legacy generic-error pattern probably surfaces. Untested.
- **Unsaved local changes on switch** — switching community while a per-task editor has queued local state likely discards the queue silently. **No confirmation observed.** Worth flagging in `rewrite_intent` as `improve` — the rewrite should add an unsaved-changes guard.
- **Concurrent sessions in different communities** — same user logged in twice (two browser tabs) selecting different communities in each tab. Behavior depends on persistence layer; not tested.
- **Long community lists** — observed list has a small number of entries. A user with 50+ communities — does the dropdown paginate, virtualize, or show all? Untested.
- **URL tampering** — manually editing a community id in the URL to one the user lacks access to. Server should 403; not exercised.

## Verification claims

1. **Initial render** — combobox is present in the page header; closed state shows the currently-selected community's name.
2. **Open behavior** — clicking the combobox opens a dropdown popover with a search input and the option list. Currently-selected option is visually highlighted.
3. **Search filtering** — typing in the search input filters the options client-side by Name substring. No network call fires.
4. **Multi-select** — the combobox is single-select only (no checkboxes; selecting an option immediately closes the dropdown).
5. **Selection effect** — selecting a different option triggers a full-page reload; the page re-renders scoped to the new community.
6. **Persistence** — after switching, navigating to another page shows data scoped to the new community; the selector's closed-state reflects the new selection.
7. **Permission gating** — only communities the current user has been granted access to appear in the option list.
8. **Escape dismissal** — pressing Escape with the dropdown open closes the dropdown without selection change.
9. **No SignalR effect** — switching community does not emit any SignalR event on this slice; the propagation is via full-page reload.
10. **Tamper boundary** — manually replacing the community id in a scoped URL (e.g. `/Care/Tracking/{otherCommunity}`) with a community the user does not have access to should yield a 403 / Unauthorized response, not silent data leakage.

## Verification log

- 2026-05-02 — initial Mode-B artifact drafted from browser observation. Claims 1, 2, 3, 4, 5, 6 verified during exploration. Claims 7, 8, 9 partially verified or inferred. Claim 10 (tamper boundary) added per Codex review feedback; explicitly unverified — must be tested before this slice is contract-complete.
- 2026-05-02 — Codex review: redacted observed tenant ids and names; added `tenant_boundary`, `failure_matrix`, per-aspect endpoint verification; documented schema extensions for `open_dropdown` / `filter_options` / `scope_change`.

## Linked artifacts in this feature / app

This slice is referenced by **every** scoped artifact in the system:

- [`care-tracking-shift-summary-card.md`](care-tracking-shift-summary-card.md) — declares `scoped_by: dashboard-community-selector`.
- [`care-tracking-record-toolbar.md`](care-tracking-record-toolbar.md) — declares `scoped_by: dashboard-community-selector`.

Future scoped artifacts will declare the same.
