using Erp.Messaging.Outbox;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Erp.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>Delivers <typeparamref name="TDbContext"/>'s outbox in the background. Modules call this for their own context.</summary>
    public static IServiceCollection AddOutboxProcessor<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddMessagingCore();
        services.AddHostedService<OutboxProcessor<TDbContext>>();
        return services;
    }

    /// <summary>Subscribes a handler to an event another module publishes.</summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddMessagingCore();
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        services.AddSingleton(new IntegrationEventRegistration(typeof(TEvent)));
        return services;
    }

    private static void AddMessagingCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IntegrationEventTypes>();
        services.TryAddSingleton<IntegrationEventDispatcher>();
        services.AddOptions<OutboxOptions>()
            .BindConfiguration(OutboxOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
