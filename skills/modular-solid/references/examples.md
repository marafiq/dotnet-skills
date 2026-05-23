# modular-solid — worked examples

C# 14 / .NET 10. Examples are illustrative, not copy-paste production code. Defaults: `internal sealed`. `public` is a deliberate act, not a typing reflex.

## ISP — role-shaped per consumer

The fat-interface trap — one `PaymentService` with `Pay`, `Refund`, `GenerateInvoice`, `CalculateTax`, `SendSms` — splits cleanly when each consumer's role is named.

```csharp
// Checkout/IPaymentProcessor.cs — what the checkout flow needs.
namespace Checkout;
public interface IPaymentProcessor
{
    Task<PaymentResult> Pay(PaymentRequest request, CancellationToken ct);
    Task<RefundResult> Refund(RefundRequest request, CancellationToken ct);
}

// Billing/IInvoiceGenerator.cs — what the billing worker needs.
namespace Billing;
public interface IInvoiceGenerator
{
    Task<Invoice> Generate(OrderId orderId, CancellationToken ct);
}

// Payments/PaymentService.cs — one type, two role-shaped public surfaces.
namespace Payments;
internal sealed class PaymentService : Checkout.IPaymentProcessor, Billing.IInvoiceGenerator
{
    public Task<PaymentResult> Pay(PaymentRequest r, CancellationToken ct) { /* ... */ }
    public Task<RefundResult> Refund(RefundRequest r, CancellationToken ct) { /* ... */ }
    public Task<Invoice> Generate(OrderId id, CancellationToken ct) { /* ... */ }
    // CalculateTax, SendSms — internal, on neither interface.
}
```

The class is `internal sealed`. Only the role-shaped contracts surface as `public`. A test interface, if needed, is exposed via `[InternalsVisibleTo]` rather than another public surface.

## DIP — consumer-owned interface

The consumer owns the abstraction. It lives in the *consumer's* namespace. The producer writes the adapter.

```csharp
// Checkout/Customers/ICustomerLookup.cs — in the CONSUMER's namespace.
namespace Checkout.Customers;
public interface ICustomerLookup
{
    Task<CustomerSummary?> Find(CustomerId id, CancellationToken ct);
}

// Customers/Adapters/CheckoutCustomerAdapter.cs — producer adapts to consumer's contract.
namespace Customers.Adapters;
internal sealed class CheckoutCustomerAdapter(CustomerRepository repo)
    : Checkout.Customers.ICustomerLookup
{
    public async Task<CustomerSummary?> Find(CustomerId id, CancellationToken ct)
        => (await repo.Get(id, ct))?.ToSummary();
}
```

Coupling direction now flows producer → consumer's contract. The consumer's stability dominates; producer evolution does not ripple in.

## Backend-for-Frontend — many consumers, projection adapters

Each consumer declares its own role-shaped read interface; the producing module writes a small per-consumer projection adapter. The canonical type stays one thing.

```csharp
namespace Web.Orders;
public interface IWebOrderReads
{
    Task<WebOrderDetail?>            GetDetail(OrderId id, CancellationToken ct);
    Task<IReadOnlyList<TrackingHop>> GetTracking(OrderId id, CancellationToken ct);
}

namespace Mobile.Orders;
public interface IMobileOrderReads
{
    Task<MobileOrderSummary?> GetSummary(OrderId id, CancellationToken ct);
}

namespace Partners.Orders;
public interface IPartnerOrderFeed
{
    Task<PartnerOrderRecord?> GetByPartnerRef(PartnerOrderRef reference, CancellationToken ct);
}

// Inside OrdersModule: one canonical Order, three internal projectors.
namespace Orders.Reads;
internal sealed class OrderReadProjections(OrderRepository repo)
    : Web.Orders.IWebOrderReads,
      Mobile.Orders.IMobileOrderReads,
      Partners.Orders.IPartnerOrderFeed
{
    // Each method projects from the canonical Order into the consumer's shape.
}
```

Consumers with the *same* shape share one interface. ISP-per-consumer is per-*role*, not per-deploy-target.

## Strangler shape — new module against legacy

```csharp
// New module declares contracts in ITS OWN namespace and vocabulary.
namespace Billing.Residents;
public interface IResidentLookup
{
    Task<ResidentBilling?> Find(ResidentId id, CancellationToken ct);
}

// Adapter bridges legacy to new module's contract — translates legacy accidents.
namespace Billing.Adapters.Legacy;
internal sealed class LegacyResidentLookup : Billing.Residents.IResidentLookup
{
    public Task<ResidentBilling?> Find(ResidentId id, CancellationToken ct)
    {
        var legacy = global::ResidentManager.GetById(id.Value);
        if (legacy is null) return Task.FromResult<ResidentBilling?>(null);
        return Task.FromResult<ResidentBilling?>(new ResidentBilling(
            Id:      new ResidentId(legacy.Id),
            Address: new BillingAddress(legacy.AddrLine1, legacy.AddrCity, legacy.AddrState, legacy.AddrZip),
            // legacy stores PrimaryPayer as a string; new module uses a typed enum.
            Payer:   legacy.PrimaryPayer == "PrivatePay" ? PayerKind.Private : PayerKind.Medicaid));
    }
}
```

When the legacy `ResidentManager` is replaced by a `ResidentsModule`, only the adapter changes — `LegacyResidentLookup` becomes `ResidentsModuleResidentLookup`, same contract.

## Anemic vs encapsulated entity (the discovery refusal in code)

What the discovery section refuses — anemic property bag with chatty mutations:

```csharp
namespace Orders;
public sealed class Order            // public default — first smell
{
    public Money Subtotal { get; set; }   // public set — second smell
    public Money Discount { get; set; }
    public Money Tax { get; set; }
    public Money Shipping { get; set; }
    public Money Total { get; set; }
}

// Four modules each set one field. Nothing enforces totals reconciling.
pricingModule.SetSubtotal(order, 100m);
discountModule.SetDiscount(order, 10m);
taxModule.SetTax(order, 9m);
shippingModule.SetShipping(order, 5m);
order.Total = order.Subtotal - order.Discount + order.Tax + order.Shipping;  // anywhere?
```

What `modular-ddd` would produce — encapsulated, atomic state transition:

```csharp
namespace Orders;
public sealed record PriceQuote(Money Subtotal, Money Discount, Money Shipping, Money Tax, Money Total);

internal sealed class Order
{
    public Money Total { get; private set; } = Money.Zero;

    public void RecordPricing(PriceQuote q)
    {
        if (q.Subtotal - q.Discount + q.Shipping + q.Tax != q.Total)
            throw new InvalidOperationException("PriceQuote totals do not reconcile.");
        Total = q.Total;
        // assign other fields atomically
    }
}
```

One method, one transition, one enforced invariant. The four modules collapse to one `IOrderPricing.Quote()` returning a `PriceQuote`, recorded once.

## Strong domain over exception-driven control flow

What the discovery section refuses — exceptions for expected outcomes:

```csharp
try
{
    var receipt = paymentService.Charge(card, amount);  // throws if declined, expired, etc.
    return Ok(receipt);
}
catch (CardDeclinedException ex)     { return Decline(ex.Reason); }
catch (CardExpiredException)         { return Decline("Card expired"); }
catch (InsufficientFundsException)   { return Decline("Insufficient funds"); }
```

What a strong domain model produces — outcomes named in the type:

```csharp
public abstract record ChargeOutcome
{
    public sealed record Approved(Receipt Receipt) : ChargeOutcome;
    public sealed record Declined(DeclineReason Reason) : ChargeOutcome;
    public sealed record Pending(AuthorizationToken Token) : ChargeOutcome;
}

public enum DeclineReason { CardExpired, InsufficientFunds, IssuerDeclined, FraudSuspected }

// Caller pattern-matches; the compiler enforces exhaustiveness.
return await paymentService.Charge(card, amount, ct) switch
{
    ChargeOutcome.Approved a => Ok(a.Receipt),
    ChargeOutcome.Declined d => Decline(d.Reason),
    ChargeOutcome.Pending p  => Accepted(p.Token),
};
```

Exceptions are reserved for *unexpected* failures (the database is unreachable; a programming-error invariant is violated). Expected outcomes belong in the type.

## Complex condition broken into named predicates

What the discovery section refuses — condition that hides its meaning:

```csharp
if (order.Total > customer.Tier.PromoThreshold
    && order.Items.Any(i => i.Category.IsRestricted)
    && customer.Country != "US"
    && DateTime.UtcNow < promotion.ExpiresAt
    && !customer.HasPriorRedemption(promotion.Id))
{
    ApplyPromo(order, promotion);
}
```

What it should look like — the if reads like a sentence:

```csharp
var orderQualifiesByValue   = order.Total > customer.Tier.PromoThreshold;
var orderHasRestrictedItems = order.Items.Any(i => i.Category.IsRestricted);
var customerIsInternational = customer.Country != "US";
var promotionStillActive    = DateTime.UtcNow < promotion.ExpiresAt;
var customerNotYetRedeemed  = !customer.HasPriorRedemption(promotion.Id);

if (orderQualifiesByValue && orderHasRestrictedItems && customerIsInternational
    && promotionStillActive && customerNotYetRedeemed)
{
    ApplyPromo(order, promotion);
}
```

Better still: each predicate becomes a domain-named method on the appropriate type (`customer.Qualifies(promotion, order)`), and the if collapses to the call. Class-level work; not a boundary question.
