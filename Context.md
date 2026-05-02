# Context.md — session-resumable context

A fresh Claude session can read this to pick up the active workstream without re-deriving goals or re-arguing framing. Complements `CLAUDE.md` (which holds editorial standards). This file captures the **goals and agreements** for whatever skill is currently in progress.

---

## Active workstream

**Skill:** `plugins/dotnet-48/skills/mvc-ui-behaviors/`

### Primary goal

Write a skill that takes one **slice** (a coherent user-visible feature) of an ASP.NET MVC 5.3 (.NET Framework 4.8) application and produces a **structured behavioral artifact** another LLM session uses to re-implement the same feature in modern ASP.NET Core MVC 10 — without that session ever reading the legacy code.

### Inputs

- Legacy source code (Razor views, view models, controllers, helpers, bundled JS).
- A running, authenticated browser session against the live app (current target: ALIS at `https://supersecret.alisonline.com`).

Both are first-class. Source-pending (browser-first when source isn't yet on disk) is a narrow contingency, not a co-equal mode.

### Two-step workflow — both steps are first-class

**Step 1 — read code:** produce a *structural skeleton* of the slice. The skeleton is incomplete by construction — JS-driven and emergent behaviors aren't visible at this stage (bundled `.js`, inline `<script>`, custom jQuery validation adapters, `data-*`-driven runtime behavior, third-party widget configurations, SignalR client subscriptions, server-side conditional branches). Step 1 alone *will* leave critical behaviors out.

**Step 2 — browser:** four roles, not one.

1. **Discover** — find behaviors step 1 couldn't see. Most important role; if step 2 surfaces no surprises, it wasn't exercised hard enough.
2. **Verify** — confirm what step 1 captured.
3. **Improve** — sharpen wording, remove ambiguity.
4. **Enrich** — add timing windows, choreography of side effects, cross-slice nuance.

### What goes IN the artifact

- Cause → effect pairs the user can observe.
- Server-side **data contract**: endpoints (method, URL/route, payload shape, response shape, response kind, error shape, anti-forgery requirement, conceptual purpose).
- Server-side **semantics** — the "how" that's behavior, not syntax: selection rules ("only residents on premise in the current community"), authorization rules ("only residents the current user can access"), computed / projected fields, default ordering, pagination contract, soft-delete and temporal scoping.
- **Field display rules** — which fields appear, formatting, fallback for empty (*"shows 'Information not filled in' when null"*).
- **Cross-slice links** by slice id; `scoped_by` for slices that depend on a global context selector.
- **Timing characteristics** — settle windows for SignalR pushes, debounce thresholds, polling intervals.

### What stays OUT

- DOM ids, CSS classes, ARIA assumptions, jQuery selectors.
- Widget-library names (Syncfusion, Kendo, Bootstrap).
- Implementation syntax: LINQ/EF queries, specific class/method/repository names, IoC bindings, attribute filter implementations.

**The line: semantics travel, syntax doesn't.**

### Outputs

One Markdown artifact per slice (YAML frontmatter + Markdown body). Format chosen for downstream LLM consumption. Stands alone — the rewrite session implements from the artifact, not the legacy code.

**Dual purpose:** the artifact is the contract for the rewrite *and* the regression seed for verifying the rewrite reproduces the behaviors. Same claims, same verification machinery, pointed at the modern implementation.

### Operating principles for the skill itself

- **Browser work must be repeatable / semi-deterministic.** Step 2 is not free-form exploration — it's a concrete procedure two sessions can execute and arrive at equivalent claims. The skill prescribes probe sequences (arm network → locate by visible label → trigger → wait the documented settle window → re-observe → diff against prior state) so the verify/improve/enrich loop is automatable, not improvisational.
- **Don't over-specify the surrounding workflow.** The skill provides framework + judgment cues for slice identification, classification, and prose phrasing. The browser playbook is the prescriptive part; the rest exercises judgment within the framework.
- **Taxonomy is a framework, not a checklist.** When a slice surfaces a behavior the categories don't cover, extend the schema explicitly and ask the user — don't shoehorn into the closest existing category.
- **Privacy-first.** Observed real data (resident names, room numbers, dates of birth, etc.) never appears in artifact prose. Genericize. Specifics live only in the verification log for human reviewers.
- **Domain.** Senior Living. Real residents, real care, regulated industry. Extreme ownership: claims must be defensible against evidence; nothing speculative passes as verified.

### Reviewer

All skill work is reviewed by Codex (adversarial). Build for that review.

---

## Files of interest

- `plugins/dotnet-48/skills/mvc-ui-behaviors/SKILL.md`
- `plugins/dotnet-48/skills/mvc-ui-behaviors/references/behavior-taxonomy.md`
- `plugins/dotnet-48/skills/mvc-ui-behaviors/references/browser-verification.md`
- `plugins/dotnet-48/skills/mvc-ui-behaviors/references/code-pending-mode.md`
- `plugins/dotnet-48/skills/mvc-ui-behaviors/references/cross-slice-context.md`
- `plugins/dotnet-48/skills/mvc-ui-behaviors/assets/artifact-template.md`

---

## Taxonomy validation status

The behavior taxonomy has two tiers. The **core 12** were grounded in the initial ALIS walk and are kept. The **advanced** categories were added speculatively and are being validated against real observations one round at a time.

### Core 12 — grounded

| Category | Evidence |
|---|---|
| Population | Care Tracking shift list bound to model; community dropdown via `ViewBag` |
| State change | Care Tracking *Completed* button: visual + local mutation, no AJAX, then toolbar commit fires the POST |
| Validation | Create Applicant modal: *"First Name is required"* etc., pink-bg field state, no POST until valid |
| Navigation | Modal-via-AJAX (`?X-Requested-With=XMLHttpRequest`), full-page Schedule Leave wizard, tabs via `?tab=` |
| Reactivity | "Show Recorded" toggle materializing after first record action — conditional presence confirmed |
| Submission | Care Tracking deferred batch; *Create* vs *Create and Go To* multi-action submit |
| Result handling | Top-right success toast on record |
| Filter / sort / page | Residents grid: multi-select Resident filter with checkboxes + search + Select all |
| Time-dependent | **SignalR-driven counter advance with 1–3 s settle window** — Care Tracking `stafftaskshub` |
| Multi-tenant context | Community selector affecting all scoped slices |
| Cross-slice signaling | Toast bus on Care Tracking record; SignalR fan-out |

### Advanced — pending or partially validated

| Category | Status |
|---|---|
| Composite / derived state | Pending — validate via Manage Orders or financial dashboards |
| Drag-drop / reordering | Pending — validate via Calendar or sortable lists |
| Concurrent editing | Pending — likely visible only via two-session test |
| Long-running operations | Pending — validate via Emergency Packet or report exports |
| Export / print | Pending — validate via Print Residents / Emergency Packet |
| Multimodal input | Partially observed (microphone icon on care notes textarea) |
| Activity / audit timelines | Pending — likely on resident profile or *Report Card* |
| Workflow / approval state machines | Pending — validate via *+ Incident* flow |
| Composite slices | Concept; refine via a real complex example |

After each validation round, a category lands in: **confirmed** (kept, refined with evidence), **adjusted** (rewritten in the shape it actually appears), or **pruned** (removed with a note explaining absence).
