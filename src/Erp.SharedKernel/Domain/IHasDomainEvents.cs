namespace Erp.SharedKernel.Domain;

/// <summary>
/// Non-generic access to an aggregate's pending events, so persistence can
/// collect them without knowing the aggregate's id type.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
