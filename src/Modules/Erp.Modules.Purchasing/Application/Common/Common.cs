using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Purchasing.Contracts;
using Erp.Modules.Purchasing.Contracts.Numbering;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Purchasing.Domain.References;
using Erp.SharedKernel.Results;
using FluentValidation.Results;

namespace Erp.Modules.Purchasing.Application.Common;

public static class Paging
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
}

internal static class ValidationExtensions
{
    /// <summary>Error codes per property; clients turn codes into UI text.</summary>
    public static ValidationError ToValidationError(this ValidationResult result) =>
        new(result.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorCode).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal));
}

/// <summary>Collects validation codes per property for checks FluentValidation cannot make (they need stored data).</summary>
internal sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool Any => _errors.Count > 0;

    public void Add(string property, string code)
    {
        if (!_errors.TryGetValue(property, out var codes))
        {
            _errors[property] = codes = [];
        }

        if (!codes.Contains(code, StringComparer.Ordinal))
        {
            codes.Add(code);
        }
    }

    public ValidationError ToError() =>
        new(_errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal));
}

/// <summary>Domain enums to their contract counterparts, and back for requests (undefined values pass through for the validator).</summary>
internal static class ContractMapping
{
    public static PurchaseDocumentState ToState(this DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => PurchaseDocumentState.Draft,
        DocumentStatus.Confirming => PurchaseDocumentState.Confirming,
        DocumentStatus.Confirmed => PurchaseDocumentState.Confirmed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static DocumentStatus ToStatus(this PurchaseDocumentState state) => state switch
    {
        PurchaseDocumentState.Draft => DocumentStatus.Draft,
        PurchaseDocumentState.Confirming => DocumentStatus.Confirming,
        PurchaseDocumentState.Confirmed => DocumentStatus.Confirmed,
        _ => (DocumentStatus)(int)state,
    };

    public static ArticleTracking ToContract(this TrackingType tracking) => tracking switch
    {
        TrackingType.None => ArticleTracking.None,
        TrackingType.Lot => ArticleTracking.Lot,
        TrackingType.Serial => ArticleTracking.Serial,
        _ => throw new ArgumentOutOfRangeException(nameof(tracking), tracking, null),
    };

    public static TrackingType ToTracking(this ArticleTracking tracking) => tracking switch
    {
        ArticleTracking.None => TrackingType.None,
        ArticleTracking.Lot => TrackingType.Lot,
        ArticleTracking.Serial => TrackingType.Serial,
        _ => throw new ArgumentOutOfRangeException(nameof(tracking), tracking, null),
    };

    public static PurchasingDocumentKind ToKind(this PurchasingDocumentType type) => type switch
    {
        PurchasingDocumentType.PurchaseOrder => PurchasingDocumentKind.PurchaseOrder,
        PurchasingDocumentType.GoodsReceipt => PurchasingDocumentKind.GoodsReceipt,
        PurchasingDocumentType.SupplierInvoice => PurchasingDocumentKind.SupplierInvoice,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static PurchasingDocumentType ToType(this PurchasingDocumentKind kind) => kind switch
    {
        PurchasingDocumentKind.PurchaseOrder => PurchasingDocumentType.PurchaseOrder,
        PurchasingDocumentKind.GoodsReceipt => PurchasingDocumentType.GoodsReceipt,
        PurchasingDocumentKind.SupplierInvoice => PurchasingDocumentType.SupplierInvoice,
        _ => (PurchasingDocumentType)(int)kind,
    };
}
