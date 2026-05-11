using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Webhooks.Events;
using Webhooks.Inbound.Domain;

namespace Webhooks.Inbound.Application.Verification;

internal sealed class SimpleHmacSha256Verifier(
    VerificationScheme.SimpleHmacSha256 scheme,
    ISecretReader secrets,
    IClock clock) : IVerificationSchemeStrategy
{
    public async Task<VerificationOutcome> VerifyAsync(
        IncomingRequest request, InboundSource source, CancellationToken ct)
    {
        if (!request.Headers.TryGetValue(scheme.SignatureHeader, out var sigHeader))
            return new VerificationOutcome.SignatureMismatch($"Missing {scheme.SignatureHeader} header.");
        if (!request.Headers.TryGetValue(scheme.TimestampHeader, out var tsHeader)
            || !long.TryParse(tsHeader, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tsUnix))
            return new VerificationOutcome.SignatureMismatch($"Missing or invalid {scheme.TimestampHeader} header.");

        var ts = DateTimeOffset.FromUnixTimeSeconds(tsUnix).UtcDateTime;
        var skew = (clock.UtcNow - ts).Duration();
        if (skew > scheme.TimestampTolerance)
            return new VerificationOutcome.TimestampOutOfTolerance(skew);

        byte[] secret;
        try
        {
            secret = await secrets.ReadAsync(scheme.Secret, ct);
        }
        catch (KeyNotFoundException)
        {
            return new VerificationOutcome.ConfigurationProblem($"Secret missing at {scheme.Secret}.");
        }

        var payload = Encoding.UTF8.GetBytes(tsHeader + ".").Concat(request.Body).ToArray();
        using var hmac = new HMACSHA256(secret);
        var expectedHex = Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
        var prefixed = $"v1={expectedHex}";

        if (!FixedTimeEquals(prefixed, sigHeader))
            return new VerificationOutcome.SignatureMismatch("Computed signature did not match.");

        var key = ExtractKey(source.IdempotencyKey, request);
        return new VerificationOutcome.Verified(new IdempotencyKey(key));
    }

    private static IdempotencyKeyValue ExtractKeyValue(IdempotencyKeyExtractor extractor, IncomingRequest req)
        => throw null!; // unused — placeholder to satisfy compiler if referenced; real method below

    private static string ExtractKey(IdempotencyKeyExtractor extractor, IncomingRequest req) =>
        extractor switch
        {
            IdempotencyKeyExtractor.FromHeader h =>
                req.Headers.TryGetValue(h.HeaderName, out var v)
                    ? v
                    : Guid.NewGuid().ToString("N"), // fall back to non-dedupable
            IdempotencyKeyExtractor.FromJsonPath j =>
                ExtractFromJson(req.Body, j.JsonPath),
            _ => Guid.NewGuid().ToString("N"),
        };

    private static string ExtractFromJson(byte[] body, string path)
    {
        try
        {
            var node = JsonNode.Parse(body);
            // Minimal $.field support; production would use JSONPath proper.
            if (node is null) return Guid.NewGuid().ToString("N");
            if (path == "$") return node.ToJsonString();
            var member = path.TrimStart('$', '.');
            var value = node[member];
            return value?.ToString() ?? Guid.NewGuid().ToString("N");
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ab.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ab, bb);
    }
}

internal readonly record struct IdempotencyKeyValue(string Value);

internal sealed class IpAllowlistVerifier(VerificationScheme.IpAllowlistOnly scheme)
    : IVerificationSchemeStrategy
{
    public Task<VerificationOutcome> VerifyAsync(
        IncomingRequest request, InboundSource source, CancellationToken ct)
    {
        if (!scheme.AllowedIps.Contains(request.SourceIp))
            return Task.FromResult<VerificationOutcome>(
                new VerificationOutcome.IpNotAllowed(request.SourceIp));
        // No request-level signature; idempotency must come from request payload.
        var key = Guid.NewGuid().ToString("N"); // accept-but-not-dedupable for the demo
        return Task.FromResult<VerificationOutcome>(
            new VerificationOutcome.Verified(new IdempotencyKey(key)));
    }
}
