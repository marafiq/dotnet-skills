# String-typed reference discovery — case-variant generation and controlled vocabulary

`scripts/string-typed-refs.csx` finds references where the symbol name
appears as a **string literal**, in non-symbol positions that LSP
`findReferences` will never resolve by design.

## Case-variant generation

For a PascalCase target name like `PictureFileName`, the scanner generates
the following variants and matches each as a distinct case-variant kind:

| Variant | Example | `variant_kind` label |
|---|---|---|
| PascalCase (canonical) | `PictureFileName` | `pascal` |
| camelCase | `pictureFileName` | `camelcase` |
| lowercase | `picturefilename` | `lowercase` |
| snake_case | `picture_file_name` | `snake_case` |
| SCREAMING_SNAKE_CASE | `PICTURE_FILE_NAME` | `SCREAMING_SNAKE` |
| kebab-case | `picture-file-name` | `kebab-case` |

The drift smell appears when the same conceptual symbol exists in 2+ casings
across the codebase (e.g. `CatalogDBInitializer.cs:178` uses
`"pictureFileName"` camelCase but `CatalogDBInitializer.cs:224` uses
`"picturefilename"` lowercase, while the actual property is
`PictureFileName` PascalCase). The assembler flags this as
`drift_smells: case-variant-drift` with severity `high` — a refactor MUST
update each variant in its own casing.

## Kind taxonomy (.cs files via Roslyn)

For each `LiteralExpressionSyntax` of kind `StringLiteralExpression` whose
value matches any case variant of the target, the scanner classifies by
walking the enclosing syntax:

| Enclosing | Kind | Example |
|---|---|---|
| `[Bind]` attribute | `string-typed-bind-attribute` | `[Bind(Include = "Id,Name,PictureFileName,...")]` |
| `[JsonProperty]` / `[JsonPropertyName]` | `string-typed-json-attribute` | `[JsonProperty("PictureFileName")]` |
| `[Display]` / `[DisplayName]` | `string-typed-display-attribute` | `[Display(Name = "PictureFileName")]` |
| `[XmlElement]` / `[XmlAttribute]` | `string-typed-xml-attribute` | `[XmlElement("PictureFileName")]` |
| Other attribute | `string-typed-other-attribute` | any `[Foo("X")]` |
| `nameof(...)` invocation | `string-typed-nameof` | (rare — `nameof` produces tokens, not strings; included for defense) |
| Other invocation | `string-typed-invocation-arg` | `Array.IndexOf(headers, "picturefilename")` |
| No enclosing context | `string-typed-other` | bare string literal in a field initializer |

When the scanner detects the variant inside a comma-separated string
(common for `[Bind(Include = "...")]`), it splits by commas and tests each
trimmed segment, setting `match_within_csv_string: true`.

## Kind taxonomy (non-.cs files, plain text scan)

| Extension | Kind | Notes |
|---|---|---|
| `.csv` | `data-csv-header` | First line of CSV files; column-header drift |
| `.json` | `string-typed-json-key` | JSON config files, schema docs, package.json-style configs |
| `.sql` | `string-typed-sql-identifier` | Stored procs, view definitions, schema migrations referencing the column |
| `.config` | `string-typed-config-value` | `web.config`, `app.config`, attribute or value strings |
| `.xml` | `string-typed-xml-content` | EDMX, project XML, custom XML configs |

`.cshtml` is excluded — `razor-refs.csx` is the authoritative scanner with
proper Razor kind classification.

## Word-boundary matching

For non-.cs files, matches are word-boundary-anchored. A character before
or after the match is considered "inside the identifier" if it's a letter,
digit, or underscore. So `MyPictureFileNameField` does NOT match
`PictureFileName` (the first surrounding char is `y` — alpha — and the
char after is `F` — alpha). This eliminates most false positives from
identifier substring matches.

## Coverage on the bench (`CatalogItem.PictureFileName` in eShopLegacyMVC)

Ground truth (5 string-typed + data refs):
- `Controllers/CatalogController.cs:62` → `string-typed-bind-attribute` (variant: pascal, in CSV string)
- `Controllers/CatalogController.cs:100` → `string-typed-bind-attribute` (variant: pascal, in CSV string)
- `Models/Infrastructure/CatalogDBInitializer.cs:178` → `string-typed-other` (variant: **camelcase** — drift)
- `Models/Infrastructure/CatalogDBInitializer.cs:224` → `string-typed-invocation-arg` (variant: **lowercase** — drift)
- `Setup/CatalogItems.csv:1` → `data-csv-header` (variant: pascal)

Scanner output: 5/5, all correctly classified, drift detected.

## When to extend

Add a new kind when you encounter a domain-specific string contract that
neither LSP nor any of the existing kinds capture. The kind name should be
self-explanatory — when the LLM reads `string-typed-<context>`, the
`<context>` should immediately tell it what kind of refactor coordination
is needed.

Examples of patterns we have NOT yet captured:

- ASP.NET Core `[Route("template")]` route templates (e.g. `[Route("api/{controller}/{action}")]`)
- `[FromQuery(Name = "x")]`, `[FromHeader(Name = "x")]`, `[FromForm(Name = "x")]`
- SignalR `[HubMethodName("X")]`
- `[ProtoMember(1, Name = "X")]` Protocol Buffers serialization
- Various ORM column attributes (`[Column("X")]`, `[Table("X")]`)
- App settings keys (`Configuration["Section:X"]`, `IConfiguration.GetSection("X")`)

These are catchable by extending the attribute-name list in
`ClassifyCsContext` and adding similar pattern recognition for invocations
(`Configuration["..."]`).
