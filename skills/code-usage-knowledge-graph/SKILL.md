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

## Cross-validation and fallback to standard LLM tools

The scripts are heuristic-bounded — regex-based for Razor diversity,
syntactic (not semantic) for typed C#, pattern-list-based for string
contexts. They can miss patterns the codebase invents (custom
`HtmlHelper` extensions, source-generator output, Blazor `.razor` files,
SignalR hub method strings, route templates, custom attribute
serialization names) and they can over-match in unusual codebases.

**Always cross-validate before trusting the graph.** Run a baseline
sanity check with the harness's standard tools and reconcile any
discrepancy with the user before proceeding to refactor design.

### Step 1: Baseline cross-check (every invocation)

```bash
# Case-insensitive identifier scan; the floor of what should be caught.
rg -i --word-regexp '<MemberName>' <solution-dir>
```

Compare the count to `graph.summary.total_refs`. Material discrepancy
(>5% miss, or a missing file the user expected) means the scripts under-
covered. Investigate before designing the refactor.

For symbol names that include compound casing variants
(`PictureFileName` / `pictureFileName` / `picturefilename`), also run:

```bash
rg --pcre2 '\b(PictureFileName|pictureFileName|picturefilename|picture_file_name|picture-file-name|PICTURE_FILE_NAME)\b' <solution-dir>
```

Every variant the script's `summary.drift_smells.evidence` flagged should
appear in this rg output. If rg surfaces a casing variant the script's
generator didn't include, extend the variant list in
`string-typed-refs.csx`.

### Step 2: Per-scanner fallback paths

When a scanner errors, returns 0 in a populated codebase, or under-covers
relative to the rg cross-check:

| Scanner failure mode | Fallback path |
|---|---|
| `typed-cs-refs.csx` crashes on parse, or returns 0 in a code-bearing project | (a) `LSP findReferences` at the declaration position via `Microsoft.CodeAnalysis.LanguageServer` if wired, (b) `roslynator find-symbol --match "Name=='X'" <sln>` if Roslynator can load the project, (c) `rg --type cs '\b<MemberName>\b' <solution-dir>` + manual `Read` of each hit's enclosing context, (d) dispatch the **`Explore` Agent** with a prompt to find every usage and classify by site type. |
| `razor-refs.csx` misses a Razor pattern (custom helper, `.razor` Blazor file, source-generated cshtml) | (a) Extend the regex list in `scripts/razor-refs.csx` with the new pattern, (b) `rg --type-add 'razor:*.{cshtml,razor}' --type razor '<MemberName>' <solution-dir>` and manually classify, (c) for `.razor` Blazor specifically, the scanner does not handle it — use rg + Read directly. |
| `string-typed-refs.csx` misses a string context the codebase uses (e.g. `[Route("...")]`, `[ProtoMember(Name="X")]`, custom attribute schemas) | (a) Extend `ClassifyCsContext` in `scripts/string-typed-refs.csx` with the new attribute name, (b) `rg --pcre2 -i '<all-case-variants>' <solution-dir>` filtered to the file types the codebase actually uses for that contract. |
| `assemble-graph.csx` fails to spawn subprocess (dotnet-script not on PATH, sandbox restriction) | Run the three scanners individually with `--out` paths, then invoke `assemble-graph.csx --typed-refs ... --razor-refs ... --string-typed-refs ...` to compose pre-computed JSON. |
| Solution truly unsupported (e.g. Roslyn parse errors on every `.cs` file due to preprocessor directives the script does not honor) | Skip the typed-cs scanner; rely on `rg --word-regexp` + Read + the `Explore` Agent. The skill degrades to a *guided* discovery rather than an *automated* one — the workflow shape is still useful even when the scripts cannot run. |

### Step 3: When discrepancies remain, surface them

If after fallback the script-graph and the rg cross-check still disagree:

1. **Do not silently degrade.** Tell the user: "the scripts found N refs;
   rg found M (Δ=…); here are the candidates the scripts missed:
   `<file>:<line>` (kind: …)". Let the user judge whether to extend the
   scanner, hand-edit, or proceed with the smaller set.
2. **Prefer the Explore Agent** for breadth-deep investigations. The
   Agent has its own context window and can read 50 candidate files and
   summarize back without polluting the main session's context.
3. **Record the gap in the plan file** so the next session knows the
   scanner needs an extension for this codebase's pattern.

### Step 4: Always-applicable conservative defaults

- For an unfamiliar codebase, *first* invocation should run the rg
  cross-check, *then* the scripts, *then* compare. If the graph
  closely matches rg ground truth, trust the graph. If not, fall back.
- The scripts' precision-vs-recall trade-off is tuned for **high recall**.
  Expect false positives in noisy codebases — the graph's `kind` and
  `enclosing_context` fields tell the LLM how to discount them.
- When in doubt, **include the rg output verbatim in the design
  evidence**, not just the graph. The LLM should triangulate, not
  trust a single source.

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
