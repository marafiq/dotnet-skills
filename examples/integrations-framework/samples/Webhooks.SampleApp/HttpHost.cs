using System.Net;
using Webhooks.Inbound.Application;
using Webhooks.Inbound.Domain;

namespace Webhooks.SampleApp;

/// <summary>
/// Minimal HttpListener that wraps the framework's IInboundReceiver.
/// Maps HTTP requests to slug + IncomingRequest, calls the receiver, returns 2xx/4xx/5xx
/// based on the discriminated outcome.
/// </summary>
internal sealed class HttpHost(IInboundReceiver receiver, string prefix) : IDisposable
{
    private readonly HttpListener _listener = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public string Prefix => prefix;

    public void Start()
    {
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException) { return; }
            catch (HttpListenerException) { return; }

            _ = HandleAsync(context, ct);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            // /hooks/in/{slug}
            var prefix = "/hooks/in/";
            string slugStr;
            if (path.StartsWith(prefix, StringComparison.Ordinal))
                slugStr = path[prefix.Length..];
            else
            {
                Respond(context, 404, "no slug");
                return;
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in context.Request.Headers)
                headers[key] = context.Request.Headers[key] ?? string.Empty;

            using var ms = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(ms, ct);
            var body = ms.ToArray();

            var sourceIp = context.Request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
            var incoming = new IncomingRequest(path, headers, body, sourceIp);

            var outcome = await receiver.ReceiveAsync(new ReceiveSlug(slugStr), incoming, ct);
            switch (outcome)
            {
                case ReceiveOutcome.Accepted:        Respond(context, 200, "{\"received\":true}"); break;
                case ReceiveOutcome.Duplicate:       Respond(context, 200, "{\"received\":true,\"dedupe\":true}"); break;
                case ReceiveOutcome.IgnoredByHandler i: Respond(context, 200, $"{{\"ignored\":\"{i.Reason}\"}}"); break;
                case ReceiveOutcome.Rejected r:      Respond(context, 401, $"{{\"rejected\":\"{r.Reason.GetType().Name}\"}}"); break;
                case ReceiveOutcome.SlugUnknown:     Respond(context, 404, "{\"error\":\"slug unknown\"}"); break;
                case ReceiveOutcome.SourceNotActive s: Respond(context, 503, $"{{\"error\":\"source {s.Status}\"}}"); break;
                case ReceiveOutcome.HandlerMissing h: Respond(context, 500, $"{{\"error\":\"handler {h.Key} missing\"}}"); break;
                case ReceiveOutcome.DispatchFailed d: Respond(context, 500, $"{{\"error\":\"dispatch failed\"}}"); break;
                default:                             Respond(context, 500, "{\"error\":\"unknown\"}"); break;
            }
        }
        catch (Exception ex)
        {
            try { Respond(context, 500, $"{{\"error\":\"{ex.GetType().Name}\"}}"); }
            catch { }
        }
    }

    private static void Respond(HttpListenerContext ctx, int status, string body)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes);
        ctx.Response.Close();
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
    }
}
