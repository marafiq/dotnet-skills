# CLAUDE.md — dotnet-skills

This repo **is** a Claude Code plugin named `dotnet-skills` and a one-plugin marketplace pointing at `./`. When you're in this repo, you're authoring skills for .NET MVC users.

## Scope

First-class targets:
- **ASP.NET MVC 5.3 on .NET Framework 4.8** (legacy: `System.Web`, `Global.asax`, `Web.config`, jQuery Unobtrusive AJAX)
- **ASP.NET Core MVC on .NET 10** (modern: top-level `Program.cs`, endpoint routing, built-in DI)

Out of scope: Blazor, Razor Pages, Web Forms.

## Layout

```
.claude-plugin/
  plugin.json          # plugin manifest
  marketplace.json     # one-plugin marketplace, source: "./"
skills/<name>/         # flat — one level only
  SKILL.md             # required
  references/          # optional, on-demand reading
  scripts/             # optional, executable
  assets/              # optional, templates / output
```

**Hard rules** ([docs](https://code.claude.com/docs/en/plugins)):
1. `.claude-plugin/` holds only manifests. `skills/`, `agents/`, `commands/`, `hooks/` go at repo root.
2. Skills are flat: `skills/<name>/SKILL.md`. No category subdirs — breaks auto-discovery.
3. Installed skills are namespaced as `/dotnet-skills:<name>`.

## Authoring a skill

Frontmatter:
```yaml
---
description: What it does AND when to trigger AND which .NET stack(s). Front-loaded — Claude uses this to decide whether to load. Combined description + when_to_use is capped at 1,536 chars.
---
```

Body rules:
- Under ~500 lines. Push detail into `references/`.
- Imperative voice. Explain *why*, not just *what* — rationale beats rigid `MUST`/`NEVER`.
- C# examples must compile against the declared framework version. No pseudocode.
- Reference bundled files via relative paths or `${CLAUDE_SKILL_DIR}` (skill dir, not plugin root). Do not use `${CLAUDE_PLUGIN_ROOT}` here.

**Multi-stack skills**: one `SKILL.md` with workflow + version-selection logic, per-stack detail in `references/net48-mvc5.md` and `references/net10-mvc.md`. Claude reads only the relevant file.

**Description must declare the stack(s)**, e.g. `"…for ASP.NET Core MVC on .NET 10"` or `"…for migrating MVC 5 → ASP.NET Core MVC"`. Vague descriptions miss triggers.

## Naming

- kebab-case everywhere (`controller-action-results`, not `ControllerActionResults`).
- Name the task, not the technology: `ef-core-migration-workflow`, not `entity-framework-stuff`.
- The folder name is the slug — pick what users would type.

## Workflow

Add a skill: drop `skills/<name>/SKILL.md`, optional supporting dirs. Auto-discovered, no registration step. Bump `version` in both `plugin.json` and `marketplace.json` when ready to ship.

Test locally:
```bash
claude --plugin-dir /path/to/dotnet-skills
```
Run `/reload-plugins` after edits.

## Refs

- Plugins: <https://code.claude.com/docs/en/plugins>
- Skills: <https://code.claude.com/docs/en/skills>
- Marketplaces: <https://code.claude.com/docs/en/plugin-marketplaces>
- Full schema: <https://code.claude.com/docs/en/plugins-reference>
