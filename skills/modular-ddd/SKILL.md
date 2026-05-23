---
name: modular-ddd
description: >
  Decide where DDD tactical patterns (aggregates, value objects, domain
  services, specifications) earn their keep in a .NET 10 modular monolith,
  and where they are ceremony. One tool in the modular-monolith toolbox
  — reach for it to decide the internal shape of a module after the
  topology is set.
  Use when the user asks "should this be an aggregate", "do I need a value
  object here", "is this a domain service or an application service",
  "where do invariants live", or reaches for DDD vocabulary on a
  CRUD-shaped module. Status: placeholder — uses the orchestrator's
  working summary until deep content is written. Target stack: .NET 10 /
  C# 14 / EF Core 10.
---

# modular-ddd

> **Status — placeholder.** Scaffolded; deep content to be written. For the working methodology today, use [`modular-monolith`](../modular-monolith/SKILL.md) — this skill is one tool in its toolbox.

## Problem

DDD tactical patterns are tools. Used where invariants live, they protect the domain from corruption and concentrate change. Used where the data is CRUD-shaped, they are bureaucracy: a `ResidentAggregate` whose only invariant is "field is non-null" is a class that does the same thing as an EF Core entity, more slowly, with more code, and with worse tooling. The job of this skill is to tell the two cases apart and call each by the right name.

The decision is per-module, sometimes per-feature. A module can be aggregate-rooted at its core and have CRUD-shaped peripheral features. Most real designs are mixed; treating every module as fully DDD or fully CRUD loses information.

## Audience

Engineers on .NET 10 deciding the internal shape of a module after the topology has been drawn (`modular-design`) and the cross-module language has been aligned (`modular-shared-language`).

## Inputs (planned)

- Module list from `modular-design`.
- Shared-language map from `modular-shared-language`.
- The invariants the business cares about (often surfaced by interviewing the SME or reading existing validation rules).

## Outputs (planned)

- Per-module shape decision: aggregate-rooted, service-with-records, or thin-pass-through.
- For aggregate-rooted modules: aggregate boundary, root entity, invariants, domain events emitted.
- For service-with-records modules: the EF Core entities, the application services, the request handlers.
- An explicit list of *refused* DDD patterns and the reason for each refusal.

## Sections to be written

- [ ] Three module shapes — aggregate-rooted, service-with-records, thin-pass-through
- [ ] When an aggregate is earned — three signs you need one, three signs you don't
- [ ] Value objects in C# 14 — `record struct`, equality, validation in the constructor, `IParsable<T>`
- [ ] Domain events as the in-tenant cross-module dispatch (links to discriminator in orchestrator)
- [ ] Specifications and the query-shape question — when to use, when `IQueryable` is enough
- [ ] Anti-corruption layers — when, where, how (relates to `modular-shared-language`)
- [ ] Refusing DDD where it does not earn rent — concrete examples
- [ ] Worked example: an aggregate-rooted *Compliance* module side-by-side with a service-with-records *Notes* module

## See also

- [`modular-monolith`](../modular-monolith/SKILL.md) — orchestrator; this skill is one tool in its toolbox
- [`modular-shared-language`](../modular-shared-language/SKILL.md) — feeds this skill the term map that decides where ACLs live
- [`modular-vertical-slice`](../modular-vertical-slice/SKILL.md) — organizes features inside the shape this skill decides
- [`modular-ddd-classifier`](../modular-ddd-classifier/SKILL.md) — uses this skill's vocabulary to label legacy code
