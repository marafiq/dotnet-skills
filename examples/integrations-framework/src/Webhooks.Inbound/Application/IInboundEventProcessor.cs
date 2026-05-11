using Webhooks.Events;
using Webhooks.Inbound.Domain;

namespace Webhooks.Inbound.Application;

/// <summary>
/// THE DIP rotation: Inbound declares this; handler-owning modules implement it.
/// Lives in Inbound's namespace because Inbound is the consumer.
/// Adding a new processor module never requires touching a shared library.
/// </summary>
public interface IInboundEventProcessor
{
    DispatchHandlerKey HandlerKey { get; }

    Task<ProcessingOutcome> HandleAsync(InboundEvent evt, CancellationToken ct);
}

public sealed record InboundEvent(
    SourceId SourceId,
    string SourceName,
    IdempotencyKey IdempotencyKey,
    byte[] RawBody,
    IReadOnlyDictionary<string, string> Headers,
    DateTime ReceivedUtc);

public abstract record ProcessingOutcome
{
    public sealed record Processed : ProcessingOutcome;
    public sealed record IgnoredByDesign(string Reason) : ProcessingOutcome;
    public sealed record Failed(string Reason) : ProcessingOutcome;
    private ProcessingOutcome() { }
}
