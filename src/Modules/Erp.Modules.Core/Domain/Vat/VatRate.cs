using Erp.SharedKernel.Domain;

namespace Erp.Modules.Core.Domain.Vat;

/// <summary>
/// Configuration (effective-dated): the PDV rate of one company-defined VAT code from <see cref="ValidFrom"/> on.
/// Never edited; a rate change is a new row with a later date. There are no rates in code.
/// </summary>
public sealed class VatRate : AggregateRoot<Guid>
{
    public const decimal MaxRate = 1m;

    private VatRate(Guid id, CompanyId companyId, string code, string name, decimal rate, DateOnly validFrom)
        : base(id, companyId)
    {
        if (rate is < 0m or > MaxRate)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "A VAT rate is a fraction between 0 and 1.");
        }

        Code = Text.Required(code);
        Name = Text.Required(name);
        Rate = rate;
        ValidFrom = validFrom;
    }

    private VatRate()
    {
    }

    /// <summary>E.g. "PDV17"; with <see cref="ValidFrom"/> unique per company.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>A fraction: 0.17 is 17 %.</summary>
    public decimal Rate { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    public static VatRate Create(CompanyId companyId, string code, string name, decimal rate, DateOnly validFrom) =>
        new(Guid.CreateVersion7(), companyId, code, name, rate, validFrom);
}
