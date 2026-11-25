using Erp.Modules.Warehouse.Contracts.StockDocuments;

namespace Erp.Modules.Warehouse.Contracts.Numbering;

/// <summary>
/// How confirmed documents of one type are numbered from <see cref="ValidFrom"/> on: <c>{Prefix}{year}-{n}</c>,
/// n padded with zeros to <see cref="Padding"/> digits and counted per document type and year.
/// </summary>
public sealed record NumberSeriesRequest(StockDocumentKind DocumentType, DateOnly ValidFrom, string Prefix, int Padding);

public sealed record NumberSeriesResponse(Guid Id, StockDocumentKind DocumentType, DateOnly ValidFrom, string Prefix, int Padding);
