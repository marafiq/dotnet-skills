namespace Webhooks.Inbound.Domain;

/// <summary>
/// Sealed discriminated value type — closed core. Per-provider semantics belong
/// in their own variants; the strategy interface dispatches.
/// </summary>
public abstract record VerificationScheme
{
    public sealed record SimpleHmacSha256(
        SecretRef Secret,
        string SignatureHeader,
        string TimestampHeader,
        TimeSpan TimestampTolerance) : VerificationScheme;

    public sealed record IpAllowlistOnly(
        IReadOnlyList<string> AllowedIps,
        Guid ComplianceApprovalId) : VerificationScheme;

    private VerificationScheme() { }
}
