---
name: modular-coupling-cohesion
description: >
  Measure and validate a modular monolith design in .NET 10 — afferent
  coupling (who calls into this module), efferent coupling (what this
  module calls out to), cohesion (do this module's types address one
  purpose). Names god modules (high efferent coupling everywhere) and
  false splits (two modules coupled tightly enough that they're one
  module pretending). One tool in the modular-monolith toolbox — reach
  for it to validate a design before implementation, or to re-evaluate
  one that has accumulated drift. Use
  when the user asks "is this design coherent", "should I split this
  module", "should I merge these two modules", "is this module doing
  too much", or any post-design validation question. Status:
  placeholder. Applies to the dotnet-10 plugin.
---

# modular-coupling-cohesion

> **Status — placeholder.** Scaffolded; deep content to be written. For the working methodology today, use [`modular-monolith`](../modular-monolith/SKILL.md) — this skill is one tool in its toolbox.

## Problem

A modular design is a hypothesis: the modules as drawn cohere internally and couple to each other in a way the team can live with. Validation makes the hypothesis testable. Without numbers — even rough ones — every disagreement about whether a module is too big, too entangled, or mis-shaped degrades into preference.

Coupling is not zero; that is an isolated module that does nothing. Coupling is *informed*: every cross-module edge has a reason, and the reason is named. Cohesion is the dual: the things this module owns address one purpose. The interesting failures live in the gap — a module with acceptable coupling counts and incoherent ownership (god module), or a module whose nominal split from another is contradicted by constant reach-across (false split).

## Audience

Engineers on .NET 10 running design review on a modular monolith design before implementation, and engineers re-evaluating a design that has accumulated drift.

## Inputs (planned)

- Module inventory and dependency graph from `modular-design`.
- Public surfaces from `modular-solid`.
- Implementation, when available, for measurement on real code.

## Outputs (planned)

- Per-module afferent and efferent coupling counts.
- Cohesion assessment per module (LCOM-style or qualitative).
- Named issues: god modules, false splits, leaky abstractions, dead modules.
- Recommendations (split, merge, rename, redraw a boundary).

## Sections to be written

- [ ] Afferent and efferent coupling — definitions, how to count edges, what to ignore (test-only references, generated code)
- [ ] Cohesion — qualitative checks vs LCOM metrics in C#
- [ ] God modules — recognition, splitting strategy
- [ ] False splits — recognition, merge strategy
- [ ] Cyclic dependencies — why they are a design smell, how to break them with `DomainEvent` inversion
- [ ] Tools — ArchUnitNET, NDepend, Roslyn analyzers, custom Roslyn-based scripts
- [ ] Worked example: validating a six-module design and finding two issues

## See also

- [`modular-monolith`](../modular-monolith/SKILL.md) — orchestrator; this skill is one tool in its toolbox
- [`modular-design`](../modular-design/SKILL.md) — produces the topology this skill measures
- [`modular-solid`](../modular-solid/SKILL.md) — surfaces public-surface issues this skill quantifies in coupling counts
