---
name: modular-solid
description: >
  Apply SOLID at module boundaries in a .NET 10 modular monolith — ISP for
  the public surface (expose only what callers need), DIP for cross-module
  dependencies (consumer-defined interfaces, in the consumer's namespace).
  The other three principles (SRP, OCP, LSP) are class-level concerns and
  are not module-level rules. One tool in the modular-monolith toolbox
  — reach for it when reviewing or refactoring a module's public surface,
  or when deciding where a cross-module interface should live.
  Use when the user asks "what should this module's public API look like",
  "should this interface live in the consumer or the producer", "is this
  module doing too much", "why do we have a Common abstractions library",
  or any boundary-shaping question. Status: placeholder. Applies to the
  dotnet-10 plugin (.NET 10 / C# 14).
---

# modular-solid

> **Status — placeholder.** Scaffolded; deep content to be written. For the working methodology today, use [`modular-monolith`](../modular-monolith/SKILL.md) — this skill is one tool in its toolbox.

## Problem

SOLID is five principles, but only two of them shape modules. The Interface Segregation Principle (ISP) says the public surface a module exposes should contain only the operations its callers actually use — not the union of everything the module can do. The Dependency Inversion Principle (DIP) says cross-module dependencies should go through interfaces the *consumer* defines in the *consumer's* namespace, not interfaces the producer broadcasts.

The other three principles (SRP, OCP, LSP) apply *inside* a module the same way they apply to any C# code. Treating them as module-level rules inflates the architecture conversation and dilutes the two principles that actually decide whether a module is shippable.

The other anti-pattern this skill refuses: the "shared abstractions" library. A `Common.Abstractions` project that holds the interfaces every module both implements and depends on inverts DIP — the consumer no longer owns its dependency, the abstraction-library does. The library accumulates unrelated interfaces, becomes a ball of coupling, and the modules that share it can no longer be reasoned about independently.

## Audience

Engineers on .NET 10 reviewing or refactoring module public surfaces.

## Inputs (planned)

- Module inventory from `modular-design`.
- The internal shape of each module from `modular-ddd`.
- The list of cross-module call sites.

## Outputs (planned)

- Per-module public surface (the C# types and methods other modules touch).
- Per cross-module edge: where the interface lives (consumer-side or producer-side), with rationale.
- A list of leaks (internal types accidentally exposed) flagged for fix.

## Sections to be written

- [ ] ISP at the module surface — what to expose, what to keep `internal`, when `[InternalsVisibleTo]` for tests is appropriate
- [ ] DIP for cross-module calls — the "consumer owns the interface" rule, with concrete C# 14 examples
- [ ] Why SRP, OCP, LSP do not belong in this skill
- [ ] Visibility tools — `internal`, `file`-scoped types, `[InternalsVisibleTo]`, separate `*.Public` projects
- [ ] Worked example: a module surface before and after ISP/DIP cleanup
- [ ] Refusing the "shared abstractions library" anti-pattern

## See also

- [`modular-monolith`](../modular-monolith/SKILL.md) — orchestrator; this skill is one tool in its toolbox
- [`modular-design`](../modular-design/SKILL.md) — produces the surfaces this skill pressure-tests
- [`modular-coupling-cohesion`](../modular-coupling-cohesion/SKILL.md) — measures the cost of bad surfaces in coupling counts
