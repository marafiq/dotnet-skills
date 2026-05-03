---
name: modular-vertical-slice
description: >
  Organize features inside an ASP.NET Core MVC 10 Area as vertical slices
  by working *with* MVC, not against it. Keep the framework's strengths —
  Controllers and Views in their conventional locations so view discovery,
  scaffolding, and Visual Studio / JetBrains Area awareness all work
  natively — and co-locate mediator handlers next to where they're used
  instead of bucketing them in a flat /Handlers/ folder. MVC Areas remain
  the slice unit: folder name stays `/Areas/<Name>/` (the conversational
  name in design docs is "Feature", but the folder name does NOT change —
  renaming `/Areas/` to `/Features/` is a common tutorial pattern that
  breaks IDE tooling and is explicitly refused). One Area = one Feature
  = one vertical slice = one logical module from the orchestrator's
  perspective. Root-level controllers exist for cross-cutting concerns
  (Home, Login, Error). One tool in the modular-monolith toolbox — reach
  for it when organizing features inside a module whose front end is MVC.
  Status: pragmatic placeholder. Direction is set; detailed conventions
  are intentionally deferred and will be tightened to prescriptive once
  in-house patterns prove out. Use when the user asks "where does this
  feature live", "should this be a controller action or a handler", "how
  do I organize files inside an Area", "where do mediator handlers go",
  "should I rename Areas to Features", or reaches for layered
  architecture (Controllers / Services / Repositories folders) inside a
  single Area. Scope: ASP.NET Core MVC 10 only — not minimal APIs, not
  Razor Pages, not Blazor. Applies to the dotnet-10 plugin.
---

# modular-vertical-slice

> **Status — pragmatic placeholder.** Direction is set (below); detailed conventions are intentionally deferred. The skill will become prescriptive once the in-house patterns prove out. For the working methodology today, use [`modular-monolith`](../modular-monolith/SKILL.md) — this skill is one tool in its toolbox.

## Problem

Vertical slice architecture (VSA), as commonly written, is framework-agnostic: organize a feature as a folder containing its request, handler, response, and validator. Translating that prescription into ASP.NET Core MVC literally — by renaming `/Areas/` to `/Features/`, dropping the `Controllers/` and `Views/` folders, and inventing custom view discovery — fights MVC's strongest conventions and breaks the IDE tooling those conventions are wired into. Visual Studio and JetBrains both have built-in Area awareness; renaming the folder loses route discovery, scaffolding, and view navigation.

The pragmatic answer for an MVC 10 application: keep what MVC is naturally good at (Areas as the slice unit, Controllers in `/Controllers/`, Views in `/Views/<Controller>/` per MVC view discovery), and bring vertical-slice locality to the *handler* layer — co-locate mediator handlers, request DTOs, response DTOs, and validators with the controller action that uses them, instead of bucketing them in flat `/Handlers/`, `/Services/`, `/Validators/` folders that fragment a feature across the project.

## Audience

Engineers on .NET 10 organizing features inside an ASP.NET Core MVC 10 module. Comfortable with MVC's Area routing, controller conventions, Razor view discovery, and a mediator pattern (MediatR or hand-rolled).

## Scope

- **In:** ASP.NET Core MVC 10 controllers (`Controller`, `[Area("X")]`), Razor views (`.cshtml`), MVC Areas, mediator handler patterns inside an Area, cross-cutting concerns wired through MVC filters and/or mediator pipeline behaviors.
- **Out:** Minimal APIs, Razor Pages, Blazor (Server or WebAssembly). These have their own organization patterns and would need their own skills.
- **Out:** Front-end build, JS/TS organization, view component design beyond folder placement.

## Direction (load-bearing, set today)

These are the conventions the skill commits to now. Detailed mechanics for each are deferred to the iteration that earns them.

1. **Folder name stays `/Areas/`.** Refuse the common tutorial pattern of renaming `/Areas/` to `/Features/`. Visual Studio's MVC project template, the `dotnet new` scaffolding, the Razor view engine's area-aware view location formats, and JetBrains Rider's Area navigation all key off this name. The cost of renaming is a worse IDE experience for every developer on the team forever; the benefit is a marginally prettier folder name. Not worth it. The conversational name is *Feature*; the folder is `Areas`.
2. **One Area = one Feature = one vertical slice = one module.** The orchestrator's "module" concept maps 1:1 to an MVC Area. Examples: `/Areas/Residents/`, `/Areas/Billing/`, `/Areas/Payment/`. Each carries its own controllers, views, view models, handlers, and any module-private types.
3. **Controllers and Views stay in MVC's conventional locations.** Inside an Area: `/Areas/<Name>/Controllers/<Name>Controller.cs` and `/Areas/<Name>/Views/<Controller>/<Action>.cshtml`. View discovery works without custom location expanders; scaffolding works; IDE navigation works. Don't fight it.
4. **Every Area-scoped controller carries `[Area("<Name>")]`.** No exceptions inside the Area folder. The attribute is what makes the Area routing work.
5. **Root-level controllers exist for cross-cutting concerns.** Home, Login, Error, and similar app-wide concerns live at the project root (`/Controllers/`, `/Views/`) without an `[Area]` attribute. Don't shoehorn cross-cutting concerns into an Area for tidiness — they don't belong to a single Feature.
6. **Mediator handlers are co-located, not bucketed.** Inside an Area, handlers, request DTOs, response DTOs, and validators live next to the controller action that consumes them — not in a flat `/Handlers/` or `/Services/` folder that scatters one feature across multiple sibling folders. Exact folder shape (per-action sub-folder vs `Features/<ActionName>/` vs co-location next to the controller) is one of the things this skill will get prescriptive about; the principle is *keep the change-locality*.
7. **Cross-cutting concerns wire through MVC filters and/or mediator pipeline behaviors.** Authorization, logging, tenant scoping, transaction management — these wrap every action; they do not live inside the action. The choice between an MVC `IAsyncActionFilter`, an `IEndpointFilter` (not used here — MVC scope), or a mediator `IPipelineBehavior` is per concern: filters when the concern is MVC-aware (e.g. authorization with `IAuthorizationFilter`), pipeline behaviors when the concern is request-shape-aware.

## Open conventions (will tighten in next iteration)

These are intentionally undecided. The team will pick once a few real Areas are built and the patterns prove out. Listing them so the future writer knows the boundaries of the placeholder.

- Exact handler folder shape inside an Area: per-action sub-folder, a `Features/` sub-folder per Area, or co-location next to the controller. Each has trade-offs against test discoverability and view-handler alignment.
- Naming convention for handler classes: `<Action>Handler` vs `<Action>RequestHandler` vs `<Action>CommandHandler`/`<Action>QueryHandler`.
- Whether to use MediatR, a hand-rolled dispatcher, or a custom Alis-internal mediator. Tied to the `modular-monolith` orchestrator's "dispatch mechanism is wiring, not architecture" principle — pick what existing code uses.
- Validator placement: alongside the request, in a `/Validators/` subfolder, or as nested classes on the request itself.
- View model naming and placement: `Areas/<Name>/Models/`, co-located with the handler, or split between input models (next to handler) and view models (next to view).
- How to handle a feature that needs a partial view shared across actions in the same Area.

## Audience-friendly counter-examples

These come up often enough that calling them out by name helps:

- **Renaming `/Areas/` to `/Features/` and writing a custom `IViewLocationExpander`.** Common in tutorials. Refused: breaks IDE Area awareness, scaffolding, and routing-template defaults.
- **Putting all handlers in one flat `/Handlers/` folder per project.** Defeats vertical slicing; one feature scatters across `/Controllers/`, `/Views/`, `/Models/`, `/Handlers/`, `/Validators/`. The IDE jump-to is fine; the change-locality is gone.
- **Cross-Area controller inheritance for "shared" behavior.** A `BaseController` in one Area inherited by controllers in another Area couples the two Areas through code that has nothing to do with their shared domain. If real cross-Feature behavior exists, it lives at the project root or in a dedicated cross-cutting type, not in a sibling Area's namespace.

## Sections to be written (when patterns prove out)

- [ ] Picking a handler folder shape (the "Open conventions" list above) — choose one, justify it, document the migration path from current code
- [ ] Mediator dispatcher choice and wiring (per orchestrator's discriminator: handlers run sync, in the request transaction)
- [ ] Filter vs pipeline-behavior decision rubric for each cross-cutting concern (authorization, logging, tenant scoping, transaction, validation)
- [ ] EF Core query shape inside a handler — `IQueryable` ergonomics, projection to view model, paging, `AsNoTracking` defaults
- [ ] Endpoint discovery and route templates per Area — when to override `[Route]`, when to lean on conventions
- [ ] Sharing partial views and tag helpers within an Area without leaking across Areas
- [ ] Worked example: `/Areas/Residents/` end-to-end, with a list action, a create action, and the cross-cutting filters wired in
- [ ] Migration recipe: lifting an existing legacy MVC 5 controller into a new MVC 10 Area without breaking behavior (pairs with `dotnet-48:mvc-ui-behaviors`)

## See also

- [`modular-monolith`](../modular-monolith/SKILL.md) — orchestrator; this skill is one tool in its toolbox
- [`modular-ddd`](../modular-ddd/SKILL.md) — decides the module shape this skill organizes within
- [`modular-solid`](../modular-solid/SKILL.md) — pressure-tests what this skill exposes outward (Area public surface)
- `dotnet-48:mvc-ui-behaviors` — captures legacy MVC 5 slice behavior; pair when migrating a slice into a new MVC 10 Area
