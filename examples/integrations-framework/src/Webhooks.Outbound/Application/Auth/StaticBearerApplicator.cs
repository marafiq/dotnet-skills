using System.Net.Http.Headers;
using System.Text;
using Webhooks.Outbound.Domain;

namespace Webhooks.Outbound.Application.Auth;

internal sealed class StaticBearerApplicator(
    AuthScheme.StaticBearer scheme,
    ISecretReader secrets) : IAuthSchemeApplicator
{
    public async Task<ApplyOutcome> ApplyAsync(
        HttpRequestMessage request, byte[] body, CancellationToken ct)
    {
        byte[] tokenBytes;
        try
        {
            tokenBytes = await secrets.ReadAsync(scheme.Token, ct);
        }
        catch (KeyNotFoundException)
        {
            return new ApplyOutcome.Failed(
                new ConfigurationProblem.SecretMissing(scheme.Token.VaultPath));
        }

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", Encoding.UTF8.GetString(tokenBytes));
        return new ApplyOutcome.Applied();
    }
}

internal sealed class NoOpApplicator : IAuthSchemeApplicator
{
    public static readonly NoOpApplicator Instance = new();
    private NoOpApplicator() { }
    public Task<ApplyOutcome> ApplyAsync(HttpRequestMessage request, byte[] body, CancellationToken ct)
        => Task.FromResult<ApplyOutcome>(new ApplyOutcome.Applied());
}
