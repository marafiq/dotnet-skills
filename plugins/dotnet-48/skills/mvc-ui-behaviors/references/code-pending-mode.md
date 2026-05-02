# Mode B — code-pending (browser-first)

Use when the running URL is reachable but legacy source code isn't yet on disk. Common in early-phase modernization where a vendor or sister team still owns the legacy codebase.

## What changes vs Mode A

Mode A starts from the view file and works outward to model, controller, and scripts. Mode B starts from the running page and works inward via observation.

Same artifact format. The differences:

- **Which fields are populated by observation vs by code reading.**
- **Which fields carry an `unknown — fill when source arrives` marker.**
- **Which behaviors can be verified now vs deferred** (server-only validation needs edge inputs to fire and observe).

The Mode B artifact is intentionally incomplete — its gaps are explicit, not implicit.

## Step 1 — slice discovery from the running page

Without code, grep for `Html.DropDownListFor` isn't an option. Identify slices visually:

1. Take a screenshot.
2. Scroll once top to bottom; capture structure.
3. Use `read_page` (filter: `interactive`) to enumerate the accessibility tree — surfaces visible labels and roles where they exist.
4. Use `find` with descriptive natural-language queries to locate ambiguous regions.
5. List slices in your draft. **Surface them to the user; let them pick priorities.**

Indicators that something is a slice:

- It has its own label or heading.
- It carries internal state (selected option, expanded panel, filled value).
- Its contents update as a unit.
- Other parts of the page reference it (the toolbar's "Record Care" depends on per-row Completed clicks).
- It appears or disappears based on context (*conditionally-present* slices — easy to miss; visit the page in different states to find them).

Indicators that something is part of a slice, not its own:

- Purely decorative (icon, divider).
- Inseparable from a larger interaction (a chevron on a tab is part of the tab).
- No internal state (a static label).

## Step 2 — observe behaviors directly

For each slice, exercise the running app:

- **Visible state**: read the accessibility tree and screenshot to capture labels, options, default values, placeholders.
- **Reactivity**: arm network capture, trigger the user-facing event, observe both halves.
- **Validation**: try empty / invalid input; observe error messages and field states.
- **Reactivity timing**: confirm whether a behavior is immediate, deferred, or push-driven (settle window).
- **Conditionally-present slices**: visit the page in different state combinations (logged-in role A vs role B; before/after an action; with/without selected items).
- **State-dependent rendering**: check both states explicitly (Punched In vs Punched Out, etc.) and document each.

## Step 3 — draft with `unknown` markers

Some artifact fields can't be filled from observation alone. Mark them explicitly:

```yaml
data_source:
  kind: api
  reference: "GET /Address/StatesForCountry"          # observed from network
  populated_by: "unknown — fill when source arrives"  # the controller method/class
  fields:
    value: "Id"                                       # observed from option values
    text: "Name"                                      # observed from option labels
validation:
  - rule: required
    trigger: "client+server (observed client; server unverified — fill when source arrives)"
    message: "Country is required."
endpoints:
  - method: GET
    url: "/Address/StatesForCountry"
    purpose: "Return states for the selected country (cascading source)."
    requires_anti_forgery: "unknown — fill when source arrives"
    response_kind: "json (observed)"
```

The marker tells the downstream LLM: *"This is a known gap, not an oversight."* When source arrives, fill it in.

## Step 4 — capture URL conventions

Even without code, observed URL patterns reveal controller / action shape and project conventions. Record them at the slice level (or repo level if they apply broadly):

```yaml
url_conventions_observed:
  - "/{controller}/{action}/{id}/Pane → drawer-loaded partial view"
  - "/{controller}/{action}/{id}/New → create-form modal partial"
  - "X-Requested-With=XMLHttpRequest → response is partial HTML, not full page"
  - "?tab=Current → tab state via querystring (clean URLs, not hash)"
  - "/{communityId}/Care/Tracking/… → community context in URL path"
```

These conventions help the modernizing session anticipate parallels in the rewrite, and seed future Mode A passes — when source arrives, the URL → controller mapping likely follows standard MVC conventions.

## Step 5 — when source arrives

1. For each slice, replace `unknown` markers by reading the relevant view, model, and controller.
2. Re-verify claims that previously passed (server-side rules may surface new edge cases — e.g. a server-only validation that fires only under specific data conditions).
3. Add a verification log entry: `<date> — source-fill complete; corrected N claims; added M edge cases.`

## What's blocked without source

These gaps are accepted in Mode B:

- **Server-only validation rules** — `IValidatableObject` logic, custom server-side checks that run after POST. Messages observable once they fire; full enumeration needs source.
- **Dead code paths** — behaviors that exist in code but aren't reachable through the running UI (deprecated buttons, hidden form fields).
- **Conditional logic by user role** — without source, only the logged-in role's UI is testable; other roles' UI stays unverified.
- **Anti-forgery requirements** — observable indirectly (a missing token causes 403), but the per-endpoint flag isn't visible without `[ValidateAntiForgeryToken]` attribute reading.
- **Hub method signatures** — SignalR usage is visible from the connection URL; precise method names and payload shapes need source or live-frame inspection.

## Privacy in Mode B

The running app holds real data. Be more careful in Mode B than Mode A:

- Never include observed real names, emails, room numbers, dates of birth, or other PII in the artifact.
- The verification log is for human reviewers; if specifics leak there, redact.
- If the running app is a production tenant, ask the user to point the skill at staging instead.

## When Mode B is actively a bad idea

- The running app has rate limiting, anti-bot, or session timeouts that make verification expensive.
- The data is so sparse that interesting behaviors can't be triggered (empty grids, no records to filter).
- The running app is broken in ways unrepresentative of the modernization target (production bugs, stale data).

In any of these cases, ask the user before proceeding.
