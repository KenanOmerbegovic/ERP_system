namespace Erp.SharedKernel.Messaging;

/// <summary>
/// A fact one module publishes for others. Saved to the publisher's outbox in the same
/// transaction as its data, then delivered to handlers in other modules. Declared in the
/// publisher's .Contracts project.
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }

    DateTimeOffset OccurredAt { get; }
}
