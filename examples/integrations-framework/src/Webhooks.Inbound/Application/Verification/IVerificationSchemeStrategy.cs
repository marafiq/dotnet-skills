using Webhooks.Inbound.Domain;

namespace Webhooks.Inbound.Application.Verification;

/// <summary>
/// Strategy. One per VerificationScheme variant.
/// </summary>
internal interface IVerificationSchemeStrategy
{
    Task<VerificationOutcome> VerifyAsync(
        IncomingRequest request,
        InboundSource source,
        CancellationToken ct);
}

internal interface IVerificationStrategyFactory
{
    IVerificationSchemeStrategy For(VerificationScheme scheme);
}
