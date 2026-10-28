namespace Erp.SharedKernel.Domain;

public readonly record struct CompanyId(Guid Value)
{
    public static CompanyId New() => new(Guid.CreateVersion7());

    public bool IsEmpty => Value == Guid.Empty;

    public override string ToString() => Value.ToString();
}
