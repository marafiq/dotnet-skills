# dotnet-skills

A Claude Code marketplace of skills and plugins for the **.NET MVC** ecosystem.

Both stacks are first-class:

- **ASP.NET MVC 5.3 on .NET Framework 4.8** (legacy)
- **ASP.NET Core MVC on .NET 10** (modern)

Out of scope: Blazor, Razor Pages, Web Forms.

## Status

Scaffolding — the marketplace is set up and ready, plugins are coming. The `plugins/` directory will fill out as individual plugins are authored.

## Install (in Claude Code)

```text
/plugin marketplace add marafiq/dotnet-skills
/plugin install <plugin-name>@dotnet-skills
```

Once plugins are published they will be listed in [`.claude-plugin/marketplace.json`](.claude-plugin/marketplace.json).

## Contributing

See [`CLAUDE.md`](CLAUDE.md) for repository conventions, layout, and the steps to add new plugins or skills.

## License

MIT — see [`LICENSE`](LICENSE).
