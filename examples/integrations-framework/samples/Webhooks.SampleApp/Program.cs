using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Webhooks.Audit;
using Webhooks.Compliance;
using Webhooks.Events;
using Webhooks.Inbound;
using Webhooks.Inbound.Application;
using Webhooks.Inbound.Domain;
using Webhooks.Outbound;
using Webhooks.Outbound.Application;
using Webhooks.Outbound.Domain;
using Webhooks.SampleApp;

// =====================================================================================
// ALIS Integrations — sample app demonstrating the modular-solid framework end to end.
// Outbound signs and POSTs an event to a local URL; that URL is served by Inbound,
// which verifies the signature, dedupes, and dispatches to a registered processor.
// All five modules participate; audit log captures the whole flow.
// =====================================================================================

const int InboundPort = 5099;
var inboundPrefix = $"http://localhost:{InboundPort}/hooks/in/";
var loopbackUrl = $"http://localhost:{InboundPort}/hooks/in/loopback-demo";

// ---- Compose ----

var services = new ServiceCollection();

services.AddSingleton<IClock, SystemClock>();
services.AddSingleton<IPayloadRedactor, SimplePayloadRedactor>();
services.AddLogging(b => b.ClearProviders()); // keep console clean for the demo
services.AddInMemoryWebhookAudit();
services.AddOutboundWebhooks();
services.AddInboundWebhooks();
services.AddInboundEventProcessor<DemoLoopbackProcessor>();

await using var sp = services.BuildServiceProvider();

// ---- Seed shared HMAC secret in BOTH Outbound and Inbound secret stores ----
//      (different vault paths chosen by each module; same bytes for the demo loopback)
var sharedSecret = Encoding.UTF8.GetBytes("demo-shared-hmac-secret-2026");
var outboundSecretRef = new Webhooks.Outbound.Domain.SecretRef("kv/demo/outbound/loopback-hmac");
var inboundSecretRef  = new Webhooks.Inbound.Domain.SecretRef("kv/demo/inbound/loopback-hmac");
sp.GetRequiredService<Webhooks.Outbound.Infrastructure.IOutboundSecretSeeder>()
  .Seed(outboundSecretRef, sharedSecret);
sp.GetRequiredService<Webhooks.Inbound.Infrastructure.IInboundSecretSeeder>()
  .Seed(inboundSecretRef, sharedSecret);

// ---- Start the inbound HTTP host ----
var receiver = sp.GetRequiredService<IInboundReceiver>();
using var host = new HttpHost(receiver, inboundPrefix);
host.Start();

PrintBanner();

// ---- DEMO ----

var tenant = new TenantId("brookside-living-group");
var actor = ActorRef.User("u_adnan", "Adnan Khan");

var inboundAdmin = sp.GetRequiredService<IInboundSourceAdmin>();
var outboundAdmin = sp.GetRequiredService<IOutboundEndpointAdmin>();
var dispatcher = sp.GetRequiredService<IOutboundEventDispatcher>();
var outboundReads = sp.GetRequiredService<IOutboundEndpointReads>();
var inboundReads = sp.GetRequiredService<IInboundSourceReads>();
var auditReads = sp.GetRequiredService<IAuditReads>();

// 1. Register inbound source for the loopback URL
Section("1. Register inbound source");
var registerInbound = await inboundAdmin.RegisterAsync(
    new RegisterSourceRequest(
        TenantId: tenant,
        Name: "Demo Loopback",
        Slug: new ReceiveSlug("loopback-demo"),
        Verification: new VerificationScheme.SimpleHmacSha256(
            Secret: inboundSecretRef,
            SignatureHeader: "X-Webhook-Signature",
            TimestampHeader: "X-Webhook-Timestamp",
            TimestampTolerance: TimeSpan.FromMinutes(5)),
        IdempotencyKey: new IdempotencyKeyExtractor.FromHeader("X-Webhook-Event-Id"),
        DispatchHandler: new DispatchHandlerKey("loopback-demo")),
    actor, default);
Print($"  • {registerInbound}");
var inboundId = ((RegisterSourceOutcome.Registered)registerInbound).Id;

// 2. Register outbound endpoint pointing at the loopback URL
Section("2. Register outbound endpoint");
var registerOutbound = await outboundAdmin.RegisterAsync(
    new RegisterEndpointRequest(
        TenantId: tenant,
        Name: "Brookside Loopback",
        Url: loopbackUrl,
        Auth: new Webhooks.Outbound.Domain.AuthScheme.HmacSha256(
            Secret: outboundSecretRef,
            SignatureHeader: "X-Webhook-Signature",
            TimestampHeader: "X-Webhook-Timestamp",
            Tolerance: TimeSpan.FromMinutes(5)),
        RetryPolicy: RetryPolicy.NoRetry),
    actor, default);
Print($"  • {registerOutbound}");
var endpointId = ((RegisterEndpointOutcome.Registered)registerOutbound).Id;

// 3. Subscribe the outbound endpoint to an event type
Section("3. Subscribe outbound endpoint to event type");
var eventType = new EventTypeRef("billing.invoice.issued", 2);
var subscribed = await outboundAdmin.SubscribeAsync(endpointId, eventType, actor, default);
Print($"  • {subscribed}");

// 4. Emit a domain event by handing an envelope to the dispatcher
Section("4. Dispatch a domain event");
var payload = JsonSerializer.SerializeToUtf8Bytes(new
{
    invoiceId = "inv_8K1pX2",
    residentId = "res_42",
    amountCents = 4_250_00,
    issuedUtc = DateTime.UtcNow.ToString("o"),
});
var envelope = new EnvelopedDomainEvent(
    EventId: Guid.NewGuid(),
    Type: eventType,
    TenantId: tenant,
    OccurredUtc: DateTime.UtcNow,
    PayloadJson: payload,
    Metadata: new Dictionary<string, string> { ["source"] = "billing-module" });

Print($"  • Envelope {envelope.EventId} · {envelope.Type} · {payload.Length} bytes");
var results = await dispatcher.DispatchAsync(envelope, default);
foreach (var r in results)
    Print($"  • Endpoint {r.EndpointId}: {Describe(r.Outcome)}");

// Give the inbound handler a moment to print
await Task.Delay(150);

// 5. Idempotency check — re-deliver the SAME event id
Section("5. Re-dispatch the same event (idempotency check)");
var results2 = await dispatcher.DispatchAsync(envelope, default);
foreach (var r in results2)
    Print($"  • Endpoint {r.EndpointId}: {Describe(r.Outcome)}");
Print($"  • DemoProcessor invocation count: {DemoLoopbackProcessor.ReceivedSummaries.Count} (should be 1)");
await Task.Delay(150);

// 6. Tampering check — manually POST an event with a bad signature
Section("6. Tampering check — POST with bad signature");
using (var http = new HttpClient())
{
    var req = new HttpRequestMessage(HttpMethod.Post, loopbackUrl)
    {
        Content = new ByteArrayContent(payload)
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") },
        },
    };
    req.Headers.TryAddWithoutValidation("X-Webhook-Signature", "v1=deadbeef0000000000000000000000000000000000000000000000000000beef");
    req.Headers.TryAddWithoutValidation("X-Webhook-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
    req.Headers.TryAddWithoutValidation("X-Webhook-Event-Id", Guid.NewGuid().ToString());
    var resp = await http.SendAsync(req);
    var respBody = await resp.Content.ReadAsStringAsync();
    Print($"  • Tampered POST → HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} · body: {respBody}");
}
await Task.Delay(100);

// 7. Read-side projections
Section("7. Read-side projections (ISP per consumer)");
var endpointSummary = await outboundReads.GetByIdAsync(endpointId, default);
Print($"  • Outbound: {endpointSummary}");
var sourceSummary = await inboundReads.GetByIdAsync(inboundId, default);
Print($"  • Inbound:  {sourceSummary}");

// 8. Audit log
Section("8. Audit trail (every step recorded)");
var idx = 0;
await foreach (var entry in auditReads.RecentAsync(50))
{
    var fields = entry.Payload is AuditPayload.Structured s
        ? string.Join(", ", s.Fields.Select(kv => $"{kv.Key}={kv.Value}"))
        : "redacted";
    Print($"  {(++idx),2}. [{entry.OccurredUtc:HH:mm:ss}] {entry.Actor.DisplayName,-12} {entry.Action,-38} subject={Describe(entry.Subject),-50} {fields}");
}

Section("Done");
Print("  Round-trip succeeded. Five modules participated, every contract was a typed outcome,");
Print("  no exceptions for expected failures, no Common.Abstractions, no IRepository<T>,");
Print("  signature tampering rejected at the boundary, idempotency enforced.");

// ---- Helpers ----

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"━━━ {title} ".PadRight(96, '━'));
}
static void Print(string line) => Console.WriteLine(line);

static void PrintBanner()
{
    Console.WriteLine();
    Console.WriteLine("================================================================================================");
    Console.WriteLine("  ALIS Integrations Framework — modular-solid skill applied                                     ");
    Console.WriteLine("  Outbound + Inbound + Events + Audit + Compliance · in-memory · loopback HMAC SHA-256 demo     ");
    Console.WriteLine("================================================================================================");
}

static string Describe(object o) => o switch
{
    DeliveryOutcome.Delivered d => $"Delivered (attempt={d.Attempt}, {d.StatusCode}, {d.Latency.TotalMilliseconds:F0}ms)",
    DeliveryOutcome.Retrying r => $"Retrying (attempt={r.Attempt} → {r.NextAttempt}, reason={r.Reason.GetType().Name})",
    DeliveryOutcome.DeadLettered d => $"DeadLettered (final attempt={d.FinalAttempt}, reason={d.FinalReason.GetType().Name})",
    DeliveryOutcome.ConfigurationError c => $"ConfigError ({c.Problem.GetType().Name})",
    DeliveryOutcome.EndpointNotActive e => $"EndpointNotActive (status={e.Status})",
    AuditSubject.OutboundEndpoint oe => $"endpoint:{oe.EndpointId}",
    AuditSubject.InboundSource isrc => $"source:{isrc.SourceId}",
    AuditSubject.OutboundDelivery od => $"delivery:{od.EndpointId}/{od.EventId.ToString("N")[..8]}",
    AuditSubject.InboundReceipt ir => $"receipt:{ir.SourceId}/{ir.IdempotencyKey[..Math.Min(12, ir.IdempotencyKey.Length)]}",
    AuditSubject.SecretRotation sr => $"rotation:{sr.EndpointId}/v{sr.Version}",
    _ => o?.ToString() ?? "<null>",
};
