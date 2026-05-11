namespace Webhooks.Outbound.Domain;

public readonly record struct EndpointId(string Value)
{
    public static EndpointId New() => new("whk_" + Guid.NewGuid().ToString("N")[..10]);
    public override string ToString() => Value;
}

public enum EndpointStatus
{
    Active,
    Paused,
    Disabled,
}
