using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Webhooks.Events;
using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Application.Auth;

internal sealed class HmacSha256Applicator(
    AuthScheme.HmacSha256 scheme,
    ISecretReader secrets,
    IClock clock) : IAuthSchemeApplicator
{
    public async Task<ApplyOutcome> ApplyAsync(
        HttpRequestMessage request, byte[] body, CancellationToken ct)
    {
        byte[] secretBytes;
        try
        {
            secretBytes = await secrets.ReadAsync(scheme.Secret, ct);
        }
        catch (KeyNotFoundException)
        {
            return new ApplyOutcome.Failed(
                new ConfigurationProblem.SecretMissing(scheme.Secret.VaultPath));
        }

        var timestamp = clock.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signaturePayload = Encoding.UTF8.GetBytes(timestamp + ".").Concat(body).ToArray();

        using var hmac = new HMACSHA256(secretBytes);
        var signature = Convert.ToHexString(hmac.ComputeHash(signaturePayload)).ToLowerInvariant();

        request.Headers.TryAddWithoutValidation(scheme.TimestampHeader, timestamp);
        request.Headers.TryAddWithoutValidation(scheme.SignatureHeader, $"v1={signature}");
        return new ApplyOutcome.Applied();
    }
}

internal static class DateTimeExtensions
{
    public static long ToUnixTimeSeconds(this DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
