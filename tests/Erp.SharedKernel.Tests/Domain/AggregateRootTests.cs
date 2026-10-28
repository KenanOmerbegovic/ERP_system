using Erp.SharedKernel.Domain;

namespace Erp.SharedKernel.Tests.Domain;

public class AggregateRootTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private sealed record Renamed(string Name, DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class Warehouse(Guid id, CompanyId companyId) : AggregateRoot<Guid>(id, companyId)
    {
        public void Rename(string name, DateTimeOffset now) => Raise(new Renamed(name, now));

        public void RaiseNull() => Raise(null!);
    }

    [Fact]
    public void New_aggregate_has_no_events()
    {
        var warehouse = new Warehouse(Guid.NewGuid(), CompanyId.New());

        Assert.Empty(warehouse.DomainEvents);
    }

    [Fact]
    public void Raised_events_are_recorded_in_order()
    {
        var warehouse = new Warehouse(Guid.NewGuid(), CompanyId.New());

        warehouse.Rename("Glavno skladište", Now);
        warehouse.Rename("Skladište Mostar", Now);

        Assert.Equal(
            [new Renamed("Glavno skladište", Now), new Renamed("Skladište Mostar", Now)],
            warehouse.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_removes_all_events()
    {
        var warehouse = new Warehouse(Guid.NewGuid(), CompanyId.New());
        warehouse.Rename("Glavno skladište", Now);

        IHasDomainEvents source = warehouse;
        source.ClearDomainEvents();

        Assert.Empty(source.DomainEvents);
    }

    [Fact]
    public void DomainEvents_cannot_be_modified_from_outside()
    {
        var warehouse = new Warehouse(Guid.NewGuid(), CompanyId.New());

        Assert.False(warehouse.DomainEvents is List<IDomainEvent>);
    }

    [Fact]
    public void Raising_null_throws()
    {
        var warehouse = new Warehouse(Guid.NewGuid(), CompanyId.New());

        Assert.Throws<ArgumentNullException>(warehouse.RaiseNull);
    }
}
