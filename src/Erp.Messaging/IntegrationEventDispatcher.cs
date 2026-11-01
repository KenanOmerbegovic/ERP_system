using Erp.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Messaging;

/// <summary>Delivers one event to every handler subscribed to its type, each handler in its own scope.</summary>
public sealed class IntegrationEventDispatcher(IServiceScopeFactory scopeFactory)
{
    public async Task DispatchAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var dispatch = typeof(IntegrationEventDispatcher)
            .GetMethod(nameof(DispatchTypedAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(integrationEvent.GetType());
        await (Task)dispatch.Invoke(this, [integrationEvent, cancellationToken])!;
    }

    private async Task DispatchTypedAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        int handlerCount;
        using (var countScope = scopeFactory.CreateScope())
        {
            handlerCount = countScope.ServiceProvider.GetServices<IIntegrationEventHandler<TEvent>>().Count();
        }

        for (var index = 0; index < handlerCount; index++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetServices<IIntegrationEventHandler<TEvent>>().ElementAt(index);
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}
