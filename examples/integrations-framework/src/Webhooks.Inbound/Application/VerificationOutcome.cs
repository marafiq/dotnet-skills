using Webhooks.Inbound.Domain;

namespace Webhooks.Inbound.Application;

/// <summary>
/// Discriminated outcome — pattern-matched by the receiver.
/// No exceptions for expected outcomes (mismatch, expired timestamp, IP not allowed).
/// </summary>
public abstract record VerificationOutcome
{
    public sealed record Verified(IdempotencyKey Key) : VerificationOutcome;
    public sealed record SignatureMismatch(string Detail) : VerificationOutcome;
    public sealed record TimestampOutOfTolerance(TimeSpan Skew) : VerificationOutcome;
    public sealed record IpNotAllowed(string SourceIp) : VerificationOutcome;
    public sealed record ConfigurationProblem(string Detail) : VerificationOutcome;
    private VerificationOutcome() { }
}
