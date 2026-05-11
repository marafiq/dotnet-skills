namespace Webhooks.Outbound.Domain;

/// <summary>
/// Schedule of retry delays. The aggregate enforces "attempt count ≤ schedule length"
/// as a real invariant.
/// </summary>
public sealed record RetryPolicy(IReadOnlyList<TimeSpan> Schedule)
{
    public int MaxAttempts => Schedule.Count;

    public static RetryPolicy Standard { get; } = new([
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(24),
    ]);

    public static RetryPolicy NoRetry { get; } = new([]);

    public static RetryPolicy Aggressive { get; } = new([
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
    ]);
}
