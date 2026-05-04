---
name: code-usage-knowledge-graph
description: >
  Use when refactoring a C# symbol or concept and you need every usage caught
  reliably (typed C# + Razor `.cshtml` + string-typed name-variants), assembled
  into a structured knowledge graph the LLM navigates before deciding the
  refactor strategy. Closes the gaps that typed find-references misses by
  design — Razor string-typed helpers (`@Html.Partial("X")`, `@RenderSection`,
  `asp-action`), `ViewBag.X`, `ViewData["X"]`, HTML `name=` attributes,
  `[Bind(Include="...")]`, `[JsonProperty("X")]`, EF mapping strings, and
  case-variant drift (`PictureFileName` vs `pictureFileName` vs
  `picturefilename`). Applies to .NET / ASP.NET MVC 5 (legacy .NET Framework
  4.x) and ASP.NET Core MVC code; the scripts have no project-load
  dependency, so they work on legacy and modern solutions alike. Do NOT use
  for refactor execution — Roslynator CLI, IDE refactorings, dotnet format,
  try-convert, and the GitHub Copilot modernization agent already cover that
  downstream.
---

# Code usage discovery + knowledge graph

The skill produces `graph.json` for a concept (e.g. `CatalogItem.PictureFileName`)
that an LLM consults before proposing a refactor. The graph captures every
reference site classified by `kind`, role (read/write/declaration), enclosing
context, and contract boundary status.

## When to invoke

- Refactoring a property, method, controller action, view model, or any C#
  member where missing a single string-typed reference would introduce a
  runtime bug or silent contract break.
- Auditing a legacy MVC 5 or Web Forms surface before modernization.
- Finding aggregate-boundary violations (external mutation sites of a domain
  property).
- Detecting drift smells — multiple casing variants of the same conceptual
  name across the codebase.

## When NOT to invoke

- Typed C# only (no `.cshtml`, no string-typed): use `LSP findReferences`
  through Microsoft.CodeAnalysis.LanguageServer or `roslynator` directly.
- Refactor execution: use Roslynator CLI (`rename-symbol`), IDE refactorings,
  `try-convert`, or the Copilot modernization agent.
- Search across non-.NET languages.

## Workflow

The skill ships four `dotnet-script` files in `scripts/`. Run them in this
order, or invoke `assemble-graph.csx` to run all three scanners and compose
in one shot.

### Step 1 — typed-cs references (Roslyn syntactic walk)

```
dotnet script scripts/typed-cs-refs.csx -- \
  --solution-dir <abs path to project subtree> \
  --member <MemberName> \
  --type   <TypeName> \
  --out    /tmp/typed-cs-refs.json
```

Produces every `IdentifierNameSyntax` whose name matches `--member`, filtered
by type-affinity heuristics when `--type` is set (lambda parameters, foreach
loop variables, object initializers of the right type, bare identifiers
inside the type's own scope). No project load required — works on legacy
.NET Framework 4.x and modern .NET solutions.

### Step 2 — Razor `.cshtml` references

```
dotnet script scripts/razor-refs.csx -- \
  --solution-dir <abs path to project subtree> \
  --symbol <MemberName> \
  --out    /tmp/razor-refs.json
```

Comprehensive Razor scan covering: `@Html.<Helper>For` lambdas (handles
`modelItem => item.X` foreach pattern), `@Html.Partial/RenderPartial/Action/RenderAction`,
`@Url.Action/RouteUrl`, `@RenderSection`, `@Model.X` raw access, `@<id>.X`
property access, `ViewBag.X`, `ViewData["X"]`, ASP.NET Core tag helpers
(`asp-for`, `asp-action`, `asp-controller`), HTML `<input name="X">`, and
Razor directives (`@model`, `@inherits`, `@inject`, `@using`). Comments
(`@*...*@`) preserved with `commented: true` on the matching kind suffix.

See `references/razor-discovery.md` for the full kind taxonomy.

### Step 3 — string-typed references in non-Razor files

```
dotnet script scripts/string-typed-refs.csx -- \
  --solution-dir <abs path to project subtree> \
  --symbol <MemberName> \
  --out    /tmp/string-typed-refs.json
```

Roslyn-aware scan over `.cs` for `[Bind]`, `[JsonProperty]`, `[Display]`,
`nameof()`, and other attribute / invocation contexts containing the
symbol name as a string literal. Plain text scan over `.csv`, `.json`,
`.sql`, `.config`, `.xml` with case-variant detection (PascalCase,
camelCase, lowercase, snake_case, SCREAMING_SNAKE, kebab-case).

`.cshtml` is intentionally excluded here — `razor-refs.csx` is the
authoritative `.cshtml` scanner with proper kind classification.

### Step 4 — assemble the knowledge graph

```
dotnet script scripts/assemble-graph.csx -- \
  --solution-dir <abs path to project subtree> \
  --type   <TypeName> \
  --member <MemberName> \
  --typed-refs        /tmp/typed-cs-refs.json   \
  --razor-refs        /tmp/razor-refs.json      \
  --string-typed-refs /tmp/string-typed-refs.json \
  --out               /tmp/graph.json
```

Or run all three scanners and compose in one shot (subprocess call):

```
dotnet script scripts/assemble-graph.csx -- \
  --solution-dir <abs path> \
  --type CatalogItem --member PictureFileName \
  --out /tmp/graph.json
```

## Output schema

See `references/knowledge-graph-shape.md`. Top-level fields:

- `concept` — `Type.Member`
- `declaration` — `{ file, line, col }`
- `nodes` — every reference, classified by `kind` and `role`
- `edges` — `{ from, to: nodeId, relation }`
- `summary` — `total_refs`, `by_kind`, `by_role`, `mutation_sites`,
  `read_sites`, `contract_boundaries`, `drift_smells`
- `sources` — counts from each scanner

## How to use the graph for refactor planning

Read `summary.contract_boundaries` first — every entry is a place where a
refactor crosses a stable contract (MVC route, Bind attribute, JSON
serialization name, CSV header). Each boundary you cross either:

1. Becomes a coordinated change (rename the contract on both sides), or
2. Requires a Strangler-Fig overload / `[Obsolete]` alias, or
3. Justifies *not* doing the refactor at all if the cost outweighs the benefit.

Read `summary.drift_smells` next. Case-variant drift (multiple casings of
the same conceptual name) means a rename has to update each variant in its
own casing. The `evidence[].variant` and `evidence[].variant_kind` fields
tell you exactly which strings to update where.

Read `summary.mutation_sites` vs `read_sites`. A property with many
external mutation sites is an anemic-model smell — the refactor target may
be encapsulating mutation through a method on the type rather than renaming
the property.

Then design the refactor against the graph, citing node IDs as evidence.
Hand the design to Roslynator CLI / IDE refactorings / the Copilot
modernization agent for execution. Do NOT execute via this skill.

## What the skill explicitly does NOT do

- Execute refactors. Producing the graph is the entire scope.
- Replace Microsoft.CodeAnalysis.LanguageServer for typed-cs queries; the
  syntactic typed-cs scanner is a project-load-free *equivalent* with
  slightly looser precision (mitigated by `--type` filter). Prefer LSP
  findReferences when wired up correctly.
- Handle non-.NET languages.

## Acceptance test (May 2026, eShopLegacyMVC)

Target: `CatalogItem.PictureFileName` in
`dotnet-architecture/eShopModernizing/eShopLegacyMVCSolution/src/eShopLegacyMVC`.

Ground truth: 35 references — 18 typed-cs, 12 razor, 5 string-typed/data.

Skill output:
- typed-cs-refs.csx → 18/18 (100% recall, 100% precision)
- razor-refs.csx → 12/12 (100% recall, 100% precision)
- string-typed-refs.csx → 5/5 (100% recall, 100% precision)
- Combined graph: 35 nodes, 3 contract boundaries detected
  (`[Bind]` x2 + CSV header), 1 drift smell
  (case-variant-drift across 3 casings).

## See also

- `references/typed-tools.md` — driving Microsoft.CodeAnalysis.LanguageServer
  and Roslynator CLI for typed-cs queries (when LSP is correctly wired).
- `references/razor-discovery.md` — full kind taxonomy and pattern coverage
  for `razor-refs.csx`.
- `references/string-typed-discovery.md` — case-variant generation,
  controlled vocabulary of string-typed kinds.
- `references/knowledge-graph-shape.md` — JSON schema and example
  traversals.
