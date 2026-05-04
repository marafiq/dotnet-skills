# Razor `.cshtml` discovery — pattern coverage and kind taxonomy

`scripts/razor-refs.csx` scans `.cshtml` files for references to a target
member name. Razor is diverse; the scanner classifies each match into one
of the following `kind` values. Comments (`@*...*@`) carry the same kind
suffixed with `-commented` and `commented: true`.

## Kind taxonomy

### Helper-`For` lambdas

```
@Html.<Helper>For(<p> => <receiver>.<chain>)
```

| Kind | Pattern |
|---|---|
| `razor-helper-lambda` | `@Html.LabelFor`, `@Html.DisplayFor`, `@Html.DisplayNameFor`, `@Html.EditorFor`, `@Html.TextBoxFor`, `@Html.ValidationMessageFor`, `@Html.HiddenFor`, `@Html.CheckBoxFor`, etc. — any helper ending in `For`. The lambda parameter need not be the same identifier as the receiver, so `modelItem => item.X` (foreach loop variable as receiver) matches. |
| `razor-helper-lambda-commented` | Same wrapped in `@* ... *@`. |

### String-typed Html helpers

```
@Html.<Helper>("name", ...)
```

These are routing strings — Razor passes them through to MVC's action/partial
resolver. LSP `findReferences` cannot resolve them (string literals are not
symbols).

| Kind | Pattern |
|---|---|
| `razor-string-helper-partial`        | `@Html.Partial("name", ...)` |
| `razor-string-helper-renderpartial`  | `@Html.RenderPartial("name", ...)` |
| `razor-string-helper-action`         | `@Html.Action("name", ...)` |
| `razor-string-helper-renderaction`   | `@Html.RenderAction("name", ...)` |

### `@Url` helpers

| Kind | Pattern |
|---|---|
| `razor-string-url-action`   | `@Url.Action("name", ...)` |
| `razor-string-url-routeurl` | `@Url.RouteUrl("name", ...)` |

### Section rendering

| Kind | Pattern |
|---|---|
| `razor-string-rendersection` | `@RenderSection("name", required: ...)` |

### Property access

```
@Model.<chain>      → razor-model-property-access
@<id>.<chain>       → razor-property-access (id is any local var, ViewBag/ViewData excluded)
```

These match when the chain's last segment is the target member name, and
they are de-duplicated against helper-`For` lambdas already consumed on
the same line.

### `ViewBag` / `ViewData`

Dynamic-typed access — LSP cannot resolve. Caught here because they're a
real source of refactor risk.

| Kind | Pattern |
|---|---|
| `razor-viewbag-access`  | `ViewBag.<member>` |
| `razor-viewdata-access` | `ViewData["key"]` |

### ASP.NET Core tag helpers

| Kind | Pattern |
|---|---|
| `razor-tag-helper-asp-for`        | `<input asp-for="X" ... />` (Razor binds asp-for semantically; matched here as defensive fallback) |
| `razor-tag-helper-asp-action`     | `<a asp-action="X" .../>`, `<form asp-action="X" .../>` |
| `razor-tag-helper-asp-controller` | `asp-controller="X"` (routing string) |

### Plain HTML attribute names

```
<input name="X" />
<select name="X">
<textarea name="X" />
<button name="X" />
```

| Kind | Pattern |
|---|---|
| `razor-html-input-name` | The `name=` attribute of `<input>`, `<select>`, `<textarea>`, `<button>`. MVC binds these by convention. |

### Razor directives

| Kind | Pattern |
|---|---|
| `razor-directive-model`    | `@model X` — the model type declaration |
| `razor-directive-inherits` | `@inherits X` |
| `razor-directive-inject`   | `@inject Type Name` |
| `razor-directive-using`    | `@using Namespace.X` |

## Comment handling

Multi-line `@*...*@` comments are tracked via per-line state. A match's
position is checked against an even/odd count of `@*`/`*@` markers between
line start (carrying state from prior lines) and the match index. If
inside a comment, the kind is suffixed `-commented` and `commented: true`
is set on the node.

## Limitations and known false positives

- The scanner is regex-based for performance and simplicity. It does not
  perform semantic resolution; the receiver-vs-target-type check is
  best-effort only. For 100% semantic Razor coverage, wire
  Microsoft.CodeAnalysis.LanguageServer with the Razor source generator
  flags (`--razorSourceGenerator`, `--razorDesignTimePath`).
- `@<id>.<member>` (raw property access) matches any identifier as the
  receiver. If the codebase has multiple unrelated types with the same
  member name accessed directly via Razor, the scanner cannot disambiguate
  without semantic info. Treat the matches as candidates and verify.
- Tag helper attributes are matched by attribute syntax `asp-for="X"`,
  `asp-action="X"`. The MVC tag helpers binding rules (e.g. `asp-for`
  binding to dotted property paths through the model) are not fully
  resolved — the literal value is captured as-is.
- `@functions { ... }` blocks are not scanned for typed C# refs by this
  scanner — they are Razor source generators' compilation unit. Use the
  typed-cs-refs.csx output of the corresponding generated `.cs` file (or
  rely on LSP).

## Coverage on the bench (`CatalogItem.PictureFileName` in eShopLegacyMVC)

Ground truth: 12 razor refs (10 helper lambda + 2 commented). Scanner
output: 12/12, all correctly classified.

## Why the scanner remains relevant after Microsoft fixed Razor LSP

Microsoft fixed typed Razor lambda find-references in
[`dotnet/razor#9804`](https://github.com/dotnet/razor/issues/9804)
(closed validated 2025-12-04). When Microsoft.CodeAnalysis.LanguageServer
is correctly wired with Razor flags, typed lambdas (`@Html.LabelFor`,
`@Model.X`, `asp-for`) resolve via LSP `findReferences`. The scanner still
adds value for:

- **String-typed Razor patterns** (`@Html.Partial("X")`, `@Html.Action("X")`,
  `@Url.Action("X")`, `@RenderSection("X")`, `asp-action`, `asp-controller`,
  HTML `name=`) — string literals, not symbols. LSP cannot resolve them by
  design.
- **`ViewBag.X` / `ViewData["X"]`** — dynamic / dictionary access. No
  symbol to resolve.
- **Commented-out references** — LSP ignores comments; the scanner surfaces
  them so a rename refactor can choose to update or delete them.
- **Defensive fallback** when the LSP isn't wired (sessions where csharp-ls
  is the configured server, or where the wiring is broken).

The assembler (`assemble-graph.csx`) deduplicates by `(file, line, col)`,
so when both LSP and the scanner catch the same lambda, only one node
appears (LSP-sourced wins on tie).
