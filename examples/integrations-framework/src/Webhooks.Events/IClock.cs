namespace Webhooks.Events;

/// <summary>
/// Foundational clock abstraction. Producer-owned (this module's namespace) is fine
/// — clocks do not flip under their consumers; it is a stable foundational service
/// that the modular-solid skill explicitly allows.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
