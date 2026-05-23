#!/usr/bin/env dotnet-script
// razor-refs.csx — comprehensive Razor (.cshtml) reference scanner.
//
// POSITIONING (verified May 2026):
//   Microsoft fixed Razor find-references in C# Dev Kit (dotnet/razor#9804
//   closed validated on 2025-12-04). LSP `findReferences` against
//   Microsoft.CodeAnalysis.LanguageServer with the Razor LS wired up now
//   handles **typed Razor patterns** (helper lambdas, @Model.X, asp-for).
//
//   This scanner remains authoritative for what LSP CANNOT resolve by design:
//
//   1. STRING-TYPED Razor (string literals, not symbols):
//        @Html.Partial/RenderPartial/Action/RenderAction("name", ...)
//        @Url.Action/RouteUrl("name", ...)
//        @RenderSection("name", ...)
//        asp-action="X", asp-controller="X" (routing strings)
//        <input name="X"> (HTML name attribute, MVC convention-bound)
//
//   2. DYNAMIC / DICTIONARY access (no compile-time symbol):
//        ViewBag.<member>
//        ViewData["key"]
//
//   3. COMMENTED-OUT references (LSP ignores comments; we surface them):
//        @*@Html.LabelFor(m => m.X)*@   →  razor-helper-lambda-commented
//
//   The typed helper-lambda + raw-property-access + asp-for cases are also
//   matched here as **defensive fallback** for sessions where the LSP is not
//   wired (current state of this harness — see SESSION-STATE.md). When LSP is
//   correctly wired post-swap, the assembler dedupes by (file,line,col).
//
// Razor is diverse. Patterns covered (each tagged with a distinct `kind`):
//
//   1.  @Html.<Helper>For(<p> => <receiver>.<chain>)         razor-helper-lambda
//   2.  Same wrapped in @* ... *@                             razor-helper-lambda-commented
//   3.  @Html.Partial("name", ...)                            razor-string-helper-partial
//       @Html.RenderPartial("name", ...)                       razor-string-helper-renderpartial
//       @Html.Action("name", ...)                              razor-string-helper-action
//       @Html.RenderAction("name", ...)                        razor-string-helper-renderaction
//   4.  @Url.Action("name", ...)                              razor-string-url-action
//       @Url.RouteUrl("name", ...)                             razor-string-url-routeurl
//   5.  @RenderSection("name", required: ...)                 razor-string-rendersection
//   6.  @Model.<chain>                                        razor-model-property-access
//       @<localVar>.<chain>                                    razor-property-access
//   7.  ViewBag.<member>                                      razor-viewbag-access
//       ViewData["key"]                                        razor-viewdata-access
//   8.  Core MVC tag helpers:
//       <* asp-for="name" .../>                                razor-tag-helper-asp-for
//       <* asp-action="name" .../>                             razor-tag-helper-asp-action
//       <* asp-controller="name" .../>                         razor-tag-helper-asp-controller
//   9.  Plain HTML name attributes (MVC binds by convention):
//       <input name="X" .../>                                  razor-html-input-name
//  10.  Razor directives:
//       @model <Type>                                          razor-directive-model
//       @inherits <Type>                                       razor-directive-inherits
//       @inject <Type> <Name>                                  razor-directive-inject
//       @using <Namespace>                                     razor-directive-using
//
// Comment handling: every kind above is also reported with `commented: true`
// when wrapped in @*...*@ (single-line). Multi-line @*...*@ tracked via
// per-line state.
//
// Deduplication: when a helper-lambda match consumes a span, the simpler
// raw property-access scan skips matches inside that span (avoids reporting
// `model.X` twice — once as helper-lambda, once as bare property access).
//
// Usage:
//   dotnet script razor-refs.csx -- \
//     --solution-dir <dir> --symbol <MemberName> [--out <file.json>]
//
// Output JSON: { symbol, scanned_files, reference_count, references[] }.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

string? solutionDir = null;
string? symbolName  = null;
string? outFile     = null;

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
    Console.Error.WriteLine("Usage: dotnet script razor-refs.csx -- --solution-dir <dir> --symbol <MemberName> [--out <file.json>]");
    return 1;
}
if (!Directory.Exists(solutionDir))
{
    Console.Error.WriteLine($"Solution dir not found: {solutionDir}");
    return 2;
}

// --- patterns -----------------------------------------------------------

string sym = symbolName;
string symEsc = Regex.Escape(sym);

// 1. Helper-For lambdas: @Html.<X>For(<p> => <receiver>.<chain>)
var helperLambda = new Regex(
    @"@Html\.(?<helper>\w+For)\s*\(\s*\w+\s*=>\s*(?<receiver>\w+)\.(?<chain>[\w.]+)",
    RegexOptions.Compiled);

// 3. String-typed Html helpers: Partial / RenderPartial / Action / RenderAction
var stringHelper = new Regex(
    @"@Html\.(?<helper>Partial|RenderPartial|Action|RenderAction)\s*\(\s*[""'](?<name>[^""']+)[""']",
    RegexOptions.Compiled);

// 4. Url helpers: Action / RouteUrl
var urlHelper = new Regex(
    @"@Url\.(?<helper>Action|RouteUrl)\s*\(\s*[""'](?<name>[^""']+)[""']",
    RegexOptions.Compiled);

// 5. RenderSection
var renderSection = new Regex(
    @"@RenderSection\s*\(\s*[""'](?<name>[^""']+)[""']",
    RegexOptions.Compiled);

// 6. Property access — must come AFTER helper-lambda scan to avoid duplicates.
//    Match @Model.<chain> or @<id>.<chain> (id starts with letter or _).
var propertyAccess = new Regex(
    @"@(?<receiver>(?:Model|[a-zA-Z_]\w*))\.(?<chain>[\w.]+)",
    RegexOptions.Compiled);

// 7a. ViewBag.<member>
var viewBag = new Regex(
    @"\bViewBag\.(?<member>\w+)",
    RegexOptions.Compiled);

// 7b. ViewData["key"]
var viewData = new Regex(
    @"\bViewData\s*\[\s*[""'](?<key>[^""']+)[""']\s*\]",
    RegexOptions.Compiled);

// 8a. Tag helper asp-for="name"
var aspFor = new Regex(
    @"\basp-for\s*=\s*[""'](?<name>[^""']+)[""']",
    RegexOptions.Compiled);
// 8b. asp-action="name"
var aspAction = new Regex(
    @"\basp-action\s*=\s*[""'](?<name>[^""']+)[""']",
    RegexOptions.Compiled);
// 8c. asp-controller="name"
var aspController = new Regex(
    @"\basp-controller\s*=\s*[""'](?<name>[^""']+)[""']",
    RegexOptions.Compiled);

// 9. <input name="X" .../>  — match name="..." anywhere in an input/select/textarea tag.
var inputName = new Regex(
    @"<(?:input|select|textarea|button)\b[^>]*\bname\s*=\s*[""'](?<name>[^""']+)[""']",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

// 10. Razor directives
var directiveModel    = new Regex(@"^\s*@model\s+(?<type>[A-Za-z_][\w.<>?,\s]*)", RegexOptions.Compiled);
var directiveInherits = new Regex(@"^\s*@inherits\s+(?<type>[A-Za-z_][\w.<>?,\s]*)", RegexOptions.Compiled);
var directiveInject   = new Regex(@"^\s*@inject\s+(?<type>[A-Za-z_][\w.<>?,\s]+?)\s+(?<name>\w+)\s*$", RegexOptions.Compiled);
var directiveUsing    = new Regex(@"^\s*@using\s+(?<ns>[A-Za-z_][\w.]*)", RegexOptions.Compiled);

const string commentOpen  = "@*";
const string commentClose = "*@";

// --- helpers ------------------------------------------------------------

static bool IsExcluded(string path) =>
    path.Contains("/bin/") || path.Contains("/obj/") ||
    path.Contains("\\bin\\") || path.Contains("\\obj\\");

static bool PositionInLineComment(string line, int pos, bool startsInComment)
{
    bool inCmt = startsInComment;
    int p = 0;
    while (p < pos)
    {
        if (inCmt)
        {
            int c = line.IndexOf("*@", p, StringComparison.Ordinal);
            if (c < 0 || c >= pos) return true;
            inCmt = false;
            p = c + 2;
        }
        else
        {
            int o = line.IndexOf("@*", p, StringComparison.Ordinal);
            if (o < 0 || o >= pos) return false;
            inCmt = true;
            p = o + 2;
        }
    }
    return inCmt;
}

static bool LineEndsInComment(string line, bool startsInComment)
{
    bool inCmt = startsInComment;
    int p = 0;
    while (p < line.Length)
    {
        if (inCmt)
        {
            int c = line.IndexOf("*@", p, StringComparison.Ordinal);
            if (c < 0) return true;
            inCmt = false;
            p = c + 2;
        }
        else
        {
            int o = line.IndexOf("@*", p, StringComparison.Ordinal);
            if (o < 0) return false;
            inCmt = true;
            p = o + 2;
        }
    }
    return inCmt;
}

// --- scan ---------------------------------------------------------------

var results = new List<Dictionary<string, object?>>();
int scanned = 0;

void Add(string file, int lineNum, int col, string kind, string? helper, string? member, string? chain, string? modelType, bool commented, string? extra = null)
{
    var d = new Dictionary<string, object?>
    {
        ["file"] = file,
        ["line"] = lineNum,
        ["col"] = col,
        ["kind"] = commented ? kind + "-commented" : kind,
        ["helper"] = helper,
        ["member"] = member,
        ["chain"] = chain,
        ["modelType"] = modelType,
        ["commented"] = commented,
    };
    if (extra is not null) d["note"] = extra;
    results.Add(d);
}

bool LastSegmentMatches(string chain, string sym) => chain.Split('.').Last() == sym;

foreach (var file in Directory.EnumerateFiles(solutionDir, "*.cshtml", SearchOption.AllDirectories))
{
    if (IsExcluded(file)) continue;
    scanned++;

    var lines = File.ReadAllLines(file);
    string? modelType = null;
    bool startsInComment = false;
    string relFile = Path.GetRelativePath(solutionDir, file);

    for (int i = 0; i < lines.Length; i++)
    {
        var line = lines[i];

        // Track per-line ranges already consumed by structured matches; later
        // raw-property-access scan skips matches inside these ranges.
        var consumed = new List<(int start, int end)>();
        void Consume(int start, int end) => consumed.Add((start, end));
        bool InConsumed(int pos)
        {
            foreach (var (s, e) in consumed) if (pos >= s && pos < e) return true;
            return false;
        }

        // 10. directives — only relevant on a line by themselves; match first to
        //     allow modelType to be set early for downstream property-access classification.
        if (modelType is null)
        {
            var m = directiveModel.Match(line);
            if (m.Success)
            {
                modelType = m.Groups["type"].Value.Trim();
                if (modelType.Contains(sym))
                    Add(relFile, i + 1, m.Groups["type"].Index + 1, "razor-directive-model", null, sym, null, modelType, PositionInLineComment(line, m.Index, startsInComment));
            }
        }
        {
            var m = directiveInherits.Match(line);
            if (m.Success && m.Groups["type"].Value.Contains(sym))
                Add(relFile, i + 1, m.Groups["type"].Index + 1, "razor-directive-inherits", null, sym, null, m.Groups["type"].Value, PositionInLineComment(line, m.Index, startsInComment));
        }
        {
            var m = directiveInject.Match(line);
            if (m.Success && (m.Groups["type"].Value.Contains(sym) || m.Groups["name"].Value == sym))
                Add(relFile, i + 1, m.Index + 1, "razor-directive-inject", null, sym, null, m.Groups["type"].Value.Trim(), PositionInLineComment(line, m.Index, startsInComment));
        }
        {
            var m = directiveUsing.Match(line);
            if (m.Success && m.Groups["ns"].Value.EndsWith("." + sym, StringComparison.Ordinal))
                Add(relFile, i + 1, m.Index + 1, "razor-directive-using", null, sym, null, m.Groups["ns"].Value, PositionInLineComment(line, m.Index, startsInComment));
        }

        // 1. Helper-For lambdas
        foreach (Match m in helperLambda.Matches(line))
        {
            var chain = m.Groups["chain"].Value;
            if (!LastSegmentMatches(chain, sym)) continue;
            int memberOff = m.Value.LastIndexOf(sym, StringComparison.Ordinal);
            int col = m.Index + (memberOff >= 0 ? memberOff : 0) + 1;
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            Add(relFile, i + 1, col, "razor-helper-lambda", "Html." + m.Groups["helper"].Value, sym, chain, modelType, inComm);
            Consume(m.Index, m.Index + m.Length);
        }

        // 3. String-typed Html helpers
        foreach (Match m in stringHelper.Matches(line))
        {
            if (m.Groups["name"].Value != sym) continue;
            int col = m.Index + m.Value.IndexOf(sym, StringComparison.Ordinal) + 1;
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            Add(relFile, i + 1, col, "razor-string-helper-" + m.Groups["helper"].Value.ToLowerInvariant(), "Html." + m.Groups["helper"].Value, sym, null, modelType, inComm);
            Consume(m.Index, m.Index + m.Length);
        }

        // 4. Url helpers
        foreach (Match m in urlHelper.Matches(line))
        {
            if (m.Groups["name"].Value != sym) continue;
            int col = m.Index + m.Value.IndexOf(sym, StringComparison.Ordinal) + 1;
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            Add(relFile, i + 1, col, "razor-string-url-" + m.Groups["helper"].Value.ToLowerInvariant(), "Url." + m.Groups["helper"].Value, sym, null, modelType, inComm);
            Consume(m.Index, m.Index + m.Length);
        }

        // 5. RenderSection
        foreach (Match m in renderSection.Matches(line))
        {
            if (m.Groups["name"].Value != sym) continue;
            int col = m.Index + m.Value.IndexOf(sym, StringComparison.Ordinal) + 1;
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            Add(relFile, i + 1, col, "razor-string-rendersection", "RenderSection", sym, null, modelType, inComm);
            Consume(m.Index, m.Index + m.Length);
        }

        // 7a. ViewBag.<member>
        foreach (Match m in viewBag.Matches(line))
        {
            if (m.Groups["member"].Value != sym) continue;
            if (InConsumed(m.Index)) continue;
            int col = m.Index + "ViewBag.".Length + 1;
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            Add(relFile, i + 1, col, "razor-viewbag-access", "ViewBag", sym, null, modelType, inComm);
            Consume(m.Index, m.Index + m.Length);
        }

        // 7b. ViewData["key"]
        foreach (Match m in viewData.Matches(line))
        {
            if (m.Groups["key"].Value != sym) continue;
            if (InConsumed(m.Index)) continue;
            int col = m.Index + m.Value.IndexOf(sym, StringComparison.Ordinal) + 1;
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            Add(relFile, i + 1, col, "razor-viewdata-access", "ViewData", sym, null, modelType, inComm);
            Consume(m.Index, m.Index + m.Length);
        }

        // 8. Tag helpers
        foreach (Match m in aspFor.Matches(line))
        {
            if (m.Groups["name"].Value != sym && !m.Groups["name"].Value.EndsWith("." + sym, StringComparison.Ordinal)) continue;
            int col = m.Index + m.Value.IndexOf('"') + 2;  // approximate
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            Add(relFile, i + 1, col, "razor-tag-helper-asp-for", "asp-for", m.Groups["name"].Value, null, modelType, inComm);
            Consume(m.Index, m.Index + m.Length);
        }
        foreach (Match m in aspAction.Matches(line))
        {
            if (m.Groups["name"].Value != sym) continue;
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            Add(relFile, i + 1, m.Index + 1, "razor-tag-helper-asp-action", "asp-action", sym, null, modelType, inComm);
            Consume(m.Index, m.Index + m.Length);
        }
        foreach (Match m in aspController.Matches(line))
        {
            if (m.Groups["name"].Value != sym) continue;
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            Add(relFile, i + 1, m.Index + 1, "razor-tag-helper-asp-controller", "asp-controller", sym, null, modelType, inComm);
            Consume(m.Index, m.Index + m.Length);
        }

        // 9. <input name="X" .../>
        foreach (Match m in inputName.Matches(line))
        {
            if (m.Groups["name"].Value != sym) continue;
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            Add(relFile, i + 1, m.Index + 1, "razor-html-input-name", "name=", sym, null, modelType, inComm);
            Consume(m.Index, m.Index + m.Length);
        }

        // 6. Property access — last; skip anything in consumed ranges.
        foreach (Match m in propertyAccess.Matches(line))
        {
            if (InConsumed(m.Index)) continue;
            var receiver = m.Groups["receiver"].Value;
            var chain = m.Groups["chain"].Value;
            if (!LastSegmentMatches(chain, sym)) continue;
            int memberOff = m.Value.LastIndexOf(sym, StringComparison.Ordinal);
            int col = m.Index + (memberOff >= 0 ? memberOff : 0) + 1;
            bool inComm = PositionInLineComment(line, m.Index, startsInComment);
            string kind = receiver == "Model" ? "razor-model-property-access" : "razor-property-access";
            Add(relFile, i + 1, col, kind, "@" + receiver, sym, chain, modelType, inComm);
        }

        startsInComment = LineEndsInComment(line, startsInComment);
    }
}

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
    ["scanned_files"] = scanned,
    ["reference_count"] = results.Count,
    ["references"] = results
};

var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });

if (outFile is not null)
{
    File.WriteAllText(outFile, json);
    Console.Error.WriteLine($"razor-refs: wrote {results.Count} references to {outFile} (scanned {scanned} .cshtml files)");
}
else
{
    Console.WriteLine(json);
}

return 0;
