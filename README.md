# dotnet-skills

A Claude Code marketplace of two plugins for the **C# / .NET** ecosystem.

Install only the runtime your project actually ships to:

| Plugin | Stack |
|---|---|
| **`dotnet-legacy`** | ASP.NET MVC 5.3, Web Forms, EF6 on **.NET Framework 4.8** with **C# 8.0** (compiler subset; polyfill caveats documented in skills) |
| **`dotnet-current`** | ASP.NET Core MVC, EF Core on **.NET 10** with **C# 14** |

Out of scope: Blazor, Razor Pages, desktop UI (WPF / WinForms / MAUI / Avalonia / Uno), F#, VB.NET, mobile, game dev.

## Status

Scaffolding — both plugin directories are wired and listed in the marketplace. Skills land next under each plugin's `skills/` folder.

## Install

In Claude Code:

```text
/plugin marketplace add marafiq/dotnet-skills
/plugin install dotnet-legacy@dotnet-skills    # legacy stack
/plugin install dotnet-current@dotnet-skills   # modern stack
```

Skills become available namespaced as `/dotnet-legacy:<name>` or `/dotnet-current:<name>`. Most skills also auto-trigger when relevant.

## Try locally without installing

```bash
git clone https://github.com/marafiq/dotnet-skills.git
claude --plugin-dir ./dotnet-skills/plugins/dotnet-legacy
# or
claude --plugin-dir ./dotnet-skills/plugins/dotnet-current
```

## Contributing

See [`CLAUDE.md`](CLAUDE.md) for layout, per-plugin scope, editorial standards, and the conventions for adding new skills, agents, commands, hooks, and MCP servers.

## License

MIT — see [`LICENSE`](LICENSE).
