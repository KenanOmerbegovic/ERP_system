namespace Erp.SharedKernel.Domain;

/// <summary>
/// Something that happened inside an aggregate. Domain events stay within a module;
/// cross-module communication uses integration events through the outbox.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
