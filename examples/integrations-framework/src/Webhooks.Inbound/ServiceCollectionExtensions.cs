using Microsoft.Extensions.DependencyInjection;
using Webhooks.Inbound.Application;
using Webhooks.Inbound.Application.Verification;
using Webhooks.Inbound.Domain;
using Webhooks.Inbound.Infrastructure;

namespace Webhooks.Inbound;

public static class InboundServiceCollectionExtensions
{
    public static IServiceCollection AddInboundWebhooks(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryInboundRepository>();
        services.AddSingleton<IInboundSourceRepository>(sp =>
            sp.GetRequiredService<InMemoryInboundRepository>());

        services.AddSingleton<InMemoryIdempotencyStore>();
        services.AddSingleton<IIdempotencyStore>(sp =>
            sp.GetRequiredService<InMemoryIdempotencyStore>());

        services.AddSingleton<InMemoryInboundSecretReader>();
        services.AddSingleton<ISecretReader>(sp =>
            sp.GetRequiredService<InMemoryInboundSecretReader>());
        services.AddSingleton<IInboundSecretSeeder>(sp =>
            sp.GetRequiredService<InMemoryInboundSecretReader>());

        services.AddSingleton<IVerificationStrategyFactory, VerificationStrategyFactory>();
        services.AddSingleton<DispatchRegistry>();

        services.AddSingleton<InboundSourceService>();
        services.AddSingleton<IInboundSourceAdmin>(sp => sp.GetRequiredService<InboundSourceService>());
        services.AddSingleton<IInboundReceiver>(sp => sp.GetRequiredService<InboundSourceService>());
        services.AddSingleton<IInboundSourceReads>(sp => sp.GetRequiredService<InboundSourceService>());

        return services;
    }

    /// <summary>
    /// Register a handler module's IInboundEventProcessor implementation.
    /// </summary>
    public static IServiceCollection AddInboundEventProcessor<TProcessor>(this IServiceCollection services)
        where TProcessor : class, IInboundEventProcessor
    {
        services.AddSingleton<IInboundEventProcessor, TProcessor>();
        return services;
    }
}
