# CLAUDE.md — dotnet-skills

This repository hosts **Claude Code skills and plugins for the .NET ecosystem**. When you're working in this repo, you are authoring skills, commands, agents, and hooks that help other Claude Code users build .NET MVC applications.

## Scope

Both target stacks are first-class:

- **ASP.NET MVC 5.3 on .NET Framework 4.8** — the legacy stack (System.Web, `Global.asax`, jQuery Unobtrusive AJAX, Web.config, Areas, conventional/attribute routing on `RouteCollection`)
- **ASP.NET Core MVC on .NET 10** — the modern stack (top-level `Program.cs`, endpoint routing, built-in DI, `appsettings.json`, middleware pipeline)

Skills must explicitly declare which stack(s) they apply to — either in the `description` frontmatter or in the body. Migration-focused skills that span both stacks are encouraged.

**Out of scope:** Blazor, Razor Pages, Web Forms.

## Repository layout

```
dotnet-skills/
├── .claude-plugin/
│   └── marketplace.json     # marketplace index — every plugin in this repo
└── plugins/
    └── <plugin-name>/
        ├── .claude-plugin/
        │   └── plugin.json  # plugin manifest (required)
        ├── skills/          # skills (each in its own subdir with SKILL.md)
        ├── commands/        # slash commands (.md files)
        ├── agents/          # subagent definitions (.md files)
        ├── hooks/
        │   └── hooks.json   # event-driven hooks
        └── scripts/         # helper scripts referenced by hooks/commands
```

Each plugin is fully self-contained under `plugins/<name>/`. Skills, commands, agents, and hooks all live inside the plugin that owns them.

## Conventions

- **kebab-case** for every directory and file name (plugin names, skill names, command names, script names)
- **Skill names describe the task, not the technology**: prefer `controller-action-results` over `mvc-controllers`, `ef-core-migration-workflow` over `entity-framework-stuff`
- **Declare the .NET version** in every skill description so Claude triggers it correctly. Examples:
  - `"Use when defining controller actions in ASP.NET Core MVC (.NET 10)…"`
  - `"Use when configuring routes in ASP.NET MVC 5 (.NET Framework 4.8)…"`
  - `"Use when migrating an MVC 5 controller to ASP.NET Core MVC…"`
- **Use `${CLAUDE_PLUGIN_ROOT}`** for any intra-plugin path reference in hooks, MCP servers, scripts. Never hardcode absolute paths or use `~`.
- **No invented code style** — base examples on real .NET conventions (Microsoft docs, official templates) or the user's existing project. Don't fabricate APIs or attributes.
- **Every public C# example should compile** against the targeted framework version. If a skill shows code, it must be runnable, not pseudocode.

## Adding a new plugin

1. Create `plugins/<plugin-name>/.claude-plugin/plugin.json` with at least:
   ```json
   {
     "name": "<plugin-name>",
     "version": "0.1.0",
     "description": "What this plugin does and which .NET stack(s) it targets",
     "author": { "name": "marafiq" },
     "license": "MIT"
   }
   ```
2. Add a corresponding entry to the root `.claude-plugin/marketplace.json` `plugins` array, pointing at `./plugins/<plugin-name>`.
3. If the plugin changes the repo's overall scope, update this `CLAUDE.md`.

## Adding a new skill

Skills live at `plugins/<plugin-name>/skills/<skill-name>/SKILL.md`.

`SKILL.md` frontmatter:

```yaml
---
name: <Skill Name>
description: When to use this skill. Trigger on phrases like "X", "Y". Applies to <stack(s)>.
version: 0.1.0
---
```

The `description` is what Claude reads to decide whether to activate the skill — it must be specific about triggers AND about which .NET stack(s) it applies to. A vague description means Claude won't fire the skill at the right moments.

## Adding commands, agents, hooks

- **Commands** (`commands/<name>.md`) — auto-discovered, become `/<name>` slash commands.
- **Agents** (`agents/<name>.md`) — subagents Claude can dispatch.
- **Hooks** (`hooks/hooks.json`) — register against `PreToolUse`, `PostToolUse`, `Stop`, `SessionStart`, etc. Use `${CLAUDE_PLUGIN_ROOT}` in command paths.

See the plugin-dev skills (`plugin-dev:command-development`, `plugin-dev:agent-development`, `plugin-dev:hook-development`, `plugin-dev:skill-development`) for the authoritative format of each.
