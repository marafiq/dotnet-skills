namespace Webhooks.Events;

/// <summary>
/// Strongly-typed tenant identifier. Stops the `string` typo class of bugs at the boundary.
/// </summary>
public readonly record struct TenantId(string Value)
{
    public override string ToString() => Value;
}
