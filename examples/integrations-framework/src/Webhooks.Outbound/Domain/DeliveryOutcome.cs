namespace Webhooks.Outbound.Domain;

/// <summary>
/// What happened when we tried to deliver. Discriminated outcome — every consumer
/// pattern-matches; the compiler enforces exhaustiveness. No exceptions for
/// expected outcomes.
/// </summary>
public abstract record DeliveryOutcome
{
    public sealed record Delivered(int Attempt, TimeSpan Latency, int StatusCode) : DeliveryOutcome;

    public sealed record Retrying(
        int Attempt,
        int NextAttempt,
        DateTime NextAttemptAt,
        FailureReason Reason) : DeliveryOutcome;

    public sealed record DeadLettered(int FinalAttempt, FailureReason FinalReason) : DeliveryOutcome;

    public sealed record ConfigurationError(ConfigurationProblem Problem) : DeliveryOutcome;

    public sealed record EndpointNotActive(EndpointStatus Status) : DeliveryOutcome;

    private DeliveryOutcome() { }
}

public abstract record FailureReason
{
    public sealed record HttpStatus(int Code, string ReasonPhrase) : FailureReason;
    public sealed record Timeout(TimeSpan Elapsed) : FailureReason;
    public sealed record ConnectionRefused(string Detail) : FailureReason;
    public sealed record TlsHandshakeFailed(string Detail) : FailureReason;
    public sealed record InvalidResponse(string Detail) : FailureReason;
    public sealed record AuthenticationFailed(string Detail) : FailureReason;
    private FailureReason() { }
}

public abstract record ConfigurationProblem
{
    public sealed record SecretMissing(string VaultPath) : ConfigurationProblem;
    public sealed record IdpRejectedCredentials(string Detail) : ConfigurationProblem;
    public sealed record ExpiredCertificate(DateTime ExpiredAt) : ConfigurationProblem;
    private ConfigurationProblem() { }
}
