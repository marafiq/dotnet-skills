namespace Webhooks.Audit;

/// <summary>
/// Foundational-module exception (per modular-solid): producer-owned interface
/// is fine because Audit is a stable sink that does not flip under consumers.
/// Append-only by design — there is no Update or Delete on this surface.
/// </summary>
public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken ct = default);
}

/// <summary>
/// Reads — separate role-shaped interface (ISP). Dashboards read; writers write.
/// </summary>
public interface IAuditReads
{
    IAsyncEnumerable<AuditEntry> RecentAsync(int max, CancellationToken ct = default);
}
