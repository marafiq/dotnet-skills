using System.Collections.Concurrent;
using Webhooks.Inbound.Application;
using Webhooks.Inbound.Domain;

namespace Webhooks.Inbound.Infrastructure;

/// <summary>
/// Registry of dispatch handlers (DIP: each is owned by its module and implements
/// the consumer-side IInboundEventProcessor).
/// </summary>
internal sealed class DispatchRegistry
{
    private readonly ConcurrentDictionary<DispatchHandlerKey, IInboundEventProcessor> _handlers = new();

    public DispatchRegistry(IEnumerable<IInboundEventProcessor> processors)
    {
        foreach (var p in processors)
            _handlers[p.HandlerKey] = p;
    }

    public IInboundEventProcessor? Find(DispatchHandlerKey key)
        => _handlers.TryGetValue(key, out var p) ? p : null;
}
