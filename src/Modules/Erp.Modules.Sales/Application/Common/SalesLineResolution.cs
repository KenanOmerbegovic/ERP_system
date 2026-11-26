using System.Globalization;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Offers;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Common;

/// <summary>A requested offer or order line; <see cref="VatCode"/> null means the article's default.</summary>
public sealed record SalesDraftLine(Guid ArticleId, decimal Quantity, decimal UnitPrice, string? VatCode);

internal sealed class SalesDraftLineValidator : AbstractValidator<SalesDraftLine>
{
    public SalesDraftLineValidator()
    {
        RuleFor(l => l.ArticleId).NotEmpty();
        RuleFor(l => l.Quantity).GreaterThan(0m).PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
        RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0m).PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
        RuleFor(l => l.VatCode).MaximumLength(FieldLengths.VatCode);
    }
}

/// <summary>
/// Checks an offer's or order's customer and lines against stored data: an active customer, active articles, and a VAT
/// code (the article's default if none is given) that has a rate on the document date. Resolves each line's kind.
/// </summary>
internal static class SalesLineResolution
{
    public static async Task<Result<IReadOnlyList<SalesLineInput>>> ResolveAsync(
        ISalesDbContext db, IVatRateLookup vatRates, CompanyId companyId, Guid customerId, DateOnly date, IReadOnlyList<SalesDraftLine> draftLines,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Id == customerId, cancellationToken);
        if (customer is null)
        {
            errors.Add("CustomerId", SalesValidationCodes.CustomerNotFound);
        }
        else if (!customer.CanBeSoldTo)
        {
            errors.Add("CustomerId", SalesValidationCodes.NotACustomer);
        }

        var articleIds = draftLines.Select(line => line.ArticleId).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking()
            .Where(a => a.CompanyId == companyId && articleIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        var knownCodes = new Dictionary<string, bool>(StringComparer.Ordinal);
        var lines = new List<SalesLineInput>();
        for (var index = 0; index < draftLines.Count; index++)
        {
            var line = draftLines[index];
            var prefix = string.Create(CultureInfo.InvariantCulture, $"Lines[{index}]");
            if (!articles.TryGetValue(line.ArticleId, out var article))
            {
                errors.Add($"{prefix}.ArticleId", SalesValidationCodes.ArticleNotFound);
                continue;
            }

            if (!article.IsActive)
            {
                errors.Add($"{prefix}.ArticleId", SalesValidationCodes.ArticleInactive);
            }

            var vatCode = string.IsNullOrWhiteSpace(line.VatCode) ? article.DefaultVatCode : line.VatCode.Trim();
            if (vatCode is null)
            {
                errors.Add($"{prefix}.VatCode", SalesValidationCodes.VatCodeRequired);
                continue;
            }

            if (!knownCodes.TryGetValue(vatCode, out var known))
            {
                known = await vatRates.FindAsync(companyId, vatCode, date, cancellationToken) is not null;
                knownCodes[vatCode] = known;
            }

            if (!known)
            {
                errors.Add($"{prefix}.VatCode", SalesValidationCodes.VatRateNotFound);
            }

            lines.Add(new SalesLineInput(line.ArticleId, article.IsService, line.Quantity, line.UnitPrice, vatCode));
        }

        return errors.Any ? errors.ToError() : lines;
    }
}

/// <summary>Turns the country calculator's PDV result into per-line amounts, or a validation error on the lines whose code has no rate.</summary>
internal static class VatApplication
{
    public static async Task<Result<(Dictionary<int, VatAmounts> Lines, List<VatAmounts> Totals)>> CalculateAsync(
        IVatCalculator calculator, CompanyId companyId, DateOnly date, IEnumerable<(int LineNumber, decimal NetAmount, string VatCode)> lines,
        CancellationToken cancellationToken)
    {
        var request = new VatCalculationRequest(
            companyId, date, lines.Select(l => new VatLineInput(l.LineNumber.ToString(CultureInfo.InvariantCulture), l.NetAmount, l.VatCode)).ToList());
        var calculation = await calculator.CalculateAsync(request, cancellationToken);
        if (calculation.IsFailure)
        {
            if (calculation.Error is not VatRateNotFoundError notFound)
            {
                return calculation.Error;
            }

            var errors = new ValidationErrors();
            foreach (var key in notFound.LineKeys)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"Lines[{int.Parse(key, CultureInfo.InvariantCulture) - 1}].VatCode"), SalesValidationCodes.VatRateNotFound);
            }

            return errors.ToError();
        }

        return (
            calculation.Value.Lines.ToDictionary(
                l => int.Parse(l.Key, CultureInfo.InvariantCulture),
                l => new VatAmounts(l.VatCode, l.Rate, l.NetAmount, l.VatAmount)),
            calculation.Value.Totals.Select(t => new VatAmounts(t.VatCode, t.Rate, t.Base, t.VatAmount)).ToList());
    }
}

/// <summary>LOT and serial numbers as the article's tracking type requires (expiry is set by receipts, never here).</summary>
internal static class TrackingRules
{
    public static void Check(
        Domain.References.TrackingType tracking, decimal quantity, string? lotNumber, IReadOnlyList<string> serialNumbers, string prefix, ValidationErrors errors)
    {
        var hasLot = !string.IsNullOrWhiteSpace(lotNumber);
        if (tracking == Domain.References.TrackingType.Lot && !hasLot)
        {
            errors.Add($"{prefix}.LotNumber", SalesValidationCodes.LotNumberRequired);
        }

        if (tracking != Domain.References.TrackingType.Lot && hasLot)
        {
            errors.Add($"{prefix}.LotNumber", SalesValidationCodes.LotNumberNotAllowed);
        }

        if (tracking == Domain.References.TrackingType.Serial && serialNumbers.Count != quantity)
        {
            errors.Add($"{prefix}.SerialNumbers", SalesValidationCodes.SerialNumberCountMismatch);
        }

        if (tracking != Domain.References.TrackingType.Serial && serialNumbers.Count > 0)
        {
            errors.Add($"{prefix}.SerialNumbers", SalesValidationCodes.SerialNumbersNotAllowed);
        }
    }
}
