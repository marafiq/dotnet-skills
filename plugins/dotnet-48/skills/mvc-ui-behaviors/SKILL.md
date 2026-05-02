---
name: mvc-ui-behaviors
description: Use to extract the user-visible BEHAVIORS of a legacy ASP.NET MVC 5.3 application — including conditionally-present slices, deferred batch submits, SignalR-driven counter updates, multi-step wizards, modals loaded via AJAX, cascading dropdowns, multi-tenant scoping, and inline grid editing — into a structured Markdown-per-slice artifact a separate LLM session can consume to re-implement those behaviors in modern ASP.NET Core MVC 10. Two-step skill — read code (~90% of the artifact) AND/OR observe the running app (the ultimate truth, fills the remaining 10% and corrects the rest). Works in code-available mode, code-pending mode (browser-first when source isn't yet on disk), or both. Trigger on phrases like "document this view's behavior", "capture how this drawer works", "spec out this wizard for modernization", "what does this dashboard do when…", "extract behaviors from this MVC 5 page", or any request to translate a legacy MVC interface into a behavioral contract. The skill READS code and OBSERVES the running app — it never writes production code, never relies on DOM ids or CSS classes, and never assumes ARIA semantics that legacy widgets routinely skip. Applies to the dotnet-48 plugin (.NET Framework 4.8 / MVC 5.3).
---

# mvc-ui-behaviors

Extract the user-visible *behavior* of a legacy ASP.NET MVC 5.3 application into a structured artifact that a separate LLM session uses to re-implement the same behavior in ASP.NET Core MVC 10.

## What "behavior" means here

A behavior is a **cause → effect pair the user can observe**. Examples from a real-world app:

- *"When the user types in the resident filter and selects two names, the grid re-renders with only those residents' rows; the request fires to `GET /Residents/{community}?filter=…` and the response replaces the row region."*
- *"When the user clicks Completed on a task, the button highlights green and a Reset link appears; the toolbar's commit button advances by one and changes from disabled-grey to active-green; **no server call fires** — the change is queued locally."*
- *"When the user clicks the toolbar commit, a POST to `/Care/Tracking/{community}/Record/{date}` fires; on 200 a success toast appears top-right; the toolbar reverts to disabled. Then within ~1–3 s, the 'X of Y recorded' counter advances via a SignalR push from the `stafftaskshub` channel — this delayed update is part of the contract."*
- *"After the first record action, a 'Show Recorded' checkbox **materializes** in the toolbar that wasn't there before; the recorded item disappears from the visible list and only reappears when the checkbox is toggled on."*

Behaviors are **not**:

- Visual styling (button colour, padding, fonts) — the rewrite picks its own.
- DOM ids, CSS classes, jQuery selectors — implementation that won't survive.
- ARIA roles or attributes — legacy widgets routinely skip these; the modern app must add them but isn't bound to the legacy markup.
- Server-side internals (which controller method, which EF query, which interceptor).
- The widget library used (Syncfusion, Kendo, Bootstrap) — modern session picks its own.

When in doubt: would the user *notice* if the modern app did this differently? If yes, it's a behavior. If no, it's implementation.

## The unit of work: one slice, one artifact

A *slice* is a coherent user-visible unit. Examples:

- A form with its fields, validation, and submit flow.
- A grid with its filters, sorting, paging, and row actions.
- A dropdown that drives others (cascading parent).
- A modal, drawer, wizard step, accordion panel.
- A toolbar that batch-commits queued changes.
- A region that updates from server pushes.
- A global context selector (community, facility) that scopes every other slice on the page.

Each slice is one Markdown file: rich YAML frontmatter for structured fields + Markdown body for prose claims the verification step exercises. The downstream session is told: *"Implement the behaviors in `<artifact.md>` in ASP.NET Core MVC 10. Do not read the legacy code."* Every artifact must stand alone.

## Workflow

```
1. Identify slices                         (always)
2. Mode A: read code  →  ~90% draft         (when source is on disk)
   Mode B: observe browser  →  partial draft (when source isn't yet)
3. Verify in browser  (always — ultimate truth, including in Mode A)
4. Iterate until every claim verifies      (always)
```

**If the user supplies neither source nor a running URL, ask for one or both.** The skill cannot work from prose alone.

**If you are unsure whether something is a behavior worth capturing, ask the user.** The user knows the modernization scope; you don't. Don't guess at the boundary.

## Preconditions for invocation

- The user supplies authenticated access to the running app — the browser session must already be logged in. The skill does not handle login flows.
- The user supplies at least one of: a starting URL, a path to a legacy view (preferably both — Mode A + verification).
- The user supplies an output directory for artifacts.
- For browser observation: arm `read_network_requests` early; capture starts only after the first call.

## Step 1 — identify slices

Whether you start from a `.cshtml` or a URL, the same heuristic applies:

1. Scroll the page once, top to bottom.
2. List every interactive control (input, button, dropdown, link, toggle).
3. List every region whose contents update as a unit (a list, a grid, a panel, a counter, a status badge that's wired to live data).
4. Look for **conditionally-present** slices — pieces of UI that appear only after a state change (a "Show Recorded" toggle that materializes after the first record action; an error banner that shows only on failure; a "View Impact" wizard step that unlocks after the previous one).
5. Group related controls into one slice when they share a single behavior (a search box + filter dropdowns + a grid that responds to all of them is *one* filter-grid slice, not four — but the toolbar that batch-commits is its own slice that *targets* the grid).
6. Identify **global context selectors** (community / facility / business unit / locale dropdowns). They're slices in their own right, AND every other slice should declare which context it depends on.
7. Identify **cross-slice signaling** — toast regions that any slice can emit into; a global loading overlay; SignalR-driven regions that update without a triggering local action.

Propose the slice list to the user. Don't auto-pick. The user picks priorities.

## Step 2A — Mode A: code-available

When you have the `.cshtml`, the view model, the controller action, and any `.js` referenced by the view:

For each slice, populate the artifact schema. Beyond the obvious (identity, data source, validation) capture the categories that legacy MVC 5 apps express richly. The full taxonomy is in [`references/behavior-taxonomy.md`](references/behavior-taxonomy.md); the highlights are:

- **Population**: model property, ViewBag, action endpoint, hardcoded list, dynamic-from-parent, server push, pre-filled defaults from the server (today's date, current user, last selection).
- **State change**: visual-only / local model mutation / immediate AJAX / **deferred batch via toolbar** / server-push update.
- **Validation**: per-field on blur / on-submit / cross-field / server-only / async-remote — with verbatim message text and the visual error state.
- **Navigation**: page navigation / **modal overlay (URL preserved)** / drawer / wizard step / tab switch (with URL persistence).
- **Reactivity**: cascading / show-hide / enable-disable / **conditional presence** / counter aggregates / region replacement.
- **Submission**: single / **multi-action (Save vs Save-and-Continue)** / batch-via-toolbar / inline-per-row / autosave.
- **Result handling**: toast, modal close + parent refresh, navigation, inline message, validation summary — for both success AND error branches.
- **Filter / sort / page**: server-side vs client-side, multi-sort, URL persistence, combined effects (e.g. filtering resets page to 1).
- **Time-dependent**: loading indicators, debounced search, autosave intervals, **settle windows for SignalR-driven mutations** (counter advances 1–3 s after the AJAX, not synchronously).
- **Auth/role gating**: slice presence by role, action authorization, re-auth requirements.
- **Multi-tenant context**: scoped_by relationships, propagation on context change.
- **Cross-slice signaling**: SignalR / WebSocket / toast bus / refresh propagation.

Claude already knows MVC 5.3 + jQuery Unobtrusive AJAX from training. This skill does not re-teach the framework — it teaches what to *extract* and what to *exclude*.

If you encounter a widget pattern that confuses you, **ask the user before guessing**. Examples worth asking about:

- A custom `Html.MyDropDown` helper with conventions you can't infer from one call site.
- A dropdown that's auto-searchable in some places and not others, with no obvious source distinction.
- A wizard step whose unlock condition lives in JS you can't trace.
- A region that updates from a SignalR Hub method whose source you can't find.

## Step 2B — Mode B: code-pending (browser-first)

Common in real modernization projects: the running app is accessible but legacy source code isn't yet on disk (vendor still owns it, separate repo, etc.). The skill still produces a useful draft. See [`references/code-pending-mode.md`](references/code-pending-mode.md) for the full procedure.

Summary:

1. Identify slices from the running page (no `.cshtml` to open).
2. Observe behaviors directly: trigger interactions, watch the network, watch the DOM and accessibility tree.
3. Draft the artifact with `unknown — fill when source arrives` markers in fields you can't verify by observation alone.
4. Capture observed URL conventions — they hint at the controller landscape (e.g. `/{controller}/{action}/{id}/Pane` → drawer-loaded partial; `X-Requested-With=XMLHttpRequest` header → AJAX-loaded partial HTML).
5. When source arrives, fill in `unknown` markers and re-verify.

Mode B + later code-fill is the typical real-world flow.

## Step 3 — verify in the browser (always)

The browser is the ultimate truth even when you have source. Static analysis misses behavior that lives in scripts you didn't find, in helpers that override defaults invisibly, in stale code paths the app no longer hits, and in server-driven behaviors (SignalR pushes, server-only validation) that don't show up in the view.

Three non-negotiable principles:

1. **Locate by behavior, not structure.** Visible label > surrounding context > action description. ARIA role only when present (legacy widgets routinely skip it).
2. **Observe both halves.** What the user sees AND what the server is asked. A claim verified on only one half is half-verified.
3. **Allow settle windows.** Server-push-driven mutations (SignalR / SSE / polling) don't arrive synchronously. After a POST that triggers a push, wait the documented settle window (typically 1–3 s) and re-check.

See [`references/browser-verification.md`](references/browser-verification.md) for the per-behavior verification playbook.

## Step 4 — iterate

For each verification mismatch:

1. Update the artifact (correct the claim, add a missing edge case, remove a stale claim).
2. Add a `## Verification log` entry: `<date> — <change>`.
3. Re-run only the changed claims.

Stop when every claim is **verified** or has been explicitly reframed as *a requirement on the modern rewrite* (e.g. *"the country dropdown's accessible name must be 'Country'"* — the legacy app fails this; the rewrite must succeed).

## Cross-slice context

Many MVC 5 apps have one or more global selectors (community, facility, fiscal year, locale) that scope every other slice. Capturing this correctly matters because:

- Every scoped slice's data depends on the current selection.
- Switching the selection refreshes (or fully reloads) all scoped slices.
- The context typically survives navigation across pages via session, cookie, or URL.

Treat the context selector as a slice. In every other artifact, set `scoped_by: <context-slice-id>`. See [`references/cross-slice-context.md`](references/cross-slice-context.md) for propagation modes (URL-driven, session-only, soft-refresh, full-page-reload).

## Privacy

In artifact prose, generalize observed data values: *"the currently-selected resident"* not *"Bond, James Jim"*. Specific values, when needed, go only in the verification log (for human reviewers) — never in the artifact's behavioral claims. The artifact is the contract for the rewrite — one tenant's data should not pollute it.

If the running app is a production tenant, ask the user whether they prefer pointing the skill at staging instead.

## Legacy stacks pull off more than people remember

ASP.NET MVC 5 + jQuery + partial views can express remarkably complex behavior: long-running export jobs with SignalR-driven progress bars, real-time-collaborative grids, drag-drop kanban boards, multi-actor approval workflows, audit timelines with diff views, voice input on notes, optimistic-locking conflict resolution. **Don't underestimate the legacy app.** When you encounter something the core taxonomy doesn't cleanly cover (a drag-drop scheduling grid, a workflow state machine, a print-with-polling export), look at the *Advanced behaviors* section of [`references/behavior-taxonomy.md`](references/behavior-taxonomy.md) — and if it's still not there, **extend the schema with new fields and ask the user**. The taxonomy is a framework, not a checklist.

A composite slice (e.g. a real-time-collaborative editable grid) decomposes into multiple categories at once: population + state change + concurrent editing + server push + validation + presence indicators. Capture each dimension as its own entry rather than forcing one category to express everything.

## Skill non-goals

- **No production code.** Read and observe; do not produce C#, Razor, or JavaScript. The downstream session does that.
- **No tutorial on MVC 5 / Unobtrusive AJAX.** Claude already knows them.
- **No DOM ids, CSS classes, ARIA assumptions, or widget library names** in artifact behavioral claims.
- **No exhaustive widget catalog.** When unsure how to represent something, ask. The behavior taxonomy is the framework, not a checklist of every legacy widget.

## When this skill is the wrong tool

- The view is plain HTML with no MVC helpers — there's nothing legacy-specific to extract.
- The team is staying on MVC 5.3 — no modernization, no contract.
- The slice is trivial (one static text label) — prose in a PR description is enough.
- You only have a prose description, no code and no running URL — the skill needs at least one.

## References

- [`references/behavior-taxonomy.md`](references/behavior-taxonomy.md) — twelve behavior categories with examples (population, state change, validation, navigation, reactivity, submission, result, filter/sort/page, time-dependent, auth, multi-tenant, cross-slice signaling).
- [`references/browser-verification.md`](references/browser-verification.md) — semantic locators, settle windows, network capture timing, both-halves observation, per-behavior verification playbook.
- [`references/code-pending-mode.md`](references/code-pending-mode.md) — browser-first workflow when source comes later, `unknown` markers, URL convention capture.
- [`references/cross-slice-context.md`](references/cross-slice-context.md) — global context selectors, propagation modes, scoping declarations, multiple stacked contexts.
- [`assets/artifact-template.md`](assets/artifact-template.md) — rich frontmatter schema accommodating all observed behavior types.
