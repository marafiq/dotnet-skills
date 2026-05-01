# dotnet-skills

A Claude Code plugin of skills for the **.NET MVC** ecosystem.

Both stacks are first-class:

- **ASP.NET MVC 5.3 on .NET Framework 4.8** — legacy
- **ASP.NET Core MVC on .NET 10** — modern

Out of scope: Blazor, Razor Pages, Web Forms.

## Status

Scaffolding — skills are landing soon under [`skills/`](skills/).

## Install

In Claude Code:

```text
/plugin marketplace add marafiq/dotnet-skills
/plugin install dotnet-skills@dotnet-skills
```

Skills become available namespaced as `/dotnet-skills:<skill-name>` (e.g. `/dotnet-skills:controller-action-results`). Most skills also auto-trigger when relevant.

## Try locally without installing

Clone the repo and run Claude Code with `--plugin-dir`:

```bash
git clone https://github.com/marafiq/dotnet-skills.git
claude --plugin-dir ./dotnet-skills
```

## Contributing

See [`CLAUDE.md`](CLAUDE.md) for the layout, per-skill anatomy, and conventions for adding new skills.

## License

MIT — see [`LICENSE`](LICENSE).
