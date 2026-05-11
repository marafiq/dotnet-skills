using System.Text;
using System.Text.Json;
using Webhooks.Inbound.Application;
using Webhooks.Inbound.Domain;

namespace Webhooks.SampleApp;

/// <summary>
/// Sample handler module's IInboundEventProcessor implementation.
/// In a real system this would translate the inbound event into a domain command
/// for whichever bounded context owns the work.
/// </summary>
internal sealed class DemoLoopbackProcessor : IInboundEventProcessor
{
    public static readonly List<string> ReceivedSummaries = [];

    public DispatchHandlerKey HandlerKey => new("loopback-demo");

    public Task<ProcessingOutcome> HandleAsync(InboundEvent evt, CancellationToken ct)
    {
        var bodyText = Encoding.UTF8.GetString(evt.RawBody);
        string preview;
        try
        {
            using var doc = JsonDocument.Parse(bodyText);
            preview = doc.RootElement.TryGetProperty("data", out var data)
                ? data.ToString()
                : bodyText[..Math.Min(96, bodyText.Length)];
        }
        catch { preview = bodyText[..Math.Min(96, bodyText.Length)]; }

        var summary = $"        ↳ DemoProcessor saw idempotency-key {evt.IdempotencyKey} ({evt.RawBody.Length} bytes): {preview}";
        ReceivedSummaries.Add(summary);
        Console.WriteLine(summary);

        return Task.FromResult<ProcessingOutcome>(new ProcessingOutcome.Processed());
    }
}
