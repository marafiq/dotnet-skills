# AGENTS.md — dotnet-skills

This repo is a flat library of C#/.NET skills used by two agent runtimes:

- **OpenAI Codex CLI** — installs skills into `~/.agents/skills/` via `scripts/install-codex.sh`. Codex also auto-discovers `<repo>/.agents/skills/` when working inside a repo, so the same install script accepts `--target <repo>/.agents/skills` for per-repo installs.
- **Claude Code** — installs the same skills as the `dotnet` plugin from the `dotnet-skills` marketplace (see [README.md](README.md)).

Both runtimes read the same `SKILL.md` format (YAML frontmatter with `name` + `description`, then markdown body; optional `scripts/`, `references/`, `assets/` subdirectories). Skills are version-agnostic at the directory level — each skill's *description* declares its target stack (e.g. ".NET Framework 4.8 / MVC 5.3" or ".NET 10 / C# 14 / EF Core 10"). There is no `dotnet-48` vs `dotnet-10` directory split.

## Working in this repo

Editorial standards, scope rules, contribution conventions, and the per-skill checklist live in [CLAUDE.md](CLAUDE.md). Codex agents should read it as the source of truth for *how* skills are written here. Notable points:

- Two skill shapes: **reference** (correctness, terse, API-exact) and **design** (surface options + trade-offs, name what is rejected and why). Most skills here are design-shaped.
- Frame the problem before the code: Problem, Audience, Functional + Non-functional requirements.
- Refuse dogma. Patterns (Clean Architecture, SOLID, DDD, repository, CQRS) are tools, not commandments; name the conditions under which each applies.
- Prose style: concise, specific, imperative; pay extra words only for precision.
- Senior-living-industry codebases run on this work — act as a responsible engineer with extreme ownership on agreed goals. Grill the user when the goal is unclear; do not start implementation until alignment is real.

## Layout

```
.claude-plugin/
  marketplace.json     # Claude Code marketplace manifest
  plugin.json          # Claude Code plugin manifest (plugin name: "dotnet")
skills/                # FLAT. Every skill lives directly here, no version nesting.
  <name>/SKILL.md      # required
  <name>/references/   # optional long-form docs
  <name>/scripts/      # optional executable helpers
  <name>/assets/       # optional templates / data
scripts/
  install-codex.sh     # links every skill into ~/.agents/skills/ (or a custom dir)
AGENTS.md              # this file
CLAUDE.md              # authoritative editorial + scope guidance
README.md              # install instructions for both runtimes
```

## Codex-specific notes

- `scripts/install-codex.sh` defaults to `~/.agents/skills/`. Codex equivalently reads `~/.codex/skills/`; if your install uses that path, pass `--target $HOME/.codex/skills`.
- Skills with `scripts/` (currently `code-usage-knowledge-graph`) ship `.csx` helpers meant to run under `dotnet-script`. The scripts have no project-load dependency, so they work against any .NET source tree.
- This repo intentionally does not ship `agents/openai.yaml` per-skill overrides. If a skill needs Codex-specific configuration in the future, add it inside that skill's folder rather than at the repo level.

## Out of scope

Blazor (Server / WebAssembly), Razor Pages, desktop UI (WPF / WinForms / MAUI / Avalonia / Uno), F#, VB.NET, Unity, Godot, Xamarin. These are .NET-ecosystem but stylistically far from MVC-style web work and would need their own libraries.
