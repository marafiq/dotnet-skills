using Webhooks.Events;

namespace Webhooks.Outbound.Domain;

/// <summary>
/// Aggregate root. Internal — never on the module's public surface.
/// All state changes go through intent-revealing methods that enforce invariants atomically.
/// No public setters, no property bag, no field-by-field mutation by callers.
/// </summary>
internal sealed class OutboundEndpoint
{
    public EndpointId Id { get; }
    public TenantId TenantId { get; }
    public string Name { get; private set; }
    public EndpointUrl Url { get; }
    public AuthScheme Auth { get; private set; }
    public RetryPolicy RetryPolicy { get; private set; }
    public EndpointStatus Status { get; private set; }
    public DateTime CreatedAt { get; }

    private readonly Dictionary<EventTypeRef, Subscription> _subscriptions = new();
    public IReadOnlyDictionary<EventTypeRef, Subscription> Subscriptions => _subscriptions;

    private OutboundEndpoint(
        EndpointId id, TenantId tenant, string name, EndpointUrl url,
        AuthScheme auth, RetryPolicy retry, DateTime createdAt)
    {
        Id = id;
        TenantId = tenant;
        Name = name;
        Url = url;
        Auth = auth;
        RetryPolicy = retry;
        Status = EndpointStatus.Active;
        CreatedAt = createdAt;
    }

    public static OutboundEndpoint Register(
        EndpointId id, TenantId tenant, string name, EndpointUrl url,
        AuthScheme auth, RetryPolicy retry, IClock clock)
        => new(id, tenant, name, url, auth, retry, clock.UtcNow);

    public SubscribeOutcome Subscribe(EventTypeRef type, IClock clock)
    {
        if (_subscriptions.ContainsKey(type))
            return new SubscribeOutcome.AlreadySubscribed(type);
        var sub = Subscription.For(type, clock);
        _subscriptions[type] = sub;
        return new SubscribeOutcome.Subscribed(type);
    }

    public void Pause() => Status = EndpointStatus.Paused;
    public void Resume() { if (Status == EndpointStatus.Paused) Status = EndpointStatus.Active; }
    public void Disable() => Status = EndpointStatus.Disabled;

    public bool ShouldDeliver(EventTypeRef type)
        => Status == EndpointStatus.Active && _subscriptions.ContainsKey(type);

    internal void RecordDelivery(EventTypeRef type)
    {
        if (_subscriptions.TryGetValue(type, out var sub))
            sub.RecordDelivery();
    }
}

public abstract record SubscribeOutcome
{
    public sealed record Subscribed(EventTypeRef EventType) : SubscribeOutcome;
    public sealed record AlreadySubscribed(EventTypeRef EventType) : SubscribeOutcome;
    private SubscribeOutcome() { }
}
