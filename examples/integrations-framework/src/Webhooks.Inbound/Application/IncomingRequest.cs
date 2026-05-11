namespace Webhooks.Inbound.Application;

/// <summary>
/// Provider-agnostic shape passed to verification + dispatch.
/// HTTP adapter parses platform request into this; framework code never sees ASP.NET types.
/// </summary>
public sealed record IncomingRequest(
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body,
    string SourceIp);
