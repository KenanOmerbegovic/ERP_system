using Erp.Modules.Warehouse.Contracts.Stock;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Domain.Articles;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Results;
using FluentValidation.Results;

namespace Erp.Modules.Warehouse.Application.Common;

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

/// <summary>Domain enums to their contract counterparts.</summary>
internal static class ContractMapping
{
    public static StockDocumentKind ToKind(this StockDocumentType type) => type switch
    {
        StockDocumentType.Receipt => StockDocumentKind.Receipt,
        StockDocumentType.Issue => StockDocumentKind.Issue,
        StockDocumentType.Transfer => StockDocumentKind.Transfer,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static StockDocumentState ToState(this StockDocumentStatus status) => status switch
    {
        StockDocumentStatus.Draft => StockDocumentState.Draft,
        StockDocumentStatus.Confirmed => StockDocumentState.Confirmed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static StockTrackingKind ToKind(this TrackingType tracking) => tracking switch
    {
        TrackingType.None => StockTrackingKind.None,
        TrackingType.Lot => StockTrackingKind.Lot,
        TrackingType.Serial => StockTrackingKind.Serial,
        _ => throw new ArgumentOutOfRangeException(nameof(tracking), tracking, null),
    };
}
