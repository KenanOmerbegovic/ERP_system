using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Orders;

/// <summary>A requested order line; <see cref="VatCode"/> null means the article's default.</summary>
public sealed record PurchaseOrderDraftLine(Guid ArticleId, decimal Quantity, decimal UnitPrice, string? VatCode);

public interface IPurchaseOrderDraft
{
    CompanyId CompanyId { get; }

    Guid SupplierId { get; }

    DateOnly OrderDate { get; }

    DateOnly? ExpectedDate { get; }

    Guid? WarehouseId { get; }

    string? Note { get; }

    IReadOnlyList<PurchaseOrderDraftLine> Lines { get; }
}

internal sealed class PurchaseOrderDraftValidator : AbstractValidator<IPurchaseOrderDraft>
{
    public const int MaxLines = 500;

    public PurchaseOrderDraftValidator(IClock clock)
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.OrderDate).Must(date => date <= clock.Today).WithErrorCode(PurchasingValidationCodes.DateInFuture);
        RuleFor(x => x.ExpectedDate).GreaterThanOrEqualTo(x => x.OrderDate).When(x => x.ExpectedDate is not null);
        RuleFor(x => x.Note).MaximumLength(FieldLengths.Note);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= MaxLines).WithErrorCode("Lines.TooMany");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ArticleId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0m).PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0m).PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
            line.RuleFor(l => l.VatCode).MaximumLength(FieldLengths.VatCode);
        });
    }
}

/// <summary>Checks a valid-shaped draft against stored data and resolves each line's VAT code.</summary>
internal static class PurchaseOrderDraftChecks
{
    public static async Task<Result<IReadOnlyList<PurchaseOrderLineInput>>> ResolveAsync(
        IPurchasingDbContext db, IVatRateLookup vatRates, IPurchaseOrderDraft draft, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyId == draft.CompanyId && s.Id == draft.SupplierId, cancellationToken);
        if (supplier is null)
        {
            errors.Add(nameof(draft.SupplierId), PurchasingValidationCodes.SupplierNotFound);
        }
        else if (!supplier.CanBeOrderedFrom)
        {
            errors.Add(nameof(draft.SupplierId), PurchasingValidationCodes.NotASupplier);
        }

        var articleIds = draft.Lines.Select(line => line.ArticleId).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking()
            .Where(a => a.CompanyId == draft.CompanyId && articleIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        var knownCodes = new Dictionary<string, bool>(StringComparer.Ordinal);
        var lines = new List<PurchaseOrderLineInput>();
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            var prefix = $"{nameof(draft.Lines)}[{index}]";
            if (!articles.TryGetValue(line.ArticleId, out var article))
            {
                errors.Add($"{prefix}.{nameof(line.ArticleId)}", PurchasingValidationCodes.ArticleNotFound);
                continue;
            }

            if (!article.CanBeOrdered)
            {
                errors.Add($"{prefix}.{nameof(line.ArticleId)}", PurchasingValidationCodes.ArticleNotOrderable);
            }

            var vatCode = string.IsNullOrWhiteSpace(line.VatCode) ? article.DefaultVatCode : line.VatCode.Trim();
            if (vatCode is null)
            {
                errors.Add($"{prefix}.{nameof(line.VatCode)}", PurchasingValidationCodes.VatCodeRequired);
                continue;
            }

            if (!knownCodes.TryGetValue(vatCode, out var known))
            {
                known = await vatRates.FindAsync(draft.CompanyId, vatCode, draft.OrderDate, cancellationToken) is not null;
                knownCodes[vatCode] = known;
            }

            if (!known)
            {
                errors.Add($"{prefix}.{nameof(line.VatCode)}", PurchasingValidationCodes.VatRateNotFound);
            }

            lines.Add(new PurchaseOrderLineInput(line.ArticleId, line.Quantity, line.UnitPrice, vatCode));
        }

        return errors.Any ? errors.ToError() : lines;
    }
}
