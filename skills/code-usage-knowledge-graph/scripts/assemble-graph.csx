#!/usr/bin/env dotnet-script
// assemble-graph.csx — assemble the code-usage knowledge graph for a target
// concept by orchestrating the three scanners (typed-cs, razor, string-typed)
// and composing their outputs into a single navigable JSON the LLM can use
// without re-querying the codebase.
//
// SHAPE OF THE OUTPUT (`graph.json`):
//
//   {
//     "concept": "<TypeName>.<MemberName>",
//     "declaration": { file, line, col },
//     "nodes": [ { id, kind, file, line, col, role, extra... } ],
//     "edges": [ { from, to, relation } ],
//     "summary": {
//       "total_refs": N,
//       "by_kind": {...},
//       "by_role": {...},
//       "mutation_sites": M,
//       "read_sites": R,
//       "contract_boundaries": [...],
//       "drift_smells": [...]
//     },
//     "scanner_versions": {...}
//   }
//
// Usage (full):
//   dotnet script assemble-graph.csx -- \
//     --solution-dir <dir> --member <Name> --type <TypeName> [--out graph.json]
//
//   Internally invokes typed-cs-refs.csx, razor-refs.csx, string-typed-refs.csx.
//
// Usage (compose pre-computed JSONs):
//   dotnet script assemble-graph.csx -- \
//     --concept "Type.Member" \
//     --typed-refs typed.json --razor-refs razor.json --string-typed-refs string.json \
//     --out graph.json

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

string? solutionDir   = null;
string? typeName      = null;
string? memberName    = null;
string? concept       = null;
string? typedRefsPath = null;
string? razorRefsPath = null;
string? stringRefsPath= null;
string? outFile       = null;

for (int i = 0; i < Args.Count; i++)
{
    switch (Args[i])
    {
        case "--solution-dir":     solutionDir   = Args[++i]; break;
        case "--type":             typeName      = Args[++i]; break;
        case "--member":           memberName    = Args[++i]; break;
        case "--concept":          concept       = Args[++i]; break;
        case "--typed-refs":       typedRefsPath = Args[++i]; break;
        case "--razor-refs":       razorRefsPath = Args[++i]; break;
        case "--string-typed-refs":stringRefsPath= Args[++i]; break;
        case "--out":              outFile       = Args[++i]; break;
        default: Console.Error.WriteLine($"Unknown arg: {Args[i]}"); return 1;
    }
}

// Resolve concept / type / member.
if (concept is not null && typeName is null && memberName is null)
{
    var parts = concept.Split('.');
    if (parts.Length >= 2)
    {
        memberName = parts.Last();
        typeName = parts[parts.Length - 2];
    }
}

if (memberName is null || typeName is null)
{
    Console.Error.WriteLine("Need either --concept Type.Member, or both --type and --member.");
    return 1;
}
concept ??= $"{typeName}.{memberName}";

// --- run scanners (or load pre-computed) -------------------------------

string scriptsDir = Path.GetDirectoryName(Path.GetFullPath(typeof(object).Assembly.Location)) ?? ".";
// Prefer scripts located alongside this script.
string thisDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";

string GetScriptPath()
{
    // dotnet-script runs the .csx; the script file path is exposed via env var when launched.
    // Fallback: assume working directory.
    return Environment.GetEnvironmentVariable("CSX_SCRIPT_PATH") ?? Directory.GetCurrentDirectory();
}

string ResolveSibling(string name)
{
    // First try the dir of THIS script (alongside).
    foreach (var dir in new[] { thisDir, Directory.GetCurrentDirectory() })
    {
        var p = Path.Combine(dir, name);
        if (File.Exists(p)) return p;
    }
    return name; // last-ditch — assume PATH or relative
}

JsonElement RunOrLoad(string? prePath, string scriptName, string[] scriptArgs)
{
    string path;
    if (prePath is not null && File.Exists(prePath))
    {
        path = prePath;
        Console.Error.WriteLine($"  using pre-computed: {path}");
    }
    else
    {
        if (solutionDir is null)
        {
            Console.Error.WriteLine($"Cannot run {scriptName}: --solution-dir not provided and no pre-computed --*-refs path.");
            Environment.Exit(2);
        }
        path = Path.Combine(Path.GetTempPath(), $"{scriptName}.{Guid.NewGuid():N}.json");
        var scriptPath = ResolveSibling(scriptName);
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("script");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("--");
        foreach (var a in scriptArgs) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("--out");
        psi.ArgumentList.Add(path);

        Console.Error.WriteLine($"  running: dotnet script {scriptPath} -- {string.Join(" ", scriptArgs)} --out {path}");
        var p = Process.Start(psi)!;
        p.WaitForExit();
        var stderr = p.StandardError.ReadToEnd();
        if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine($"  [{scriptName}] {stderr.Trim()}");
        if (p.ExitCode != 0)
        {
            Console.Error.WriteLine($"  {scriptName} failed with exit {p.ExitCode}");
            Environment.Exit(3);
        }
    }
    using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
    return doc.RootElement.Clone();
}

Console.Error.WriteLine($"assemble-graph: concept={concept}");

var typedDoc = RunOrLoad(typedRefsPath, "typed-cs-refs.csx",
    new[] { "--solution-dir", solutionDir!, "--member", memberName, "--type", typeName });
var razorDoc = RunOrLoad(razorRefsPath, "razor-refs.csx",
    new[] { "--solution-dir", solutionDir!, "--symbol", memberName });
var stringDoc = RunOrLoad(stringRefsPath, "string-typed-refs.csx",
    new[] { "--solution-dir", solutionDir!, "--symbol", memberName });

// --- assemble nodes / edges / summary ----------------------------------

var nodes = new List<Dictionary<string, object?>>();
var edges = new List<Dictionary<string, object?>>();
int idCounter = 0;
string NextId() => "n" + (++idCounter);

(string file, int line, int col) Loc(JsonElement r) =>
    (r.GetProperty("file").GetString()!, r.GetProperty("line").GetInt32(), r.GetProperty("col").GetInt32());

string? StrOrNull(JsonElement r, string prop) =>
    r.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

bool BoolOrFalse(JsonElement r, string prop) =>
    r.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;

// Track seen positions to dedupe across sources (typed wins > razor > string-typed).
var seen = new Dictionary<(string, int, int), Dictionary<string, object?>>();

void AddNode(string source, JsonElement r, int rank)
{
    var (f, l, c) = Loc(r);
    var key = (f, l, c);
    if (seen.TryGetValue(key, out var existing))
    {
        // Already added by a higher-ranked source — append note about overlap.
        var sources = existing["sources"] as List<string> ?? new List<string>();
        sources.Add(source);
        existing["sources"] = sources;
        return;
    }

    var node = new Dictionary<string, object?>
    {
        ["id"] = NextId(),
        ["file"] = f,
        ["line"] = l,
        ["col"] = c,
        ["kind"] = r.GetProperty("kind").GetString(),
        ["role"] = StrOrNull(r, "role"),
        ["sources"] = new List<string> { source }
    };

    foreach (var p in new[] { "extra", "receiver", "snippet", "helper", "member", "chain", "modelType",
                              "literal", "matched_variant", "variant_kind", "match_within_csv_string",
                              "enclosing_context", "commented", "note" })
    {
        if (r.TryGetProperty(p, out var v))
        {
            node[p] = v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => v.GetInt32(),
                JsonValueKind.Null => null,
                _ => v.ToString()
            };
        }
    }

    nodes.Add(node);
    seen[key] = node;

    // Edge: concept → node (relation depends on role)
    string relation = ((string?)node["role"]) switch
    {
        "declaration" => "declared-at",
        "write" => "mutated-by",
        "read" => "read-by",
        _ => "referenced-by"
    };
    edges.Add(new()
    {
        ["from"] = "concept",
        ["to"] = node["id"],
        ["relation"] = relation
    });
}

// Highest priority: typed-cs (definitive symbol-bound)
foreach (var r in typedDoc.GetProperty("references").EnumerateArray())
    AddNode("typed-cs", r, 0);
// Then razor (specific kinds)
foreach (var r in razorDoc.GetProperty("references").EnumerateArray())
    AddNode("razor", r, 1);
// Then string-typed (catches what neither resolved)
foreach (var r in stringDoc.GetProperty("references").EnumerateArray())
    AddNode("string-typed", r, 2);

// --- declaration node lookup -------------------------------------------

var decl = nodes.FirstOrDefault(n => (string?)n["role"] == "declaration");

// --- summary ----------------------------------------------------------

var byKind = nodes.GroupBy(n => (string)n["kind"]!).ToDictionary(g => g.Key, g => g.Count());
var byRole = nodes.GroupBy(n => (string?)n["role"] ?? "(none)").ToDictionary(g => g.Key, g => g.Count());
int mutSites = nodes.Count(n => (string?)n["role"] == "write");
int readSites = nodes.Count(n => (string?)n["role"] == "read");

// Contract boundaries — heuristics based on `kind` patterns.
var contractBoundaries = new List<Dictionary<string, object?>>();
foreach (var n in nodes)
{
    var kind = (string?)n["kind"] ?? "";
    string? cbKind = kind switch
    {
        "string-typed-bind-attribute"               => "mvc-bind-attribute",
        var k when k.StartsWith("razor-string-helper-action") => "mvc-action-route",
        var k when k.StartsWith("razor-string-url-action")    => "mvc-url-action",
        var k when k.StartsWith("razor-tag-helper-asp-action")=> "core-mvc-asp-action",
        "data-csv-header"                            => "data-file-binding",
        "string-typed-json-attribute"                => "json-serialization-contract",
        "string-typed-display-attribute"             => "ui-display-contract",
        _ => null
    };
    if (cbKind is null) continue;
    contractBoundaries.Add(new()
    {
        ["kind"] = cbKind,
        ["file"] = n["file"],
        ["line"] = n["line"],
        ["nodeId"] = n["id"]
    });
}

// Drift smells — variants of the same name with different casing.
var driftSmells = new List<Dictionary<string, object?>>();
var variantsSeen = nodes
    .Where(n => n.TryGetValue("matched_variant", out var v) && v is string)
    .Select(n => (variant: (string)n["matched_variant"]!, kind: (string?)n["variant_kind"]))
    .GroupBy(t => t.variant)
    .ToDictionary(g => g.Key, g => g.First().kind);
if (variantsSeen.Count > 1)
{
    driftSmells.Add(new()
    {
        ["smell"] = "case-variant-drift",
        ["evidence"] = variantsSeen.Select(kv => new Dictionary<string, object?>
        {
            ["variant"] = kv.Key,
            ["variant_kind"] = kv.Value
        }).ToList(),
        ["severity"] = "high",
        ["note"] = "Multiple casing variants of the symbol name appear as string-typed references; refactor must update each variant in its own casing."
    });
}

// --- output -----------------------------------------------------------

var graph = new Dictionary<string, object?>
{
    ["concept"] = concept,
    ["declaration"] = decl is null ? null : new Dictionary<string, object?>
    {
        ["file"] = decl["file"],
        ["line"] = decl["line"],
        ["col"]  = decl["col"]
    },
    ["nodes"] = nodes,
    ["edges"] = edges,
    ["summary"] = new Dictionary<string, object?>
    {
        ["total_refs"] = nodes.Count,
        ["by_kind"] = byKind,
        ["by_role"] = byRole,
        ["mutation_sites"] = mutSites,
        ["read_sites"] = readSites,
        ["contract_boundaries"] = contractBoundaries,
        ["drift_smells"] = driftSmells
    },
    ["sources"] = new Dictionary<string, object?>
    {
        ["typed_cs_count"]    = typedDoc.GetProperty("reference_count").GetInt32(),
        ["razor_count"]       = razorDoc.GetProperty("reference_count").GetInt32(),
        ["string_typed_count"]= stringDoc.GetProperty("reference_count").GetInt32()
    }
};

var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
if (outFile is not null)
{
    File.WriteAllText(outFile, json);
    Console.Error.WriteLine($"assemble-graph: wrote {nodes.Count} nodes / {edges.Count} edges / {contractBoundaries.Count} boundaries / {driftSmells.Count} drift smells to {outFile}");
}
else
{
    Console.WriteLine(json);
}

return 0;
