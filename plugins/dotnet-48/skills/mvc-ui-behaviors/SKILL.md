---
name: mvc-ui-behaviors
description: >
  Use to extract user-visible BEHAVIORS and concrete server-side BUSINESS LOGIC
  from a legacy ASP.NET MVC 5.3 slice — conditionally-present UI, deferred batch
  submits, SignalR-driven updates, multi-step wizards, AJAX-loaded modals,
  cascading dropdowns, multi-tenant scoping, JS-driven runtime behavior — into a
  Markdown-per-slice artifact another LLM session uses to re-implement the slice
  in modern ASP.NET Core MVC 10. Two-step process — (1) read code with
  `csharp-lsp` for symbol navigation, find-references, find-implementations;
  (2) exercise the running app to discover, verify, improve, and enrich what
  code reading missed (especially JS-driven behavior). Browser is a first-class
  probe surface. Capture concrete business logic such as "loads residents in
  community X with status OnPremise, sorted by LastName ASC, paged 25, with
  computed fields CareLevel and CompliancePct" — backed by code references.
  Never DOM ids, CSS classes, ARIA assumptions, jQuery selectors, widget-library
  names, or implementation syntax (LINQ/EF queries, class names, IoC bindings).
  Logs / OpenTelemetry / metrics out of scope. Trigger on phrases like "document
  this view's behavior", "capture how this drawer works", "spec out this wizard",
  "extract behaviors from this MVC 5 page", or any request to translate a legacy
  MVC interface into a behavioral contract for re-implementation. Applies to the
  dotnet-48 plugin (.NET Framework 4.8 / MVC 5.3).
---

# mvc-ui-behaviors

Extract the user-visible *behavior* of a legacy ASP.NET MVC 5.3 application into a structured artifact that a separate LLM session uses to re-implement the same behavior in ASP.NET Core MVC 10.

The artifact is the **answer**. Even when the rewrite session has access to legacy code, the artifact should carry every concrete behavior and the supporting server-side business logic — backed by evidence — so the rewrite session doesn't have to re-derive what the legacy app does. Don't shift burden by underspecifying.

The full agreed goal is in [`goal.md`](goal.md). Where this file disagrees with `goal.md`, `goal.md` wins.

## What "behavior" means here

A behavior is a **cause → effect pair the user can observe** — and, where applicable, the **concrete server-side rules** that produce those effects.

Examples:

- *"When the user types in the resident filter and selects two names, the grid re-renders with only those residents' rows. The request fires to `GET /Residents/{community}?filter=…` returning JSON. The server filters: `WHERE Status = 'OnPremise' AND IsArchived = false AND ResidentId IN (filter)`, sorted by `LastName` ASC then `FirstName` ASC, paged 25 per page."* (Both the visible effect and the server-side selection rules are part of the contract.)
- *"When the user clicks Completed on a task, the button highlights green and a Reset link appears; the toolbar's commit button advances by one and changes from disabled-grey to active-green; **no server call fires** — the change is queued locally."*
- *"When the user clicks the toolbar commit, a POST to `/Care/Tracking/{community}/Record/{date}` fires; on 200 a success toast appears top-right; the toolbar reverts to disabled. Then within ~1–3 s, the 'X of Y recorded' counter advances via a SignalR push from the `stafftaskshub` channel — the delayed update is part of the contract."*
- *"After the first record action, a 'Show Recorded' checkbox **materializes** in the toolbar that wasn't there before."*

Behaviors are **not**:

- Visual styling (button colour, padding, fonts) — the rewrite picks its own.
- DOM ids, CSS classes, jQuery selectors — implementation that won't survive.
- ARIA roles or attributes — legacy widgets routinely skip these; the modern app must add them but isn't bound to the legacy markup.
- Implementation syntax: LINQ/EF query expressions, specific controller/action/repository names, IoC bindings, attribute filter implementations.
- The widget library used (Syncfusion, Kendo, Bootstrap) — modern session picks its own.

When in doubt: would the user *notice* if the modern app did this differently? Or would the rewrite session need this fact to reproduce the data shown? If yes to either, it's a behavior.

## The unit of work: one slice, one artifact

A *slice* is a coherent user-visible unit. Examples:

- A form with its fields, validation, and submit flow.
- A grid with its filters, sorting, paging, row actions, **and the server-side selection / authorization / projection rules** that produce its rows.
- A dropdown that drives others (cascading parent).
- A modal, drawer, wizard step, accordion panel.
- A toolbar that batch-commits queued changes.
- A region that updates from server pushes.
- A global context selector (community, facility) that scopes every other slice on the page.

Each slice is one Markdown file: rich YAML frontmatter for structured fields + Markdown body for prose claims the verification step exercises.

**Larger features decompose into multiple linked slice artifacts.** The Care Tracking feature, for example, surfaces as several slices: the shift summary card, the per-task editor, the commit toolbar, the filter bar, the sort segmented controls. Each gets its own focused artifact (typically 100–250 lines); they link via `related_controls` (parent / child / sibling / trigger / target) and `scoped_by` for global context. The feature is the graph of artifacts, not one mega-document — small, linked artifacts beat one verbose file for both authoring economy and downstream consumption.

## Workflow

```
1. Identify slices               (always)
2. Read code with proper tools   (always — produces a structural skeleton)
3. Exercise the running app      (always — discover, verify, improve, enrich)
4. Iterate until verified        (always)
```

Mode B (browser-first when source isn't yet on disk) is a legitimate working mode — common in early-phase modernization when source access lags. As source arrives, transition to Mode A (code + browser). See [`references/code-pending-mode.md`](references/code-pending-mode.md).

**If the user supplies neither source nor a running URL, ask for one or both.** The skill cannot work from prose alone. Source + browser is the standard configuration; either alone is degraded.

**If you are unsure whether something is a behavior worth capturing, ask the user.** The user knows the modernization scope; don't guess at the boundary.

## Preconditions

- Authenticated browser session against the running app (the skill does not handle login flows).
- At least one of: a starting URL, a path to a legacy view (preferably both — code reading + browser verification is the standard configuration).
- An output directory for artifacts.
- For browser observation: arm `read_network_requests` early; capture starts only after the first call.

## Step 1 — identify slices

Whether you start from a `.cshtml` or a URL, the same heuristic applies:

1. Scroll the page once, top to bottom.
2. List every interactive control (input, button, dropdown, link, toggle).
3. List every region whose contents update as a unit (a list, a grid, a panel, a counter, a status badge that's wired to live data).
4. Look for **conditionally-present** slices — UI that appears only after a state change (a "Show Recorded" toggle that materializes after the first record action; an error banner that shows only on failure; a wizard step that unlocks after the previous one).
5. Group related controls into one slice when they share a single behavior.
6. Identify **global context selectors** (community / facility / business unit / locale dropdowns).
7. Identify **cross-slice signaling** — toast regions any slice can emit into; global loading overlays; SignalR-driven regions.

Propose the slice list to the user. Don't auto-pick. The user picks priorities.

## Step 2 — read code with the best available tools

For each slice, use the C# language server (`csharp-lsp`) and equivalent tooling — **not naive grep** — to read the code accurately.

- **Symbol navigation** — go-to-definition on helper calls, view-model properties, controller actions. Don't guess at where `Html.MyDropDown` is defined.
- **Find references / find usages** — see every call site of a custom helper, every place a view-model property is bound, every place a controller action is linked.
- **Find implementations** — for interfaces (`IValidatableObject`, `IClientValidatable`, custom auth attributes), surface concrete implementations.
- **Project structure awareness** — let the language server tell you the project graph; don't infer it from filenames.

Output of step 2 is a *structural skeleton* — necessarily incomplete because JS-driven and emergent behaviors aren't fully visible in source. Capture in the artifact:

- **Concrete server-side business logic** for any slice that loads or saves data — *"returns residents in community {currentCommunity} where `Status = 'OnPremise'` AND `IsArchived = false`, sorted by `LastName` ASC then `FirstName` ASC, paged 25 per page; with computed fields: `CareLevel` (resolved from primary `CarePlan`), `CompliancePct` (= completed / total tasks in last 30 days)"*. Specific. Backed by `code_refs` (`path:line` or `path:symbol`) in the artifact's `business_logic` block. Don't paraphrase as "filtered by status and community"; state the rules. Logs / OpenTelemetry / metrics are out of scope; the business logic that produces user-visible data is in scope.
- **Endpoint contract** — HTTP method, URL/route, request payload, response shape, error shape, anti-forgery requirement, conceptual purpose.
- **Field display rules** — which fields appear, formatting, empty-state fallback (*"shows 'Information not filled in' when null"*).
- **Validation rules** — verbatim message text; client / server / both; on-blur / on-submit; conditional triggers.
- **Reactivity** — events, targets, actions, endpoints, response handling.
- **Cross-slice links** — parent / child / sibling / scoped_by.

The full taxonomy with examples is in [`references/behavior-taxonomy.md`](references/behavior-taxonomy.md).

If you encounter a widget pattern that confuses you, **ask the user before guessing**.

## Step 3 — exercise the running app

The browser is a first-class probe surface, not a verification afterthought. Static analysis misses behavior in scripts you didn't find, in helpers that override defaults invisibly, in stale code paths, and in server-driven behaviors (SignalR pushes, server-only validation, push notifications) that don't show up in the view.

### What step 3 does — four modes

1. **Discover** — find behaviors step 2 couldn't see (JS handlers in bundled `.js`, runtime `data-*` manipulation, third-party widget defaults, server-pushed regions, server-side conditional branches that didn't fire when you read). If exercising the slice surfaces no surprises, you didn't exercise it hard enough.
2. **Verify** — confirm the claims that did land in step 2.
3. **Improve** — sharpen wording even when claims verify; remove ambiguity.
4. **Enrich** — add timing windows, choreography of side effects, cross-slice nuance, dynamic content variants, edge cases that only appear under interaction.

### How step 3 operates — one operating constraint that shapes all four modes

5. **Run a semi-deterministic / automatic probe sequence** where the slice allows it. Two sessions exercising the same slice should produce equivalent claims. The trade-off between procedural rigor and creative judgment is per slice — a required-field check is highly automatable; a multi-actor workflow involves more judgment. The browser is a first-class probe surface, not a verification afterthought.

### Three operational practices for the probes

These keep step 3 reproducible:

1. **Locate by behavior, not structure.** Visible label > surrounding context > action description. ARIA role only when present (legacy widgets routinely skip it). Never DOM ids, CSS chains, tag-name selectors.
2. **Observe both halves.** What the user sees AND what the server is asked. A claim verified on only one half is half-verified. *"No network call fires"* is a valid assertion when that's the contract.
3. **Allow settle windows.** Server-push-driven mutations don't arrive synchronously. After a POST that triggers a push, wait the documented settle window (typically 1–3 s) and re-check.

See [`references/browser-verification.md`](references/browser-verification.md) for the per-behavior playbook (probe sequences for population, validation, reactivity, cascading, modals, drawers, wizards, grids, toasts, loading indicators, and the advanced categories).

## Step 4 — iterate

For each verification mismatch:

1. Update the artifact (correct the claim, add missing edge case, remove stale claim).
2. Add a `## Verification log` entry: `<date> — <change>`.
3. Re-run only the changed claims.

Stop when every claim is **verified** or has been explicitly reframed as *a requirement on the modern rewrite* (e.g. *"the country dropdown must expose its label as the accessible name"* — the legacy fails this; the rewrite must succeed).

## Cross-slice context

Many MVC 5 apps have one or more global selectors (community, facility, fiscal year, locale) that scope every other slice. Treat the selector as a slice; in every dependent artifact, set `scoped_by: <context-slice-id>`. See [`references/cross-slice-context.md`](references/cross-slice-context.md) for propagation modes.

## Privacy (lint)

Generalize observed data values throughout the artifact — both prose AND YAML values. **Reject** in artifact frontmatter / claims: tenant names, tenant ids, resident names, room numbers, emails, dates of birth, hostnames, any identifier that ties an artifact to a single tenant. **Accept**: generic placeholders (*"the currently-selected community"*, *"a resident on premise"*, *"the user's accessible communities"*).

Specific values may appear *only* in the `## Verification log` (for human reviewers), and even there preferably redacted.

If the running app is a production tenant, ask the user whether they prefer pointing the skill at staging instead.

## Required blocks for security-sensitive slices

Two artifact-template blocks are **required** for the slices they apply to:

- **`tenant_boundary`** under `authorization` — required for any slice `scoped_by` a context selector (community / facility / business unit) or whose endpoints carry a tenant id in the route. Captures: context sources + a structured **`tamper_matrix`** with one row per endpoint and required scenarios per endpoint: `route_tenant_mismatch`, `body_tenant_mismatch`, `foreign_key_ownership`, `revoked_grant`, `read_vs_write`. Each scenario carries `status` (unknown / observed / source_confirmed) and `observed_result`. A single prose `tamper_test_evidence` is **not sufficient** — the matrix forces explicit coverage of every cross-tenant attack surface.
- **`failure_matrix`** at the top level — required for any slice with mutating endpoints (POST / PUT / DELETE, batch toolbars, drag-drop persistence). Each cell is structured as `{ status, behavior, evidence }`, not free prose. Required cells (cannot be silently omitted): `http_4xx`, `http_5xx`, `network_timeout`, `double_click_or_resubmit`, `retry_after_failure`, `partial_success`, `refresh_mid_flight`, `context_switch_mid_edit`, `push_disconnect`, `idempotency_strategy`, `queue_retention`.

## Contract-completeness gate

Every artifact carries `contract_status: complete | incomplete` at the frontmatter root. A slice is `complete` only when:

- All required `failure_matrix` cells (mutating slices) have `status ∈ {source_confirmed, observed}` (NOT `unknown`).
- All required `tenant_boundary.tamper_matrix` scenarios (scoped or tenant-routed slices) have `status ∈ {source_confirmed, observed}`.
- All `endpoints[].verification` aspects relevant to the endpoint's role are at status ∈ `{source_confirmed, observed, observed_partial, n/a}` (NOT `unknown`).
- All required validation messages, business_logic.selection.rules, and authorization.action_authorization entries are non-null.

Otherwise: `incomplete`. `contract_status_reason` lists the gating gaps. The downstream rewrite session should treat `incomplete` artifacts as in-progress contracts — they can inform implementation but should not be sole source-of-truth for production work.

A fresh LLM session producing artifacts at scale must **not** mark `contract_status: complete` unless all preconditions hold. This is the structural gate that prevents `unknown` cells from sneaking past as "verified."

## Endpoint verification is per-aspect

URL + method observed in the network is *not* enough to call a mutating endpoint "verified." `endpoints[].verification` carries per-aspect flags: `method`, `route`, `payload_schema`, `response_shape`, `error_shape`, `anti_forgery`, `authorization`. Each is `unknown | observed | observed_partial | source_confirmed | n/a`. Mark only what the evidence supports. An endpoint with method+route observed but payload_schema unknown is **partially verified** — and unless the unknown is acceptable for that endpoint's role, it gates `contract_status` to `incomplete`.

## Extension mechanism

The artifact-template publishes sanctioned values for `event` / `action` / `relation` / `control_type` enums (see comments in the template). If a slice surfaces a behavior whose enum value isn't sanctioned:

- Use a kebab-case custom value where it appears (e.g. `event: queue_changed`).
- AND register it in the structured `extensions:` block at the bottom of the frontmatter, with `kind`, `value`, `reason`, `evidence`, `status: proposed`.

**Sanctioned values must NEVER appear in `extensions:`** — they're already part of the schema. Listing a sanctioned value pollutes the schema-evolution signal Codex review uses to decide future enum additions. The skill-learning discipline applies: repeated proposed values across artifacts are candidates for promotion to sanctioned via Codex review.

## Skill evolution discipline

The skill is meant to **learn** — over time, real patterns observed in this app (and others) should refine the taxonomy and the artifact schema. The skill should not be corrupted by treating every one-off observation as a pattern.

A pattern enters the skill (taxonomy or artifact schema) only when:

1. **Facts establish it** — multiple observations or strong evidence; not a single anecdote.
2. **The reviewer agrees** — Codex (adversarial review) passes the addition.

A one-off observation goes into the slice's artifact (in `## Edge cases`, the verification log, or as a behavioral claim of that slice). It does not immediately reshape the skill.

## Legacy stacks pull off more than people remember

ASP.NET MVC 5 + jQuery + partial views can express remarkably complex behavior: long-running export jobs with progress, real-time-collaborative grids, drag-drop kanban boards, multi-actor approval workflows, audit timelines, voice input on notes, optimistic-locking conflict resolution. **Don't underestimate the legacy app.** When you encounter something the core taxonomy doesn't cleanly cover, look at the *Advanced behaviors* section of [`references/behavior-taxonomy.md`](references/behavior-taxonomy.md) — and if it's still not there, **extend the schema with new fields and ask the user**. The taxonomy is a framework, not a checklist.

A composite slice (e.g. a real-time-collaborative editable grid) decomposes into multiple categories at once: population + state change + concurrent editing + server push + validation + presence indicators. Capture each dimension as its own entry rather than forcing one category to express everything.

## Skill non-goals

- **No production code.** Read and observe; do not produce C#, Razor, or JavaScript. The downstream session does that.
- **No tutorial on MVC 5 / Unobtrusive AJAX.** Claude already knows them.
- **No DOM ids, CSS classes, ARIA assumptions, or widget library names** in artifact behavioral claims.
- **No implementation syntax** — LINQ/EF queries, class/method names, IoC bindings stay out. Server-side *behavior* (selection, authorization, projection, ordering, paging) stays in.
- **No exhaustive widget catalog.** The taxonomy is a framework; extend when a slice surfaces something new.

## When this skill is the wrong tool

- The view is plain HTML with no MVC helpers — there's nothing legacy-specific to extract.
- The team is staying on MVC 5.3 — no modernization, no contract.
- The slice is trivial (one static text label) — prose in a PR description is enough.
- You only have a prose description, no code and no running URL — the skill needs at least one.

## References

- [`goal.md`](goal.md) — agreed goal (verbatim + paraphrase). Source of truth.
- [`references/behavior-taxonomy.md`](references/behavior-taxonomy.md) — twelve core categories + advanced patterns.
- [`references/browser-verification.md`](references/browser-verification.md) — semantic locators, settle windows, network capture timing, both-halves observation, per-behavior probe sequences.
- [`references/code-pending-mode.md`](references/code-pending-mode.md) — narrow contingency when source is temporarily unavailable.
- [`references/cross-slice-context.md`](references/cross-slice-context.md) — global context selectors, propagation modes, scoping declarations.
- [`assets/artifact-template.md`](assets/artifact-template.md) — rich frontmatter schema accommodating all observed behavior types.
