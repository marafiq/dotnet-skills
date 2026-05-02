# Behavior taxonomy

Twelve categories of user-visible behavior in legacy MVC 5 apps. The artifact must capture every behavior in a slice; this file enumerates the kinds so nothing gets missed. Each category names what to observe, what to capture in the artifact, and a real-world example.

## 1. Population

How a control gets its initial data when the page or slice first renders.

| Source | Example | Capture |
|---|---|---|
| Server endpoint (AJAX) | Dropdown loaded from `GET /Address/Countries` | `data_source.kind: api`, `populated_by`, payload shape |
| Model property | `<select>` bound to `Model.Countries` | `data_source.kind: model_property` |
| ViewBag / ViewData | `(SelectList)ViewBag.Countries` | `data_source.kind: viewbag`, key |
| Parent control's selection | State dropdown loads after Country chosen | `data_source.kind: dynamic_via_parent`, parent slice id |
| Global context | List scoped by current community | `scoped_by: <context-slice-id>` |
| Hardcoded | Yes/No options | `data_source.kind: hardcoded` |
| Server push | Counter that initializes from SignalR `OnConnected` | `populated_by: <hub>.<method>` |
| Pre-filled default | Today's date in a date field; current user in an Author field | `default_value: today` / `default_value: current_user` |

For lists also capture: `fields.value` (option value), `fields.text` (visible label), `group_by` if grouped, default sort.

## 2. State change

What happens inside the slice when the user interacts. Differentiated by *where the change is felt*.

| Kind | Example | Network? | Capture |
|---|---|---|---|
| Visual only | Toggle button highlights, link toggles "Add Notes" → "Hide Notes" | No | `action: visual_state_change`, no endpoint |
| Local model mutation | Time-Taken input filled, queued for batch commit | No | `action: queue`, indicate the queue's commit slice |
| Immediate AJAX | Auto-save on blur | Yes | `endpoint`, `request_payload`, `response_handling` |
| Deferred batch | Multiple "Completed" clicks queue locally; toolbar commits all | Yes (on commit) | `submit_strategy: batch_via_toolbar`; commit slice references the queue |
| Server push | Bell counter updates from SignalR | Yes (push, not request) | `event: server_push`, hub method, payload, mutation |

The deferred-batch pattern is common in legacy MVC apps with toolbar commit buttons. The artifact must distinguish between *queueing* (local-only) and *committing* (network) — they are different slices' responsibilities.

## 3. Validation

When input is checked. Capture *where*, *when*, and *what the user sees*.

| Kind | Trigger | Capture |
|---|---|---|
| Per-field on blur | Email format check after tab away | `trigger: client`, `event: blur` |
| On submit attempt | Required-field check when Save clicked | `trigger: client`, `event: submit` |
| Cross-field | "End date must be after start date" | `rule: custom`, `parameters.kind: cross_field`, fields list |
| Server-only | `IValidatableObject` rule that requires DB lookup | `trigger: server`, fires after POST |
| Async / remote | `RemoteAttribute` username uniqueness check | `trigger: client`, separate `endpoint` |

Capture the **visible result** exactly:

- Inline message text — verbatim, character-for-character. Message wording is part of the contract.
- Field highlight (border + background — note the project's visual convention, not the colour).
- Summary banner (location + content).
- Modal block (does the modal stay open on validation failure?).

The project's required-field visual convention (asterisk, bold blue label, helper text, etc.) belongs at the top of every artifact in that codebase — it's project-specific and should be consistent across slices.

## 4. Navigation

How the user moves between contexts.

| Kind | Example | URL changes? | Capture |
|---|---|---|---|
| Page navigation | "Schedule Leave" link → full page | Yes | `action: navigate`, target URL |
| Modal overlay | "Create Applicant" button → modal at same URL | No | `action: open_modal`, modal slice id, `loaded_via_ajax` flag |
| Drawer (off-canvas) | Right-side filter panel | Sometimes | `action: open_drawer`, drawer slice id, URL pattern if applicable |
| Wizard step advance | "Next" in 3-step wizard | Sometimes | `action: advance_step`, target step, gating conditions |
| Tab switch (page-level) | "Current Residents" → "Applicants" | Yes (`?tab=`) | `action: switch_tab`, target tab slug, URL persistence |
| Tab switch (form-level) | Switching between General/Contact form sections | No | `action: show_section`, target section |

**Modals loaded via AJAX**: the modal HTML often comes from a partial-view endpoint with `X-Requested-With=XMLHttpRequest` header. Capture the loader URL.

**Wizards** have gating conditions: which steps are reachable, and from where. Capture explicitly:

```yaml
steps:
  - id: select_duration
    title: "Select Leave Duration"
    gates_next: "all required fields in this step must be filled and valid"
  - id: view_impact
    title: "View Impact of Leave"
    gates_next: "user reviewed projected impact (no separate input required)"
  - id: confirmation
    title: "Confirmation"
    gates_next: null
back_navigation:
  always_allowed: true
  preserves_state: true
```

## 5. Reactivity (cross-slice)

When one slice's interaction affects another.

| Kind | Example | Capture |
|---|---|---|
| Cascading dropdown | Country → State | `reactivity[].targets`, action `reload`, child's `data_source.kind: dynamic_via_parent` |
| Conditional show / hide | Card-section appears when payment = Card | `action: show / hide`, no network |
| Conditional enable / disable | Submit disabled until form valid | `action: enable / disable`, condition |
| Conditional presence | "Show Recorded" toggle materializes after first record | `presence_condition` on the appearing slice |
| Counter / live aggregate | "1 of 4 recorded" updates from many sources | `data_source.kind: server_push` + multiple trigger slices |
| Region replacement | Detail pane updates on master selection | `action: replace_partial`, target region by role/context |

**Conditional presence** is distinct from show/hide: a hidden control is in the DOM, just not visible. A *not-yet-present* slice doesn't exist until a state change creates it. The artifact must record this — the modern rewrite must reproduce the materialization, not just the visibility toggle.

## 6. Submission

How data gets to the server.

| Strategy | Example | Capture |
|---|---|---|
| Single submit | Standard form Save button | `submit_strategy: single` |
| Multi-action submit | "Save" / "Save and Continue" / "Save and New" / "Create and Go To" | `submit_actions: [{ id, label, primary, post_action }]` |
| Batch via toolbar | Care Tracking commit-many | `submit_strategy: batch_via_toolbar`, queue slice id |
| Inline per row | Grid edit-in-place | `submit_strategy: inline_per_row` |
| Auto-save | Debounced post on idle | `submit_strategy: autosave`, `debounce_ms`, indicator |

**Multi-action submit** is common: the same form has multiple submit buttons with different post-submit flows. *"Create"* might re-open the form for another entry; *"Create and Go To"* navigates to the new record. Capture each action's flow distinctly.

Capture **anti-forgery** requirements: `requires_anti_forgery: true` on each endpoint that needs it (most POSTs in MVC 5 do).

## 7. Result handling

What the user sees after submission. **Both success AND error branches must be captured.**

| Result | Capture |
|---|---|
| Toast notification | Variant (success/error/warn/info), text template, position, timeout |
| Inline alert (persistent) | Variant, location, persistence, dismiss controls |
| Modal close + parent refresh | Which slice refreshes; via what mechanism (full reload / partial / SignalR push) |
| Page navigation (PRG) | Target URL, optional flash message persisted via TempData |
| Stay-on-page | Re-render with errors; field-level vs summary |
| Validation summary at top | Header text, included rules |

A submission with two outcomes is **two reactivity entries**: one for success, one for error. The downstream rewrite must preserve both paths.

## 8. Filter / sort / page (lists and grids)

Distinct behavior cluster, deserves its own framing.

```yaml
configuration:
  filtering:
    enabled: true
    column_filters: [name, status]
    global_search: true
    server_side: true
    persists_in_url: true                  # ?filter[name]=foo
    filter_operators: [equals, contains, between]
  sorting:
    enabled: true
    multi_sort: false
    sortable_columns: [name, room, dob]
    server_side: true
    default_sort: { column: name, direction: asc }
  paging:
    enabled: true
    server_side: true
    page_size: 25
    page_size_options: [10, 25, 50]
    persists_in_url: true                  # ?page=N
combined_effect_rules:
  - "Changing a filter resets page to 1."
  - "Changing sort does NOT reset page."
  - "All three combine into a single GET request when any one changes."
```

The combined effect (filter + sort + page applied together) is one observable behavior. Capture the reset rules — they vary across legacy apps.

## 9. Time-dependent behaviors

When timing matters for verification.

| Kind | Detail | Capture |
|---|---|---|
| Initial load delay | "Please wait while we load …" | `loading_indicator` slice; settle expectation |
| Debounced search | Type-pause threshold | `debounce_ms` |
| Auto-save | Idle timer | `autosave.debounce_ms`, indicator |
| Server-push settle | SignalR-driven counter lags AJAX response by 1–3 s | `settle_ms` on the affected reactivity entry |
| Polling | UI refreshes every N seconds | `poll_interval_ms` |

A claim involving timing is *"after the AJAX returns 200 AND a brief settle window (typically <3 s), the counter advances"* — not *"the counter advances immediately"*. The settle window is part of the contract.

## 10. Authentication / authorization

What the user sees depending on identity and role.

```yaml
visibility:
  presence_condition: "user is in role 'Admin' OR has 'CanRecordCare' permission"
action_authorization:
  - action: delete
    requires: "role: Admin"
  - action: re_publish
    requires: "permission: ContentApprover"
re_auth:
  required_for: ["delete", "billing-action"]
  prompt: "Re-enter your password to confirm."
session:
  timeout_minutes: 20
  on_timeout: "redirects to /Account/Login with returnUrl set; no data loss prompt"
```

Capture role-driven slice presence at the slice level (not per-control inside a present slice).

## 11. Multi-tenant context

Global selectors that scope every other slice. Treat the selector as a slice; mark every dependent slice with `scoped_by`.

```yaml
# context selector slice
control_type: dropdown
title: "Community / facility selector"
configuration:
  searchable: true
  multi_select: false
  default_value: "user's last selection"
behavior_propagation:
  on_change: full_page_reload
  persists_across_navigation: true
  persistence_layer: session
```

Three propagation modes:

| Mode | What happens on change |
|---|---|
| `full_page_reload` | Browser reloads the current URL with new context. URL or cookie carries the selection. |
| `soft_refresh_all_scoped` | Page stays; every scoped slice re-fetches its data via JS orchestration or SignalR. |
| `session_only_no_refresh` | Selection recorded; page doesn't refresh; navigating elsewhere picks up new context. Stale-data risk. |

Every scoped slice declares `scoped_by`. The downstream rewrite must preserve the propagation mode — and the persistence layer (session/cookie/URL) is part of that contract.

See [`cross-slice-context.md`](cross-slice-context.md) for the full treatment.

## 12. Cross-slice signaling

Beyond direct cascade. One slice's action affects others through a shared channel.

| Mechanism | Example | Capture |
|---|---|---|
| SignalR / WebSocket push | Bell counter, live grid updates | Hub + method + frame shape, affected slice ids |
| Toast bus | Any slice can emit a toast that appears in a global toast region | Toast region as its own slice; emitting slices reference it via `emits_toast: true` |
| Refresh propagation | Saving in a modal refreshes the parent grid | Document on the modal: `on_close: refresh_parent` |
| Global event bus | jQuery `.trigger()` events the page listens for | Event name + payload + listener slices |

**Multi-source mutations need extra care**: a counter that updates from 5 different actions has 5 trigger slices, all referencing the same display slice. The artifact must list ALL the sources so the modern rewrite preserves all paths.

---

# Advanced behaviors

The twelve core categories cover the bulk of what most slices express. Real enterprise apps regularly go further. Legacy MVC 5 + jQuery + partial views is more capable than people remember — these are real patterns you'll find in mature codebases. Treat each as a refinement of the core categories with extra fields and verification considerations.

## 13. Composite / derived state

A field's value is computed from other fields, not directly bound. The value updates reactively when its inputs change.

| Example | Capture |
|---|---|
| Order subtotal recalculates when line item quantity changes | `data_source.kind: derived`, `derived_from: [line_items.quantity, line_items.unit_price]`, `derivation: "sum(line_items.quantity * line_items.unit_price)"` |
| Days remaining = move_in_date - today | `derivation` prose |
| Compliance % from completed/total ratio | aggregate |

Derivation can run client-side (jQuery handlers on input changes), server-side (re-render after POST), or both. Capture which.

## 14. Reordering / drag-drop

Items rearranged by dragging.

| Example | Capture |
|---|---|
| Sortable list — drag to reorder priority | `interaction: drag_reorder`, `persists: server_on_drop` |
| Kanban board — drag card between columns | `interaction: drag_to_column`, `column_change_endpoint` |
| Calendar / scheduling — drag a task onto a time slot | `interaction: drop_to_target`, target schema |

Capture: drag affordance (which element is draggable), valid drop targets, what the user sees during drag (ghost/preview), what happens on drop (immediate AJAX vs queued), what happens on invalid drop (snap back, error toast).

## 15. Concurrent editing

Multiple users (or sessions) acting on the same data.

| Pattern | Detail | Capture |
|---|---|---|
| Optimistic locking | "This record was changed by another user. Reload?" | The conflict-detection check on save; the user-facing prompt; the resolution path (overwrite, reload-and-edit, merge) |
| Pessimistic lock | Single-editor mode; others see read-only or "Bob is editing" | Lock acquisition / release endpoints; lock timeout; what other users see |
| Real-time collaboration | Multiple cursors, live updates | Push channel; conflict resolution rules |

The artifact captures the user-visible flow on conflict, not the underlying merge algorithm.

## 16. Long-running operations

Operations that take more than a typical request-response cycle.

| Pattern | Detail | Capture |
|---|---|---|
| Background job + polling | Submit triggers a job; UI polls `/Job/Status/{id}` every N seconds | Polling interval, job status states, on-completion behavior |
| Progress bar driven by SignalR | Live progress updates pushed | Hub method, payload, how UI renders 0–100% |
| Queued operation with status indicator | "Export queued" → indicator at top → "Export ready, click to download" | Queue submission endpoint, indicator state machine, completion notification |

Capture the **state machine** (submitted → running → completed / failed / cancelled) and what the user sees in each state.

## 17. Export / print

Generating documents or printable views from data.

| Pattern | Detail |
|---|---|
| PDF generation | Server renders PDF; download triggers; spinner during generation |
| CSV / Excel export | Often via `Content-Disposition: attachment`; can be synchronous or queued |
| Print-only view | A separate URL or `@media print` styles; usually opens in a new tab |
| Mail merge / document templating | Generate Word/PDF from per-record data |

Capture: the trigger, the format(s) offered, whether it blocks the UI or works in background, what filename pattern the download uses, whether server-side state changes (some print actions log who printed what).

## 18. Multimodal / advanced input

Beyond text and click.

| Input | Detail |
|---|---|
| Voice / speech-to-text | Microphone icon; recording indicator; result inserted as text |
| Signature capture | Drawing canvas; stored as image or vector |
| Camera capture | Phone/webcam capture for document scanning |
| Drag-drop file upload | Drop zone with hover state; file preview before submit |
| Annotations on images / PDFs | Draw/highlight on a rendered document |
| Rich-text editor | Toolbar, paste-cleanup, image embedding |

Capture: trigger UI, expected output format, edge cases (permission denied, no microphone, empty input).

## 19. Activity / audit timelines

Per-record event streams.

| Pattern | Detail |
|---|---|
| Audit log | Every change recorded; UI shows old → new diff |
| Comment thread | Inline comments on a record; mentions trigger notifications |
| Activity timeline | Chronological feed of events (created, edited, status-changed) |

Capture: data source (typically a paged API), filter options (by user, by event type, by date), per-entry UI (expand for diff, link to related records).

## 20. Workflow / approval state machines

Multi-actor processes with explicit states.

| Pattern | Detail |
|---|---|
| Approval flow | Submit → Pending → Approved / Rejected / Request changes |
| Multi-step intake | Application → review → background check → decision |
| Lifecycle states | Draft → Published → Archived |

Capture:
- The state diagram (states + allowed transitions).
- Who can perform each transition (role/permission gating).
- Required fields per transition (e.g. rejection requires a reason).
- Side effects per transition (notifications, audit entries, downstream record creation).
- The user-visible state indicator (badge, progress bar, status field).

The artifact captures the user-facing semantics; the rewrite implements the state machine in code.

## 21. Composite slices

Sometimes a slice is the *combination* of multiple categories at once. A real-time-collaborative grid with cell editing involves:

- Population (server endpoint)
- State change (local mutation + immediate AJAX per cell)
- Concurrent editing (conflict detection)
- Server push (other users' changes)
- Validation (per-cell)
- Presence indicators (who else is viewing)

Don't try to fit this into one category — capture each dimension as its own entry in the artifact's `reactivity` and `validation` arrays. The composite is the slice; the categories are how you express it.

---

# Extending the taxonomy

The numbered categories are a starting framework, not a complete catalog. Real enterprise apps surface patterns these don't anticipate. Two rules:

1. **When a behavior doesn't fit, capture it explicitly with new fields rather than shoehorning into the closest existing category.** It's better to write `behavior_kind: custom-workflow-with-side-effects` and prose-describe it than to misclassify it.
2. **When you're unsure whether something is a behavior worth capturing, ask the user.** The user knows the modernization scope. Some legacy quirks shouldn't survive (in which case capture as *"legacy behavior; rewrite intent: drop"*); others are essential.

Better to extend the schema explicitly than to lose a behavior in the wrong category.
