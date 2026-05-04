---
name: modular-monolith
description: >
  Design a modular monolith in .NET 10 for an application backed by a
  single SQL Server tenant database where atomic commits across modules
  are already guaranteed — use synchronous DomainEvent dispatch inside
  the request transaction, and reach for IntegrationEvent + outbox only
  when the side-effect genuinely crosses the tenant boundary (3rd-party
  API, external DB, message broker, file, search index). Modules are
  logical C# boundaries (folders, namespaces, public surfaces); the
  database is a separate layer with its own choices already made. This
  skill teaches the *reasoning* behind the in-tenant vs cross-system
  discriminator and points to a tool-box of modular-* sub-skills
  (modular-design, modular-shared-language, modular-ddd,
  modular-vertical-slice, modular-solid, modular-coupling-cohesion,
  modular-ddd-classifier), each surfacing its own pros and cons. Use
  whenever the user asks to design, structure, slice, refactor, or
  review modules in a .NET 10 application; whenever they reach for
  outbox or eventual consistency for a write that stays inside the
  tenant DB; whenever they ask about cross-module communication,
  DomainEvent vs IntegrationEvent, module boundaries, single vs
  per-module DbContext, or how DDD / SOLID / vertical slice /
  coupling+cohesion / a shared language fit a modular shape. Trigger
  on phrases like "design the modules for X", "should I add an outbox
  here", "how do modules talk in this app", "where does this
  integration event live", "carve a module out of the legacy app",
  "vertical slice this", or any framing that contrasts a modular
  monolith with microservices or with an event-driven monolith.
  Applies to the dotnet-10 plugin (.NET 10 / C# 14 / ASP.NET Core
  MVC 10 / EF Core 10).
---

# modular-monolith

Design modular monoliths in .NET 10 by reasoning from constraints — what the database already gives you, what the deployment topology already is — instead of reaching for the popular outbox + eventual-consistency plumbing on reflex.

This skill is *the orchestrator*. It owns the central reasoning — *atomic commits across modules are a gift; do not pay eventual-consistency tax to throw the gift away* — and points to a tool-box of `modular-*` sub-skills, each of which surfaces its own pros and cons when the conversation needs it. The sub-skills are not sequential gates; they are tools to reach for as the design surfaces a need.

## Problem

Public modular-monolith literature defaults to one shape: each module owns a schema, modules communicate via integration events on an in-process bus, an outbox enforces eventual consistency between modules. That shape is a real answer to a real problem — independently deployable modules with independent data stores. It is *not* the only answer, and it is the wrong default for an application that already runs as one process against one shared database.

When atomic commits are already guaranteed by the database, choosing eventual consistency is a tax — added latency, dual-write coordination, retry plumbing, monitoring overhead — paid for benefits the deployment shape does not need. Naming the constraint up front and reasoning forward from it produces a design that is smaller, simpler, and easier to change.

## Audience

Engineers on .NET 10 designing or reviewing modules in a large, long-lived application backed by a single SQL Server database (one DB per tenant; tenant context resolved at request scope by existing application abstractions). Comfortable with C# 14, ASP.NET Core MVC 10, EF Core 10, dependency injection. Likely exposed to Kamil Grzybek's *Modular Monolith with DDD* and Vaughn Vernon's *Implementing DDD*; less commonly trained on the trade-off line between in-tenant and cross-system events, which is where this skill lives.

Worked examples in this skill use a senior-living-industry line-of-business application as the running example: ~2.5 million lines of C# accumulated across many years and many CRUD styles, a mix of strong code and weak-or-old tech, modernized incrementally while it continues to run in production. Tenant-scoped SQL Server. Modules around resident care, billing, compliance.

Scale matters. A 2.5-million-line codebase cannot be magically refactored into a modular monolith. The methodology is built for *progressive modernization*: pick a feature slice, classify what is there, design the new modules around it, ship that slice, move to the next. Behaviors are preserved across the cut — the legacy and modern code coexist for as long as it takes — and the toolbox supports that pacing. Greenfield use is supported and simpler; the skill is shaped for the harder case.

## Requirements

This skill operates at the C# / back-end layer. The database is a separate layer with its own choices already made — in the running example, the schema is shared with a legacy .NET Framework 4.8 application and is not changing. The orchestrator does not propose schema changes; it organizes C# code on top of the existing database.

Before reaching for any sub-skill, define what is being designed. The CLAUDE.md editorial bar applies in full: open with **Problem** (one sentence), **Audience** (who runs this code), **Functional requirements** (what it must do), **Non-functional requirements** (latency, throughput, security, operability, cost). Then reason forward.

A common shortcut to resist: starting at the toolbox before defining the problem produces designs that solve generic problems beautifully and the actual problem badly.

**Modernization principle.** When the work is incremental modernization rather than greenfield, two non-negotiables apply. *Behaviors must be preserved across the cut* — the modern module reproduces what the legacy slice did, observably, before any refactor on top is allowed. Pair `modular-ddd-classifier` (structural, where modules live) with `dotnet-48:mvc-ui-behaviors` (behavioral, what the slice does) to capture both axes. *Pace by feature slice* — pick a vertical slice, study it deeply with the classifier, design the module around it, ship, repeat. Resist the urge to redesign the whole topology before any code lands; the design will be wrong and the team will not recover the time.

## The discriminator (and the reasoning behind it)

A C# write happens. **Where does the side-effect land?**

| Target | Event kind | Mechanism | Consistency |
|---|---|---|---|
| Inside the tenant SQL Server DB | `DomainEvent` | sync, in the same `DbContext` transaction | immediate (ACID) |
| Outside the tenant DB — 3rd-party API, external DB, message broker, file, search index, analytics warehouse | `IntegrationEvent` | outbox row written in the same transaction; relay publishes asynchronously | eventual |

The reasoning is what matters; the table is the artifact.

**Why `DomainEvent` in-tenant.** When two modules write to the same database in the same request, the database already gives ACID across both writes for free. Wrapping a `DomainEvent` dispatch in `DbContext.SaveChangesAsync` keeps it that way: handlers run synchronously, mutate the same `DbContext`, and a single commit either succeeds or the whole request rolls back. No outbox row, no dual-write race, no eventual-consistency window to monitor. The mechanism (an EF Core `SaveChangesInterceptor`, a mediator pipeline, a custom dispatcher invoked from aggregate roots) is wiring; this skill teaches the *semantic* and trusts the codebase to pick a wiring.

**Why `IntegrationEvent` cross-system.** When a side-effect lands outside the tenant DB, the database can no longer guarantee atomicity across the two writes. A naive sync call ("commit row, then call CRM") is a dual-write: the row commits, the call fails, the system is inconsistent. The outbox pattern fixes this by committing the cross-system intent *as a row* in the same transaction; a relay reads outbox rows after commit and publishes them, with retry, until the external system acknowledges. Eventual consistency lives here because it cannot be avoided — the price is paid because the constraint is real. How the relay publishes (in-process handler, Service Bus, RabbitMQ, custom worker) is per-case; the outbox itself is the durability primitive.

**The cost of the in-tenant choice.** Modules cannot be physically split into separate databases or processes later without revisiting cross-module flows that today rely on shared transactions. That is a real cost — pay it when, and only when, a module's data genuinely needs to live elsewhere (different tenancy model, different durability story, different SLA, a separate team owning a separate service). The default is *do not pre-pay* — but the default flips the moment a real cross-system target appears.

## What this skill does NOT pin

- **Dispatch mechanism for `DomainEvent`.** EF Core `SaveChangesInterceptor`, mediator pipeline, custom dispatcher, hand-rolled domain-event collector on aggregate roots — all valid. The skill teaches *semantics* (sync, in-transaction, immediate) and refuses to mandate the wiring. Existing code already picks one; new modules adopt it.
- **Module physicality.** A module is a logical C# boundary. It might be a `.csproj`, a top-level folder under `src/Modules/<Name>/`, or just a namespace. The right level depends on team size, build-time isolation needs, and enforcement strategy (tests, analyzers, convention).
- **DbContext shape.** A single shared `DbContext` is the simpler default for one-database applications and what this skill assumes by default. Per-module `DbContext` is a pragmatic choice when a module genuinely needs different lifetime, different change-tracking behavior, or compile-time isolation from sibling modules' entity sets — name the reason and move on.
- **`IntegrationEvent` transport.** Outbox is the durability mechanism; what publishes the row (in-process handler, Service Bus relay, RabbitMQ, custom worker) is per-case. Pick what existing infrastructure supports; the skill does not prescribe.
- **DDD tactical patterns.** Aggregates, value objects, domain services, specifications — earned per case (see [`modular-ddd`](../modular-ddd/SKILL.md)). Many modules are CRUD-shaped at heart and need none of it.

## The tool box

Once the problem space is named, reach for sub-skills as the conversation surfaces a need. They are tools, not sequential gates. Each sub-skill names what it gets you and what it costs.

| Sub-skill | What it surfaces | Pros / cons | When to reach for it |
|---|---|---|---|
| [`modular-ddd-classifier`](../modular-ddd-classifier/SKILL.md) | A reviewable artifact of the legacy .NET 4.8 source: candidate modules, deep modules (Ousterhout sense — narrow surface, deep implementation), hierarchical / nested modules, dependency relationships, touched areas. The classifier is itself a deep-thinking interactive skill — one slice at a time, one question at a time, building shared understanding with whoever knows the legacy. | Pros: grounds the design in what already exists; surfaces seams the team had not noticed; supports progressive disclosure on a 2.5-million-line codebase nobody can hold in their head. Cons: significant effort per slice; requires interactive collaboration with a domain SME or long-time maintainer. | Modernizing legacy code into new .NET 10 modules. Use one slice at a time. Skip on greenfield. |
| [`modular-design`](../modular-design/SKILL.md) | Module inventory, names, dependency graph, physicality decisions (`.csproj` vs folder vs namespace). | Pros: forces explicit topology so later decisions are made against a real map. Cons: early naming and physicality decisions can over-commit; revisit when you learn more. | Deciding what the modules *are* and what they are called. |
| [`modular-shared-language`](../modular-shared-language/SKILL.md) | Term map: which modules use which words, where conflicts hide, where anti-corruption layers earn rent. | Pros: prevents the silent-corruption failure mode where two modules use the same word for different concepts. Cons: requires SME engagement; cheap to do early, expensive to retrofit. | Multiple modules use the same word and you suspect they do not mean the same thing. |
| [`modular-ddd`](../modular-ddd/SKILL.md) | Per-module shape decision: aggregate-rooted, service-with-records, or thin pass-through; where invariants live; where domain events are emitted. | Pros: protects domains with real invariants; concentrates change. Cons: ceremony when the data is CRUD-shaped; an aggregate whose only invariant is "field is non-null" is a slow EF entity. | Deciding the internal shape of a module after topology is set. |
| [`modular-vertical-slice`](../modular-vertical-slice/SKILL.md) | MVC Area-based feature organization: each Area is one Feature is one vertical slice; Controllers and Views in MVC's conventional locations (so view discovery and IDE Area awareness work natively); mediator handlers co-located, not bucketed in a flat `/Handlers/` folder. The folder name `/Areas/` is preserved — *Feature* is the conversational name only. | Pros: change-isolation while preserving MVC tooling. Cons: scope is ASP.NET Core MVC 10 only — minimal APIs, Razor Pages, Blazor are out and would need their own skills. | Organizing features inside a module whose front end is ASP.NET Core MVC 10. |
| [`modular-solid`](../modular-solid/SKILL.md) | Pressure-tests the public surface (ISP — expose only what callers need) and cross-module dependencies (DIP — consumer-defined interfaces in the consumer's namespace). | Pros: shrinks the contract surface; reduces accidental coupling. Cons: fights the "shared abstractions library" reflex some teams have; takes review discipline. | Reviewing a module's public types; deciding where an interface should live. |
| [`modular-coupling-cohesion`](../modular-coupling-cohesion/SKILL.md) | Coupling counts (afferent, efferent) and cohesion assessment per module; names god modules and false splits. | Pros: makes design hypotheses testable with numbers, not preference. Cons: numbers without interpretation mislead; a god module hides behind acceptable counts. | Validating a design before implementation; re-evaluating after drift. |

The sub-skills are scaffolded today; deeper content fills in incrementally. When a sub-skill is still a stub, the orchestrator's working summary above carries the load, and contributors expand the sub-skill when the topic earns deeper treatment.

## When the discriminator flips

The bias is in-tenant + sync. The flip happens whenever the *target* of a write is genuinely outside the tenant DB. Three concrete cases:

- **External provider call.** Calling a payment processor, a CRM, a notification service, an SMS provider, an EHR. The provider is a different system with its own failure mode and SLA. The local DB write commits; the outbox row commits in the same transaction; the relay calls the provider asynchronously, with retry. Whether the relay publishes via in-process handler or message broker is per-case.
- **Separate read store.** A read model in PostgreSQL, Elasticsearch, or Redis used for search or reporting. The read store is a different database; outbox + relay projects into it asynchronously.
- **Cross-tenant analytics rollup.** Writes go to a warehouse that aggregates across tenants. Different database, often different physical server. Outbox + relay.

In each case the choice is forced by the target, not by a desire for module independence. If the target moves back inside the tenant DB later (e.g. analytics is folded back into the same SQL Server), the dispatch flips back to a `DomainEvent`.

## Verifying a design

A design is coherent when a representative request walks end-to-end and every dispatch lands on the correct side of the discriminator. Three passes.

**Pass 1 — transaction shape.** Pick a write-heavy request the design has to handle. Trace it through the modules. Where does it call across? Are those calls direct method invocations on a public interface, or `DomainEvent` dispatches? Does the whole request commit in one `SaveChangesAsync`? If a step writes to anything other than the tenant DB, is it an `IntegrationEvent` with an outbox row?

**Pass 2 — public surfaces.** For each module, list the types other modules import. Do they include domain entities, internal value objects, EF Core entities? They should include only the request DTOs, response DTOs, and interfaces this module exposes for consumers. Internal types stay `internal`.

**Pass 3 — does the reasoning still hold?** Walk the choices made: outbox where? per-module `DbContext` where? aggregate where? thin pass-through where? For each, name the constraint that forced the choice. If every choice traces back to a real constraint, the design is shippable. If a choice was made on reflex, revisit — pragmatic-not-dogmatic means every default is examined, not enforced.

If all three pass, capture the design as a one-page module map (topology diagram + table of public surfaces + the one or two outbox-using flows) so the next contributor reads it without re-deriving it.

## Non-goals

This skill does not:

- Generate C# code. The output is a *design*: modules, surfaces, dispatches, the reasoning behind each choice. Implementation is a separate concern.
- Cover hosting, configuration, deployment, or CI for the application as a whole. Those are real decisions but not module-level decisions; they have their own skills (planned).
- Mirror to `dotnet-48`. The legacy plugin has `mvc-ui-behaviors` for behavior extraction; modular-monolith methodology is forward-looking and lives here.
- Propose database schema changes. The database is a separate layer with its own constraints and is treated as fixed.
- Replace judgment. Every default flips under the right pressure (real cross-system target, real distinct SLA, real separate team). The skill names defaults so deviations are deliberate, not the other way around.

## See also

- `dotnet-48:mvc-ui-behaviors` — extracts user-visible behaviors from legacy MVC 5 slices. Pair with `modular-ddd-classifier` when modernizing: behaviors are the *what*, the classifier output is the *where*.
- Ousterhout, *A Philosophy of Software Design* — source for "deep module" terminology used by `modular-ddd-classifier`.
- Vernon, *Implementing Domain-Driven Design* — DDD tactical patterns referenced by `modular-ddd`, used selectively.
- Grzybek, *Modular Monolith with DDD* — the popular pattern this skill reasons against. Read it to understand the constraint difference (independently deployable modules with independent data stores) that makes it the right default for one shape and the wrong default for an application backed by a single shared database.
