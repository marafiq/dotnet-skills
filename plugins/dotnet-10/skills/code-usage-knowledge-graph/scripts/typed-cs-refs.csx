#!/usr/bin/env dotnet-script
// typed-cs-refs.csx — find typed C# references to a member name via Roslyn syntax walk.
//
// SCOPE:
//   Pure SYNTACTIC find-references. Loads each .cs file with
//   CSharpSyntaxTree.ParseText — no MSBuildWorkspace, no project graph,
//   no legacy build host required. Works equally on .NET Framework 4.x
//   and modern .NET solutions.
//
// PRECISION TRADE-OFF:
//   Without semantic model, we cannot tell that `someOtherType.PictureFileName`
//   refers to a different `PictureFileName` than ours. Mitigation: optional
//   `--type <TypeName>` filter requires that:
//     (a) the file declares or imports `<TypeName>` (best-effort), AND
//     (b) for member-access expressions, the receiver's identifier name
//         matches a heuristic for `TypeName` (lambda parameter, local
//         variable, foreach loop variable bound to TypeName, etc.).
//   When `--type` is omitted, every textual identifier match is emitted —
//   higher recall, lower precision.
//
//   For semantic-grade accuracy, route through Microsoft.CodeAnalysis.LanguageServer
//   via the LSP `findReferences` operation. This script is the project-load-free
//   equivalent: equally reliable on legacy projects, slightly less precise on
//   solutions where multiple types share member names.
//
// OUTPUT KINDS:
//   typed-cs-decl   — declaration of the member
//   typed-cs-ref    — reference (read or write)
// Each ref includes `role`: "declaration" | "read" | "write".
//
// Usage:
//   dotnet script typed-cs-refs.csx -- \
//     --solution-dir <dir> --member <MemberName> [--type <TypeName>] [--out <file.json>]

#r "nuget: Microsoft.CodeAnalysis.CSharp, 4.14.0"

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

string? solutionDir = null;
string? memberName  = null;
string? typeName    = null;
string? outFile     = null;

for (int i = 0; i < Args.Count; i++)
{
    switch (Args[i])
    {
        case "--solution-dir": solutionDir = Args[++i]; break;
        case "--member":       memberName  = Args[++i]; break;
        case "--type":         typeName    = Args[++i]; break;
        case "--out":          outFile     = Args[++i]; break;
        default: Console.Error.WriteLine($"Unknown arg: {Args[i]}"); return 1;
    }
}

if (solutionDir is null || memberName is null)
{
    Console.Error.WriteLine("Usage: dotnet script typed-cs-refs.csx -- --solution-dir <dir> --member <MemberName> [--type <TypeName>] [--out <file.json>]");
    return 1;
}
if (!Directory.Exists(solutionDir))
{
    Console.Error.WriteLine($"Solution dir not found: {solutionDir}");
    return 2;
}

bool IsExcluded(string p) =>
    p.Contains("/bin/") || p.Contains("/obj/") ||
    p.Contains("\\bin\\") || p.Contains("\\obj\\");

// --- type-context tracking (best-effort, syntactic) -------------------

// Walks a syntax tree and infers, for each member-access node, whether the
// receiver is plausibly of `typeName`. Heuristics:
//   * Lambda params: (TypeName x) =>          → x is TypeName
//   * Lambda params: x =>  (untyped)          → unknown — accept
//   * Local var:   var x = new TypeName(...)  → x is TypeName
//   * foreach (var x in items)                → unknown — accept
//   * foreach (TypeName x in items)           → x is TypeName
//   * Parameter:   (TypeName x, ...)          → x is TypeName
// All "unknown" cases are accepted (recall over precision).
sealed class TypeAffinity
{
    public string TypeName { get; }
    Dictionary<string, bool> _bindings = new(StringComparer.Ordinal); // identifier → known-to-be-T
    public TypeAffinity(string typeName) { TypeName = typeName; }

    // Returns true if `receiverName` is plausibly of TypeName (or unknown).
    public bool IsAffinityCompatible(string receiverName)
    {
        if (_bindings.TryGetValue(receiverName, out var known)) return known;
        return true; // unknown — accept (recall>precision policy)
    }

    public void NoteBinding(string name, bool isType) => _bindings[name] = isType;

    // Special tokens we always accept regardless of binding map.
    public bool IsSelfReference(string name) => name == "this" || name == "base";
}

void WalkForBindings(SyntaxNode root, TypeAffinity? affinity)
{
    if (affinity is null) return;
    foreach (var node in root.DescendantNodes())
    {
        switch (node)
        {
            case ParameterSyntax p when p.Type is IdentifierNameSyntax t && t.Identifier.Text == affinity.TypeName:
                affinity.NoteBinding(p.Identifier.Text, true);
                break;
            case ParameterSyntax p when p.Type is QualifiedNameSyntax q && q.Right.Identifier.Text == affinity.TypeName:
                affinity.NoteBinding(p.Identifier.Text, true);
                break;
            case VariableDeclaratorSyntax v when v.Initializer?.Value is ObjectCreationExpressionSyntax oc && IsTypeName(oc.Type, affinity.TypeName):
                affinity.NoteBinding(v.Identifier.Text, true);
                break;
            case ForEachStatementSyntax fe when IsTypeName(fe.Type, affinity.TypeName):
                affinity.NoteBinding(fe.Identifier.Text, true);
                break;
        }
    }
}

bool IsTypeName(TypeSyntax t, string name) =>
    t is IdentifierNameSyntax id && id.Identifier.Text == name ||
    t is QualifiedNameSyntax q && q.Right.Identifier.Text == name;

// --- per-file scan ----------------------------------------------------

var results = new List<Dictionary<string, object?>>();
int filesScanned = 0;

(string role, string? extra) ClassifyRole(SyntaxNode root, IdentifierNameSyntax id)
{
    for (var n = (SyntaxNode?)id; n is not null; n = n.Parent)
    {
        if (n is NameEqualsSyntax) return ("write", "object-initializer");
        if (n is AssignmentExpressionSyntax assn && assn.Left.Span.Contains(id.SpanStart))
            return ("write", "assignment-lhs");
        if (n is PostfixUnaryExpressionSyntax pf && pf.Operand.Span.Contains(id.SpanStart))
            return ("write", "increment-decrement");
        if (n is PrefixUnaryExpressionSyntax pr && pr.Operand.Span.Contains(id.SpanStart))
            return ("write", "increment-decrement");
        if (n is ArgumentSyntax arg && arg.RefOrOutKeyword.ValueText is "out" or "ref")
            return ("write", "out-or-ref-argument");
        // Property declaration site
        if (n is PropertyDeclarationSyntax pd && pd.Identifier == id.Identifier)
            return ("declaration", "property-decl");
        if (n is FieldDeclarationSyntax)
        {
            var vd = id.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
            if (vd is not null && vd.Identifier == id.Identifier)
                return ("declaration", "field-decl");
        }
        if (n is StatementSyntax) break;
    }
    return ("read", null);
}

void Add(string file, FileLinePositionSpan ls, string kind, string role, string? extra, string? receiver, string? snippet)
{
    results.Add(new Dictionary<string, object?>
    {
        ["file"] = file,
        ["line"] = ls.StartLinePosition.Line + 1,
        ["col"]  = ls.StartLinePosition.Character + 1,
        ["kind"] = kind,
        ["role"] = role,
        ["extra"] = extra,
        ["receiver"] = receiver,
        ["snippet"] = snippet
    });
}

string Excerpt(string source, TextSpan span, int pad = 40)
{
    int start = Math.Max(0, span.Start - pad);
    int end = Math.Min(source.Length, span.End + pad);
    return source.Substring(start, end - start).Replace('\n', ' ').Replace('\r', ' ').Trim();
}

foreach (var file in Directory.EnumerateFiles(solutionDir, "*.cs", SearchOption.AllDirectories))
{
    if (IsExcluded(file)) continue;
    filesScanned++;

    var source = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(source);
    var root = tree.GetRoot();
    string relFile = Path.GetRelativePath(solutionDir, file);

    var affinity = typeName is null ? null : new TypeAffinity(typeName);
    if (affinity is not null) WalkForBindings(root, affinity);

    // Property / field declaration sites — these aren't `IdentifierNameSyntax`,
    // their name lives on a `SyntaxToken`. Surface them separately when type filter
    // matches the enclosing type.
    foreach (var prop in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
    {
        if (prop.Identifier.Text != memberName) continue;
        if (typeName is not null)
        {
            var enclosing = prop.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (enclosing is null || enclosing.Identifier.Text != typeName) continue;
        }
        var ls = prop.Identifier.GetLocation().GetLineSpan();
        Add(relFile, ls, "typed-cs-decl", "declaration", "property-decl", null, Excerpt(source, prop.Identifier.Span));
    }
    foreach (var fld in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
    {
        foreach (var v in fld.Declaration.Variables)
        {
            if (v.Identifier.Text != memberName) continue;
            if (typeName is not null)
            {
                var enclosing = fld.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                if (enclosing is null || enclosing.Identifier.Text != typeName) continue;
            }
            var ls = v.Identifier.GetLocation().GetLineSpan();
            Add(relFile, ls, "typed-cs-decl", "declaration", "field-decl", null, Excerpt(source, v.Identifier.Span));
        }
    }

    foreach (var id in root.DescendantNodes().OfType<IdentifierNameSyntax>())
    {
        if (id.Identifier.Text != memberName) continue;

        // Filter by type affinity if a type filter is set.
        if (affinity is not null)
        {
            bool accept = false;

            // (1) Member access: obj.Member — require receiver type-affinity.
            if (id.Parent is MemberAccessExpressionSyntax ma && ma.Name == id)
            {
                string? receiverName = ma.Expression switch
                {
                    IdentifierNameSyntax rId => rId.Identifier.Text,
                    ThisExpressionSyntax => "this",
                    BaseExpressionSyntax => "base",
                    _ => LeftmostIdentifierName(ma.Expression)
                };
                if (receiverName is null) accept = true; // unresolvable receiver — accept
                else if (receiverName == typeName) accept = true; // TypeName.Member (static)
                else if (affinity.IsSelfReference(receiverName)) accept = true;
                else accept = affinity.IsAffinityCompatible(receiverName);
            }
            // (2) Object initializer entry: new TypeName { Member = X } — left-side IdentifierName.
            else if (id.Parent is AssignmentExpressionSyntax assn1 &&
                     assn1.Left == id &&
                     assn1.Parent is InitializerExpressionSyntax initExpr &&
                     initExpr.Parent is ObjectCreationExpressionSyntax oce)
            {
                accept = IsTypeName(oce.Type, typeName!);
            }
            // (3) Bare identifier inside the type's own scope — e.g. Member = X inside ctor/method
            //     of the type. Accept if enclosing type matches.
            else if (id.Parent is AssignmentExpressionSyntax assn2 && assn2.Left == id)
            {
                var enclosingType = id.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                accept = enclosingType is not null && enclosingType.Identifier.Text == typeName;
            }
            // (4) Bare identifier as expression (read inside the type's own scope).
            else if (id.Parent is not MemberAccessExpressionSyntax)
            {
                var enclosingType = id.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                accept = enclosingType is not null && enclosingType.Identifier.Text == typeName;
            }

            if (!accept) continue;
        }

        // Skip if this id is the simple-name of an already-emitted property declaration.
        // (Defensive — properties don't have IdentifierNameSyntax for their declaration name.)

        var (role, extra) = ClassifyRole(root, id);

        // Skip declarations here — we already emitted them via PropertyDeclaration/FieldDeclaration walks above.
        if (role == "declaration") continue;

        var loc = id.GetLocation().GetLineSpan();
        string? receiver = id.Parent is MemberAccessExpressionSyntax ma3 && ma3.Name == id
            ? LeftmostIdentifierName(ma3.Expression)
            : null;

        Add(relFile, loc, "typed-cs-ref", role, extra, receiver, Excerpt(source, id.Span));
    }
}

string? LeftmostIdentifierName(ExpressionSyntax expr)
{
    while (true)
    {
        switch (expr)
        {
            case IdentifierNameSyntax id: return id.Identifier.Text;
            case MemberAccessExpressionSyntax ma: expr = ma.Expression; continue;
            case InvocationExpressionSyntax inv: expr = inv.Expression; continue;
            case ElementAccessExpressionSyntax el: expr = el.Expression; continue;
            case ParenthesizedExpressionSyntax pe: expr = pe.Expression; continue;
            default: return null;
        }
    }
}

// Sort + dedupe by (file, line, col)
results = results
    .GroupBy(r => ((string)r["file"]!, (int)r["line"]!, (int)r["col"]!))
    .Select(g => g.OrderBy(r => (string)r["kind"]! == "typed-cs-decl" ? 0 : 1).First())
    .OrderBy(r => (string)r["file"]!)
    .ThenBy(r => (int)r["line"]!)
    .ThenBy(r => (int)r["col"]!)
    .ToList();

var output = new Dictionary<string, object?>
{
    ["member"] = memberName,
    ["type_filter"] = typeName,
    ["files_scanned"] = filesScanned,
    ["reference_count"] = results.Count,
    ["references"] = results
};

var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
if (outFile is not null)
{
    File.WriteAllText(outFile, json);
    Console.Error.WriteLine($"typed-cs-refs: wrote {results.Count} refs to {outFile} (scanned {filesScanned} .cs files)");
}
else
{
    Console.WriteLine(json);
}

return 0;
