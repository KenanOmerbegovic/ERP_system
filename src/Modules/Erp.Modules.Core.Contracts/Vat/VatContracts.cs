using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;

namespace Erp.Modules.Core.Contracts.Vat;

/// <summary>
/// Body of create requests. <see cref="Rate"/> is a fraction (0.17 = 17 %). A rate is never edited: a change is a new
/// row with the same <see cref="Code"/> and a later <see cref="ValidFrom"/>.
/// </summary>
public sealed record VatRateRequest(string Code, string Name, decimal Rate, DateOnly ValidFrom);

public sealed record VatRateResponse(Guid Id, string Code, string Name, decimal Rate, DateOnly ValidFrom);

/// <summary>The rate of one VAT code that applies on a date.</summary>
public sealed record VatRateInfo(string Code, string Name, decimal Rate, DateOnly ValidFrom);

/// <summary>Synchronous query (CLAUDE.md: queries through Contracts are allowed). Implemented by the Core module.</summary>
public interface IVatRateLookup
{
    /// <summary>The latest rate of <paramref name="code"/> valid on <paramref name="date"/>; null if there is none.</summary>
    Task<VatRateInfo?> FindAsync(CompanyId companyId, string code, DateOnly date, CancellationToken cancellationToken);
}

/// <summary>One amount to tax. <see cref="Key"/> is the caller's line id, echoed in the result.</summary>
public sealed record VatLineInput(string Key, decimal NetAmount, string VatCode);

public sealed record VatCalculationRequest(CompanyId CompanyId, DateOnly Date, IReadOnlyList<VatLineInput> Lines);

public sealed record VatLineResult(string Key, decimal NetAmount, string VatCode, decimal Rate, decimal VatAmount);

/// <summary>Tax base and PDV of one VAT code, as a PDV return and an invoice footer show them.</summary>
public sealed record VatRateTotal(string VatCode, decimal Rate, decimal Base, decimal VatAmount);

public sealed record VatCalculation(
    IReadOnlyList<VatLineResult> Lines,
    IReadOnlyList<VatRateTotal> Totals,
    decimal NetAmount,
    decimal VatAmount,
    decimal GrossAmount);

/// <summary>
/// PDV of invoice lines (KM), by the country's rules. Defined here so every module that invoices (Purchasing, Sales)
/// uses the same calculation; implemented in the country localization (Erp.Localization.BiH), registered by the host.
/// </summary>
public interface IVatCalculator
{
    Task<Result<VatCalculation>> CalculateAsync(VatCalculationRequest request, CancellationToken cancellationToken);
}

public static class VatErrors
{
    public const string RateNotFoundCode = "Vat.RateNotFound";

    public static Error RateNotFound(string code, DateOnly date) =>
        Error.Validation(RateNotFoundCode, $"No VAT rate {code} is valid on {date:yyyy-MM-dd}.");
}
