using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Webhooks.Events;
using Webhooks.Inbound.Domain;

namespace Webhooks.Inbound.Application.Verification;

internal sealed class VerificationStrategyFactory(IServiceProvider sp)
    : IVerificationStrategyFactory
{
    public IVerificationSchemeStrategy For(VerificationScheme scheme) => scheme switch
    {
        VerificationScheme.SimpleHmacSha256 h =>
            new SimpleHmacSha256Verifier(h, sp.GetRequiredService<ISecretReader>(), sp.GetRequiredService<IClock>()),
        VerificationScheme.IpAllowlistOnly i =>
            new IpAllowlistVerifier(i),
        _ => throw new UnreachableException("VerificationScheme is sealed."),
    };
}
