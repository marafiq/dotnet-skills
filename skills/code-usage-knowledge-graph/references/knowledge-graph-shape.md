# Knowledge graph schema and example traversals

`scripts/assemble-graph.csx` produces a single `graph.json` per concept.
This document specifies the schema and how to navigate it for refactor
planning.

## Top-level schema

```json
{
  "concept": "Type.Member",
  "declaration": { "file": "...", "line": N, "col": N },
  "nodes": [ <Node>, ... ],
  "edges": [ <Edge>, ... ],
  "summary": <Summary>,
  "sources": {
    "typed_cs_count": N,
    "razor_count": N,
    "string_typed_count": N
  }
}
```

## Node shape

```json
{
  "id": "n1",
  "file": "Models/CatalogItem.cs",
  "line": 28,
  "col": 23,
  "kind": "typed-cs-decl",
  "role": "declaration",
  "sources": ["typed-cs"],

  // Optional, populated when relevant:
  "extra": "property-decl",
  "receiver": "item",
  "snippet": "...code excerpt around the match...",
  "helper": "Html.LabelFor",
  "member": "PictureFileName",
  "chain": "PictureFileName",
  "modelType": "eShopLegacyMVC.Models.CatalogItem",
  "literal": "pictureFileName",
  "matched_variant": "pictureFileName",
  "variant_kind": "camelcase",
  "match_within_csv_string": false,
  "enclosing_context": "[Bind]",
  "commented": false,
  "note": "..."
}
```

`role` values: `declaration` | `read` | `write` | (none — for razor and
string-typed nodes where role isn't determinable from syntax alone).

`sources` is the array of scanners that found this node. When more than
one scanner finds the same `(file, line, col)`, the higher-precision
scanner's data wins (typed-cs > razor > string-typed) but the `sources`
array records all of them for traceability.

## Edge shape

```json
{
  "from": "concept",
  "to": "n1",
  "relation": "declared-at"
}
```

`relation` values currently emitted: `declared-at`, `mutated-by`,
`read-by`, `referenced-by`. Edges always originate from the synthetic
`"concept"` node toward the actual reference nodes.

For a method-call shape, future versions will add edges between method
nodes (`incoming-call`, `outgoing-call`) but the property/field shape
in this version is concept-centric only.

## Summary shape

```json
{
  "total_refs": 35,
  "by_kind": {
    "typed-cs-decl": 1,
    "typed-cs-ref": 17,
    "razor-helper-lambda": 10,
    "razor-helper-lambda-commented": 2,
    "string-typed-bind-attribute": 2,
    "string-typed-other": 1,
    "string-typed-invocation-arg": 1,
    "data-csv-header": 1
  },
  "by_role": {
    "declaration": 1,
    "write": 14,
    "read": 3,
    "(none)": 17
  },
  "mutation_sites": 14,
  "read_sites": 3,
  "contract_boundaries": [
    {
      "kind": "mvc-bind-attribute",
      "file": "Controllers/CatalogController.cs",
      "line": 62,
      "nodeId": "n5"
    }
  ],
  "drift_smells": [
    {
      "smell": "case-variant-drift",
      "evidence": [
        { "variant": "PictureFileName", "variant_kind": "pascal" },
        { "variant": "pictureFileName", "variant_kind": "camelcase" },
        { "variant": "picturefilename", "variant_kind": "lowercase" }
      ],
      "severity": "high",
      "note": "Multiple casing variants of the symbol name appear as string-typed references; refactor must update each variant in its own casing."
    }
  ]
}
```

## Contract boundary kinds

The assembler infers contract boundaries from each node's `kind`:

| Node kind | Boundary kind |
|---|---|
| `string-typed-bind-attribute` | `mvc-bind-attribute` |
| `razor-string-helper-action` | `mvc-action-route` |
| `razor-string-url-action` | `mvc-url-action` |
| `razor-tag-helper-asp-action` | `core-mvc-asp-action` |
| `data-csv-header` | `data-file-binding` |
| `string-typed-json-attribute` | `json-serialization-contract` |
| `string-typed-display-attribute` | `ui-display-contract` |

Each boundary represents a place where renaming the symbol crosses a
stable external contract — either the change must be coordinated on
both sides, or a deprecation alias is needed, or the refactor is too
expensive to justify.

## Example traversal — refactor planning for `CatalogItem.PictureFileName → PictureName`

1. **Read declaration**: `graph.declaration` → file/line of the property.
2. **Read summary**: `graph.summary.contract_boundaries` lists the MVC `[Bind]`
   attributes and the CSV header. Each is a coordinated change point.
3. **Read drift smells**: `case-variant-drift` is high severity. The
   `evidence[].variant` array tells the rename to update each casing in
   its own form (`pictureFileName` → `pictureName`, `picturefilename` →
   `picturename`, `PictureFileName` → `PictureName`).
4. **Walk write sites**: `nodes[].role == "write"` → 14 mutation sites.
   Most are seed-data initializers (low risk). One is the CSV row
   loader at line 224 (depends on the CSV header — must update both).
   One is the constructor default at line 12 (trivial). Two are MVC
   `[Bind(Include="...")]` attributes — these are *string-typed* writes
   (the rename must update the string content).
5. **Walk read sites**: `nodes[].role == "read"` → 3 reads. Two in
   `PicController.cs` (constructing image paths). One in
   `CatalogDBContext.cs:74` (EF `builder.Property(ci => ci.X)` mapping).
   The EF mapping site is a *contract boundary* if external systems
   read this column by name — promote it to `contract_boundaries` if so.
6. **Plan the refactor**:
   - Symbol rename via `roslynator rename-symbol` (handles 18 typed-cs sites).
   - Razor rename via the LSP (post-Dec-2025 fix) or hand-edit using the
     12 razor node positions from the graph.
   - Three string-typed sites — hand-edit using the literal in each
     casing. The two `[Bind]` attribute strings (split by comma) need
     surgical replacement of the segment, not the whole string.
   - The CSV header change is coordinated with the runtime-loader update
     at line 224.
   - Contract boundaries flagged in `summary.contract_boundaries`: review
     each, decide whether to rename the contract or keep the old contract
     name with an internal alias.

## Producing your own graph

For a concept that isn't in the bench:

```bash
SOLN=/abs/path/to/your/SolutionDir
TYPE=YourType
MEMBER=YourMember

dotnet script razor-refs.csx -- --solution-dir "$SOLN" --symbol "$MEMBER" --out /tmp/razor.json
dotnet script string-typed-refs.csx -- --solution-dir "$SOLN" --symbol "$MEMBER" --out /tmp/stringtyped.json
dotnet script typed-cs-refs.csx -- --solution-dir "$SOLN" --member "$MEMBER" --type "$TYPE" --out /tmp/typedcs.json
dotnet script assemble-graph.csx -- --solution-dir "$SOLN" --type "$TYPE" --member "$MEMBER" \
  --typed-refs /tmp/typedcs.json --razor-refs /tmp/razor.json --string-typed-refs /tmp/stringtyped.json \
  --out /tmp/graph.json

jq '.summary' /tmp/graph.json
```

Or use the all-in-one form (assemble-graph runs the three scanners):

```bash
dotnet script assemble-graph.csx -- --solution-dir "$SOLN" --type "$TYPE" --member "$MEMBER" --out /tmp/graph.json
```
