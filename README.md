# dotnet-skills

A flat library of C#/.NET skills for agent runtimes — installable in **Claude Code** as a plugin and in **OpenAI Codex CLI** via an install script. The same `SKILL.md` files serve both.

Each skill declares its own target stack in its frontmatter description. There is no `dotnet-48` vs `dotnet-10` directory split; skills targeting legacy ASP.NET MVC 5 / Web Forms / EF6 on .NET Framework 4.8 sit side-by-side with skills targeting ASP.NET Core MVC / EF Core on .NET 10.

Out of scope: Blazor, Razor Pages, desktop UI (WPF / WinForms / MAUI / Avalonia / Uno), F#, VB.NET, mobile, game dev.

## Skills

| Skill | Target stack | What it does |
|---|---|---|
| [`code-usage-knowledge-graph`](skills/code-usage-knowledge-graph/SKILL.md) | .NET Framework 4.x **and** .NET 10 (no project-load needed) | Build a knowledge graph of every usage of a C# symbol — typed C# + Razor + string-typed name-variants — before refactoring. |
| [`mvc-ui-behaviors`](skills/mvc-ui-behaviors/SKILL.md) | .NET Framework 4.8 / ASP.NET MVC 5.3 | Extract user-visible behaviors from a legacy MVC 5 slice into a Markdown artifact a separate session uses to re-implement it on .NET 10. |
| [`modular-monolith`](skills/modular-monolith/SKILL.md) | .NET 10 / C# 14 / EF Core 10 | Design a modular monolith using the in-tenant-vs-cross-system discriminator (sync DomainEvent vs IntegrationEvent + outbox). Orchestrator for the modular-* family. |
| [`modular-design`](skills/modular-design/SKILL.md) | .NET 10 / C# 14 | Inventory modules, draw the dependency graph, decide module physicality (.csproj vs folder vs namespace). |
| [`modular-shared-language`](skills/modular-shared-language/SKILL.md) | .NET 10 | Align the ubiquitous language across modules; place anti-corruption layers where they earn rent. |
| [`modular-ddd`](skills/modular-ddd/SKILL.md) | .NET 10 / C# 14 / EF Core 10 | Decide where DDD tactical patterns earn their keep, and where they are ceremony. |
| [`modular-ddd-classifier`](skills/modular-ddd-classifier/SKILL.md) | Input: legacy .NET 4.8 source · Output: .NET 10 design | Classify a legacy codebase one feature slice at a time — deep modules (Ousterhout) and hierarchical modules — for incremental modernization. |
| [`modular-solid`](skills/modular-solid/SKILL.md) | .NET 10 modular monoliths | Apply SOLID at module boundaries — surface shrinking, dependency rotation; pressure-test via blind review. |
| [`modular-vertical-slice`](skills/modular-vertical-slice/SKILL.md) | ASP.NET Core MVC 10 | Organize features as MVC Areas without renaming `/Areas/` to `/Features/`; co-locate mediator handlers next to their actions. |
| [`modular-coupling-cohesion`](skills/modular-coupling-cohesion/SKILL.md) | .NET 10 modular monoliths | Measure afferent/efferent coupling and cohesion; name god modules and false splits. |

## Install — Claude Code

```text
/plugin marketplace add marafiq/dotnet-skills
/plugin install dotnet@dotnet-skills
```

Skills become available namespaced as `/dotnet:<name>` and also auto-trigger when relevant. The marketplace points at this repo; the single plugin is named `dotnet`.

### Try locally without installing (Claude Code)

```bash
git clone https://github.com/marafiq/dotnet-skills.git
claude --plugin-dir ./dotnet-skills
```

After local edits, `/reload-plugins`.

## Install — OpenAI Codex CLI

Codex discovers skills from `~/.agents/skills/` (personal scope) and `<repo>/.agents/skills/` (per-repo scope). Clone this repo, then run the install script:

```bash
git clone https://github.com/marafiq/dotnet-skills.git
cd dotnet-skills
bash scripts/install-codex.sh
```

This creates one symlink per skill under `~/.agents/skills/`. The script is idempotent — re-run it after `git pull` to pick up new skills; the symlinks resolve to the latest source automatically.

Useful flags:

```bash
bash scripts/install-codex.sh --copy                          # copy instead of symlink (Windows / restricted FS)
bash scripts/install-codex.sh --target ~/.codex/skills        # alternative Codex skills dir
bash scripts/install-codex.sh --target /path/to/repo/.agents/skills  # per-repo install
bash scripts/install-codex.sh --dry-run                       # print what would happen
bash scripts/install-codex.sh --uninstall                     # remove only links created by this script
```

Restart Codex if it does not pick up the new skills automatically.

## Authoring

See [CLAUDE.md](CLAUDE.md) for editorial standards, scope per stack, and the conventions for adding skills (and where to put agents, commands, hooks, or MCP servers). [AGENTS.md](AGENTS.md) is the Codex-facing pointer at the same content.

## License

MIT — see [LICENSE](LICENSE).
