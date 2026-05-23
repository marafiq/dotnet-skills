---
name: modular-design
description: >
  Inventory candidate modules in a .NET 10 modular monolith — name them,
  draw the dependency graph, decide module physicality (.csproj vs folder
  vs namespace). One tool in the modular-monolith toolbox — reach for
  it to decide what the modules ARE and what they are called. Use when the
  user asks "how should I split this into modules", "what should this
  module be called", "should this be a separate project", or any
  topology-shaping question. Status: placeholder — uses the orchestrator's
  working summary until deep content is written. Target stack: .NET 10 /
  C# 14 / ASP.NET Core MVC 10.
---

# modular-design

> **Status — placeholder.** Scaffolded; deep content to be written. For the working methodology today, use [`modular-monolith`](../modular-monolith/SKILL.md) — this skill is one tool in its toolbox.

## Problem

Modular design starts with a list. Without a written inventory of candidate modules and the dependencies between them, every later decision (where DDD earns rent, how to slice features, what crosses a boundary) is made against imagined topology. The first artifact a design produces is a *map*: module names, what each module owns, which other modules each one calls.

Naming and physicality are the two hard parts. Names compress the design's intent into a single word that downstream code, tests, and conversation will inherit; bad names cost forever. Physicality — `.csproj` vs folder vs namespace — trades build-time isolation for friction; the right level depends on team size, enforcement strategy, and whether modules need to build independently.

## Audience

Engineers on .NET 10 in the planning phase of a new feature area or a legacy modernization carve-out, working in C# 14.

## Inputs (planned)

- Functional scope of the design area (the features in scope).
- Optional: `modular-ddd-classifier` artifact when modernizing from .NET 4.8 source.

## Outputs (planned)

- Module inventory table: name, one-line description, primary tables/types owned, public surface summary, callers.
- Dependency graph (Mermaid or DOT) of cross-module edges.
- Physicality decision per module with rationale (project / folder / namespace).

## Sections to be written

- [ ] Naming heuristics — domain-term-first, verb-vs-noun, names to avoid (`Common`, `Shared`, `Utils`, `Manager`, `Helper`)
- [ ] The dependency graph — how to draw it, what counts as an edge, how to read it
- [ ] Physicality decision tree — `.csproj` vs folder vs namespace; when each pays for itself
- [ ] Enforcement options — ArchUnitNET, Roslyn analyzers, source generators, convention-with-review
- [ ] Worked example: designing a new *Compliance* module end-to-end (using the orchestrator's running example)
- [ ] Hand-off shape — what `modular-shared-language` consumes from this artifact

## See also

- [`modular-monolith`](../modular-monolith/SKILL.md) — orchestrator; this skill is one tool in its toolbox
- [`modular-shared-language`](../modular-shared-language/SKILL.md) — aligns terms across the modules this skill names
- [`modular-ddd-classifier`](../modular-ddd-classifier/SKILL.md) — produces the legacy-mining artifact this skill consumes when modernizing
- [`modular-coupling-cohesion`](../modular-coupling-cohesion/SKILL.md) — validates the topology this skill produces
