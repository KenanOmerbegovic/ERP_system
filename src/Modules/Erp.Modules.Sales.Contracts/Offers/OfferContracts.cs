using Erp.Modules.Core.Contracts.Articles;

namespace Erp.Modules.Sales.Contracts.Offers;

/// <summary>Body of create and update requests for a draft offer. Prices are KM per unit, without PDV.</summary>
public sealed record OfferRequest(
    Guid CustomerId,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    string? Note,
    IReadOnlyList<SalesLineRequest> Lines);

/// <summary>A goods or service line of an offer or order; <see cref="VatCode"/> defaults to the article's default VAT code.</summary>
public sealed record SalesLineRequest(Guid ArticleId, decimal Quantity, decimal UnitPrice, string? VatCode);

/// <summary>
/// Accepting turns the offer into a draft sales order (FR-SAL-02); <see cref="WarehouseId"/> is where its deliveries go
/// out from by default.
/// </summary>
public sealed record AcceptOfferRequest(DateOnly OrderDate, Guid? WarehouseId);

public sealed record OfferResponse(
    Guid Id,
    OfferState Status,
    string? Number,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    string? Note,
    DateTimeOffset? SentAt,
    DateTimeOffset? DecidedAt,
    Guid? SalesOrderId,
    decimal NetAmount,
    IReadOnlyList<OfferLineResponse> Lines);

public sealed record OfferLineResponse(
    Guid Id,
    int LineNumber,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    ArticleKind Kind,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    decimal NetAmount);

public sealed record OfferSummaryResponse(
    Guid Id,
    OfferState Status,
    string? Number,
    Guid CustomerId,
    string CustomerName,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    Guid? SalesOrderId,
    decimal NetAmount);
