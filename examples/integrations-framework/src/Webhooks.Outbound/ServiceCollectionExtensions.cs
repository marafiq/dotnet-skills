using Microsoft.Extensions.DependencyInjection;
using Webhooks.Outbound.Application;
using Webhooks.Outbound.Application.Auth;
using Webhooks.Outbound.Domain;
using Webhooks.Outbound.Infrastructure;

namespace Webhooks.Outbound;

public static class OutboundServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbound module's role-shaped public surfaces against an
    /// internal service implementation. In-memory backing for the demo.
    /// </summary>
    public static IServiceCollection AddOutboundWebhooks(this IServiceCollection services)
    {
        // Internal infrastructure
        services.AddSingleton<InMemoryOutboundRepository>();
        services.AddSingleton<IOutboundEndpointRepository>(sp =>
            sp.GetRequiredService<InMemoryOutboundRepository>());

        services.AddSingleton<InMemorySecretReader>();
        services.AddSingleton<ISecretReader>(sp =>
            sp.GetRequiredService<InMemorySecretReader>());
        services.AddSingleton<IOutboundSecretSeeder>(sp =>
            sp.GetRequiredService<InMemorySecretReader>());

        services.AddSingleton<IAuthSchemeApplicatorFactory, AuthSchemeApplicatorFactory>();

        services.AddHttpClient(); // registers IHttpClientFactory; service resolves a client.
        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("webhooks-outbound");
            client.Timeout = TimeSpan.FromSeconds(10);
            return client;
        });

        // The same internal service satisfies all three role-shaped public surfaces.
        services.AddSingleton<OutboundEndpointService>();
        services.AddSingleton<IOutboundEndpointAdmin>(sp => sp.GetRequiredService<OutboundEndpointService>());
        services.AddSingleton<IOutboundEventDispatcher>(sp => sp.GetRequiredService<OutboundEndpointService>());
        services.AddSingleton<IOutboundEndpointReads>(sp => sp.GetRequiredService<OutboundEndpointService>());

        return services;
    }
}
