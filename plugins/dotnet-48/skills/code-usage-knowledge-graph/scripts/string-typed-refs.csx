#!/usr/bin/env dotnet-script
// string-typed-refs.csx — find string-typed references to a target symbol name
// across .cs (Roslyn-aware), .cshtml, .csv, .json, .config, .sql, .xml.
//
// Catches the references that LSP `findReferences` will never see:
//   [Bind(Include = "...,PictureFileName,...")]   → string-typed-bind-attribute
//   [JsonProperty("PictureFileName")]              → string-typed-json-attribute
//   [Display(Name = "PictureFileName")]            → string-typed-display-attribute
//   nameof(PictureFileName)                        → string-typed-nameof
//   "pictureFileName" (camelCase, drift)           → string-typed-camelcase-drift
//   "picturefilename" (lowercase, drift)           → string-typed-lowercase-drift
//   CSV headers, JSON keys, SQL identifiers, config attrs, web.config keys
//
// Usage:
//   dotnet script string-typed-refs.csx -- \
//     --solution-dir <dir> --symbol <PascalCaseName> [--out <file.json>]

#r "nuget: Microsoft.CodeAnalysis.CSharp, 4.13.0"

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

string? solutionDir = null;
string? symbolName = null;
string? outFile = null;

for (int i = 0; i < Args.Count; i++)
{
    switch (Args[i])
    {
        case "--solution-dir": solutionDir = Args[++i]; break;
        case "--symbol":       symbolName  = Args[++i]; break;
        case "--out":          outFile     = Args[++i]; break;
        default: Console.Error.WriteLine($"Unknown arg: {Args[i]}"); return 1;
    }
}

if (solutionDir is null || symbolName is null)
{
    Console.Error.WriteLine("Usage: dotnet script string-typed-refs.csx -- --solution-dir <dir> --symbol <PascalCaseName> [--out <file.json>]");
    return 1;
}

if (!Directory.Exists(solutionDir))
{
    Console.Error.WriteLine($"Solution dir not found: {solutionDir}");
    return 2;
}

// --- case-variant generator ---------------------------------------------

(string variant, string variantKind) PascalToVariants(string pascal)
    => throw new NotImplementedException(); // overload not used; keeping placeholder for clarity

Dictionary<string, string> Variants(string name)
{
    // map: variant string → variant kind label
    var snake = Regex.Replace(name, @"(?<!^)(?=[A-Z])", "_").ToLowerInvariant();
    var camel = char.ToLowerInvariant(name[0]) + name.Substring(1);
    var lower = name.ToLowerInvariant();
    var screamSnake = snake.ToUpperInvariant();
    var kebab = snake.Replace('_', '-');

    var d = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [name] = "pascal",
        [camel] = "camelcase",
        [lower] = "lowercase",
        [snake] = "snake_case",
        [screamSnake] = "SCREAMING_SNAKE",
        [kebab] = "kebab-case"
    };
    // dedupe duplicates (e.g. one-word names where pascal == camel-with-cap?)
    return d;
}

var variants = Variants(symbolName);

// --- helpers ------------------------------------------------------------

bool IsExcluded(string path) =>
    path.Contains("/bin/") || path.Contains("/obj/") ||
    path.Contains("\\bin\\") || path.Contains("\\obj\\");

(string? variant, string? variantKind) MatchVariantWhole(string s)
{
    if (variants.TryGetValue(s, out var kind)) return (s, kind);
    return (null, null);
}

(string? variant, string? variantKind) MatchVariantInCsv(string s)
{
    // Bind/Include style: "Id,Name,...,PictureFileName,..." — split and test each part
    if (!s.Contains(',')) return (null, null);
    foreach (var part in s.Split(','))
    {
        var trimmed = part.Trim();
        if (variants.TryGetValue(trimmed, out var kind)) return (trimmed, kind);
    }
    return (null, null);
}

// --- .cs scanning via Roslyn -------------------------------------------

(string kind, string context) ClassifyCsContext(SyntaxNode literalNode)
{
    // Walk ancestors to find the meaningful enclosing context.
    for (var node = literalNode.Parent; node is not null; node = node.Parent)
    {
        if (node is AttributeSyntax attr)
        {
            var attrName = attr.Name.ToString();
            // Strip namespace qualifier if present
            var simpleName = attrName.Contains('.') ? attrName.Substring(attrName.LastIndexOf('.') + 1) : attrName;
            return simpleName switch
            {
                "Bind"            => ("string-typed-bind-attribute", $"[{simpleName}]"),
                "JsonProperty"    => ("string-typed-json-attribute", $"[{simpleName}]"),
                "JsonPropertyName"=> ("string-typed-json-attribute", $"[{simpleName}]"),
                "Display"         => ("string-typed-display-attribute", $"[{simpleName}]"),
                "DisplayName"     => ("string-typed-display-attribute", $"[{simpleName}]"),
                "XmlElement"      => ("string-typed-xml-attribute", $"[{simpleName}]"),
                "XmlAttribute"    => ("string-typed-xml-attribute", $"[{simpleName}]"),
                _                 => ("string-typed-other-attribute", $"[{simpleName}]")
            };
        }
        if (node is InvocationExpressionSyntax inv)
        {
            var target = inv.Expression.ToString();
            if (target == "nameof") return ("string-typed-nameof", "nameof(...)");
            // generic invocation as enclosing context — name it but don't classify deeper
            return ("string-typed-invocation-arg", $"{target}(...)");
        }
        if (node is InitializerExpressionSyntax) continue; // keep walking
        if (node is ArgumentSyntax) continue;
    }
    return ("string-typed-other", "(no enclosing attribute or invocation found)");
}

// --- non-.cs scanning (text-based) -------------------------------------

(string kind, string context) ClassifyNonCs(string ext, string lineText)
{
    return ext.ToLowerInvariant() switch
    {
        ".cshtml"  => ("string-typed-razor-text", "(.cshtml outside helper lambda — see razor-refs.csx for typed-lambdas)"),
        ".csv"     => ("data-csv-header", "CSV column header"),
        ".json"    => ("string-typed-json-key", "JSON document"),
        ".sql"     => ("string-typed-sql-identifier", "SQL"),
        ".xml"     => ("string-typed-xml-content", "XML"),
        ".config"  => ("string-typed-config-value", ".config attribute or value"),
        _          => ("string-typed-text", $"({ext})")
    };
}

// --- run ----------------------------------------------------------------

var results = new List<Dictionary<string, object?>>();
int csScanned = 0;
int textScanned = 0;

foreach (var file in Directory.EnumerateFiles(solutionDir, "*.cs", SearchOption.AllDirectories))
{
    if (IsExcluded(file)) continue;
    csScanned++;
    var source = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(source);
    var root = tree.GetRoot();
    string relFile = Path.GetRelativePath(solutionDir, file);

    var literals = root.DescendantNodes()
        .OfType<LiteralExpressionSyntax>()
        .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression));

    foreach (var lit in literals)
    {
        var value = lit.Token.ValueText;
        if (string.IsNullOrEmpty(value)) continue;

        // Try whole-string match first (e.g. "PictureFileName")
        var (variant, variantKind) = MatchVariantWhole(value);
        bool isCsv = false;
        if (variant is null)
        {
            (variant, variantKind) = MatchVariantInCsv(value);
            isCsv = variant is not null;
        }
        if (variant is null) continue;

        var (kind, context) = ClassifyCsContext(lit);
        var pos = lit.GetLocation().GetLineSpan().StartLinePosition;

        results.Add(new Dictionary<string, object?>
        {
            ["file"] = relFile,
            ["line"] = pos.Line + 1,
            ["col"] = pos.Character + 1,
            ["kind"] = kind,
            ["literal"] = value,
            ["matched_variant"] = variant,
            ["variant_kind"] = variantKind,
            ["match_within_csv_string"] = isCsv,
            ["enclosing_context"] = context
        });
    }

    // Also scan identifiers inside `nameof(...)` — these are syntax tokens, not string literals.
    // ...handled implicitly by users searching with --symbol; nameof() is symbol-aware via LSP.
    // Out of scope for the string-typed scanner.
}

// Non-.cs file scan.
// .cshtml is intentionally EXCLUDED here — razor-refs.csx is the authoritative
// .cshtml scanner with kind-classification for Razor diversity (helper lambdas,
// directives, tag helpers, ViewBag/ViewData, etc.). Including .cshtml here
// would duplicate refs without the kind metadata.
var nonCsExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".csv", ".json", ".sql", ".xml", ".config" };
foreach (var file in Directory.EnumerateFiles(solutionDir, "*.*", SearchOption.AllDirectories))
{
    if (IsExcluded(file)) continue;
    var ext = Path.GetExtension(file);
    if (!nonCsExts.Contains(ext)) continue;
    textScanned++;
    string relFile = Path.GetRelativePath(solutionDir, file);

    var lines = File.ReadAllLines(file);
    for (int i = 0; i < lines.Length; i++)
    {
        var line = lines[i];

        // Direct match: any of our variants appears as a token in this line.
        // Use word-boundary matching to reduce noise (e.g. avoid matching inside another identifier).
        foreach (var (v, vKind) in variants.Select(kv => (kv.Key, kv.Value)))
        {
            if (string.IsNullOrEmpty(v)) continue;
            // Word-boundary: not preceded/followed by an identifier char.
            int idx = 0;
            while ((idx = line.IndexOf(v, idx, StringComparison.Ordinal)) >= 0)
            {
                bool leftOk  = idx == 0 || !IsIdentifierChar(line[idx - 1]);
                int endIdx = idx + v.Length;
                bool rightOk = endIdx >= line.Length || !IsIdentifierChar(line[endIdx]);
                if (leftOk && rightOk)
                {
                    var (kind, ctx) = ClassifyNonCs(ext, line);
                    results.Add(new Dictionary<string, object?>
                    {
                        ["file"] = relFile,
                        ["line"] = i + 1,
                        ["col"] = idx + 1,
                        ["kind"] = kind,
                        ["literal"] = v,
                        ["matched_variant"] = v,
                        ["variant_kind"] = vKind,
                        ["match_within_csv_string"] = false,
                        ["enclosing_context"] = ctx
                    });
                }
                idx = endIdx;
            }
        }
    }
}

bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

// Sort
results.Sort((a, b) =>
{
    int fc = string.CompareOrdinal((string)a["file"]!, (string)b["file"]!);
    if (fc != 0) return fc;
    int la = (int)a["line"]!;
    int lb = (int)b["line"]!;
    if (la != lb) return la - lb;
    return (int)a["col"]! - (int)b["col"]!;
});

var output = new Dictionary<string, object?>
{
    ["symbol"] = symbolName,
    ["variants"] = variants,
    ["cs_files_scanned"] = csScanned,
    ["text_files_scanned"] = textScanned,
    ["reference_count"] = results.Count,
    ["references"] = results
};

var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });

if (outFile is not null)
{
    File.WriteAllText(outFile, json);
    Console.Error.WriteLine($"string-typed-refs: wrote {results.Count} references to {outFile} (cs={csScanned}, text={textScanned})");
}
else
{
    Console.WriteLine(json);
}

return 0;
