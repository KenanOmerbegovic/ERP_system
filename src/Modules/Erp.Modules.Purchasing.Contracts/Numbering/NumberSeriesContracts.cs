namespace Erp.Modules.Purchasing.Contracts.Numbering;

public enum PurchasingDocumentKind
{
    PurchaseOrder,
    GoodsReceipt,
    SupplierInvoice,
}

/// <summary>
/// How confirmed documents of one type are numbered from <see cref="ValidFrom"/> on: <c>{Prefix}{year}-{n}</c>,
/// n padded with zeros to <see cref="Padding"/> digits and counted per document type and year.
/// </summary>
public sealed record PurchasingNumberSeriesRequest(PurchasingDocumentKind DocumentType, DateOnly ValidFrom, string Prefix, int Padding);

public sealed record PurchasingNumberSeriesResponse(Guid Id, PurchasingDocumentKind DocumentType, DateOnly ValidFrom, string Prefix, int Padding);
