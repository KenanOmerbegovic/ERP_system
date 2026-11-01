namespace Erp.SharedKernel.Messaging;

/// <summary>
/// Handles an integration event from another module. Delivery is at least once,
/// so handlers must be idempotent.
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
