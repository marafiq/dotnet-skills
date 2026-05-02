# Cross-slice context — multi-tenant scoping and propagation

Many MVC 5 apps have one or more global selectors (community, facility, business unit, fiscal year, locale) that scope every other slice on the page. Capturing this correctly is critical: every artifact for a scoped slice must declare its scope, or the modern rewrite can't reproduce the propagation.

## What's a context selector

A control whose value:

- Persists across page navigation within a session.
- Affects the data shown in many or all other slices on the page (and elsewhere in the app).
- Is usually located in a global region (top bar, side rail, page header).

Examples from real apps:

- *Community* dropdown at the top right of every page.
- *Facility* selector in a side rail.
- *Effective date* picker that re-scopes financial dashboards.
- *Locale* selector that changes language and date formats throughout.

Sometimes there's just one. Sometimes there are several stacked (community + effective date + report period, all global on the same page).

## Treat the selector as a slice

The context selector is a slice in its own right. Its artifact:

```yaml
id: dashboard-community-selector
title: "Community / facility selector"
control_type: dropdown
configuration:
  searchable: true
  multi_select: false
  default_value: "user's last selection"   # or alphabetical, or first-permitted
data_source:
  kind: api
  reference: "Communities the current user has access to"
  populated_by: "<controller action>"
  fields: { value: Id, text: Name }
behavior_propagation:
  on_change: full_page_reload              # or "soft_refresh_all_scoped" or "session_only_no_refresh"
  persists_across_navigation: true
  persistence_layer: session                # session | cookie | querystring | url_path
related_controls:
  # Every scoped slice in the app, listed here as targets
```

`behavior_propagation` is the key. Three common modes:

| Mode | What happens on change |
|---|---|
| `full_page_reload` | Browser navigates / reloads the current URL with new context. URL or cookie carries the selection. |
| `soft_refresh_all_scoped` | Page stays; every scoped slice re-fetches its data. Often via JS orchestration listening to the selector's change event, or via a SignalR fan-out. |
| `session_only_no_refresh` | Selection recorded in session/cookie but the page doesn't refresh; navigating elsewhere picks up the new context. Stale-data risk if the user lingers. |

## Mark dependent slices

Every slice whose data depends on the context declares the dependency:

```yaml
# in a scoped slice's artifact
scoped_by: dashboard-community-selector
```

The downstream rewrite must:

1. Read the current context value at slice render time.
2. Filter / scope the slice's data accordingly.
3. Subscribe to context changes (via the propagation mode in the selector's artifact).

## Persistence layer

The context's selection has to live somewhere across navigations. The layer is part of the contract:

- **Session** (server-side): legacy MVC 5 default — `Session["CurrentCommunityId"]`. Survives page navigations within session lifetime. Hostile to testability and stateless scaling.
- **Cookie**: the selection survives session restarts. Cleared by clearing browser data.
- **Querystring**: makes the URL self-contained but verbose. Some apps mix this — `?communityId=1` on key pages, session for others.
- **URL path**: the most explicit — `/{communityId}/Care/Tracking` style routing.

Capture which one the legacy app uses. The modern rewrite will likely change this (modern apps often prefer querystring or URL path over session for testability), and the propagation mode shifts accordingly. If the rewrite changes the persistence layer, that's a deliberate decision — but it must be made explicit, not silently.

## Multiple stacked contexts

Some apps have several global selectors that combine:

- Community + Effective Date + Report Period — three independent dimensions, each scoping different parts of the page.

Each is its own slice. Scoped slices can declare multiple `scoped_by` entries:

```yaml
scoped_by: [dashboard-community-selector, dashboard-effective-date]
```

The modern rewrite must reproduce all of them with the propagation modes documented for each.

## Edge cases worth capturing

- **Permission gating**: not all users can see all communities. The selector's options list depends on the current user's permissions — different from a static list.
- **Default selection**: which context is selected on first load (last-used, sticky, alphabetical, first-permitted, blank-with-chooser).
- **Required vs optional**: some apps allow "no community selected" (showing a chooser screen); others always have one. Capture which.
- **Switch confirmation**: changing context with unsaved local changes — does the app warn? Auto-save? Discard silently?
- **Concurrent sessions**: if the user is logged in twice (two browser tabs), what happens when they switch context in one tab? Does the other tab realize? (Often: only on next page load.)
- **Broken state**: if the context becomes invalid (community deleted while user was logged in), what does the user see?

These are part of the contract — capture them in the selector slice's `## Edge cases`.

## Cross-slice signaling beyond context

Context propagation is one form of cross-slice signaling. Others worth capturing as their own slices:

### Toast bus

Any slice's action can emit a toast that appears in a global toast region. Document the toast region as its own slice. Emitting slices reference it:

```yaml
# in an action slice
reactivity:
  - event: submit
    targets: [global-toast-region]
    action: emit_toast
    response_handling: "On success, emit toast variant=success with text matching <claim>; on error, emit toast variant=error with the server message."
```

### Refresh propagation

Saving in a modal refreshes the parent grid. Document on the modal:

```yaml
on_close:
  refresh_parent: true
  refresh_mechanism: full_reload          # or "ajax_partial" or "signalr_event"
  refresh_target: <parent-grid-slice-id>
```

### SignalR / WebSocket fan-out

One server event updates multiple regions. Document the hub method on the source slice; each affected slice references the same trigger:

```yaml
# in slice receiving the push
data_source:
  kind: server_push
  reference: "stafftaskshub.OnTaskRecorded"
  populated_by: "Hub method invoked when any user's POST /Care/Tracking/.../Record completes"
  fields:
    delta: "{ taskId, recordedAt, byUserId }"
behaviors:
  on_push: "advance the 'X of Y recorded' counter by 1"
```

Multiple slices may all listen to the same hub method — list them in the Hub's source slice.

### Global event bus

Some legacy apps use jQuery `.trigger()` events the page listens for. Capture event name + payload + listener slices.

```yaml
emits_global_event:
  name: "alis:residentSelected"
  payload: "{ residentId, residentName }"
listens_to_global_events:
  - name: "alis:residentSelected"
    on_event: "refresh resident-detail-pane with the new residentId"
```

## When the propagation rules are unclear

Three things to do:

1. **Test by changing context** — switch the global selector; observe what reloads, what stays, what becomes stale.
2. **Inspect the network during context change** — what requests fire? Does the app reload the whole page or AJAX-refresh specific regions?
3. **Ask the user** if the observed behavior contradicts what the team intends for the modern rewrite. Sometimes the legacy behavior is a bug the team plans to fix in the rewrite — capture as *"legacy behavior: X; rewrite intent: Y"* in `## Edge cases`.

## Multi-source mutations

A counter or region that updates from many sources needs all sources captured. Example: a "tasks remaining" counter that decrements when any user records a task, when an admin reassigns one, or when a task auto-expires.

The artifact lists every trigger source. The modern rewrite must preserve all paths, even ones that fire rarely.
