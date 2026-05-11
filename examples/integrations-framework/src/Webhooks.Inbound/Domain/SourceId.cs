namespace Webhooks.Inbound.Domain;

public readonly record struct SourceId(string Value)
{
    public static SourceId New() => new("inb_" + Guid.NewGuid().ToString("N")[..10]);
    public override string ToString() => Value;
}

public enum SourceStatus
{
    Active,
    Paused,
    Disabled,
}

public readonly record struct DispatchHandlerKey(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct IdempotencyKey(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ReceiveSlug(string Value)
{
    public override string ToString() => Value;
}
