namespace Webhooks.Inbound.Domain;

/// <summary>
/// Specification — where in the request does the idempotency key live?
/// JSON body (path) vs HTTP header. Discriminated value type.
/// </summary>
public abstract record IdempotencyKeyExtractor
{
    public sealed record FromHeader(string HeaderName) : IdempotencyKeyExtractor;
    public sealed record FromJsonPath(string JsonPath) : IdempotencyKeyExtractor;
    private IdempotencyKeyExtractor() { }
}
