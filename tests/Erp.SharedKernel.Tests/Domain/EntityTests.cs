using Erp.SharedKernel.Domain;

namespace Erp.SharedKernel.Tests.Domain;

public class EntityTests
{
    private static readonly CompanyId Company = CompanyId.New();

    private sealed class Customer(Guid id, CompanyId companyId) : Entity<Guid>(id, companyId);

    private sealed class Supplier(Guid id, CompanyId companyId) : Entity<Guid>(id, companyId);

    [Fact]
    public void Entities_with_same_type_and_id_are_equal()
    {
        var id = Guid.NewGuid();
        var a = new Customer(id, Company);
        var b = new Customer(id, Company);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        var a = new Customer(Guid.NewGuid(), Company);
        var b = new Customer(Guid.NewGuid(), Company);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Entities_of_different_types_with_same_id_are_not_equal()
    {
        var id = Guid.NewGuid();

        Assert.False(new Customer(id, Company).Equals(new Supplier(id, Company)));
    }

    [Fact]
    public void Entity_is_not_equal_to_null()
    {
        var customer = new Customer(Guid.NewGuid(), Company);

        Assert.False(customer.Equals(null));
        Assert.False(customer == null);
    }

    [Fact]
    public void Entity_keeps_company()
    {
        var customer = new Customer(Guid.NewGuid(), Company);

        Assert.Equal(Company, customer.CompanyId);
    }

    [Fact]
    public void Entity_without_company_throws()
    {
        Assert.Throws<ArgumentException>(() => new Customer(Guid.NewGuid(), default));
    }
}
