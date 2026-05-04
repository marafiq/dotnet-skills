# Driving Microsoft.CodeAnalysis.LanguageServer + Roslynator for typed-cs queries

The skill ships `typed-cs-refs.csx` as a project-load-free Roslyn syntactic
walker. When Microsoft.CodeAnalysis.LanguageServer (Roslyn-based LSP) is
correctly wired into the harness, prefer driving it via the LSP tool — the
semantic model gives semantic precision the syntactic walker cannot.

## When to use which

| Tool | When | Trade-off |
|---|---|---|
| `LSP findReferences` (Microsoft.CodeAnalysis.LanguageServer) | Modern .NET solutions; correctly-wired LSP server | **Semantic precision** — disambiguates same-named members across types via the SemanticModel. Requires the LSP server to be running with the workspace registered. |
| `roslynator find-symbol` / `rename-symbol --dry-run` | Solutions Roslynator can load (modern + many legacy via Microsoft.Build.Locator) | Symbol-aware via Roslyn workspace; CLI invocation, no LSP server required. Sometimes fails on non-SDK-style legacy projects. |
| `typed-cs-refs.csx` (this skill) | Legacy .NET Framework 4.x with old-format `.csproj`, environments where LSP is broken or workspace-rooted elsewhere, or any case where you need a project-load-free syntactic answer | **Recall == LSP** in practice for property/field/method names. **Precision** lower when the symbol name is shared across multiple types — mitigated by the `--type` filter. |

## Microsoft.CodeAnalysis.LanguageServer wiring

Locally available via the VS Code C# Dev Kit extension at:

```
~/.vscode/extensions/ms-dotnettools.csharp-<version>-<arch>/.roslyn/Microsoft.CodeAnalysis.LanguageServer
```

Recommended `~/.claude/plugins/csharp-ls/.lsp.json`:

```json
{
  "csharp": {
    "command": "<abs path>/Microsoft.CodeAnalysis.LanguageServer",
    "args": ["--stdio", "--logLevel", "Information", "--autoLoadProjects"],
    "extensionToLanguage": {".cs": "csharp"},
    "transport": "stdio",
    "maxRestarts": 3
  }
}
```

`--autoLoadProjects` discovers projects from the LSP `workspaceFolders`
parameter at runtime — not from the harness's cwd. This is the standards-
compliant LSP behavior and avoids the cwd-discovery failure mode of csharp-ls.

Razor support: append `--razorSourceGenerator <path>` and
`--razorDesignTimePath <path>` once the Razor SDK paths are known. Razor
find-references was fixed in Microsoft's stack in
[`dotnet/razor#9804`](https://github.com/dotnet/razor/issues/9804) (closed
validated 2025-12-04), so when wired, the LSP will surface typed Razor
lambda references natively. The skill's `razor-refs.csx` remains
authoritative for STRING-typed Razor patterns (`@Html.Partial("X")`,
`asp-action="X"`, `ViewBag.X`, etc.) which the LSP cannot resolve by design.

## Roslynator CLI usage

Install:

```
dotnet tool install -g Roslynator.DotNet.Cli
```

Find a symbol by name match:

```
roslynator find-symbol \
  --match "match Type=='Property' and Name=='PictureFileName'" \
  /path/to/solution.sln
```

Rename (dry-run first to inspect):

```
roslynator rename-symbol \
  --match "match Name=='PictureFileName'" \
  --new-name PictureName \
  --dry-run \
  /path/to/solution.sln
```

The match expression uses Roslynator's CQLinq-like predicate language —
see [`josefpihrt.github.io/docs/roslynator/cli/`](https://josefpihrt.github.io/docs/roslynator/cli/)
for the grammar.

## Why `typed-cs-refs.csx` exists alongside both

The harness's LSP wiring sometimes fails (csharp-ls's cwd-based discovery
crashes when Claude Code's cwd has no .sln; Microsoft.CodeAnalysis.LanguageServer
needs `--autoLoadProjects` and may need additional Razor flags). Loading legacy
.NET Framework 4.x projects via MSBuildWorkspace requires the BuildHost-net472
binary which isn't always in the NuGet package's expected path.

`typed-cs-refs.csx` parses each `.cs` file with `CSharpSyntaxTree.ParseText` —
no MSBuildWorkspace, no project graph, no language server, no build host.
Works on legacy and modern code identically. Recall is equal to LSP for
named members in our bench (18/18). Precision is mitigated by the `--type`
filter; for our test case (`CatalogItem.PictureFileName` in eShopLegacyMVC),
precision is 100% because no other type in the codebase has a property
named `PictureFileName`.

In codebases where multiple types share member names, prefer LSP for
semantic precision. The script remains a robust fallback when LSP is
unavailable.
