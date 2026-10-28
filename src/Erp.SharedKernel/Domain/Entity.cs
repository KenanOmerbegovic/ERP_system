namespace Erp.SharedKernel.Domain;

/// <summary>
/// Base class for entities. Identity is the <see cref="Id"/>: two entities of the
/// same type with the same id are equal. Every entity belongs to a company.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id, CompanyId companyId)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (companyId.IsEmpty)
        {
            throw new ArgumentException("Entity must belong to a company.", nameof(companyId));
        }

        Id = id;
        CompanyId = companyId;
    }

    /// <summary>For EF Core materialization only; EF sets the properties afterwards.</summary>
    protected Entity()
    {
        Id = default!;
    }

    public TId Id { get; private init; }

    public CompanyId CompanyId { get; private init; }

    public bool Equals(Entity<TId>? other) =>
        other is not null
        && (ReferenceEquals(this, other)
            || (other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id)));

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
