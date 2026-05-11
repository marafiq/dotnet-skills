using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Webhooks.Events;
using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Application.Auth;

internal sealed class AuthSchemeApplicatorFactory(IServiceProvider sp)
    : IAuthSchemeApplicatorFactory
{
    public IAuthSchemeApplicator For(AuthScheme scheme) => scheme switch
    {
        AuthScheme.HmacSha256 h =>
            new HmacSha256Applicator(h, sp.GetRequiredService<ISecretReader>(), sp.GetRequiredService<IClock>()),

        AuthScheme.StaticBearer b =>
            new StaticBearerApplicator(b, sp.GetRequiredService<ISecretReader>()),

        AuthScheme.OAuth2ClientCredentials =>
            // Not implemented in this demo — a real impl would use IHttpClientFactory + token cache.
            throw new NotSupportedException("OAuth2 client credentials not wired in this demo."),

        AuthScheme.IpAllowlistOnly =>
            NoOpApplicator.Instance,

        _ => throw new UnreachableException("AuthScheme is sealed."),
    };
}
