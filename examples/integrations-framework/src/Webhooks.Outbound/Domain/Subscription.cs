using Webhooks.Events;

namespace Webhooks.Outbound.Domain;

internal sealed class Subscription
{
    public EventTypeRef EventType { get; }
    public DateTime SubscribedAt { get; }
    public long DeliveryCount { get; private set; }

    private Subscription(EventTypeRef type, DateTime subscribedAt)
    {
        EventType = type;
        SubscribedAt = subscribedAt;
    }

    public static Subscription For(EventTypeRef type, IClock clock)
        => new(type, clock.UtcNow);

    public void RecordDelivery() => DeliveryCount++;
}
