---
name: modular-solid
description: >
  SOLID at module boundaries — boundaries are encapsulation; SOLID is
  the toolkit; DDD is where the inference lands. ISP and DIP are
  mechanical (shrink surface, rotate direction). The deep work is in
  SRP (cohesion, aggregate, bounded context), OCP (strategy,
  specification, policy), and LSP (adapter substitutability —
  semantics, not signatures). Discovers first; refuses class-level
  smells (public defaults, oversized parameter lists, mutable state,
  nested control, bool flags, exceptions for expected outcomes,
  naming drift), missing value objects, anemic entities, the "all
  coupling is bad" myth, Common.Abstractions, and generic-repository
  patterns. Pressure-tests via blind review of the public surface.
  Names patterns the frame produces: Hexagonal, Strangler Fig, ACL,
  BFF, CQRS. One tool in the modular-monolith toolbox.
---

# modular-solid

Every boundary in software encapsulates: hides state, exposes a contract, enforces the invariants that make the contract trustworthy. The class boundary hides fields. The module boundary hides types. The aggregate boundary hides children. The bounded context boundary hides a model. **Boundaries are encapsulation; SOLID is its toolkit; DDD is where the inference lands.**

Five letters, uneven work at module scope:

- **ISP and DIP are mechanical** — shrink the surface, rotate the direction. Easy moves, once the boundary is named.
- **SRP, OCP, LSP do the design work** — they decide what the module *owns* (cohesion → aggregate, bounded context), what *changes* across it (extension → strategy, specification, policy), and whether *substitutes are honest* (semantics, not signatures). They are useless without DDD beside them.

The body leads with the design work — the part that takes thinking. The mechanical moves come after.

## Discover before applying

The frame applies *only* when the problem is genuinely a contract between separately-changeable modules. Most problems framed as boundary issues are not — they are class-level shape problems, domain-modeling problems, or naming problems wearing module-shaped clothing. Surface them first.

### Class-level smells masquerading as boundary problems

| Cue in the code | What it actually is | Where the fix lives |
|---|---|---|
| Default visibility is `public` | The boundary was never narrowed; nothing kept `internal`. | Class-level: defaults to `internal sealed`. `public` is a deliberate act, not a typing reflex. |
| Constructor with > ~6 parameters | Class is doing too much, OR injecting too low-level dependencies, OR missing a parameter object. | Class-level: split, aggregate dependencies, or extract options/value objects. |
| Method with > ~3 parameters | Often hides an unstated record. | Class-level: introduce a request type. |
| Optional/null defaults beyond ~1 parameter | Multiple modes of one method hidden behind null sentinels. | Class-level: query object, builder, or split into intent-named methods. |
| Bool flag parameters | Each flag is a hidden if; two flags = four code paths. | Class-level: split into named methods; each flag becomes a verb. |
| Nested ifs / nested loops | The control flow is the abstraction the code is missing. | Class-level: extract steps, specifications, strategies. |
| Complex condition not broken into named steps | A long boolean expression whose parts have meanings the code does not name. | Class-level: assign each predicate to a domain-named local or method; the if reads like a sentence. |
| Mutable state exposed via setters; multiple modules set fields in sequence | Property bag; consistency is everyone's problem. | Domain modeling: encapsulate. One method per state transition that takes a value object and enforces invariants atomically. **Anemic domain model.** |
| Generic exceptions for expected outcomes (`CardDeclinedException`) | Pretends an outcome is an error. | Domain modeling: a discriminated outcome type that names the cases (`Approved` / `Declined(reason)` / `Pending`); compiler enforces exhaustiveness. Exceptions reserve for *unexpected* failures. |
| Domain-meaningless names (`CalculateThing`, `HandleData`) | The boundary surface is unreadable because the domain is unread. | Class-level: name from the domain. The right interface shape becomes obvious once the names are right. |
| Reflexive `IFoo` where an abstract base would carry shared substance (`IEntity { Guid Id; }`) | Interface used because "interfaces are good," not because there is a *role* to extract. | Class-level: interface for *role*; abstract for *shared substance*. Don't expose either at the module surface. |

### Boundary-shaped refusals (the symptom looks structural, the cause isn't)

| Cue | Real cause | Where it lives |
|---|---|---|
| A module is granted field-by-field mutation rights over another module's state | Anemic domain model | [`modular-ddd`](../modular-ddd/SKILL.md) |
| A 47-property transport object pushes consumers to ask for ISP | Missing value objects (`Address`, `PriceQuote`, `PaymentSummary`) | Domain modeling first; ISP, if any, is downstream |
| The proposed module split would not change any module's public surface | Class-level refactoring framed as architecture | Class-level inside the existing module |
| Two modules share a stable value object; "decoupling" via interface is being proposed | "All coupling is bad" myth — coupling to *types* is not coupling to *behavior* | Leave it alone; the interface buys nothing |
| Generic `IRepository<T>` exposed across boundaries | Persistence semantics leaking | See *Anti-patterns* below |

### Legacy contexts cost more discovery

Legacy may carry an anemic shape, missing value objects, *and* overgrown methods simultaneously, and the same surface question reads differently against each cause. Pair with [`code-usage-knowledge-graph`](../code-usage-knowledge-graph/SKILL.md) (legacy call-site discovery) and [`mvc-ui-behaviors`](../mvc-ui-behaviors/SKILL.md) (behavior extraction) before applying boundary moves; a wrong shape applied confidently to a legacy slice is harder to undo than the original mess.

## State the problem before applying

From the discovery output, state the problem in a form that picks the right tool. Break larger questions into atomic units of this form before reaching for any letter:

- **What is the problem?** One sentence — the actual contract pressure, not the symptom.
- **How is it solved?** Which letter engages — and which DDD construct does the inference hint point at?
- **Solution review.** What the change buys; what it costs; the conditions under which the choice would flip.

If a problem cannot be stated in this form, the discovery is incomplete — go back.

## SRP at boundaries — cohesion, aggregates, bounded contexts

SRP at module level is "one reason to change." That phrase is empty until cohesion is operationally defined.

**The 10-file test.** A feature change touches ten files. Cohesive — *only* if each of the ten is the **single point of purpose** for the slice it owns, and the change naturally fans out to each single point. **Cohesion is one place per concern, not zero places per change.** A ten-file change where each file is the canonical owner of its part of the change is healthy. A ten-file change where the same concept lives scattered across the ten is rot.

The DDD inference: when SRP at a boundary is the question, the answer is rarely "split the module." It is one of three:

- **Aggregate root.** The boundary owns one aggregate; mutations go through the root; the root enforces invariants atomically. Symptom that this is missing: setters scattered across fields, callers ordering mutations, intermittent consistency bugs.
- **Bounded context.** The boundary owns one ubiquitous language; the same word means one thing inside; an Anti-Corruption Layer translates at the edge. Symptom that this is missing: the same domain word (`Customer`, `Order`, `Resident`) means different things in different parts of the codebase, and developers ask "which one?"
- **Vertical slice.** The boundary owns one user-facing capability end-to-end; controller, handler, persistence, projection co-located. Symptom that this is missing: a change to one feature touches files in `/Controllers`, `/Services`, `/Repositories`, `/DTOs`, `/Mappers` across four modules.

SRP at boundaries asks: *what one thing does this module own?* DDD answers in those three voices.

## OCP at boundaries — extension shape

OCP at module level is "open for extension, closed for modification." The naive read is wrong: modules are not extended by hot-loading behavior into existing ones; new behavior comes from *adding* a new module. The deep read: **every boundary is a contract about what changes and what doesn't.** OCP names the dial.

Inside a module:

- **Closed:** the aggregate's invariants. The shape of the public contract. The transactional guarantees. Load-bearing claims that consumers depend on; they do not change without coordinated migration.
- **Open:** the strategies, specifications, and policies that compose with the closed core. Pricing rules behind `IPricingStrategy`. Discount eligibility behind `IDiscountSpecification`. Approval flow behind `IApprovalPolicy`. New rules are new classes; the closed core does not move.

The DDD inference: OCP at a boundary points at one of the **tactical patterns**:

- **Strategy** — interchangeable algorithms behind a stable interface (different shipping calculators, different tax engines).
- **Specification** — composable predicates that pick or filter (eligible-for-discount, valid-for-shipping).
- **Policy** — domain rules whose evaluation is named and replaceable (cancellation policy, retention policy).

Across modules, the OCP question is *which contracts admit new modules without renegotiation?* A `PromotionsModule` plugs into `CheckoutModule` without `CheckoutModule` editing if `CheckoutModule` exposes a stable shape that `PromotionsModule` adapts to (DIP rotation), and if the new behavior is *additive* (a strategy registered into a known list) rather than *modal* (a new branch in `CheckoutModule`'s code).

## LSP at boundaries — substitutability of adapters

Wherever DIP has put a consumer-owned interface and a producer-written adapter, LSP is doing real work. The question: **can the consumer trust the contract regardless of which adapter is behind it?**

Three implementations of `Checkout.Customers.ICustomerLookup` — production, in-memory fake for tests, legacy bridge during a Strangler Fig migration — must all honor the *same* contract. The compiler enforces method signatures; that is the cheap part. The expensive part:

- **Outcome semantics.** A `Find` that returns null on missing must return null on missing in *every* implementation. An adapter that throws instead breaks every consumer that pattern-matches on null.
- **Failure semantics.** What does production throw on a network timeout? What does the legacy bridge throw on the same condition? If they differ, the consumer's error handling is fictional.
- **Side-effect semantics.** Does `Find` audit, log, emit telemetry? If production does and the fake does not, tests pass that production fails.

The DDD inference: LSP at a boundary points at the **published language** — the contract is not the method signature; it is the full agreed semantics, written down, that every implementation honors. When a contract is undocumented, LSP cannot be checked, and substitution is gambling. The published language is the contract; method signatures are its skeleton.

## ISP and DIP — the mechanical moves

ISP shrinks the *surface* of coupling; DIP rotates the *direction* of coupling. Two notations for managing one underlying force.

**ISP.** The contract a module exposes is *role-shaped per consumer*, not *type-shaped per module*. One internal type can satisfy three role interfaces; each consumer depends only on the role it plays. Noise when there is one consumer with a naturally narrow contract. At scale (many consumers, sharply different shapes) the frame produces **Backend-for-Frontend**.

**DIP.** The consumer owns the abstraction. It lives in the *consumer's* namespace. The producer writes the adapter. Coupling direction now flows producer → consumer's contract. This is **Ports and Adapters** at module scope; applied to legacy modernization the same rotation is the **Strangler Fig**, with the adapter growing into a full **Anti-Corruption Layer** when the legacy carries accidents the new module must not inherit. Noise when the producer is foundational (a clock, an id generator) and will not move.

Correction the discovery should already have raised: *coupling is not always bad*. Coupling to a stable, immutable value object that two modules share is not a violation; "decoupling" via interface adds an indirection that buys nothing and obscures what the type means. The frame manages coupling that has *cost* — surface that grows with consumer count, direction that lets a volatile producer ripple into a stable consumer. Coupling without cost is not the frame's business.

Worked code: [`references/examples.md`](references/examples.md) — *ISP*, *DIP*, *Backend-for-Frontend*, *Strangler*.

## Encapsulation is the spine

The same principle, applied at different scales:

| Boundary | What is hidden | What is exposed | Where invariants live |
|---|---|---|---|
| Class | Fields, helpers | Methods that enforce invariants on each transition | In the methods |
| Module | Types, repositories, internal services | Role-shaped interfaces and outbound DTOs | In the adapters that implement the interfaces |
| Aggregate | Child entities and their state | Root entity's intent-revealing methods | At the root, enforced before commit |
| Bounded context | The internal domain model | A published language (events, requests, value objects) | At the ACL that translates outside concepts in |

When a boundary leaks — public setters, public child entities, internal types reachable across modules, the ACL bypassed by direct calls — the encapsulation has been broken and SOLID's other letters cannot patch it. **First close the boundary; then design the surface.**

## The anti-patterns

**`Common.Abstractions` library.** Looks like DIP — there are interfaces between modules. Is the opposite of DIP — the consumer no longer owns its dependency, the library does. Every module afferent-couples to one library; the library accumulates unrelated interfaces; one contract change ripples through everything. Tidy at three modules, ball of coupling at fifteen. Legitimate alternative: per-producer `*.Public` projects — narrow, owned by the producing module, holding only that producer's outbound DTOs.

**Generic `IRepository<T>` across modules.** Same failure mode as `Common.Abstractions` plus a worse one — leaks persistence semantics across boundaries. Query patterns, eager-load preferences, cascade behaviors, transaction boundaries spill across modules that should not know about them. Earns its keep in *exactly one shape*: a per-aggregate repository inside the aggregate's module, designed around writes (the root, its invariants, the transactional boundary it enforces), kept `internal`. At any other scope, refuse.

## Pressure-testing through blind review

Listing pressure-testing techniques does not pressure-test anything. **You do not test a floor by setting a sheet of paper on it.** The test is what happens when a real load is applied — and the realest load on a boundary is a consumer who has only the public surface in front of them and a job to do.

The central technique: **blind review of the public surface.**

- Hand a teammate (or a future self with a cooled context) only the public types: interfaces, DTOs, outcome contracts, the XML docs that come with them.
- Give them a use case to implement against the surface — concretely, with inputs and expected outputs.
- Watch where they stumble. The stumbles are the design's failures.

If the reviewer arrives at the right implementation quickly and reports that the surface "felt right," the design is sound. If they ask "where do I get an X?", "what does this null mean?", "what happens if I call these in this order?", or "does this throw or return?", the surface has unstated semantics. **Fix them at the surface, not in the docs.**

Use cases — each a different kind of load:

- **The new-feature use case.** A new partner integration is plugged into `OrdersModule`. Reviewer is given the public surface and asked to make a partner-specific order placement work. They should not need to read internals.
- **The 3 a.m. production debug.** A consumer of `BillingModule` reports an invoice with the wrong total. Reviewer is given the public surface and the failing call. Can they find the diagnostic affordances (events, return values, traced operations) that name the failure? If the only path to diagnosis is opening internals, the boundary has hidden the diagnosis along with the implementation.
- **The hostile-client use case.** Reviewer is told to abuse the contract within its legal use — call methods out of expected order, pass minimum and maximum values, retry idempotent operations, race two callers on the same input. The boundary either holds or names the violation precisely. Anything in between (silent corruption, surprising states) is an invariant the surface failed to enforce.
- **The 5-year sediment use case.** Imagine the module five years on, after twenty new requirements, three product pivots, and complete team turnover. Does the public surface still tell its story without internal archaeology? If names have drifted, types have grown vestigial fields, and invariants are scattered across handlers, the boundary did not age.

Each use case applies a different kind of stress — sudden, sustained, adversarial, time-shifted. The blind reviewer's experience is the readout. Their *enjoyment* of the surface — programming against a designed thing rather than a leaked thing — is the qualitative signal no metric replaces.

## Patterns this frame produces

The frame predicts which named pattern a real boundary pressure summons:

| Pressure | Named pattern | What the frame produces |
|---|---|---|
| Domain core must not depend on infrastructure | Hexagonal / Ports and Adapters | Domain declares ports; database, message bus, external APIs are adapters that conform. Dependencies point inward. |
| New module needs capabilities the legacy provides | Strangler Fig + Anti-Corruption Layer | New module declares contracts in its own namespace and vocabulary; an adapter wraps the legacy and translates. The legacy is replaced behind the same contract when ready. |
| Many consumers want different shapes of the same data | Backend-for-Frontend | Each consumer declares a role-shaped read interface; the producing module writes a small per-consumer projection adapter. The canonical type stays one thing. |
| Reads and writes diverge in shape and scaling | CQRS | Read interface and write interface are different roles; ISP separates them; reads project from a state that writes mutate. |

Negative prediction: with no pressure, these patterns are ceremony. A one-team, one-process, one-database application with no external integrations does not need ports and adapters — the rotation is invisible because the producer never moves. Patterns are crystallizations of pressure; without pressure they are scaffolding nobody asked for, and they confuse the next maintainer who looks for the load-bearing reason and finds none.

## See also

- [`modular-monolith`](../modular-monolith/SKILL.md) — orchestrator; this skill is one tool in its toolbox.
- [`modular-design`](../modular-design/SKILL.md) — module topology decisions.
- [`modular-coupling-cohesion`](../modular-coupling-cohesion/SKILL.md) — numeric pressure on what this skill argues qualitatively.
- [`modular-ddd`](../modular-ddd/SKILL.md) — aggregates, value objects, bounded contexts, ubiquitous language; where SOLID's deep work lands.
- [`references/examples.md`](references/examples.md) — worked C# 14 code.
- [`references/test-problems.md`](references/test-problems.md) — regression corpus, including blind-review use cases.
