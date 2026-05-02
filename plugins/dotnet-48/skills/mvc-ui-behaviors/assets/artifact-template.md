---
# ──────────────── Identity ────────────────
id: <kebab-case slug, e.g. care-tracking-record-toolbar>
title: <human-readable name>
view: <path/to/View.cshtml | "unknown — fill when source arrives">
view_lines: <e.g. 42-78, optional>
control_type: <dropdown | textbox | textarea | checkbox | radio | grid | form | modal | drawer | wizard | wizard_step | toolbar | accordion | tab_set | side_menu | breadcrumb | datepicker | daterange | autocomplete | masked_input | numeric_input | toggle_buttons | rich_text | file_upload | loading_indicator | toast | confirmation_dialog | alert | paginator | pane | context_selector | custom>

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
# The concrete rules that produce the slice's data. Backed by `code_refs`.
# The rewrite session reproduces the rules in whatever data layer it picks.
business_logic:
  # Which records the slice's data load returns
  selection:
    rules:
      - <e.g. "Returns residents where Status = 'OnPremise' AND IsArchived = false">
      - <e.g. "Scoped to the currently-selected community (see scoped_by)">
    code_refs: [<"Controllers/ResidentsController.cs:Index">, <"Services/ResidentQuery.cs:GetOnPremise">]

  # Permission-based filtering applied at query time
  # (orthogonal to action_authorization, which gates user actions)
  authorization_filters:
    - rule: <e.g. "Only residents in communities the current user has been granted access to">
      code_ref: <"Filters/CommunityAccessFilter.cs">

  # Fields the API surfaces or the view derives that aren't direct properties
  computed_fields:
    - name: <e.g. "CareLevel">
      derivation: <e.g. "Resolved from the resident's primary CarePlan; falls back to 'Unassigned' when no plan">
      code_ref: <"Models/ResidentListItem.cs:CareLevel">
    - name: <e.g. "CompliancePct">
      derivation: <e.g. "completed / total care tasks in the trailing 30 days">
      code_ref: <"Services/ComplianceCalculator.cs:For">

  ordering:
    default: <e.g. "LastName ASC, FirstName ASC">
    user_changeable: <true | false>
    code_ref: <controller action where ordering applies>

  paging:
    default_size: <int>
    server_side: <true | false>

  soft_delete: <e.g. "Records with IsArchived=true are excluded by default; included on 'Show Archived'" | null>

  temporal_scoping: <e.g. "as of the effective date in the global selector" | null>

  # Side effects beyond the immediate UI response that ARE user-visible
  # (audit row appears in an Activity panel, email/notification triggered, search-index update the user notices)
  user_visible_side_effects:
    - kind: <audit_entry | email | notification | search_index | downstream_record>
      description: <prose: what the user observes elsewhere as a result>
      code_ref: <where the side effect originates>

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

# Cross-slice signal sources beyond direct cascade
signal_sources:
  - kind: <signalr | sse | toast_bus | global_event | refresh_propagation>
    detail: <prose>

# Behaviors when the slice closes / unmounts (for modals, drawers, wizards)
on_close:
  refresh_parent: <true | false>
  refresh_mechanism: <full_reload | ajax_partial | signalr_event | none>
  refresh_target: <parent-slice-id | null>
  unsaved_changes: <prose: "discard silently" | "prompt confirm" | "auto-save">

# ──────────────── Endpoints ────────────────
# Per-aspect verification. URL+method observed in the network is NOT enough
# to call an endpoint "verified" — especially for mutating writes. Each
# aspect carries its own confirmation state. An overall "verified" status
# requires every relevant aspect to be source-confirmed (or explicitly
# marked not-applicable for read-only endpoints).
endpoints:
  - method: <GET | POST | PUT | DELETE>
    url: <route path>
    purpose: <prose>
    response_kind: <html_full | html_partial | json | json_problem_details | redirect>
    verification:
      method:           <observed | source_confirmed>
      route:            <observed | source_confirmed>
      payload_schema:   <unknown | observed_partial | source_confirmed>
      response_shape:   <unknown | observed_partial | source_confirmed>
      error_shape:      <unknown | observed_partial | source_confirmed>
      anti_forgery:     <unknown | observed | source_confirmed>
      authorization:    <unknown | observed | source_confirmed>
    # For mutating endpoints (POST/PUT/DELETE), the failure_matrix below
    # MUST be populated before the endpoint is treated as contract-complete.

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

  # ── Tenant boundary (REQUIRED for any slice scoped_by a context selector
  #    or whose endpoints carry a tenant id in the route)
  tenant_boundary:
    context_sources: [<e.g. "url_path: /Care/Tracking/{communityId}", "session: CurrentCommunityId", "cookie: alis_community", "claim: tenant_id">]
    validation_rule: <prose: "URL communityId must match session-bound CurrentCommunityId; mismatch → 403">
    mismatch_behavior: <prose: "what the user sees if URL tenant ≠ session tenant">
    denied_response: <html_full | json_problem_details | redirect>
    revocation_behavior: <prose: "what happens if user's grant is revoked mid-session">
    tamper_test_evidence: <prose: how this was verified, or "unverified — fill when source arrives">

# ──────────────── Failure matrix (REQUIRED for any slice with mutating endpoints) ────────────────
# Mutating slices (commit toolbars, form submits, batch operations, drag-drop
# persistence) must answer each cell. Leaving cells "unknown" is acceptable in
# Mode B, but they cannot be silently omitted.
failure_matrix:
  http_4xx:                  <prose: how the user sees a 400/403/422 response>
  http_5xx:                  <prose: how the user sees a 500>
  network_timeout:           <prose: client-side handling, retry, message shown>
  double_click_or_resubmit:  <prose: idempotency — does a second click queue, replay, or no-op?>
  retry_after_failure:       <prose: explicit retry path, automatic retry, or none>
  partial_success:           <prose: what if 3 of 5 batch items save, 2 fail>
  refresh_mid_flight:        <prose: client refreshes browser before response arrives>
  context_switch_mid_edit:   <prose: user changes community/date while local queue has data>
  push_disconnect:           <prose: SignalR connection drops mid-flight; does counter stay stale?>
  idempotency_strategy:      <none | client_dedupe_token | server_idempotency_key | natural_idempotent>
  queue_retention:           <prose: client queue cleared on success only? on submit attempt? per-row immediately?>

# ──────────────── Mode B helpers ────────────────
url_conventions_observed: [<prose: "/{controller}/{action}/{id}/Pane → drawer partial", …>]
unknowns_to_fill_when_source_arrives:
  - <field path: "validation[2].parameters">
  - <field path: "endpoints[0].requires_anti_forgery">
  - <…>
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
