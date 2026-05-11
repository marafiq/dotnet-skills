namespace Webhooks.Events;

/// <summary>
/// A versioned reference to an event type.
/// Shared directly between modules — value objects do not need decoupling interfaces.
/// </summary>
public readonly record struct EventTypeRef(string Name, int SchemaVersion)
{
    public override string ToString() => $"{Name}@v{SchemaVersion}";

    public static EventTypeRef Parse(string s)
    {
        var atIdx = s.LastIndexOf('@');
        if (atIdx <= 0 || !s.AsSpan(atIdx + 2).TryParseInt(out var v))
            throw new FormatException($"Expected 'name@vN', got '{s}'");
        return new EventTypeRef(s[..atIdx], v);
    }
}

internal static class SpanIntExtensions
{
    public static bool TryParseInt(this ReadOnlySpan<char> s, out int value)
        => int.TryParse(s, out value);
}
