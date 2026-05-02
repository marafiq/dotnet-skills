# Mode B — code-pending (browser-first)

Use when the running URL is available but legacy source code isn't yet on disk. Common in real modernization projects where a vendor or sister team still owns the legacy codebase and source-code access is gated.

## What changes vs Mode A

Mode A starts from the view file and works outward to the model, controller, and scripts. Mode B starts from the running page and works inward via observation.

The artifact format is the same. The differences:

- **Which fields are populated by observation vs by code reading.**
- **Which fields carry an `unknown — fill when source arrives` marker until source arrives.**
- **Which behaviors can be verified now vs deferred** (server-only validation rules can't fully fire until you can construct edge inputs that trigger them).

The Mode B artifact is intentionally incomplete — its gaps are explicit, not implicit.

## Step 1 — slice discovery from the running page

Without code, you can't grep for `Html.DropDownListFor`. Slices are identified visually:

1. Take a screenshot.
2. Scroll once top to bottom; capture structure.
3. Use `read_page` (filter: `interactive`) to enumerate the accessibility tree — this gives you visible labels and roles where they exist.
4. Use `find` with descriptive natural-language queries to locate ambiguous regions.
5. List slices in your draft. **Surface them to the user; let them pick priorities.**

Indicators that something is a slice:

- It has its own label or heading.
- It has internal state (selected option, expanded panel, filled value).
- It's a region whose contents update as a unit.
- It's referenced by other parts of the page (the toolbar's "Record Care" depends on per-row Completed clicks).
- It appears or disappears based on context (these are *conditionally-present* slices — easy to miss; visit the page in different states to find them).

Indicators that something is part of a slice, not its own:

- It's purely decorative (icon, divider).
- It's inseparable from a larger interaction (a chevron on a tab is part of the tab).
- It has no internal state (a static label).

## Step 2 — observe behaviors directly

For each slice, exercise the running app:

- **Visible state**: read the accessibility tree and screenshot to capture labels, options, default values, placeholders.
- **Reactivity**: arm network capture, trigger the user-facing event, observe both halves.
- **Validation**: try empty / invalid input; observe error messages and field states.
- **Reactivity timing**: confirm whether a behavior is immediate, deferred, or push-driven (settle window).
- **Conditionally-present slices**: visit the page in different state combinations (logged-in role A vs role B; before/after an action; with/without selected items).
- **State-dependent rendering**: check both states explicitly (Punched In vs Punched Out, etc.) and document each.

## Step 3 — draft with `unknown` markers

Some artifact fields cannot be filled from observation alone. Mark them explicitly:

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

Even without code, observed URL patterns reveal controller/action shape and project conventions. Record them at the slice level (or repo level if they apply broadly):

```yaml
url_conventions_observed:
  - "/{controller}/{action}/{id}/Pane → drawer-loaded partial view"
  - "/{controller}/{action}/{id}/New → create-form modal partial"
  - "X-Requested-With=XMLHttpRequest → response is partial HTML, not full page"
  - "?tab=Current → tab state via querystring (clean URLs, not hash)"
  - "/{communityId}/Care/Tracking/… → community context in URL path"
```

These conventions help the modernizing session anticipate parallels in the rewrite. They also help future Mode A passes — when source arrives, you already know the URL → controller mapping likely uses standard MVC conventions.

## Step 5 — when source arrives

1. For each slice, replace `unknown` markers by reading the relevant view, model, and controller.
2. Re-verify the claims that previously passed (server-side rules might surface new edge cases — e.g. a server-only validation that only fires under specific data conditions you didn't trigger before).
3. Add a verification log entry: `<date> — source-fill complete; corrected N claims; added M edge cases.`

## What you can't do without source

These gaps are accepted in Mode B:

- **Server-only validation rules** — `IValidatableObject` logic, custom server-side checks that run after POST. You can observe their messages once they fire; you can't enumerate them exhaustively.
- **Dead code paths** — behaviors that exist in code but aren't reachable through the running UI (deprecated buttons, hidden form fields).
- **Conditional logic by user role** — without source, you can only test the role you're logged in as. Other roles' UI is unverified.
- **Anti-forgery requirements** — observable indirectly (a missing token causes 403), but the per-endpoint flag isn't visible without `[ValidateAntiForgeryToken]` attribute reading.
- **Hub method signatures** — you can confirm SignalR is in use from the connection URL, but the precise method name and payload shape needs source or live-frame inspection.

## Privacy in Mode B

The running app is real data. Be more careful in Mode B than Mode A:

- Never include observed real names, emails, room numbers, dates of birth, or other PII in the artifact.
- The verification log is for human reviewers; if specifics leak there, redact.
- If the running app is a production tenant, ask the user to point the skill at staging instead.

## When Mode B is actively a bad idea

- The running app has rate limiting, anti-bot, or session timeouts that make verification expensive.
- The running app's data is so sparse that you can't trigger interesting behaviors (empty grids, no records to filter).
- The running app is broken in ways that aren't representative of the modernization target (production bugs, stale data).

In any of these cases, ask the user before proceeding.
