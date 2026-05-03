---
name: modular-shared-language
description: >
  Align the ubiquitous language across modules in a .NET 10 modular monolith
  — list each module's domain terms, mark synonyms and conflicts, decide
  shared meaning vs distinct types with explicit translation, place
  anti-corruption layers where they earn rent. One tool in the
  modular-monolith toolbox — reach for it whenever multiple modules use
  the same domain word and you suspect they do not mean the same thing.
  Use when the user asks "what should this
  type be called", "do these two modules mean the same thing by
  Resident", "where do I put the anti-corruption layer", "do I need an
  ACL between these modules", or surfaces a domain-term conflict between
  modules. Status: placeholder. Applies to the dotnet-10 plugin.
---

# modular-shared-language

> **Status — placeholder.** Scaffolded; deep content to be written. For the working methodology today, use [`modular-monolith`](../modular-monolith/SKILL.md) — this skill is one tool in its toolbox.

## Problem

Modules silently corrupt each other when they use the same word for different things. A `Resident` in *Care* may be the person currently living on premise; a `Resident` in *Billing* may include the responsible party; a `Resident` in *Compliance* may be the person plus their entire care plan history. Three meanings, one word, one type — the type accumulates fields nobody owns and the bugs are silent.

The fix is alignment, not avoidance. List every domain term each module uses, mark the conflicts, then choose per case: *one shared meaning* (all modules agree), *two distinct types with explicit translation* (an anti-corruption layer at the boundary), or *one type with a context marker* (rare; only for genuinely-the-same with cosmetic differences). Done early, this is the cheapest part of the design; done late, it becomes a refactor that touches every module.

## Audience

Engineers and SMEs working through the language used by a feature area before deciding the internal shape of each module (`modular-ddd`).

## Inputs (planned)

- Module inventory from `modular-design`.
- The terms each module uses, gathered from existing code, validation rules, screens, or interview.

## Outputs (planned)

- A term map: term → modules using it → meaning per module → resolution.
- Per cross-module data flow with a translation: source type, target type, mapping (anti-corruption layer).
- A short glossary the team uses in conversation and code.

## Sections to be written

- [ ] Surfacing terms — code, screens, validation, SME interview
- [ ] Conflict patterns — same word/different meaning, different word/same meaning, partial overlap
- [ ] Resolution choices — shared, translated, marked
- [ ] Anti-corruption layers in C# — where they live, how thin they should be, when to skip
- [ ] Glossary maintenance — keeping it alive without ceremony
- [ ] Worked example: aligning *Resident* across *Care*, *Billing*, *Compliance*

## See also

- [`modular-monolith`](../modular-monolith/SKILL.md) — orchestrator; this skill is one tool in its toolbox
- [`modular-design`](../modular-design/SKILL.md) — produces the modules whose language this skill aligns
- [`modular-ddd`](../modular-ddd/SKILL.md) — decides where ACLs and translation live in code
