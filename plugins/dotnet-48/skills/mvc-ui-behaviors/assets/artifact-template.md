---
# ──────────────── Schema version ────────────────
# Increment when the artifact schema changes in a way that breaks downstream
# consumers (linters, the rewrite session, cross-artifact tooling). Current
# version: 6.
#
# Version history (canonical: see references/pattern-candidates.md):
#   v5 — Round 4 — added mutates_state, signal_sources.endpoint_id,
#        cross_slice_refs_pending, concurrency_conflict + audit_emission
#        failure cells, evidence coherence gate.
#   v6 — Round 5 — typed evidence shape, n/a coherence gate (rule 10),
#        structured business_logic.selection.{predicates, projection,
#        ordering, paging} (REPLACES selection.rules), regulated_data_handling
#        block + gate rule 11, schema_version field + gate rule 12.
schema_version: 6

# ──────────────── Identity ────────────────
id: <kebab-case slug, e.g. care-tracking-record-toolbar>
title: <human-readable name>
view: <path/to/View.cshtml | "unknown — fill when source arrives">
view_lines: <e.g. 42-78, optional>
control_type: <dropdown | textbox | textarea | checkbox | radio | grid | form | modal | drawer | wizard | wizard_step | toolbar | accordion | tab_set | side_menu | breadcrumb | datepicker | daterange | autocomplete | masked_input | numeric_input | toggle_buttons | rich_text | file_upload | loading_indicator | toast | confirmation_dialog | alert | paginator | pane | context_selector | card | custom>

# ──────────────── Contract status ────────────────
# `complete` only when EVERY gate rule in SKILL.md "Contract-completeness
# gate" passes structurally. The gate is mechanical — a fresh LLM session
# producing artifacts at scale MUST NOT mark contract_status: complete
# unless every rule passes.
#
# SKILL.md is the SINGLE SOURCE OF TRUTH for the rule list. Keeping a
# parallel summary here causes drift; deliberate pointer-only.
contract_status: <complete | incomplete>
contract_status_reason: <prose: which required cells/scenarios are unknown or untested; what evidence is missing>
contract_status_exceptions:
  # Used when a slice is claimed `complete` but a structurally-blocking
  # status is being explicitly waived. Each exception MUST carry an
  # explicit reason and a risk owner (named human or role).
  #
  # Two waiver shapes are accepted:
  #
  #   state: observed_partial
  #     For aspects that are partially-verified on security-sensitive
  #     fields. Default: BLOCKING. Waiver requires reason + risk_owner.
  #
  #   state: na_waiver
  #     For aspects at status: n/a on gate-critical fields where the
  #     n/a is a deliberate choice rather than a structural truth.
  #     Specifically required when: verification.authorization: n/a,
  #     verification.error_shape: n/a, or verification.anti_forgery: n/a
  #     on a mutating (mutates_state: true) or tenant-scoped endpoint.
  #     Without an na_waiver entry, the gate (rule 10) blocks `complete`.
  #
  # Anti-forgery `n/a` on a GET-shaped write is the canonical case
  # demanding na_waiver — the legacy app may not enforce anti-forgery
  # on link-triggered state changes, but the rewrite must surface that
  # explicitly rather than waive it silently.
  - aspect: <e.g. "endpoints[record_care_post].verification.payload_schema">
    state: <observed_partial | na_waiver>
    reason: <prose: why partial / why n/a is acceptable for this slice/endpoint>
    risk_owner: <name or role>
    follow_up: <prose: what work would close the gap, or "no follow-up — n/a is structurally correct">

# Optional. Used when this slice's frontmatter references a sibling artifact
# id that has not yet been authored. Forces contract_status: incomplete by
# default; presence here ACKNOWLEDGES the gap rather than waiving it.
cross_slice_refs_pending:
  - ref: <unresolved-artifact-id>
    referenced_from: <e.g. "scoped_by[0]" | "related_controls[2].id" | "signal_sources[0].artifact_ref">
    reason: <prose: why this artifact does not yet exist in the corpus>
    expected_to_land: <prose: when / by whom>

# URLs where the user encounters this slice (same in legacy and rewrite — routes are preserved).
# Use route-pattern syntax with named parameters; multiple entries when the slice appears in more than one place.
routes:
  - <e.g. "/Residents/Profiles/{residentId}">
  - <e.g. "/Care/Tracking/{communityId}/.../Pane">         # if also AJAX-loaded as a fragment

# Optional: which view-model property the slice binds to
binding:
  view_model: <class name | "unknown">
  property: <property name | "unknown">

# ──────────────── Population ────────────────
data_source:
  kind: <model_property | viewbag | viewdata | helper | hardcoded | api | dynamic_via_parent | server_push>
  reference: <e.g. "ViewBag.Countries", "Url.Action('Data')", "stafftaskshub.OnTaskRecorded">
  populated_by: <controller action or hub method | "unknown — fill when source arrives">
  fields:                                          # for list-like controls
    value: <field name>
    text: <field name>
  group_by: <field name | null>
  default_value: <typed value | "today" | "current_user" | "user's last selection" | null>
  pre_filled_from_server: <true | false | null>

# ──────────────── Server-side business logic ────────────────
# The concrete rules that produce the slice's data. Each substantive
# claim carries `status` + `evidence` (gate rule 6 applies). Free-prose
# `selection.rules: ["Returns the residents for this screen"]` is rejected
# by review — the contract requires concrete predicates the rewrite session
# can reproduce.
business_logic:
  # Which records the slice's data load returns. Decomposed into concrete
  # parts the rewrite session can reproduce verbatim. Each predicate is one
  # filter clause; ordering / projection / paging are separate concerns.
  selection:
    # Atomic filter clauses. A rule is a single comparison or set predicate.
    # "Returns residents on premise and not archived" → two predicates, not one.
    predicates:
      - rule: <e.g. "Status = 'OnPremise'">
        status: <unknown | observed | source_confirmed>
        evidence: <typed source_ref or test_id; required when status ≠ unknown>
        rationale: <prose, optional: business reason for the filter>
      - rule: <e.g. "IsArchived = false">
        status: <unknown | observed | source_confirmed>
        evidence: <…>
        rationale: <…>
    # Server-side projection: which fields of the entity make it into the
    # response. Lower-bound for the rewrite — anything not listed here is
    # not contractual.
    projection:
      fields: [<e.g. "ResidentId", "FirstName", "LastName", "RoomNumber", "CareLevel">]
      status: <unknown | observed | source_confirmed>
      evidence: <typed source_ref or test_id>
    # Sort order applied server-side before paging.
    ordering:
      sort_keys:
        - { field: <e.g. "LastName">, direction: <asc | desc> }
        - { field: <e.g. "FirstName">, direction: <asc | desc> }
      user_changeable: <true | false>
      status: <unknown | observed | source_confirmed>
      evidence: <typed source_ref or test_id>
    # Server-side paging.
    paging:
      default_size: <int | null>
      server_side: <true | false>
      status: <unknown | observed | source_confirmed>
      evidence: <typed source_ref or test_id>
    # Free-form rationale & cross-references — narrative for human readers.
    # NOT a substitute for predicates/projection/ordering/paging.
    rules_summary: <prose: 1–2 sentences for human reviewers; the
                    structured fields above are the contract>
    code_refs: [<typed source_ref entries>]

  # Permission-based filtering applied at query time
  # (orthogonal to action_authorization, which gates user actions)
  authorization_filters:
    - rule: <e.g. "Only residents in communities the current user has been granted access to">
      code_ref: <typed source_ref>
      status: <unknown | observed | source_confirmed>

  # Fields the API surfaces or the view derives that aren't direct properties
  computed_fields:
    - name: <e.g. "CareLevel">
      derivation: <e.g. "Resolved from the resident's primary CarePlan; falls back to 'Unassigned' when no plan">
      code_ref: <typed source_ref>
      status: <unknown | observed | source_confirmed>
    - name: <e.g. "CompliancePct">
      derivation: <e.g. "completed / total care tasks in the trailing 30 days">
      code_ref: <typed source_ref>
      status: <unknown | observed | source_confirmed>

  soft_delete: <e.g. "Records with IsArchived=true are excluded by default; included on 'Show Archived'" | null>

  temporal_scoping: <e.g. "as of the effective date in the global selector" | null>

  # Side effects beyond the immediate UI response that ARE user-visible
  # (audit row appears in an Activity panel, email/notification triggered, search-index update the user notices)
  user_visible_side_effects:
    - kind: <audit_entry | email | notification | search_index | downstream_record>
      description: <prose: what the user observes elsewhere as a result>
      code_ref: <typed source_ref>
      status: <unknown | observed | source_confirmed>

# ──────────────── Regulated data handling (REQUIRED for PHI / PII slices) ────────────────
# Senior-Living is regulated (HIPAA in the US, plus state-level retention
# rules). When a slice surfaces PHI / PII (resident demographics, medical
# data, room assignments, medication, care notes), this block is REQUIRED.
# Slices that surface no regulated data set `surfaces_regulated_data: false`
# and the rest of the block can be `n/a` with `n/a_reason`.
#
# Gate rule 11 (PHI coverage): when surfaces_regulated_data: true, every
# field below must be at status observed | source_confirmed | n/a — and
# n/a requires n/a_reason. The downstream rewrite session relies on this
# block to preserve compliance posture across the migration.
regulated_data_handling:
  surfaces_regulated_data: <true | false>
  data_categories: [<e.g. "resident_demographics", "medical_record", "medication", "care_note", "room_assignment", "guardian_contact">]

  # Who-saw-what audit. For PHI reads this is often as important as
  # who-changed-what. The legacy app may not have this; if it doesn't,
  # flag rewrite_intent: improve.
  read_audit:
    emits_view_audit: <true | false | unknown>
    audit_target: <prose: where the read-audit row lands (table, file, log)>
    user_visible_to: <prose: whom the audit is exposed to — admin only? compliance officer? resident on request?>
    status: <unknown | observed | source_confirmed | n/a>
    evidence: <typed source_ref or test_id | "untested">
    n/a_reason: <prose | null>
    rewrite_intent: <preserve | improve | drop | unspecified>

  # Export & print are exfiltration vectors and need the same audit.
  export_audit:
    emits_export_audit: <true | false | unknown>
    formats_audited: [<pdf | csv | xlsx | print_view>]
    status: <unknown | observed | source_confirmed | n/a>
    evidence: <typed source_ref or test_id | "untested">
    n/a_reason: <prose | null>

  # Retention & deletion. Soft-delete is the legacy norm in this domain;
  # hard-delete (right-to-erasure) is rare. Both are legitimate.
  retention:
    policy: <prose: e.g. "records retained 7 years per state regulation; archived after resident departure">
    soft_delete: <true | false | unknown>
    hard_delete: <true | false | unknown>
    status: <unknown | observed | source_confirmed | n/a>
    evidence: <typed source_ref or test_id | "untested">
    n/a_reason: <prose | null>

  # Minimum-necessary: does the slice show only what the user's role needs?
  # Or does it default-show everything and rely on the user not to look?
  minimum_necessary:
    role_filtered_fields: <true | false | unknown>
    description: <prose: e.g. "Care Aide sees demographics + care plan; Nurse sees those plus medication; Admin sees all + audit">
    status: <unknown | observed | source_confirmed | n/a>
    evidence: <typed source_ref or test_id | "untested">
    n/a_reason: <prose | null>

# ──────────────── Configuration ────────────────
# Keys depend on control_type. Populate what fits, drop the rest.
# Add new keys when a slice has behaviors the schema doesn't anticipate.
configuration:
  placeholder: <string | null>
  read_only: <true | false>
  disabled_when: <prose condition | null>
  required_indicator_convention: <project-wide convention, e.g. "bold blue label">

  # Conditionally-present slices (slice doesn't exist until state changes)
  presence_condition: <prose: "after first record action" | "user has role X" | null>

  # ── for context selectors (community / facility / fiscal year / locale)
  # Describes how a selection change propagates to scoped slices in the app.
  # See references/cross-slice-context.md for the canonical mode list.
  behavior_propagation:
    on_change: <full_page_reload | soft_refresh_all_scoped | session_only_no_refresh>
    persists_across_navigation: <true | false>
    persistence_layer: <session | cookie | querystring | url_path | "unknown — fill when source arrives">

  # Multi-state slices (different appearance / labels per state)
  states: [<state names, e.g. punched_in, punched_out>]

  # ── for dropdowns
  multi_select: <true | false | null>
  searchable: <true | false | null>
  grouped: <true | false | null>
  select_all: <true | false | null>
  free_text_allowed: <true | false | null>

  # ── for grids / lists
  filtering:
    enabled: <true | false>
    column_filters: [<column ids>]
    global_search: <true | false>
    server_side: <true | false>
    persists_in_url: <true | false>
    filter_operators: [<e.g. equals, contains, between>]
  sorting:
    enabled: <true | false>
    multi_sort: <true | false>
    sortable_columns: [<column ids>]
    server_side: <true | false>
    default_sort: { column: <id>, direction: <asc | desc> }
  paging:
    enabled: <true | false>
    server_side: <true | false>
    page_size: <int>
    page_size_options: [<ints>]
    persists_in_url: <true | false>
  selection:
    mode: <none | single | multi>
    indicator: <none | row_highlight | checkbox>
    persist_across_pages: <true | false>
  row_actions:
    - { id: <slug>, label: "<label>", endpoint: <url-or-null>, requires_selection: <bool>, post_action: <prose> }
  toolbar: [<toolbar action ids>]
  empty_state: <prose>
  inline_indicators:                                # status badges / pills shown in rows
    - { kind: <slug>, color: <semantic name>, semantics: <prose> }
  combined_effect_rules: [<prose, e.g. "filter change resets page to 1">]

  # ── for date / time pickers
  format: <e.g. "yyyy-MM-dd">
  min: <date | "today" | null>
  max: <date | null>
  picker_type: <calendar | text-with-icon | inline>
  range: <true | false>

  # ── for file upload
  multiple: <true | false>
  accepted_extensions: [<".pdf", ".docx", …>]
  max_file_size_bytes: <int>
  chunked_upload: <true | false>

  # ── for autocomplete
  min_length: <int>
  debounce_ms: <int>

  # ── for modals / drawers
  size: <small | medium | large>
  closable: { close_icon: <bool>, escape_key: <bool>, backdrop_click: <bool> }
  static: <true | false>                            # static = no dismiss except explicit
  loaded_via_ajax: <true | false>
  load_url: <e.g. "/Residents/Applicants/1/New" | null>

  # ── for wizards / tabbed forms
  layout: <tabs | wizard>
  steps_or_tabs:
    - id: <slug>
      title: "<label>"
      gates_next: <prose: "all required fields filled" | null>
      load_strategy: <eager | lazy | lazy_once>
  load_strategy: <eager | lazy | lazy_once>
  back_navigation:
    always_allowed: <true | false>
    preserves_state: <true | false>

  # ── for forms
  sections:                                         # named sub-sections within a form
    - { id: <slug>, title: "<label>" }
  submit_strategy: <single | multi_action | batch_via_toolbar | inline_per_row | autosave>
  submit_actions:                                   # for multi-action submit
    - { id: <slug>, label: "<label>", primary: <bool>, post_action: <prose: navigate-where, refresh-what> }
  autosave: { debounce_ms: <int>, indicator: <prose>, on_failure: <prose> }
  unsaved_changes_guard: { enabled: <bool>, message: "<text>", triggers_on: [<events>], bypassed_by: [<events>] }

  # ── for toggle / segmented buttons
  options: [<option labels — e.g. ["Yes","No"], ["Time","Room"]>]

  # ── for toasts / alerts
  position: { x: <left | center | right>, y: <top | middle | bottom> }
  timeout_ms: <int>
  dismissible: <true | false>
  variants:
    - { kind: <success | error | warning | info>, color: <semantic name>, icon: <semantic name> }
  source: <client_only | tempdata | server_push | both>

  # ── for loading indicators
  scope: <global | regional | inline>
  shows_when: <prose: "any AJAX in flight" | "specific button pressed, request pending">
  blocks_interaction: <true | false>

  # ── for derived / composite state (Advanced)
  derived_from: [<source field paths or slice ids>]
  derivation: <prose: "sum(line_items.quantity * unit_price)" | "today - move_in_date in days">
  derivation_runs: <client | server | both>

  # ── for reordering / drag-drop (Advanced)
  draggable_elements: <prose: "rows" | "cards" | "tasks">
  valid_drop_targets: <prose>
  drop_persistence: <immediate_ajax | queued | local_only>
  invalid_drop: <prose: "snap back" | "error toast" | "silently rejected">

  # ── for concurrent editing (Advanced)
  concurrency_strategy: <optimistic_lock | pessimistic_lock | real_time_collab | none>
  conflict_indicator: <prose: "this record was changed by <user>; reload?">
  resolution_paths: [<overwrite | reload_and_edit | merge | abandon>]
  presence_indicator: <prose: "shows who else is viewing/editing" | null>

  # ── for long-running operations (Advanced)
  job_state_machine: [<submitted | queued | running | completed | failed | cancelled>]
  status_polling: { interval_ms: <int>, endpoint: <url> }
  status_via_push: { hub: <name>, method: <name> }
  on_completion: <prose: "download link appears" | "row disappears from queue" | "toast">

  # ── for export / print (Advanced)
  export_formats: [<pdf | csv | xlsx | docx | print_view>]
  generation_mode: <synchronous_download | queued_with_notification>
  filename_pattern: <prose>
  print_only_view: <url | null>

  # ── for multimodal input (Advanced)
  input_modes: [<keyboard | voice | drawing | camera | drag_drop_file>]
  voice_indicator: <prose: "mic icon; recording state shown">
  signature_storage: <png | svg | base64_string | null>

  # ── for activity / audit / comments (Advanced)
  timeline_data_source: <api endpoint or null>
  filter_options: [<by_user | by_event_type | by_date>]
  per_entry_ui: <prose: "expandable diff" | "linked record" | "mention triggers notification">

  # ── for workflow / state machines (Advanced)
  workflow_states: [<state names>]
  allowed_transitions: [{ from: <state>, to: <state>, requires: <role/permission> }]
  transition_required_fields: [{ transition: <slug>, fields: [<names>] }]
  side_effects_per_transition: [{ transition: <slug>, effects: [<prose>] }]
  user_state_indicator: <prose: "badge with state color" | "progress bar">

# ──────────────── Validation ────────────────
validation:
  - rule: <required | string_length | range | regex | compare | email | remote | custom>
    parameters: <e.g. { min: 1, max: 100 } | null>
    trigger: <client | server | client+server>
    event: <blur | submit | change | input>
    message: <verbatim error message text>
    conditional_on: <prose | null>
    visible_state: <prose: "pink background, red border, message below field">

# ──────────────── Reactivity ────────────────
# Sanctioned event values: change | click | focus | blur | submit | load |
#   row_click | row_expand | server_push | toolbar_commit | type | select |
#   open_dropdown | dismiss | advance_step | scope_change | external_signal
# Sanctioned action values: reload | hide | show | enable | disable | submit |
#   navigate | replace_partial | open_modal | open_drawer | open_dropdown |
#   dismiss | advance_step | switch_tab | queue | commit_batch |
#   visual_state_change | emit_toast | filter_options | scope_change | export
# When the slice surfaces a behavior whose event/action isn't in the sanctioned
# list, use a kebab-case custom value AND mention it in `extensions:` at the
# bottom of the frontmatter so future schema versions can absorb it.
reactivity:
  - event: <one of the sanctioned values, or kebab-case custom>
    targets: [<related slice ids>]
    action: <one of the sanctioned values, or kebab-case custom>
    endpoint:
      method: <GET | POST | PUT | DELETE | null>
      url: <route path | null>
      request_payload: <prose summary | null>
      response_handling: <prose>
    settle_ms: <int | null>                         # for push-driven mutations
    immediate_response: <prose>                     # what user sees right after the trigger
    final_response: <prose>                         # what user sees after settle
    requires_selection: <true | false | null>       # for grid bulk actions

# ──────────────── Cross-slice ────────────────
# Sanctioned relation values: parent | child | sibling | trigger | target |
#   scope_provider | scope_consumer
related_controls:
  - id: <slice-id>
    relation: <one of the sanctioned values, or kebab-case custom>

# Which context selector(s) this slice's data is scoped by
scoped_by: [<context-slice-id>] | null

# Cross-slice signal sources beyond direct cascade.
# IMPORTANT for SignalR/SSE: each signalr|sse source MUST have a matching
# entry in `endpoints[]` with a stable id (referenced via `endpoint_id`
# below), and that endpoint must participate in tamper_matrix coverage and
# failure_matrix.push_disconnect — otherwise tenant push-frame leakage and
# disconnect behavior are structurally untracked.
signal_sources:
  - kind: <signalr | sse | toast_bus | global_event | refresh_propagation>
    detail: <prose>
    endpoint_id: <reference to endpoints[].id when kind ∈ {signalr, sse}; null otherwise>
    artifact_ref: <sibling artifact id when this signal originates in another slice; null when local>

# Behaviors when the slice closes / unmounts (for modals, drawers, wizards)
on_close:
  refresh_parent: <true | false>
  refresh_mechanism: <full_reload | ajax_partial | signalr_event | none>
  refresh_target: <parent-slice-id | null>
  unsaved_changes: <prose: "discard silently" | "prompt confirm" | "auto-save">

# ──────────────── Endpoints ────────────────
# Per-aspect verification. URL+method observed in the network is NOT enough
# to call an endpoint "verified" — especially for mutating writes. Each
# aspect carries its own confirmation state. The full enum (canonical here):
#   unknown          — no information yet
#   observed         — exercised in browser; outcome captured
#   observed_partial — partially exercised (e.g. one path traced, others not)
#   source_confirmed — confirmed in source code
#   n/a              — not applicable for this endpoint kind (e.g. anti_forgery for GET)
#
# `mutates_state` decouples "is this a write?" from HTTP method. Legacy MVC
# is full of GET-shaped writes (e.g. /Residents/{id}/Deactivate, link-
# triggered status changes, queue-pop links). The mutating-endpoint gate
# fires on `mutates_state: true` regardless of HTTP method.
endpoints:
  - id: <stable kebab-case identifier; referenced from tenant_boundary.tamper_matrix and contract_status_exceptions>
    method: <GET | POST | PUT | PATCH | DELETE>
    url: <route path>
    purpose: <prose>
    response_kind: <html_full | html_partial | json | json_problem_details | redirect>
    # Does this endpoint change server-side state the user would notice?
    # - true  for any state-changing call regardless of HTTP method
    # - false for pure reads (selects, server-rendered views, JSON list/get)
    # If true, the mutating-endpoint gate applies; failure_matrix coverage,
    # idempotency, anti-forgery, and audit assertions all become required.
    mutates_state: <true | false>
    verification:
      method:           <unknown | observed | observed_partial | source_confirmed | n/a>
      route:            <unknown | observed | observed_partial | source_confirmed | n/a>
      payload_schema:   <unknown | observed | observed_partial | source_confirmed | n/a>
      response_shape:   <unknown | observed | observed_partial | source_confirmed | n/a>
      error_shape:      <unknown | observed | observed_partial | source_confirmed | n/a>
      anti_forgery:     <unknown | observed | observed_partial | source_confirmed | n/a>
      authorization:    <unknown | observed | observed_partial | source_confirmed | n/a>
    # Evidence for each aspect. Required when aspect status is observed |
    # source_confirmed (gate rule 6) OR n/a on a gate-critical aspect (gate
    # rule 10). Aspects at unknown can omit the entry.
    #
    # Each entry carries:
    #   source_refs    — list of typed refs (see SKILL.md "Evidence shapes"):
    #                    {path, symbol}, {path, line}, or {test_id} for browser probes.
    #                    REQUIRED when status is source_confirmed.
    #   observed_result — prose describing what the browser exercise saw.
    #                    REQUIRED (not "untested"/"unknown") when status is observed.
    #   n/a_reason     — prose justifying why the aspect doesn't apply.
    #                    REQUIRED when status is n/a on any of:
    #                      authorization, error_shape, anti_forgery
    #                    on a MUTATING (mutates_state: true) or TENANT-SCOPED
    #                    endpoint. Default-BLOCKING without n/a_reason.
    verification_evidence:
      method:           { source_refs: [], observed_result: <prose | null>, n/a_reason: null }
      route:            { source_refs: [], observed_result: <prose | null>, n/a_reason: null }
      payload_schema:   { source_refs: [], observed_result: <prose | null>, n/a_reason: null }
      response_shape:   { source_refs: [], observed_result: <prose | null>, n/a_reason: null }
      error_shape:      { source_refs: [], observed_result: <prose | null>, n/a_reason: null }
      anti_forgery:     { source_refs: [], observed_result: <prose | null>, n/a_reason: null }
      authorization:    { source_refs: [], observed_result: <prose | null>, n/a_reason: null }
    # Gate rules for contract_status: complete:
    # - method, route at unknown    → BLOCKING
    # - For MUTATING endpoints (mutates_state: true) AND/OR TENANT-SCOPED endpoints:
    #   payload_schema, error_shape, anti_forgery, authorization must be
    #   observed | source_confirmed | n/a. observed_partial is BLOCKING
    #   unless listed under contract_status_exceptions with reason + risk_owner.
    # - Evidence coherence (rule 6): status observed | source_confirmed REQUIRES
    #   non-empty evidence (observed_result not "untested"/"unknown" AND/OR
    #   source_refs non-empty for source_confirmed).
    # - n/a coherence (rule 10): status n/a on gate-critical aspects REQUIRES
    #   n/a_reason. authorization: n/a and error_shape: n/a on a mutating or
    #   tenant-scoped endpoint require an EXPLICIT contract_status_exceptions
    #   entry — the default is to BLOCK these.
    # - For pure-read GET endpoints (mutates_state: false): observed_partial
    #   acceptable for response_shape when the endpoint returns server-rendered
    #   HTML (the rewrite redefines the response surface anyway).
    # - Required cells in failure_matrix at status: unknown are BLOCKING.

# ──────────────── Authorization ────────────────
authorization:
  presence_condition: <prose: "user is in role 'Admin'" | null>
  action_authorization:
    - action: <slug>
      requires: <prose>
      on_denied:
        response_kind: <html_full | json_problem_details | redirect | n/a>
        user_sees: <prose: what the user observes>
        legacy_quirk: <prose | null>
        rewrite_intent: <preserve | improve | drop | unspecified>
  re_auth_required: <true | false>
  re_auth_for: [<action slugs>]

  # ── Tenant boundary (REQUIRED for any slice that touches tenant context,
  #    not only those with a tenant id explicitly in the route).
  #
  # Trigger conditions (ANY one forces tenant_boundary to be populated and
  # tamper_matrix to cover every relevant endpoint):
  #   - scoped_by is non-null (slice consumes a context selector)
  #   - routes contain a tenant placeholder ({communityId}, {facilityId}, …)
  #   - business_logic.authorization_filters mentions a tenant filter
  #   - business_logic.selection.rules mention community / facility / tenant
  #   - context_sources is non-empty (session/cookie/claim carrying tenant)
  #   - any reactivity endpoint posts a body field that resolves to a tenant
  #
  # Implicit / session-bound tenant context (e.g. /Residents/Profiles/{id}
  # with community resolved from session) STILL requires tenant_boundary —
  # this is the highest-risk shape because the developer can't see the
  # tenant in the URL.
  #
  # Required scenarios (each must appear at status source_confirmed or
  # observed before the endpoint is contract-complete):
  #   - route_tenant_mismatch   — URL tenant id ≠ session/permitted tenant
  #   - body_tenant_mismatch    — POST body carries a tenant id ≠ URL
  #   - foreign_key_ownership   — POST body references an entity (resident_id,
  #                               task_id, …) belonging to a different tenant
  #   - revoked_grant           — user's grant revoked mid-session
  #   - read_vs_write           — read vs write denial behavior may differ
  tenant_boundary:
    context_sources: [<"url_path: /Care/Tracking/{communityId}", "session: CurrentCommunityId", "cookie: alis_community", "claim: tenant_id">]
    # tamper_matrix MUST have one entry per tenant-scoped endpoint listed
    # in `endpoints[]` (referenced by stable endpoint_id). Each entry
    # includes scenarios for every required kind that applies to that
    # endpoint's role; scenarios that don't apply are explicitly marked
    # `status: n/a` with a justification in `observed_result`.
    tamper_matrix:
      - endpoint_id: <reference to endpoints[].id>
        scenarios:
          - kind: <route_tenant_mismatch | body_tenant_mismatch | foreign_key_ownership | revoked_grant | read_vs_write>
            baseline_context: <prose: authorized starting state>
            tampered_input: <prose: what was changed>
            expected_status: <int | "deny" | "allow">
            expected_shape: <html_full | json_problem_details | redirect | n/a>
            observed_result: <prose | "untested">
            source_refs: [<code_ref or "unknown">]
            status: <unknown | observed | source_confirmed | n/a>

# ──────────────── Failure matrix (REQUIRED for any slice with mutating endpoints) ────────────────
# A slice is "mutating" when ANY endpoint has mutates_state: true (NOT just
# POST/PUT/DELETE — legacy GET-shaped writes count). Each cell is structured:
# status + behavior + evidence. Cells at status: unknown for required
# mutating-write semantics force contract_status: incomplete at the artifact
# level.
#
# Required cells for mutating slices: http_4xx, http_5xx, network_timeout,
# double_click_or_resubmit, retry_after_failure, partial_success,
# refresh_mid_flight, context_switch_mid_edit, push_disconnect,
# idempotency_strategy, queue_retention, concurrency_conflict,
# audit_emission.
#
# Cells legitimately n/a are marked status: n/a (e.g. push_disconnect on a
# slice with no SignalR involvement; partial_success on a non-batch endpoint;
# concurrency_conflict on append-only journals). Mark with justification in
# behavior + evidence.
#
# Canonical failure_matrix status enum: unknown | observed | source_confirmed | n/a
# (NOT observed_partial — partial knowledge of failure semantics is treated as
# unknown for gating purposes. NOT inferred — inferences are not evidence.)
#
# Evidence coherence: any cell at status: observed | source_confirmed MUST
# have a non-empty `evidence` field (not "untested" / "unknown" /
# placeholder). The gate enforces this — see SKILL.md gate rule 6.
failure_matrix:
  http_4xx:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: how the user sees a 400/403/422 response>
    evidence: <code_ref or test_id or "untested">
  http_5xx:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: how the user sees a 500>
    evidence: <code_ref or test_id or "untested">
  network_timeout:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: client-side handling, retry, message shown>
    evidence: <code_ref or test_id or "untested">
  double_click_or_resubmit:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: idempotency — does a second click queue, replay, or no-op?>
    evidence: <code_ref or test_id or "untested">
  retry_after_failure:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: explicit retry path, automatic retry, or none>
    evidence: <code_ref or test_id or "untested">
  partial_success:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: what if 3 of 5 batch items save, 2 fail>
    evidence: <code_ref or test_id or "untested">
  refresh_mid_flight:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: client refreshes browser before response arrives>
    evidence: <code_ref or test_id or "untested">
  context_switch_mid_edit:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: user changes community/date while local queue has data>
    evidence: <code_ref or test_id or "untested">
  push_disconnect:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: SignalR connection drops mid-flight; does counter stay stale?>
    evidence: <code_ref or test_id or "untested">
  idempotency_strategy:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <none | client_dedupe_token | server_idempotency_key | natural_idempotent>
    evidence: <code_ref or test_id or "untested">
  queue_retention:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: client queue cleared on success only? on submit attempt? per-row immediately?>
    evidence: <code_ref or test_id or "untested">
  concurrency_conflict:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: two users save the same record concurrently. Last-write-wins? Optimistic-lock with conflict prompt? Merge? Append-only no-conflict?>
    evidence: <code_ref or test_id or "untested">
  audit_emission:
    status:   <unknown | observed | source_confirmed | n/a>
    behavior: <prose: does the slice emit a user-visible audit row (Activity panel, change log, history pane)? Where does the user see it? Is the audit entry tied to the actor + timestamp + before/after values? `n/a` only when the slice is provably non-recordable (e.g. ephemeral UI toggle).>
    evidence: <code_ref or test_id or "untested">

# ──────────────── Mode B helpers ────────────────
url_conventions_observed: [<prose: "/{controller}/{action}/{id}/Pane → drawer partial", …>]
# `unknowns_to_fill_when_source_arrives` is gate-aware. Entries here that
# describe gate-critical fields (endpoint paths/methods, anti_forgery,
# authorization rules, tenant_boundary scenarios, business_logic.selection
# rules, validation parameters, audit emission) FORCE contract_status:
# incomplete. Mode B is a legitimate working mode but cannot ship `complete`
# while security or correctness fundamentals are deferred.
unknowns_to_fill_when_source_arrives:
  - <field path: "validation[2].parameters">
  - <field path: "endpoints[0].verification.payload_schema">
  - <…>

# ──────────────── Schema extensions ────────────────
# Use this block ONLY for kebab-case custom values that are NOT in the
# sanctioned enum lists for event / action / relation / control_type.
# Sanctioned values must NEVER appear here — they're already part of the
# schema. Listing a sanctioned value here pollutes the schema-evolution
# signal Codex review uses to decide future enum additions.
#
# Sanctioned event values: change | click | focus | blur | submit | load |
#   row_click | row_expand | server_push | toolbar_commit | type | select |
#   open_dropdown | dismiss | advance_step | scope_change | external_signal |
#   queue_changed
# Sanctioned action values: reload | hide | show | enable | disable | submit |
#   navigate | replace_partial | open_modal | open_drawer | open_dropdown |
#   dismiss | advance_step | switch_tab | queue | commit_batch |
#   visual_state_change | emit_toast | filter_options | scope_change | export
# Sanctioned relation values: parent | child | sibling | trigger | target |
#   scope_provider | scope_consumer
# Sanctioned control_type values: see the control_type field at top.
#
# Each entry below records: what kind of enum (event / action / relation /
# control_type), the kebab-case proposed value, the reason it isn't covered,
# evidence of where it's used, and status (always proposed — sanctioned
# values do not appear here).
extensions:
  - kind: <event | action | relation | control_type>
    value: <kebab-case slug>
    reason: <prose: why this isn't covered by sanctioned values>
    evidence: <prose: where in this artifact it's used; "observed at /Care/Tracking/…">
    status: proposed
---

# <Title>

## Behavior summary

<2–4 sentences. Lead with the user-visible purpose. Mention key interactions and dependencies. The downstream LLM reads this first.>

## Code references

(For human reviewers cross-checking the artifact. The downstream LLM does not need these.)

- View: `<path>:<line>` (or "unknown — fill when source arrives")
- Model: `<class>.<property>`
- Helper / extension: `<call site>`
- Client script: `<path/to/script.js>:<function>`
- Controller action: `<Controller>.<Action>`
- Hub: `<HubName>.<MethodName>`

## Edge cases

- <Empty / null data — what does the user see?>
- <Invalid input — what message, what state?>
- <Network failure during AJAX — does the user see an error, a stale state, a retry?>
- <Permission-denied — does the slice hide, disable, show a message?>
- <Stale state across context switches — what if community changes mid-edit?>
- <Race conditions between AJAX and SignalR pushes>
- <Unsaved changes when navigating away>
- <Missing accessibility semantics — flag as requirement on the rewrite>
- <Browser refresh mid-flow — does state survive?>

## Verification claims

Each claim is a testable assertion. Step 3 of the workflow exercises these.

1. <Initial-render claim — what's there when the page loads.>
2. <Validation claim with verbatim message text.>
3. <Reactivity claim with both halves: visible + network.>
4. <Settle-window claim for SignalR-driven mutation: "after AJAX returns 200 AND ~2 s settle, the counter advances".>
5. <Cross-slice claim — "when X changes, Y reacts in this specific way".>
6. <Conditional-presence claim — "after first record action, the Show Recorded toggle materializes in the toolbar".>
7. <…>

## Verification log

(Updated by step 4 as claims are confirmed or corrected. Format: `<date> — <change>`. The downstream LLM ignores this section; it's for human reviewers.)

- <date> — <change>
