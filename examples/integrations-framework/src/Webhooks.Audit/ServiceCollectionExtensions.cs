using Microsoft.Extensions.DependencyInjection;

namespace Webhooks.Audit;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryWebhookAudit(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryAuditStore>();
        services.AddSingleton<IAuditWriter>(sp => sp.GetRequiredService<InMemoryAuditStore>());
        services.AddSingleton<IAuditReads>(sp => sp.GetRequiredService<InMemoryAuditStore>());
        return services;
    }
}
