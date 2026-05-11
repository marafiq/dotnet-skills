using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Application.Auth;

/// <summary>
/// Strategy. One implementation per AuthScheme variant.
/// Adding a new scheme is one new case in AuthScheme + one new applicator + one DI line.
/// The OutboundEndpoint aggregate doesn't change.
/// </summary>
internal interface IAuthSchemeApplicator
{
    Task<ApplyOutcome> ApplyAsync(HttpRequestMessage request, byte[] body, CancellationToken ct);
}

internal abstract record ApplyOutcome
{
    public sealed record Applied : ApplyOutcome;
    public sealed record Failed(ConfigurationProblem Problem) : ApplyOutcome;
    private ApplyOutcome() { }
}

internal interface IAuthSchemeApplicatorFactory
{
    IAuthSchemeApplicator For(AuthScheme scheme);
}
