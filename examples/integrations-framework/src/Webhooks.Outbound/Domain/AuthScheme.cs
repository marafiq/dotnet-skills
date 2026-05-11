namespace Webhooks.Outbound.Domain;

/// <summary>
/// Sealed discriminated value type — closed core. Adding a new auth scheme is
/// a new case here AND a new strategy class. The aggregate doesn't care which
/// variant it holds.
/// </summary>
public abstract record AuthScheme
{
    public sealed record HmacSha256(
        SecretRef Secret,
        string SignatureHeader,
        string TimestampHeader,
        TimeSpan Tolerance) : AuthScheme;

    public sealed record StaticBearer(SecretRef Token) : AuthScheme;

    public sealed record OAuth2ClientCredentials(
        Uri TokenEndpoint,
        string ClientId,
        SecretRef ClientSecret,
        string? Scope) : AuthScheme;

    public sealed record IpAllowlistOnly(
        IReadOnlyList<string> AllowedIps,
        Guid ComplianceApprovalId) : AuthScheme;

    private AuthScheme() { }
}
