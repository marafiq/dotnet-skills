# CLAUDE.md — dotnet-skills

This repository **is a single Claude Code plugin** named `dotnet-skills`. It also doubles as a one-plugin marketplace so users can install it via `/plugin marketplace add marafiq/dotnet-skills`.

When you're working in this repo, you're authoring skills (and optionally agents, commands, hooks) that help other Claude Code users build .NET MVC applications.

## Scope

Both target stacks are first-class:

- **ASP.NET MVC 5.3 on .NET Framework 4.8** — legacy stack (`System.Web`, `Global.asax`, jQuery Unobtrusive AJAX, `Web.config`, Areas, conventional/attribute routing on `RouteCollection`)
- **ASP.NET Core MVC on .NET 10** — modern stack (top-level `Program.cs`, endpoint routing, built-in DI, `appsettings.json`, middleware pipeline)

**Out of scope:** Blazor, Razor Pages, Web Forms.

## Repository layout

```
dotnet-skills/
├── .claude-plugin/
│   ├── plugin.json          # this repo IS the plugin (manifest)
│   └── marketplace.json     # one-plugin marketplace; "source": "./"
├── skills/                  # all skills live here, FLAT
│   └── <skill-name>/
│       ├── SKILL.md         # required
│       ├── scripts/         # optional — executable code
│       ├── references/      # optional — docs loaded on demand
│       └── assets/          # optional — templates / output files
├── agents/                  # optional — subagent definitions
├── commands/                # optional — slash commands (legacy form, prefer skills)
├── hooks/                   # optional — hooks/hooks.json
├── CLAUDE.md
├── README.md
└── LICENSE
```

**Authoritative rules (from [docs.claude.com](https://code.claude.com/docs/en/plugins)):**

1. Only `plugin.json` and `marketplace.json` go inside `.claude-plugin/`. **Never** put `skills/`, `commands/`, `agents/`, or `hooks/` in there — they belong at the plugin root.
2. Skills are **flat** under `skills/`: `skills/<skill-name>/SKILL.md`. The folder name becomes the skill's slug. Don't nest categories like `skills/net48/foo/SKILL.md` — that breaks auto-discovery.
3. Skills installed via this plugin are namespaced as `/dotnet-skills:<skill-name>` (the plugin's `name` field is the namespace).

## Authoring a skill

A skill is a folder with `SKILL.md` plus optional supporting files:

```
skills/controller-action-results/
├── SKILL.md                  # required
├── references/
│   ├── net48-mvc5.md         # variant docs loaded on demand
│   └── net10-mvc.md
├── scripts/
│   └── generate-action.py    # optional helper
└── assets/
    └── action-template.cs    # optional template/example
```

### `SKILL.md` frontmatter

```yaml
---
description: One-line description with explicit trigger phrases AND which .NET stack(s) apply. Front-load the use case — the description is what Claude reads to decide whether to load the skill.
---

# Skill body — under 500 lines, push detail into references/
```

**Rules for the description:**

- State **what the skill does** and **when to trigger** (which user phrases / contexts).
- **Always declare the .NET stack(s)** the skill applies to. Examples:
  - `"...for ASP.NET Core MVC on .NET 10"`
  - `"...for ASP.NET MVC 5 on .NET Framework 4.8"`
  - `"...for migrating an MVC 5 controller to ASP.NET Core MVC"`
- Be specific. Vague descriptions cause Claude to miss the trigger.
- Combined `description` + `when_to_use` is truncated at 1,536 characters in the listing — front-load.

### Multi-stack skills

When a skill applies to **both** stacks, write one `SKILL.md` with the workflow + version-selection logic, and split the per-stack reference material into `references/`:

```
skills/controller-action-results/
├── SKILL.md                  # shared workflow + "if .NET 4.8 read net48-mvc5.md, else read net10-mvc.md"
└── references/
    ├── net48-mvc5.md
    └── net10-mvc.md
```

Claude reads only the relevant reference file — keeps the main skill body short and avoids cross-contamination between stacks. This is the pattern recommended in the official skill-creator docs for multi-domain skills.

### Body conventions

- Keep `SKILL.md` under ~500 lines. If you're approaching that, move detail into `references/`.
- Use imperative voice. Don't write "this skill will…"; write "do X then Y".
- Explain the **why** behind instructions — Claude reasons better with rationale than with rigid `MUST` / `NEVER`.
- Compile every C# example mentally against the targeted framework version. No pseudocode in user-facing examples.
- Avoid `${CLAUDE_PLUGIN_ROOT}` confusion: skills should reference their own bundled files via relative paths from the skill folder, or use `${CLAUDE_SKILL_DIR}` (which points to the skill's own directory, not the plugin root).

## Adding a new skill

1. Create `skills/<skill-name>/SKILL.md` with frontmatter and body.
2. Add `references/`, `scripts/`, `assets/` only if the skill needs them.
3. Bump `version` in `.claude-plugin/plugin.json` and `.claude-plugin/marketplace.json` once the skill is ready to ship.

That's it. There's no separate registration — the `skills/` directory is auto-discovered.

## Naming conventions

- **kebab-case** for every directory and file (`controller-action-results`, not `ControllerActionResults`).
- **Skills name the task, not the technology**: `controller-action-results` over `mvc-controllers`, `ef-core-migration-workflow` over `entity-framework-stuff`.
- Skill slugs become the namespace tail: `/dotnet-skills:controller-action-results`. Pick names users would actually type.

## Testing a skill locally

```bash
claude --plugin-dir /path/to/dotnet-skills
```

This loads the plugin without installing. After edits, run `/reload-plugins` inside Claude Code to pick up changes without restarting.

## Reference docs (Anthropic)

- Plugins: <https://code.claude.com/docs/en/plugins>
- Plugin marketplaces: <https://code.claude.com/docs/en/plugin-marketplaces>
- Skills: <https://code.claude.com/docs/en/skills>
- Plugins reference (full schema): <https://code.claude.com/docs/en/plugins-reference>
