---
name: modular-ddd-classifier
description: >
  Classify a legacy .NET Framework 4.8 codebase one feature slice at a
  time, producing a reviewable artifact that names candidate modules,
  *deep modules* (Ousterhout sense — narrow public surface, deep
  implementation) and *hierarchical / nested modules* (parent-of-children),
  dependency relationships, and touched areas. The classifier is a
  *deep-thinking interactive skill*: it studies legacy code carefully,
  asks one question at a time, builds shared understanding with whoever
  knows the slice, and embraces progressive disclosure — a 2.5-million-line
  codebase cannot be held in any one head, so the classifier works slice
  by slice and accumulates a map. Pragmatic, not dogmatic; uses Ousterhout
  depth or nesting hierarchy or both, whichever lens fits the slice.
  Pairs with `dotnet-48:mvc-ui-behaviors` (behavioral classification)
  during modernization. Use when the user asks "what natural module seams
  already exist", "find deep modules in our .NET 4.8 source", "carve a
  new module out of MVC 5", "what bounded contexts are hiding in this
  legacy app", "study this legacy slice with me", "let's classify the
  X area", or is planning incremental modernization of legacy ASP.NET
  MVC 5 / Web Forms code into a .NET 10 modular monolith. Status:
  placeholder; the artifact schema and worked examples are to be
  written. Applies to the dotnet-10 plugin (input is .NET 4.8 source;
  output is consumed by .NET 10 modular design).
---

# modular-ddd-classifier

> **Status — placeholder.** Scaffolded; deep content (artifact schema, worked examples, the interactive question protocol) is to be written. For the working methodology today, use [`modular-monolith`](../modular-monolith/SKILL.md). For legacy *behavior* extraction (the sibling concern that captures *what* a slice does), see `dotnet-48:mvc-ui-behaviors`.

## Problem

A 2.5-million-line legacy codebase cannot be classified in a sweep. Static analyzers produce graphs nobody can read; SME interviews without grounding produce wishlists; whole-codebase rewrites die in the first iteration. The only thing that works at this scale is *progressive disclosure*: pick a feature slice, study it deeply with someone who knows it, write down what you found, and move to the next slice. The artifact accumulates over months; the design that emerges is grounded in what actually exists.

The classifier is the *structural* half of that work. Its sibling, `dotnet-48:mvc-ui-behaviors`, is the *behavioral* half. Together they capture both axes for one slice: what the slice does (behavior) and where its module seams live (structure). Using only one half ships a modern module that either misses required behaviors or re-derives the wrong boundaries.

The classifier looks for two kinds of structure simultaneously:

1. **Deep modules** in Ousterhout's sense (*A Philosophy of Software Design*) — code where a *narrow* public surface hides a *deep* implementation. Deep modules are natural module candidates because the future module wraps the same implementation behind the same narrow surface; the rewrite preserves the seam the legacy already has.
2. **Hierarchical / nested modules** — parent modules that contain child sub-modules. A *Care* area might contain *Care.Assessments*, *Care.Goals*, *Care.Notes*. The hierarchy informs both the .csproj/folder layout and the cross-module communication design.

A slice may surface deep modules, hierarchical modules, both, or neither. The classifier names what it finds; it does not force a lens that does not fit.

## Audience

Engineers planning incremental modernization of a large .NET Framework 4.8 codebase into a .NET 10 modular monolith. Comfortable reading C# 8 code with `System.Web` / EF6 / MVC 5 idioms. Working with a domain SME or long-time maintainer who knows the slice's history.

## How the classifier works (planned)

This is an *interactive* skill, not a batch tool. The shape of a session:

1. **Pick one slice.** A feature, a screen, a controller area — small enough that one engineer plus one SME can hold it in their head for an hour.
2. **Read the code carefully.** Static read first: what controllers, what entities, what services, what views. Note touched areas (the files this slice imports from, and the files that import from it).
3. **Ask one question at a time.** Build shared understanding with the SME. Examples: *"What is this `IPmsService` actually responsible for?"*, *"When this method calls into `BillingHelper`, is that an accident of history or a real coupling?"*, *"Has anyone ever extended this safely without touching three other places?"* Record both answers and the questions that the SME could not answer (those are signals too).
4. **Look for deep-module signals.** Narrow public surface, deep implementation, high fan-in, low fan-out. Mark candidates.
5. **Look for hierarchical signals.** A namespace or folder whose contents naturally cluster into sub-areas with shared concepts. Mark parent and proposed children.
6. **Apply DDD-lens labels where they earn rent.** Bounded context candidate, aggregate root candidate, anti-corruption boundary candidate. Refuse the labels where the data is CRUD-shaped and an aggregate would be ceremony.
7. **Write the artifact.** A markdown file per slice with the findings, the unanswered questions, the SME conversation log, and the recommended new-world module shape.
8. **Hand off to `modular-design`.** The artifact is one input among several to the topology design.

The interactive shape matters because the classifier's value is in the conversation, not in the metrics. A static graph of 2.5M LoC tells you nothing useful; a focused conversation with the person who built the slice tells you what the metrics meant.

## Inputs (planned)

- Legacy .NET 4.8 source tree (path) and the slice's identifying artifacts (controllers, views, scripts).
- Optional: existing `mvc-ui-behaviors` artifact for the same slice, for cross-checking that the structural classification agrees with the captured behaviors.
- A domain SME or long-time maintainer available for interactive questioning.
- Optional: previous classifier artifacts for adjacent slices, to check whether candidate modules align across slices.

## Outputs (planned)

A markdown classification artifact per slice, including:

- **Slice identity** — name, scope, the controllers / views / entities it covers.
- **Touched areas** — what this slice imports from, what imports from it, what tables/columns it reads or writes.
- **Candidate modules** — proposed new-world module names with one-line descriptions.
- **Deep-module candidates** — code with narrow public surface and deep implementation; LOC, public-method count, fan-in count, fan-out count.
- **Hierarchical-module candidates** — proposed parent + children groupings.
- **Dependency relationships** — afferent, efferent, and any cycles found in the slice.
- **DDD-lens labels** — entity, value-object, aggregate-root candidate, ACL boundary candidate, applied where they earn rent and explicitly refused where they do not.
- **Open questions** — what the SME could not answer; what the static read could not resolve. These are work items, not embarrassments.
- **Recommended new-world shape** — the module(s) the classifier suggests building, the public surface for each, and the cross-slice interactions to watch for.
- **SME conversation log** — the interactive history that produced the artifact, kept for the next contributor to read.

## Sections to be written

- [ ] Inputs and preconditions — what the source tree needs, what tools to install, what to ask the SME up front
- [ ] Picking a slice — sizing, common mistakes, what to do if a slice is too large
- [ ] The static read — what to look at first, what signals matter, what to ignore
- [ ] The interactive question protocol — the one-question-at-a-time pattern, examples per slice type
- [ ] Deep-module signals — concrete metrics (interface width, implementation depth, fan-in), thresholds, how to count them in C# 8 / MVC 5 source
- [ ] Hierarchical-module signals — namespace clustering, folder structure, repeated naming roots
- [ ] DDD-lens labels — when each one applies to legacy code, when it is ceremony to assign one
- [ ] Output artifact schema — markdown structure, frontmatter fields, naming convention, slice ID format
- [ ] Worked example — a real legacy slice classified end-to-end, with the SME conversation log
- [ ] Hand-off shape — what `modular-design` (the topology consumer) reads from this artifact; what `modular-shared-language` reads
- [ ] Pairing with `mvc-ui-behaviors` — how the structural and behavioral artifacts cross-check each other

## See also

- [`modular-monolith`](../modular-monolith/SKILL.md) — orchestrator; this skill is one tool in its toolbox
- [`modular-design`](../modular-design/SKILL.md) — direct consumer of this skill's output
- [`modular-ddd`](../modular-ddd/SKILL.md) — provides the DDD vocabulary this skill uses as labels
- [`modular-shared-language`](../modular-shared-language/SKILL.md) — pairs with this skill when the legacy already encodes term conflicts
- [`modular-coupling-cohesion`](../modular-coupling-cohesion/SKILL.md) — uses this skill's dependency findings to validate the proposed new-world topology
- `dotnet-48:mvc-ui-behaviors` — sibling extraction skill in the legacy plugin; pair the two during modernization (behaviors are the *what*, this artifact is the *where* and the *seams*)
