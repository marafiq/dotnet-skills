# CLAUDE.md — dotnet-skills

Flat library of C#/.NET skills, installable in two agent runtimes from the same `SKILL.md` files:

- **Claude Code** — installed as the `dotnet` plugin from the `dotnet-skills` marketplace. Skills auto-discovered from `./skills/` at the repo root.
- **OpenAI Codex CLI** — installed via `scripts/install-codex.sh`, which symlinks every `skills/<name>/` folder into `~/.agents/skills/` (or `<repo>/.agents/skills/` for per-repo installs).

There is no `dotnet-48` vs `dotnet-10` directory split. Skills live flat under `./skills/`; each skill's *description* declares its target stack. The legacy stack (ASP.NET MVC 5.3 + Web Forms + EF6 on .NET Framework 4.8 / C# 8.0) and the modern stack (ASP.NET Core MVC + EF Core on .NET 10 / C# 14) are both in-scope.

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

- Grill user if you do not understand the goal, ask one question at a time, do not start the work until it is clear to you and we are in agreement.
- All the work will be reviewed by Codex. Expect multi-round feedback on substantive skills — commit history shows the pattern ("Codex Round N", `schema_version` bumps, "rule-count drift" cleanups); prefer one consolidated version bump per stable round over a bump per commit.
- We work in Senior Living Industry and people rely on our software thus you must act as a responsible engineer with extreme ownership on agreed goals.
- A rigid mindset that accepts mediocre results is not a good thing, excellence comes from practicing small things at all times, and it goes long way. If there is a fix that will have impact on the code, and is small enough should be done, commit or long session should not be used as an excuse.

## Scope

Both stacks are in-scope, flat. Each skill names its own target in the description; the directory does not carry a stack tag.

### Legacy stack — .NET Framework 4.8 with C# 8.0

Set `<LangVersion>8.0</LangVersion>` in csproj when targeting this stack.

In:
- ASP.NET MVC 5.3 — `System.Web`, `Global.asax`, `Web.config`, jQuery Unobtrusive AJAX
- ASP.NET Web Forms — `.aspx`, code-behind, ViewState, page lifecycle, server controls
- Entity Framework 6.x
- Cross-cutting on this stack: testing, validation, identity (OWIN / `System.Web` / Membership), logging, caching, error handling
- Migration patterns toward the modern stack

**C# 8 caveat.** On `net48`, C# 8 features split three ways:
- **Compiler-only — work as-is**: switch expressions, nullable reference types, pattern matching, `using` declarations, static local functions, readonly members, null-coalescing assignment.
- **Need polyfills**: async streams (`Microsoft.Bcl.AsyncInterfaces`), ranges/indices (`System.Memory`).
- **Don't work on `net48` at all**: default interface methods, some IL-level features.

Skills that demonstrate C# 8 features must declare which bucket the feature falls in and what NuGet polyfills (if any) the user needs.

### Modern stack — .NET 10 with C# 14

In:
- ASP.NET Core MVC 10 — `Program.cs`, endpoint routing, built-in DI and configuration, middleware pipeline
- Entity Framework Core 10
- Cross-cutting on this stack: xUnit / NUnit, FluentValidation / data annotations, ASP.NET Core Identity, `ILogger<T>`, `IMemoryCache` / `IDistributedCache`, ProblemDetails error handling

### Out of scope

Blazor (Server / WebAssembly), Razor Pages, desktop UI (WPF, WinForms, MAUI, Avalonia, Uno), F#, VB.NET, Unity, Godot, Xamarin. These are .NET-ecosystem but stylistically far enough from MVC-style web work to need their own libraries.

## Layout

```
.claude-plugin/
  marketplace.json     # Claude Code marketplace; lists one plugin
  plugin.json          # Claude Code plugin manifest (plugin name: "dotnet")
skills/                # FLAT — every skill is a peer here
  <name>/
    SKILL.md           # required
    references/        # optional long-form docs
    scripts/           # optional executable helpers (e.g. .csx)
    assets/            # optional templates / data
scripts/
  install-codex.sh     # links every skills/<name>/ into ~/.agents/skills/
AGENTS.md              # Codex-facing pointer
CLAUDE.md              # this file
README.md              # user-facing install + skill index
[agents/, commands/, hooks/, .mcp.json — at repo root if/when added]
```

`.claude-plugin/` holds **only** manifests. Components are auto-discovered from the repo root for Claude (plugin source is `"."`). Codex discovers each `skills/<name>/` after the install script links it into `~/.agents/skills/` or `<repo>/.agents/skills/`. Installed namespace in Claude is `/dotnet:<skill-name>`; Codex has no plugin namespacing.

## Authoring

Each skill declares its target stack in the description — there is no longer a plugin choice that pins it for you. State the stack explicitly (e.g. ".NET Framework 4.8 with ASP.NET MVC 5.3" or ".NET 10 / C# 14 / EF Core 10"). When a skill applies to both stacks, say so directly.

### Skill — `skills/<name>/SKILL.md`
Frontmatter requires `name` (kebab-case, must match the folder) and `description` (the trigger — state *what* and *when*; front-load; capped at 1,536 chars combined with `when_to_use`). Body under ~500 lines; push detail into `references/`. The same file is read by Claude Code and Codex CLI without modification.

**Orchestrator skills.** A skill may act as an orchestrator that routes to sibling sub-skills. Both the orchestrator and its sub-skills live as peers under `skills/`. The orchestrator's `description` declares the family it dispatches to; each sub-skill's `description` declares which orchestrator(s) call it. Sub-skills must be useful standalone. Treat the orchestrator pattern as a fit when a domain decomposes into a stable set of sub-decisions inside one mental model.

### Subagent — `agents/<name>.md`
Claude-only. Frontmatter: `name`, `description` (when to dispatch), optional `tools`, optional `model`. Body is the system prompt. Use specialized agents (e.g. `controller-author`, `efcore-migration-runner`) to protect the main thread's context for long, focused tasks. Codex equivalents are not configured here today.

### Slash command — `commands/<name>.md`
Claude-only. Markdown with optional frontmatter. `$ARGUMENTS` is user input. Skills are preferred for new work — commands are the legacy form retained for compatibility.

### Hook — `hooks/hooks.json`
Claude-only. Fires on Claude Code events (`PreToolUse`, `PostToolUse`, `UserPromptSubmit`, `Stop`, etc.). Use for deterministic guardrails (block, validate, normalize) — not advisory text.

### MCP server — `.mcp.json`
Claude-side configuration. Add only when the integration genuinely needs a server (auth flows, long-running connections, external state). For one-shot or scriptable work, prefer `scripts/` inside a skill.

### Path env vars across components
- Skill referencing its own files → relative paths (works in both runtimes). `CLAUDE_SKILL_DIR` is Claude-only and points at the skill folder.
- Hooks, MCP, LSP, and monitor configs referencing plugin-bundled files → `CLAUDE_PLUGIN_ROOT`. Treat as read-only — the path changes on every plugin update.
- Persistent state that must survive plugin updates → `CLAUDE_PLUGIN_DATA`. Auto-created on first reference; resolves under `~/.claude/plugins/data/{id}/`.

### Stack-variant content within a skill
When a skill applies to both stacks (e.g. `code-usage-knowledge-graph`), keep one `SKILL.md` and put per-stack detail in `references/<stack>.md`. Suggested slugs:
- `mvc5.md`, `webforms.md` — legacy web-stack variants
- `efcore.md`, `ef6.md` — ORM variants
- `net48.md`, `net10.md` — runtime variants when the whole skill divides cleanly

Most skills target one stack and need no split.

## Naming

kebab-case. Name the task, not the technology — `controller-action-results`, not `mvc-controllers`. Folder name = slug = what users actually type. The stack lives in the description, not the folder name. (`mvc-ui-behaviors` is named for the task — extracting UI behaviors from legacy MVC slices — not for ".NET Framework 4.8".)

## Commits

Conventional Commits with a scope: `<type>(<scope>): <subject>`. Valid scopes:
- A skill folder name (`code-usage-knowledge-graph`, `modular-monolith`, …) for skill-scoped work.
- `claude` for CLAUDE.md edits, `agents` for AGENTS.md, `readme` for README.md.
- `plugin` for `.claude-plugin/plugin.json` or `.claude-plugin/marketplace.json` changes.
- `codex` for `scripts/install-codex.sh` or other Codex-side wiring.
- `repo` for cross-cutting structural changes (layout moves, license, gitignore).

Types in active use: `feat`, `fix`, `docs`, `example`, `refactor`, `chore`.

## Workflow

Test the whole library locally without installing:

```bash
claude --plugin-dir /path/to/dotnet-skills          # Claude side
bash scripts/install-codex.sh --target /tmp/codex   # Codex side, dry-target
```

After edits, run `/reload-plugins` inside Claude Code. For Codex, restart the CLI if it does not pick up new skills automatically.

After publishing, verify the marketplace install path inside Claude Code:

```text
/plugin marketplace add marafiq/dotnet-skills
/plugin install dotnet@dotnet-skills
```

And the Codex install:

```bash
git clone https://github.com/marafiq/dotnet-skills.git
bash dotnet-skills/scripts/install-codex.sh
```

## Adding a skill

End-to-end checklist; each step links to the section with detail.

1. Create `skills/<kebab-name>/SKILL.md` with `name` and `description` frontmatter — see *Authoring → Skill*. The description must declare the target stack.
2. Write the body. Push detail into `references/` if length exceeds ~500 lines.
3. Load and iterate: `claude --plugin-dir .`, then `/reload-plugins` after each edit. For Codex, the symlink picks up edits automatically — no re-run needed.
4. Validate: `claude plugin validate .` from the repo root.
5. Update the skill table in [README.md](README.md) so users discover it.
6. Release: bump `version` per *Before release*.

## Before release

1. `claude plugin validate .` — checks the plugin manifest and every skill's frontmatter.
2. `claude --plugin-dir .` — exercise the plugin end-to-end before users install via the marketplace.
3. `bash scripts/install-codex.sh --dry-run` — confirm Codex enumerates every skill folder.
4. Bump `version` in `.claude-plugin/plugin.json` **and** the matching plugin entry in `.claude-plugin/marketplace.json`. Without a version bump, marketplace updates do not propagate to installed Claude users. Codex picks up edits via the live symlink with no version gate, but a version bump is still the source of truth for the release.
5. If discovery fails, run `claude --debug` to see what loaded and what didn't.

## Reference

### Claude Code (Anthropic)
- Plugins: <https://code.claude.com/docs/en/plugins>
- Skills: <https://code.claude.com/docs/en/skills>
- Subagents: <https://code.claude.com/docs/en/sub-agents>
- Hooks: <https://code.claude.com/docs/en/hooks>
- Marketplaces: <https://code.claude.com/docs/en/plugin-marketplaces>
- Plugins reference: <https://code.claude.com/docs/en/plugins-reference>

### Codex CLI (OpenAI)
- Agent Skills: <https://developers.openai.com/codex/skills>
- AGENTS.md: <https://developers.openai.com/codex/guides/agents-md>
- CLI reference: <https://developers.openai.com/codex/cli/reference>
