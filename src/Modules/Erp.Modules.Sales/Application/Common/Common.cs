using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Contracts.Numbering;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Modules.Sales.Domain.Offers;
using Erp.Modules.Sales.Domain.References;
using Erp.SharedKernel.Results;
using FluentValidation.Results;

namespace Erp.Modules.Sales.Application.Common;

public static class Paging
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
}

internal static class Limits
{
    public const int MaxLines = 500;
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
    public static SalesDocumentState ToState(this DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => SalesDocumentState.Draft,
        DocumentStatus.Confirming => SalesDocumentState.Confirming,
        DocumentStatus.Confirmed => SalesDocumentState.Confirmed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static DocumentStatus ToStatus(this SalesDocumentState state) => state switch
    {
        SalesDocumentState.Draft => DocumentStatus.Draft,
        SalesDocumentState.Confirming => DocumentStatus.Confirming,
        SalesDocumentState.Confirmed => DocumentStatus.Confirmed,
        _ => (DocumentStatus)(int)state,
    };

    public static OfferState ToState(this OfferStatus status) => status switch
    {
        OfferStatus.Draft => OfferState.Draft,
        OfferStatus.Sent => OfferState.Sent,
        OfferStatus.Accepted => OfferState.Accepted,
        OfferStatus.Rejected => OfferState.Rejected,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static OfferStatus ToStatus(this OfferState state) => state switch
    {
        OfferState.Draft => OfferStatus.Draft,
        OfferState.Sent => OfferStatus.Sent,
        OfferState.Accepted => OfferStatus.Accepted,
        OfferState.Rejected => OfferStatus.Rejected,
        _ => (OfferStatus)(int)state,
    };

    public static CreditNoteLineKind ToContract(this CreditKind kind) => kind switch
    {
        CreditKind.Return => CreditNoteLineKind.Return,
        CreditKind.ValueReduction => CreditNoteLineKind.ValueReduction,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static CreditKind ToKind(this CreditNoteLineKind kind) => kind switch
    {
        CreditNoteLineKind.Return => CreditKind.Return,
        CreditNoteLineKind.ValueReduction => CreditKind.ValueReduction,
        _ => (CreditKind)(int)kind,
    };

    public static ArticleKind ToArticleKind(this bool isService) => isService ? ArticleKind.Service : ArticleKind.Goods;

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

    public static SalesDocumentKind ToKind(this SalesDocumentType type) => type switch
    {
        SalesDocumentType.Offer => SalesDocumentKind.Offer,
        SalesDocumentType.SalesOrder => SalesDocumentKind.SalesOrder,
        SalesDocumentType.DeliveryNote => SalesDocumentKind.DeliveryNote,
        SalesDocumentType.SalesInvoice => SalesDocumentKind.SalesInvoice,
        SalesDocumentType.CreditNote => SalesDocumentKind.CreditNote,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static SalesDocumentType ToType(this SalesDocumentKind kind) => kind switch
    {
        SalesDocumentKind.Offer => SalesDocumentType.Offer,
        SalesDocumentKind.SalesOrder => SalesDocumentType.SalesOrder,
        SalesDocumentKind.DeliveryNote => SalesDocumentType.DeliveryNote,
        SalesDocumentKind.SalesInvoice => SalesDocumentType.SalesInvoice,
        SalesDocumentKind.CreditNote => SalesDocumentType.CreditNote,
        _ => (SalesDocumentType)(int)kind,
    };
}
