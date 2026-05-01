# CLAUDE.md — dotnet-skills

Claude Code plugin and one-plugin marketplace (`source: "./"`) for C#/.NET skills, agents, commands, hooks, and MCP servers.

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

Treat patterns and architectures as *candidates*, not defaults. Weigh ≥ 2 realistic options:

- Explore the **public surface from the consumer's perspective** first. Pick the notation that fits the question — method signatures, HTTP contracts, sequence / class / activity / flow diagrams, or plain prose when reasoning *is* the artifact. Verbose is sometimes the right answer; terse is sometimes the right answer. The point is to force trade-offs into the open before implementation hides them.
- For each option, name what it gets you, what it costs you, what it forecloses later.
- Record what you **rejected and why** — the rejected branch is half the lesson.
- Record what you **chose and why**, plus the conditions under which the choice would flip.

This is where critical time should go — not on writing more code, on choosing not to.

### Refuse dogma

Clean Architecture, SOLID, DDD, repository, CQRS, Result types, mediator, vertical slices — useful scaffolds, not commandments. A skill ending in "always do X" is wrong by construction. The honest answer is *"X when these conditions hold; Y when these others do; here is the seam between them."*

### Prose style

- **Concise.** Imperative voice. Lead with verbs — cut *you can*, *there is*, *there are*. Each sentence carries weight.
- **Specific.** Definite, concrete language. *"EF Core 7's `ExecuteUpdateAsync`"*, not *"EF supports updates"*.
- **Honest about trade-offs.** Prose serves the chosen path that creates value. Record rejected alternatives concisely — *what* and *why not* — so the next reader can follow the choice without re-deriving it.

### Accuracy

Match claims to what you can defend; put the warrant on the page. *"Completed X — verified by running Y; output matched Z"* beats *"done"*. *"Vaguely familiar with X"* beats false confidence. *"EF Core 7's `ExecuteUpdateAsync`"* beats *"EF supports it"*. Cite authoritative sources (Microsoft Learn, language spec) when at hand; otherwise name what you actually checked — decompiled source, a runtime test, a doc page.

When concision and precision conflict, pay the words for precision.

## Scope

### Legacy stack — **.NET Framework 4.8** with **C# 8.0**
- ASP.NET MVC 5.3 — `System.Web`, `Global.asax`, `Web.config`, jQuery Unobtrusive AJAX
- ASP.NET Web Forms — `.aspx`, code-behind, ViewState, page lifecycle, server controls
- Entity Framework 6.x

### Modern stack — **.NET 10** with **C# 14**
- ASP.NET Core MVC 10 — `Program.cs`, endpoint routing, built-in DI and configuration
- Entity Framework Core 10

### Cross-cutting (both stacks)
C# language features at the declared version; testing, validation, identity/authorization, logging, caching, error handling; migration patterns (legacy → modern).

### Out of scope
Blazor (Server / WebAssembly), Razor Pages, desktop UI (WPF, WinForms, MAUI, Avalonia, Uno), F#, VB.NET, Unity, Godot, Xamarin.

## Layout

```
.claude-plugin/
  plugin.json
  marketplace.json
skills/<name>/
  SKILL.md             # required
  references/          # variant-specific detail, on-demand reading
  scripts/             # executable helpers
  assets/              # templates / output
agents/<name>.md       # subagents
commands/<name>.md     # slash commands
hooks/hooks.json       # event handlers
.mcp.json              # MCP servers
```

`.claude-plugin/` holds **only** manifests. Components are auto-discovered. Installed namespace is `/dotnet-skills:<name>`. The marketplace lists this single plugin with `"source": "./"` — don't restructure that unless splitting the repo into multiple plugins.

## Authoring

### Skill — `skills/<name>/SKILL.md`
Frontmatter requires `name` (kebab-case, must match the folder) and `description` (the trigger — state *what*, *when*, *which variant*; front-load; capped at 1,536 chars combined with `when_to_use`). Body under ~500 lines; push detail into `references/`.

### Subagent — `agents/<name>.md`
Frontmatter: `name`, `description` (when to dispatch), optional `tools`, optional `model`. Body is the system prompt. Use specialized agents (e.g. `controller-author`, `efcore-migration-runner`) to protect the main thread's context for long, focused tasks.

### Slash command — `commands/<name>.md`
Markdown with optional frontmatter. `$ARGUMENTS` is user input. Skills are preferred for new work — commands are the legacy form retained for compatibility.

### Hook — `hooks/hooks.json`
Fires on Claude Code events (`PreToolUse`, `PostToolUse`, `UserPromptSubmit`, `Stop`, etc.). Use for deterministic guardrails (block, validate, normalize) — not advisory text.

### MCP server — `.mcp.json`
Only when the integration genuinely needs a server (auth flows, long-running connections, external state). For one-shot or scriptable work, prefer `scripts/` inside a skill.

### Path env vars across components
- Skill referencing its own files → `CLAUDE_SKILL_DIR` or relative paths. Points at the skill folder, not the plugin root.
- Hooks, MCP, LSP, and monitor configs referencing plugin-bundled files → `CLAUDE_PLUGIN_ROOT`. Treat as read-only — the path changes on every plugin update.
- Persistent state that must survive plugin updates (dependency installs, caches, generated code) → `CLAUDE_PLUGIN_DATA`. Auto-created on first reference; resolves under `~/.claude/plugins/data/{id}/`.

### Multi-variant content
Per-variant detail in `references/<variant>.md`; `SKILL.md` selects by version and readers load only the matching file. Slugs:
- Runtime: `net48-cs8.md`, `net10-cs14.md`
- ORM: `ef6.md`, `efcore.md`
- Web stack: `mvc5.md`, `webforms.md`, `aspnetcore-mvc.md`

Do not duplicate variant content in `SKILL.md`.

## Naming

kebab-case. Name the task, not the technology — `ef-migration-workflow`, not `entity-framework-stuff`. Folder name = slug = what users actually type.

## Workflow

Test locally without installing:
```bash
claude --plugin-dir /path/to/dotnet-skills
```
After edits, run `/reload-plugins`.

## Before release

1. `claude plugin validate .` — checks `plugin.json`, skill/agent/command frontmatter, and `hooks/hooks.json` for syntax and schema errors.
2. `claude --plugin-dir .` — exercise the plugin end-to-end locally before installing from the marketplace.
3. Bump `version` in both `plugin.json` and `marketplace.json`. With a fixed `version` set, marketplace updates don't propagate to installed users until the field changes.
4. If discovery fails, run `claude --debug` to see what loaded and what didn't.

## Reference (Anthropic docs)

Linked live. A local mirror under `docs/anthropic/` is on the table — open if turns start hitting these pages frequently.

- Plugins: <https://code.claude.com/docs/en/plugins>
- Skills: <https://code.claude.com/docs/en/skills>
- Subagents: <https://code.claude.com/docs/en/sub-agents>
- Hooks: <https://code.claude.com/docs/en/hooks>
- Marketplaces: <https://code.claude.com/docs/en/plugin-marketplaces>
- Plugins reference: <https://code.claude.com/docs/en/plugins-reference>
