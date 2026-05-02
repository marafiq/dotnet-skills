# goal.md — agreed goal for `mvc-ui-behaviors`

This file records the agreed goal, in the repo owner's own words, plus a structured paraphrase for fast scanning. **Where the two diverge, the verbatim section wins.**

---

## 1. Verbatim agreement

The owner's statements, dated as agreed during skill design. Quoted to prevent paraphrase drift.

### Original goal statement

> Write a skill that can produce a structured artifact with all the behaviors of a feature (slice) in the application written using ASP.NET MVC 5.3 in .NET Framework 4.8 — You will have access to the source code, and browser where real application is running. I proposed two step process, read code, and write structure artifact for the behaviors, then same artifact is verified, improved, enriched without mixing dom ids and likes. Focus on the behavior. This artifact will be used to re-write the UI in modern stack.

### Correction 1 — server-side scope

I drew the line wrong by saying *"never how it's implemented (specific classes, EF queries, interceptor pipelines, IoC bindings)"*. The owner corrected:

> I understand from where you coming from but its wrong; Lets take an example, if a grid is showing records, I need to know server side behavior — what kinds of residents are loaded, and what fields are displayed and how. Now how part is behavior not exact query.

**Agreed line: semantics travel, syntax doesn't.** Selection rules, authorization rules, computed/projected fields, default ordering, pagination contract, soft-delete and temporal scoping — all behavior. Specific LINQ/EF query syntax, class/method/repository names, IoC bindings — implementation, not part of the contract.

### Correction 2 — testing framing

I phrased a non-goal as *"Not a complete UI testing methodology — verification serves the contract, not regression QA."* The owner corrected:

> Framing wrong, yes it is not about testing, but at the same time, modern app will have all the behaviors.

**Agreed:** the artifact's claims are dual-purpose — contract for the rewrite **and** regression seed for verifying the rewrite reproduces the behaviors. Same claims, same verification machinery, pointed at the modern implementation.

### Correction 3 — step 2 is not a contingency

> I repeat step 2 is not contigency, verify, improved, enriched. what you understand from these words

**Agreed:** step 2 has three distinct roles, not one.

- **Verify** — confirm the draft's claims against the running app.
- **Improve** — sharpen wording / remove ambiguity even when a claim verifies.
- **Enrich** — add behaviors the artifact didn't have because code-reading couldn't surface them.

### Correction 4 — code reading alone misses JS-driven behaviors

> But if you missed nuances of UI because there is JS involved sometime, then if solely relying on your code reading will leave critical behaviors out.

**Agreed:** step 1 produces a *structural skeleton* — necessarily incomplete. Step 2's most important role is **discover** — finding behaviors that aren't visible in source (bundled `.js`, inline `<script>`, custom jQuery validators, `data-*`-driven runtime behavior, third-party widget configurations, SignalR client subscriptions, server-side conditional branches). If step 2 surfaces no surprises, it wasn't exercised hard enough.

### Correction 5 — meaning of "automatic"

I misread "automatic" as autonomy. Owner clarified:

> Now when I say automatic, you should think of writing repeatable/automatic/semi-determistic way to work in browser with in the context of this skill.

**Agreed:** the browser-work part of step 2 should be a concrete, repeatable, semi-deterministic procedure (probe sequences: arm network → locate by visible label → trigger → wait the documented settle window → re-observe → diff). Two sessions exercising the same slice should produce equivalent claims.

### Correction 6 — "should" not "must"; trade-off per slice

> I used the work should - there is nuance diff in making the trade off given a feature/slice you are working on

**Agreed:** the procedural nature of step 2 *should* hold where the slice allows it, not as a blanket *must*. A required-field validation is highly automatable; a timing-sensitive multi-actor workflow involves more judgment. The trade-off is per slice, not a flat rule.

### Constraint — scale of the target app

> This app you saw in browser has 5000 routes, so think in right frame of mind, and try to engage with right amount weigtage on my nuances

**Implication:** the skill must scale — invokable on any of those 5,000 routes consistently. Don't try to brute-force-validate every taxonomy category by exploration; validate the framework's robustness via a few real probes and trust that the framework + judgment cues handle the rest.

### Operating directives

- **Inputs:** source code **and** authenticated browser session — both first-class. Source-pending mode is a narrow contingency, not co-equal.
- **Environment:** beta — destructive actions permitted with reasonable restraint (*"u can create edit remove data using the app as this is beta"*).
- **Reviewer:** all skill work is reviewed by Codex (adversarial).
- **Domain:** Senior Living. Real residents, real care, regulated industry. Extreme ownership: claims must be defensible against evidence; nothing speculative passes as verified.
- **Process:** explore incrementally, improve skill incrementally; ask one question at a time when uncertain about goal; **always commit when a goal is reached**.

---

## 2. Restated framing (paraphrase)

This is my structured restatement for fast scan. **It is paraphrase**; if it conflicts with the verbatim section above, the verbatim section wins.

### Primary goal

Write a skill that takes one **slice** (a coherent user-visible feature) of an ASP.NET MVC 5.3 (.NET Framework 4.8) application and produces a **structured behavioral artifact** another LLM session uses to re-implement the same feature in modern ASP.NET Core MVC 10 — without that session ever reading the legacy code.

### Two-step workflow

**Step 1 — read code:** produce a structural skeleton from source. Incomplete by construction. JS-driven and emergent behaviors aren't visible at this stage. Step 1 alone *will* leave critical behaviors out.

**Step 2 — browser:** four roles.

1. **Discover** — find behaviors step 1 couldn't see. Most important.
2. **Verify** — confirm what step 1 captured.
3. **Improve** — sharpen wording.
4. **Enrich** — add timing windows, choreography, cross-slice nuance.

The browser-work component should be a repeatable, semi-deterministic probe sequence where the slice allows it. Trade-off is per slice.

### Scope of the artifact

In:

- Cause → effect pairs the user can observe.
- Server-side data contract: endpoints (method, URL, payload, response, error shape, anti-forgery).
- Server-side **semantics** — selection rules, authorization rules, computed / projected fields, default ordering, pagination, soft-delete and temporal scoping.
- Field display rules (which fields, formatting, empty-state fallback).
- Cross-slice links by slice id; `scoped_by` for global context dependencies.
- Timing characteristics (settle windows, debounces, polling intervals).

Out:

- DOM ids, CSS classes, ARIA assumptions, jQuery selectors.
- Widget-library names (Syncfusion, Kendo, Bootstrap).
- Implementation syntax: LINQ/EF queries, specific class/method/repository names, IoC bindings, attribute filter implementations.

### Output

One Markdown artifact per slice (YAML frontmatter + Markdown body). Stands alone — the rewrite session implements from the artifact without legacy code access. Dual-purpose: contract for the rewrite **and** regression seed.

### Files of interest in this skill

- `SKILL.md` — workflow, two-step process, scope.
- `references/behavior-taxonomy.md` — twelve core categories + advanced patterns.
- `references/browser-verification.md` — semantic locators, settle windows, per-behavior probe sequences.
- `references/code-pending-mode.md` — narrow contingency when source isn't yet on disk.
- `references/cross-slice-context.md` — multi-tenant scoping and propagation.
- `assets/artifact-template.md` — rich frontmatter schema.

### Validation status of advanced taxonomy categories

Tracked in commit history; updated each time a category is grounded in observation, adjusted, or pruned.
