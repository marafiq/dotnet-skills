using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Webhooks.Audit;

/// <summary>
/// In-memory implementation backing both writer and reads roles.
/// One internal type, two role-shaped public surfaces — composition
/// over duplication; the encapsulation stays tight.
/// </summary>
internal sealed class InMemoryAuditStore : IAuditWriter, IAuditReads
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();

    public Task WriteAsync(AuditEntry entry, CancellationToken ct = default)
    {
        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<AuditEntry> RecentAsync(
        int max, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        var snapshot = _entries.ToArray();
        var slice = snapshot.Length <= max ? snapshot : snapshot[^max..];
        foreach (var e in slice)
        {
            ct.ThrowIfCancellationRequested();
            yield return e;
        }
    }
}
