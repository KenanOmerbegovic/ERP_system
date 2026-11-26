namespace Erp.Modules.Sales.Contracts.Numbering;

public enum SalesDocumentKind
{
    Offer,
    SalesOrder,
    DeliveryNote,
    SalesInvoice,
    CreditNote,
}

/// <summary>
/// How documents of one type are numbered from <see cref="ValidFrom"/> on: <c>{Prefix}{year}-{n}</c>, n padded with zeros
/// to <see cref="Padding"/> digits and counted per document type and year. Offers are numbered when sent, the others
/// when confirmed.
/// </summary>
public sealed record SalesNumberSeriesRequest(SalesDocumentKind DocumentType, DateOnly ValidFrom, string Prefix, int Padding);

public sealed record SalesNumberSeriesResponse(Guid Id, SalesDocumentKind DocumentType, DateOnly ValidFrom, string Prefix, int Padding);
