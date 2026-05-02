# CLAUDE.md — dotnet-skills

Claude Code marketplace hosting **two plugins** for C#/.NET work. Users install only the runtime they actually ship to:

- **`dotnet-48`** — ASP.NET MVC 5.3, Web Forms, EF6 on **.NET Framework 4.8** with **C# 8.0** (compiler subset; see caveats below). Includes migration patterns toward ASP.NET Core.
- **`dotnet-10`** — ASP.NET Core MVC, EF Core on **.NET 10** with **C# 14**.

## Editorial standards (read first)

This repo produces two shapes of skill:

- **Reference skills** — for APIs and features that already exist. Value is correctness and specificity. *"EF Core 7 introduced set-based `ExecuteUpdateAsync` / `ExecuteDeleteAsync`"* beats hand-waving. Be terse and exact.
- **Design skills** — for choices about architecture, patterns, and contracts. Value is surfacing options and trade-offs. The reader should leave understanding the *space* of choices, not just the chosen one.

Most non-trivial skills are design-shaped. The next four sections are the bar for those. Pure reference skills (small, unambiguous API surfaces) skip *Frame the problem* and *Discover before deciding* — go straight to the API.

### Frame the problem before the code

Open with:
- **Problem.** What is being solved, in one plain sentence?
- **Audience.** Who runs this? What do they know and not know?
- **Requirements.** Functional (what it must do) and non-functional (latency, throughput, security, operability, cost).

Code written without these fits an answer to a question no one asked.

### Discover before deciding

Treat patterns and architectures as *candidates*, not defaults. Weigh ≥ 2 realistic options when there is genuine architectural ambiguity. For canonical, one-right-answer questions, skip the comparison and state the answer.

- Explore the **public surface from the consumer's perspective** first. Pick the notation that fits the question — method signatures, HTTP contracts, sequence / class / activity / flow diagrams, or plain prose when reasoning *is* the artifact. Verbose is sometimes the right answer; terse is sometimes the right answer. The point is to force trade-offs into the open before implementation hides them.
- For each option, name what it gets you, what it costs, what it forecloses later.
- Record what you **rejected and why** — the rejected branch is half the lesson.
- Record what you **chose and why**, plus the conditions under which the choice would flip.

### Refuse dogma

Clean Architecture, SOLID, DDD, repository, CQRS, Result types, mediator, vertical slices — useful scaffolds, not commandments. Name the conditions that make a pattern apply, and the conditions that make it noise. *"X when these conditions hold; Y when these others do; here is the seam between them"* beats *"always do X"*.

### Prose style

- **Concise.** Imperative voice. Lead with verbs — cut *you can*, *there is*, *there are*. Each sentence carries weight.
- **Specific.** Definite, concrete language. *"EF Core 7's `ExecuteUpdateAsync`"*, not *"EF supports updates"*.
- **Honest about trade-offs.** Prose serves the chosen path that creates value. Record rejected alternatives concisely — *what* and *why not* — so the next reader can follow the choice without re-deriving it.

### Accuracy

Match claims to what you can defend; put the warrant on the page. *"Completed X — verified by running Y; output matched Z"* beats *"done"*. *"Vaguely familiar with X"* beats false confidence. *"EF Core 7's `ExecuteUpdateAsync`"* beats *"EF supports it"*. Cite authoritative sources (Microsoft Learn, language spec) when at hand; otherwise name what you actually checked — decompiled source, a runtime test, a doc page.

When concision and precision conflict, pay the words for precision.
## Ethics 
- Grill user if you do not understand the goal, ask one question at a time, do not start the work untill it is clear to you and we are in agreement. 
- All the work will be reviewed by Codex 
- We work in Senior Living Industry and people rely on our software thus you must act as a responsible [role] with extreme ownership on agreed goals.
- A rigid mindset that accepts medicore results is not a good thing, excellence comes from practicing small things at all times, and it goes long way. If there is a fix that will have impact on the code, and is small enough should be done, commit or long session should not be used as an excuse.

## Scope per plugin

### `dotnet-48`
Target: **.NET Framework 4.8** with **C# 8.0** (set `<LangVersion>8.0</LangVersion>` in csproj).

In:
- ASP.NET MVC 5.3 — `System.Web`, `Global.asax`, `Web.config`, jQuery Unobtrusive AJAX
- ASP.NET Web Forms — `.aspx`, code-behind, ViewState, page lifecycle, server controls
- Entity Framework 6.x
- Cross-cutting on this stack: testing, validation, identity (OWIN / `System.Web` / Membership), logging, caching, error handling
- Migration patterns toward `dotnet-10`

**C# 8 caveat.** On `net48`, C# 8 features split three ways:
- **Compiler-only — work as-is**: switch expressions, nullable reference types, pattern matching, `using` declarations, static local functions, readonly members, null-coalescing assignment.
- **Need polyfills**: async streams (`Microsoft.Bcl.AsyncInterfaces`), ranges/indices (`System.Memory`).
- **Don't work on `net48` at all**: default interface methods, some IL-level features.

Skills that demonstrate C# 8 features must declare which bucket the feature falls in and what NuGet polyfills (if any) the user needs.

### `dotnet-10`
Target: **.NET 10** with **C# 14**.

In:
- ASP.NET Core MVC 10 — `Program.cs`, endpoint routing, built-in DI and configuration, middleware pipeline
- Entity Framework Core 10
- Cross-cutting on this stack: xUnit / NUnit, FluentValidation / data annotations, ASP.NET Core Identity, `ILogger<T>`, `IMemoryCache` / `IDistributedCache`, ProblemDetails error handling

### Out of scope (both plugins)
Blazor (Server / WebAssembly), Razor Pages, desktop UI (WPF, WinForms, MAUI, Avalonia, Uno), F#, VB.NET, Unity, Godot, Xamarin. These are .NET-ecosystem but stylistically far enough from MVC-style web work to need their own plugins later if anyone wants them.

## Layout

```
.claude-plugin/
  marketplace.json            # lists both plugins
plugins/
  dotnet-48/
    .claude-plugin/
      plugin.json
    skills/<name>/
      SKILL.md
      references/             # optional, used for MVC 5 ↔ Web Forms variant split inside this plugin
      scripts/, assets/       # optional
    [agents/, commands/, hooks/, .mcp.json — at plugin root if used]
  dotnet-10/
    .claude-plugin/
      plugin.json
    skills/<name>/
      SKILL.md
      [references/, scripts/, assets/]
    [agents/, commands/, hooks/, .mcp.json]
```

`.claude-plugin/` directories hold **only** manifests. Components are auto-discovered from each plugin root. Installed namespaces are `/dotnet-48:<name>` and `/dotnet-10:<name>`.

## Authoring

The plugin choice already pins the runtime — you do not need to declare ".NET 4.8" or ".NET 10" in every skill description. Declare it only when a skill is variant-aware *within* its plugin (e.g. an MVC 5 vs Web Forms skill in `dotnet-48`).

### Skill — `plugins/<plugin>/skills/<name>/SKILL.md`
Frontmatter requires `name` (kebab-case, must match the folder) and `description` (the trigger — state *what* and *when*; front-load; capped at 1,536 chars combined with `when_to_use`). Body under ~500 lines; push detail into `references/`.

### Subagent — `plugins/<plugin>/agents/<name>.md`
Frontmatter: `name`, `description` (when to dispatch), optional `tools`, optional `model`. Body is the system prompt. Use specialized agents (e.g. `controller-author`, `efcore-migration-runner`) to protect the main thread's context for long, focused tasks.

### Slash command — `plugins/<plugin>/commands/<name>.md`
Markdown with optional frontmatter. `$ARGUMENTS` is user input. Skills are preferred for new work — commands are the legacy form retained for compatibility.

### Hook — `plugins/<plugin>/hooks/hooks.json`
Fires on Claude Code events (`PreToolUse`, `PostToolUse`, `UserPromptSubmit`, `Stop`, etc.). Use for deterministic guardrails (block, validate, normalize) — not advisory text.

### MCP server — `plugins/<plugin>/.mcp.json`
Only when the integration genuinely needs a server (auth flows, long-running connections, external state). For one-shot or scriptable work, prefer `scripts/` inside a skill.

### Path env vars across components
- Skill referencing its own files → `CLAUDE_SKILL_DIR` or relative paths. Points at the skill folder, not the plugin root.
- Hooks, MCP, LSP, and monitor configs referencing plugin-bundled files → `CLAUDE_PLUGIN_ROOT`. Treat as read-only — the path changes on every plugin update.
- Persistent state that must survive plugin updates → `CLAUDE_PLUGIN_DATA`. Auto-created on first reference; resolves under `~/.claude/plugins/data/{id}/`.

### Variant content within a plugin
`dotnet-48` covers two web stacks (MVC 5 and Web Forms). When a skill applies to both, put per-variant detail in `references/<variant>.md` and let `SKILL.md` select. Slugs:
- `mvc5.md`, `webforms.md` — web stack split inside `dotnet-48`
- `ef6.md`, `efcore.md` — only relevant for cross-stack skills, which should be rare given the plugin split

`dotnet-10` is single-stack (Core MVC + EF Core), so most of its skills will not need a `references/` split.

## Naming

kebab-case. Name the task, not the technology — `controller-action-results`, not `mvc-controllers`. Folder name = slug = what users actually type.

## Workflow

Test a plugin locally without installing:
```bash
claude --plugin-dir /path/to/dotnet-skills/plugins/dotnet-48
claude --plugin-dir /path/to/dotnet-skills/plugins/dotnet-10
```
After edits, run `/reload-plugins`.

## Before release

Run for **each** plugin you changed:

1. `claude plugin validate plugins/<plugin-name>` — checks the plugin manifest and component frontmatter.
2. `claude --plugin-dir plugins/<plugin-name>` — exercise it end-to-end before users install via the marketplace.
3. Bump `version` in the plugin's `plugin.json` **and** update the matching entry in the root `marketplace.json`. Without a version bump, marketplace updates do not propagate to installed users.
4. If discovery fails, run `claude --debug` to see what loaded and what didn't.

## Reference (Anthropic docs)

- Plugins: <https://code.claude.com/docs/en/plugins>
- Skills: <https://code.claude.com/docs/en/skills>
- Subagents: <https://code.claude.com/docs/en/sub-agents>
- Hooks: <https://code.claude.com/docs/en/hooks>
- Marketplaces: <https://code.claude.com/docs/en/plugin-marketplaces>
- Plugins reference: <https://code.claude.com/docs/en/plugins-reference>
