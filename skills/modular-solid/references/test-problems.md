# modular-solid — test corpus

15 problems for pressure-testing the skill. Mix of refusal (the skill should decline and redirect), discover-first (boundary work is downstream of upstream cleanup), and engage (the skill owns the question). Use this corpus when revising the skill — every revision should produce coherent answers across all 15.

The expected response shape is *what the skill, when invoked correctly, should produce*. Not a script — a verification that the discovery gate fires, the refusal cues map to the right place, and engagement cases land the right pattern.

---

## Refusal — class-level smell in module clothing

### 1. All-public default
> "Our new BillingModule has every class as `public`. Should we add interfaces for ISP?"

**Expected:** Refuse. Cue: `public` default. Class-level fix — switch defaults to `internal sealed`; `public` is reserved for the role-shaped contracts consumers explicitly name. Adding interfaces over an over-public class doesn't shrink the surface; it adds a second surface on top of the first.

### 2. Constructor with 11 dependencies
> "Our `OrderProcessor` constructor takes 11 services. Wrap it in a facade interface so consumers see fewer methods?"

**Expected:** Refuse. Cue: constructor > ~6 parameters. Class-level fix — split the class (it is doing too much), aggregate low-level deps into options/value objects, or extract collaborators that group cleanly. A facade hides the smell instead of fixing it; the 11 things still have to live somewhere.

### 3. Four-level nested ifs
> "`Pricing.ComputeTotal` has 4 levels of nested ifs across discount eligibility, customer tier, time-based promos, and region rules. Split each branch into a different module?"

**Expected:** Refuse. Cue: nested control. Class-level fix — extract specifications, strategies, or named steps; each branch becomes a domain-named predicate or strategy. Module-per-branch scatters related logic and creates four-way coupling that did not exist before.

### 4. Bool-flag explosion
> "Our `SubmitOrder(order, sendEmail: true, validate: true, autoApprove: false, applyDiscount: true, ...)` has 7 bool flags. Split into multiple methods at the module surface?"

**Expected:** Refuse. Cue: bool flags. Class-level fix — name the modes (`SubmitOrder`, `PreviewOrder`, `AutoApproveOrder`); each flag becomes a method whose name carries the meaning. Two flags = four code paths; the boundary is the wrong place to surface that combinatoric.

### 5. Nullable-defaults proliferation
> "All our query methods are `(string id, string? customerId = null, DateTime? from = null, DateTime? to = null, OrderStatus? status = null)` and we are adding more. Should each consumer get its own interface to escape the nulls?"

**Expected:** Refuse. Cue: optional/null defaults beyond ~1 parameter. Class-level fix — a query object (`OrderQuery { ... }`), a builder, or split methods with intent-revealing names. A per-consumer interface would just push the nulls to a different layer.

### 6. Exceptions for expected outcomes
> "We throw `InvalidPaymentException`, `ExpiredCardException`, `InsufficientFundsException`. Each module catches them. Should we standardize the exception hierarchy at the module boundary?"

**Expected:** Refuse. Cue: exceptions for expected outcomes. Domain-modeling fix — a `ChargeOutcome` discriminated type with `Approved` / `Declined(reason)` / `Pending` cases; the caller pattern-matches and the compiler enforces exhaustiveness. Standardizing the exception hierarchy standardizes the wrong abstraction.

### 7. Reflexive `IEntity` interface
> "Should we add `IEntity { Guid Id; }` that every entity implements, exposed at module boundaries so we can write generic infrastructure code?"

**Expected:** Refuse. Cue: reflexive interface where an abstract base would carry shared substance. Class-level fix — an abstract `Entity` base with id, audit fields, equality semantics, lifecycle hooks. Pick interface for *role*, abstract for *shared substance*. Entities should stay `internal` to their module — nothing exposed at the surface.

### 8. Domain-meaningless naming
> "Our methods are `CalculateThing()`, `ProcessData()`, `HandleEntity()`. Should we add interfaces over them to make the calls more type-safe?"

**Expected:** Refuse. Cue: domain-meaningless naming. Class-level fix — rename from the domain first (`ComputeShiftPay`, `RecordResidentVitalSign`, `IssueInvoice`). Strong domain naming makes the right interface shape obvious. Interfaces over generic verbs add ceremony without information.

---

## Refusal — domain modeling in module clothing

### 9. Anemic domain (30 setters)
> "Our `Order` has 30 public setters. Four modules call them in sequence to compute the final state. We are getting bugs where setters are called out of order. Add `IPricingTarget`, `IDiscountTarget`, `ITaxTarget` interfaces per module to control which setters each can call?"

**Expected:** Refuse. Cue: mutable state exposed via setters; field-by-field mutation rights granted across modules. Anemic domain model. Owned by `modular-ddd`. The fix is `Order.RecordPricing(PriceQuote)` — one method, one transition, one enforced invariant. The four modules collapse to one call.

### 10. 47-property flat ViewModel (discover first)
> "Our legacy `OrderService.GetOrder()` returns a flat `OrderViewModel` with 47 properties. Modernizing into .NET 10 modules. Multiple consumers want different subsets. Split into role-shaped read interfaces (`IOrderForCheckout`, `IOrderForShipping`)?"

**Expected:** Discover first. Cue: a single transport carries multiple unrelated concepts as siblings. Look for hidden value objects (`Address`, `PriceQuote`, `PaymentSummary`, `OrderLine`); the response often collapses to one record of a handful of typed fields and ISP becomes moot. If consumers genuinely need different subsets *after* the value-object work, then ISP earns its keep — but model first, boundary second.

---

## Refusal — coupling-is-always-bad myth

### 11. Decoupling a shared value object
> "`OrdersModule` directly imports `Pricing.PriceQuote`. Should we add an `IPriceQuoteContract` interface in `OrdersModule`'s namespace and have `Pricing` adapt to it, to decouple the modules?"

**Expected:** Refuse. Cue: "all coupling is bad" myth. Coupling to a stable, immutable value object is not the kind of coupling the frame manages. `PriceQuote` *is* the contract — an interface around an immutable record adds an indirection that buys nothing and obscures what the type actually means. The frame manages coupling that has *cost* (surface that grows with consumer count, direction that lets a volatile producer ripple into a stable consumer); shared value objects have neither.

---

## Refusal — anti-pattern proposals

### 12. `Common.Abstractions` library
> "We are adding a `Company.Common.Abstractions` library and putting `IUserService`, `IOrderRepository`, and `INotificationSender` in it so `OrdersModule` and `BillingModule` can both depend on it for shared contracts."

**Expected:** Refuse — anti-pattern. The library inverts DIP: the consumer no longer owns its dependency, the library does. Per-consumer interfaces in the consumer's namespace; producer writes the adapter. For producer-owned outbound DTOs, a per-producer `*.Public` project — narrow, owned by the producing module, not a shared dump. `IOrderRepository` should not be exposed at all (see the generic-repository anti-pattern).

---

## Engage — genuine boundary problems

### 13. Strangler legacy modernization
> "Carving a new .NET 10 `BillingModule` out of the legacy 4.8 `Billing.cs` (12,000-line god class). New module needs to read residents from legacy `ResidentManager`, write invoices via legacy `InvoiceWriter`, and notify via legacy `EmailQueue`. Legacy will run for many months. What does the boundary look like?"

**Expected:** Engage — Strangler shape. New module declares contracts in its own namespace and vocabulary (`Billing.Residents.IResidentLookup`, `Billing.Notifications.IBillingNotifier`, `Billing.Persistence.IInvoiceStore`); an adapter — owned by a thin seam library if runtimes can't share assemblies — implements those interfaces by calling the legacy. When the legacy carries accidents (untyped ids, stringly-typed enums, god-objects), the adapter grows into a full Anti-Corruption Layer that translates legacy concepts into the new module's vocabulary. When the legacy is eventually replaced, only the adapter changes; the new module's contract is unchanged.

### 14. Multi-consumer / BFF
> "`OrdersModule` is consumed by web, mobile, three partner APIs, and the internal admin tool. Each wants a different shape. Currently `IOrderQueries` has 18 methods that union every consumer's needs. Right SOLID move?"

**Expected:** Engage — ISP per role / Backend-for-Frontend. Each consumer declares its own role-shaped read interface in its own namespace; `OrdersModule` writes a small projection adapter per consumer; the canonical `Order` aggregate stays one thing. Three partners with the *same* shape share one interface — ISP-per-consumer is per-*role*, not per-deploy-target. When projections start carrying real logic (caching, denormalization), the next textbook escalation is CQRS — the read model maintained separately from the write model.

### 15. Cohesion-restoration merge
> "We have `OrdersModule`, `PricingModule`, `DiscountModule`, `TaxModule`. To price an order, `OrdersModule` calls each in sequence; the same `items` collection is passed three times; ordering matters; we get intermittent bugs where total disagrees with line items because someone updated one module without re-running the chain. Better ISP on each interface, or a shared context object?"

**Expected:** Engage — cohesion-restoration. The four symptoms (shared input, ordering matters, growing interfaces, things-that-must-change-together split across boundaries that pretend independence) all point to low cohesion at the module boundary. Frame's response: collapse pricing-discount-tax into one module behind one boundary (`IOrderPricing.Quote(PriceQuoteRequest) → PriceQuote`). The four old modules become `internal` collaborators inside the merged module; class-level SOLID applies to them normally; the boundary question becomes trivial because the surface naturally narrows. Sometimes the SOLID-at-boundaries answer is more cohesion, not more separation.

---

## Engage — SRP, OCP, LSP at boundary depth

These exercise the design work, not the mechanical moves. The right answer is not "ISP" or "DIP" — it is a bounded context boundary, an extension-shape decision, or a published-language contract.

### 16. SRP at boundary — bounded context conflict
> "Our `OrdersModule` and `BillingModule` both have a `Customer` type. Overlapping fields but each carries domain-specific state — `Orders.Customer` has shipping preferences, `Billing.Customer` has payment methods. A new requirement needs both to share the customer's loyalty tier. Where does loyalty live, and which `Customer` owns it?"

**Expected:** Engage — bounded context question, not ISP/DIP. The two `Customer`s are not the same concept; they are two views of the same real-world entity, each in its own bounded context. Loyalty belongs to an `IdentityModule` (the bounded context of "who is this person"); `OrdersModule` and `BillingModule` each get a published-language read of loyalty tier through their own consumer-owned read interface, mapped at the boundary. SRP at boundaries is the bounded-context question; the inference hint points at DDD.

### 17. OCP at boundary — extension shape across payer types
> "Shipping a `BillingModule` for residents. Today: private-pay and Medicare. Next quarter: Medicaid. The year after: VA and Hospice. We do not want to ship a new module per payer. How do we shape the boundary so adding a payer is cheap?"

**Expected:** Engage — OCP as extension shape. The module's public surface is `IInvoicePosting` (one role for the controller); adding a payer is adding a class that implements an `internal IPayerBillingRules` (strategy) inside the module, registered in DI, no boundary change. The *closed* part: the public contract, the aggregate's invariants, the transactional guarantee. The *open* part: the strategies that compose with the closed core. This is OCP doing its actual job, with strategy as the DDD inference.

### 18. LSP at boundary — adapter substitutability under test
> "`Checkout.Customers.ICustomerLookup` has three implementations: production (calls `CustomersModule`), an in-memory fake for tests, and a CSV-backed adapter for a one-time data migration. Tests pass against the fake; production throws on a `Find` for a missing id where the fake returned null. What's the LSP move?"

**Expected:** Engage — LSP at the boundary as published-language contract. The compiler-checked signature is the cheap part; the expensive part is outcome semantics that all three implementations must honor. Document in the interface itself: `Find` returns `null` when the customer is missing, throws only on infrastructure failure, never throws on missing. Write a contract test that runs against every implementation. The fake was honest by accident; once the contract is documented, every adapter is verifiable against it. The published language is the contract; method signatures are its skeleton.

---

## Pressure-testing through blind review — worked use cases

The skill's central pressure test: hand a consumer only the public surface and a job to do. Their stumbles are the design's failures; their enjoyment is the qualitative signal no metric replaces. Three use cases worked end-to-end:

### Use case A — designing a new module's surface

Hand a new hire who has never read the codebase the public surface of a fresh `BillingModule`: the interfaces (`IInvoicePosting`, `IInvoiceReplay`), the DTOs (`InvoiceRequest`, `InvoiceResult`), the outcome contracts. Give them this task: *"Write an admin tool that issues a manual invoice, then voids it, then reissues with a corrected amount."*

Watch for:
- **Where do they pause?** Each pause is a hole in the surface.
- **What questions do they ask?** "Does void need the original `InvoiceId` or the new one?" "Is reissue idempotent if I retry?" "What does the outcome say if the original was already paid?" Each question is a missing semantic.
- **What do they invent?** If they construct a workaround (a try/catch around `Void` because the outcome doesn't distinguish "already-voided" from "doesn't-exist"), the surface lacked an outcome the domain has.

The fix is *at the surface*. Add the missing outcome cases. Document the idempotency guarantee. Name the relationship between original and reissued invoice. Then re-run the test with a different reviewer.

### Use case B — choosing between two designs

The team is debating two shapes for `IOrderPricing`:
- **(A)** A single `Quote(PriceQuoteRequest) → PriceQuote` returning a complete result.
- **(B)** Four separate calls (`GetSubtotal`, `ApplyDiscount`, `ApplyTax`, `ApplyShipping`) returning intermediate values.

Hand a teammate both surfaces. Give them the same task: *"Price an order with a tiered loyalty discount and out-of-state tax for a customer in California shipping to Texas."* Time them. Then ask: which felt easier, and why?

Likely outcome: (A) is faster to implement and harder to misuse. (B) requires the caller to know the order of operations, to know that `ApplyTax` needs the post-discount amount, to know whether to round between steps. (B) leaks the calculation's internal structure across the boundary; (A) keeps it inside.

The reviewer's preference, *and the reasons they give*, is the design choice. Their preference often beats the architect's intuition because the architect knows what to do; the reviewer has to discover it from the surface.

### Use case C — diagnosing a production bug from the surface

Production reports an invoice with `$0.00` total. The reviewer is given only `BillingModule`'s public surface (no source access) and the failing call (`IInvoicePosting.Issue(request)` returned `Approved` but the persisted total was zero). Ask: *"What hypotheses can you form?"*

A surface that hides the diagnosis along with the implementation produces "I have no idea — I need to read the code." A surface that surfaces the diagnosis produces hypotheses:
- Did the request carry a zero-line-item collection? (Surface should reject with a typed outcome.)
- Did a registered `IPayerBillingRules` strategy return zero for an unknown payer? (Surface should expose a `Diagnostics` slot on the outcome naming which strategy fired.)
- Did an idempotent retry land on a stale outcome? (Surface should name the idempotency key on the outcome.)

If the reviewer can form *no* hypothesis without reading internals, the surface has hidden the diagnosis. At 3 a.m. someone will wish it hadn't. Add the diagnostic affordances at the surface, not in a logging system that nobody can find from the boundary.

---

## Using this corpus

For each problem, the skill should produce:
1. A diagnosis that names the cue (or recognizes the engagement case).
2. A redirect (for refusal) or a worked frame application (for engagement).
3. For the deep ones (16–18), the DDD inference hint that names the construct under the boundary question.
4. The condition under which the answer would flip.

For the blind-review use cases, the skill should produce:
1. The use case as a process, not a point-in-time answer.
2. The specific stumbles, questions, or hypotheses the reviewer will produce.
3. The fix at the *surface*, not in docs or runbooks.

Drift to watch for in revisions:
- **Over-engagement.** The skill answering refusal cases as if they were boundary problems, with prettier interfaces over the wrong abstraction.
- **Author-anchoring.** Citations to specific authors creeping back in; pattern names should stand on their own.
- **Repository revival.** Repository pattern reappearing as a positive entry rather than a refusal.
- **Lost coupling-is-not-always-bad.** The frame collapsing into "more interfaces = more SOLID."
- **Three-letter dismissal.** SRP/OCP/LSP at boundaries getting reduced back to "they don't promote, here are three bullets." They are the deep work; treat them so.
- **Encapsulation lost as the spine.** Boundary discussions detaching from the encapsulation framing.
- **Pressure-testing as a list.** The blind reviewer being replaced by an enumerated checklist; the central technique is a person with a job and the surface alone.
