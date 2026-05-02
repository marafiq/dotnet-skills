# goal.md — agreed goal for `mvc-ui-behaviors`

Two sections:

1. **Restated framing (paraphrase) — Agreed Goal** — fast scan.
2. **Verbatim agreement (history)** — the owner's own words. **Where the two diverge, the verbatim section wins.**

---

## 1. Restated framing (paraphrase) — Agreed Goal

### Primary goal

Write a skill that takes one **slice** (a coherent user-visible feature) of an ASP.NET MVC 5.3 (.NET Framework 4.8) application and produces a **structured behavioral artifact** that another LLM session uses to re-implement the same feature in modern ASP.NET Core MVC 10.

The artifact is the **answer**. Even when the rewrite session can read the legacy code, the artifact must carry every concrete behavior and the supporting server-side **business logic** — backed by evidence — so the rewrite session does not have to re-derive what the legacy app does. Don't shift burden to the rewrite by underspecifying.

### Inputs

- Legacy source code (Razor views, view models, controllers, helpers, bundled JS).
- Authenticated browser session against the running app (current target: ALIS).

Both first-class.

### Two-step workflow

**Step 1 — read code with the best available tools.** Use the C# language server (e.g. `csharp-lsp`) for symbol navigation, find-references, find-implementations. Trace helpers to their definitions; follow partial-view chains; resolve model bindings; identify event handler attachments. Naive grep is the fallback, not the default. The output is a *structural skeleton* — necessarily incomplete because JS-driven and emergent behavior aren't fully visible in source.

**Step 2 — browser, five rules** (was four):

1. **Discover** — find behaviors step 1 couldn't see (JS handlers, runtime data-* manipulation, third-party widget defaults, server-pushed updates, server-side conditional branches that didn't fire in your read path).
2. **Verify** — confirm what step 1 captured.
3. **Improve** — sharpen wording / remove ambiguity even when claims pass.
4. **Enrich** — add timing windows, choreography of side effects, cross-slice nuance, dynamic content variants.
5. **Run a semi-deterministic / automatic probe sequence** where the slice allows it. Two sessions exercising the same slice should produce equivalent claims. The browser is a first-class probe surface, not a verification afterthought. The trade-off between procedural rigor and creative judgment is per slice (a required-field check is highly automatable; a multi-actor workflow involves more judgment).

### Scope of the artifact — concrete, evidence-backed behavior

The artifact captures the **specific** behavior, not abstractions about behavior categories.

**In:**

- Cause → effect pairs the user observes.
- **Concrete server-side business logic** — the rules that determine what records load, what fields surface, how authorization filters, ordering, paging, soft-delete, and temporal scoping are applied. State the rules specifically:
  - *"Returns residents where `Community.Id == currentCommunity` AND `Status == 'OnPremise'` AND `IsArchived == false`, sorted by `LastName` ASC then `FirstName` ASC, paged 25 per page; with computed fields: `CareLevel` resolved from the resident's primary `CarePlan`; `CompliancePct` = completed / total tasks in the last 30 days."*
  - **Backed by evidence** — the artifact's `code_refs` point to where the rules live (`path:line` or `path:symbol`). The rewrite session reproduces the rules in whatever data layer it picks; the rules themselves are the contract.
- Endpoint contract: HTTP method, URL/route, request payload shape, response shape, error shape, anti-forgery requirement, conceptual purpose.
- The `routes:` field — URL routes where the user encounters the slice (same in legacy and rewrite, since the .NET 10 rewrite preserves routes). Pattern syntax: `/Residents/Profiles/{residentId}`.
- Field display rules: which fields appear, formatting, empty-state fallback (*"shows 'Information not filled in' when null"*).
- User-visible side effects beyond the immediate UI mutation: audit-history rows that surface in an "Activity" panel, emails / notifications a state-change triggers, search-index updates the user notices later. Captured because the rewrite must reproduce them.
- Cross-slice links by slice id; `scoped_by` for global context dependencies (community / facility / locale selectors).
- Timing characteristics: settle windows for SignalR pushes, debounce thresholds, polling intervals.

**Out:**

- DOM ids, CSS classes, ARIA assumptions, jQuery selectors.
- Widget-library names (Syncfusion, Kendo, Bootstrap).
- Implementation syntax: LINQ / EF query expressions, specific class / method / repository names, IoC bindings, attribute filter implementations, framework primitives the rewrite picks for itself.
- Logs, OpenTelemetry, metrics — observability is the project's call. Business logic is in scope; pure log-only telemetry is not.
- Localization machinery — the application is **English only**; visible message text in the artifact is captured verbatim, no resource keys or template/locale fields needed.

**The line:** behavior facts, server-side business logic, and data contracts travel; implementation syntax doesn't.

### Boundary with project-context

The rewrite session reads the slice artifact alongside a per-project `project-context.md` (provided separately, not by this skill). That project-context document covers concerns the artifact stays silent on:

- Cross-cutting framework choices: validation library, DI lifetimes, logger framework, middleware order, ProblemDetails shape, anti-forgery wiring.
- Identity-provider integration and the canonical role/permission vocabulary the rewrite uses.
- Data-layer choice (EF Core 10 vs Dapper vs other). The database **schema is preserved** in the rewrite, so the artifact's `code_refs` to table/column names remain accurate; the layer choice is the rewrite's.
- House style for error UX (toast / inline / ProblemDetails). The artifact captures the legacy error UX plus a `rewrite_intent` flag per legacy quirk; the new house style is the project's.
- Acceptance criteria: a11y target, mobile breakpoints, performance budgets, browser support.
- Rollout strategy: feature flag, shadow deployment, big-bang.
- Cross-slice dependency *order* — the artifact captures `scoped_by` and `related_controls` per slice; project-context decides which slices to migrate first.

The skill is silent on all of these. The artifact references them only by `scoped_by` / `code_refs` where applicable.

### Output

One Markdown artifact per slice (YAML frontmatter + Markdown body). Stands alone — the rewrite session reads it as the answer, alongside the project-context document.

**Dual-purpose:** the artifact is the **contract** for the rewrite **and** the **regression seed** for verifying the rewrite reproduces the legacy behaviors.

### Skill evolution discipline

The skill is meant to learn over time — real patterns observed in this app (and others) should refine the taxonomy and the artifact schema. The skill must NOT be corrupted by treating every one-off observation as a pattern.

A pattern enters the skill only when:

1. **Facts establish it** — multiple observations or strong evidence; not a single anecdote.
2. **The reviewer agrees** — Codex (adversarial review) passes the addition.

A one-off observation belongs in the slice's artifact (its `## Edge cases` or verification log), where it informs future pattern candidates without prematurely reshaping the skill.

### Operating principles

- **Inputs:** source code + authenticated browser. Both first-class. Source-pending mode is a narrow contingency.
- **Environment:** beta — destructive actions permitted with reasonable restraint.
- **Reviewer:** Codex (adversarial). Build for that review.
- **Domain:** Senior Living. Real residents, real care, regulated industry. Extreme ownership: claims are defensible against evidence; nothing speculative passes as verified.
- **Process:** explore incrementally, improve skill incrementally, ask one question at a time when uncertain about goal, **always commit when a goal is reached**.
- **Scale:** the target app has ~5,000 routes. The skill must be invokable on any of them consistently. Don't try to brute-force-validate every taxonomy category by exploration; validate the framework's robustness via a few real probes.

---

## 2. Verbatim agreement (history)

The owner's statements, quoted to prevent paraphrase drift, in the order they were made.

### Original goal statement

> Write a skill that can produce a structured artifact with all the behaviors of a feature (slice) in the application written using ASP.NET MVC 5.3 in .NET Framework 4.8 — You will have access to the source code, and browser where real application is running. I proposed two step process, read code, and write structure artifact for the behaviors, then same artifact is verified, improved, enriched without mixing dom ids and likes. Focus on the behavior. This artifact will be used to re-write the UI in modern stack.

### Correction 1 — server-side scope

I drew the line wrong by saying *"never how it's implemented (specific classes, EF queries, interceptor pipelines, IoC bindings)."* Owner's correction:

> I understand from where you coming from but its wrong; Lets take an example, if a grid is showing records, I need to know server side behavior — what kinds of residents are loaded, and what fields are displayed and how. Now how part is behavior not exact query.

### Correction 2 — testing framing

I phrased a non-goal as *"Not a complete UI testing methodology — verification serves the contract, not regression QA."* Owner's correction:

> Framing wrong, yes it is not about testing, but at the same time, modern app will have all the behaviors.

### Correction 3 — step 2 is not contingency

> I repeat step 2 is not contigency, verify, improved, enriched. what you understand from these words

### Correction 4 — code reading alone misses JS-driven behaviors

> But if you missed nuances of UI because there is JS involved sometime, then if solely relying on your code reading will leave critical behaviors out.

### Correction 5 — meaning of "automatic"

I misread "automatic" as autonomy. Owner's correction:

> Now when I say automatic, you should think of writing repeatable/automatic/semi-determistic way to work in browser with in the context of this skill.

### Correction 6 — "should" not "must"; trade-off per slice

> I used the work should - there is nuance diff in making the trade off given a feature/slice you are working on

### Constraint 7 — scale

> This app you saw in browser has 5000 routes, so think in right frame of mind, and try to engage with right amount weigtage on my nuances

### Addition 8 — best tools for code reading

> SKILL must use best tools to read code, like csharp lsp or best way to read mvc code

### Addition 9 — skill learning discipline

> SKILL must keep learning and identifying patterns and update itself but only after pattern is established by facts and reviewer ( be aware of corrupting the skill with every thing as pattern)

### Addition 10 — 5th browser rule

> Browser - four rules - there should be 5th rule, with clear wording of have a semi-determistic/automatic way (i refain from determistc because that can push you in wrong direction, i have provided you access to the app)

### Correction 11 — "semantics" framing too weak

I framed scope as *"Server-side semantics — selection rules, authorization rules, computed / projected fields, default ordering, pagination, soft-delete and temporal scoping. Field display rules (which fields, formatting, empty-state fallback)."* Owner's correction:

> is week. Because you are imply semantics as some high level thing, again example, if you semantic = behavior, load the residents in community x, with status active, and so on... modern app can not invent these without you reading code, and give that information in artifact, baced by evidence. Modern will have access to code, but do not use this an excuse to throw burden on other side.

### Clarification 12 — English-only application

> localization_keys - does not exist, its english only.

Agreed: no `localization_keys`, no `message_template` field. Visible message text is captured verbatim.

### Clarification 13 — same routes, same DB schema; other choices vary

> .NET 10 re-write will have same route, same database schema. But other choices will vary, and is not concern of this skill.

Agreed: drop the `legacy_` prefix on the routes field — it's just `routes`. Database schema is preserved, so artifact `code_refs` to table/column names stay accurate. Cross-cutting framework / identity / data-layer / a11y / rollout choices are project-context, not the skill's concern.

### Clarification 14 — "business logic" terminology; logs / OTel out, business logic in

> logs or otel is out of scope. But server side busines logic is not out of scope, i kept calling it behavior perhaps business logic to load resident must be produced in artifact

Agreed: server-side business logic that determines what records load and what fields surface is **first-class artifact content**, captured concretely with evidence (`code_refs`). Logs / OpenTelemetry / metrics are out of scope.

### Operating directives (running)

- Inputs: source code AND running authenticated browser.
- Beta environment; destructive actions permitted (*"u can create edit remove data using the app as this is beta"*).
- Reviewer: Codex (adversarial).
- Domain: Senior Living. Extreme ownership.
- Always commit when a goal is reached.
- If unsure about goal, ask one question at a time before starting work.
