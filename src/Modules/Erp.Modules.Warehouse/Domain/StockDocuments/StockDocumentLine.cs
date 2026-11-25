using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Monetary;

namespace Erp.Modules.Warehouse.Domain.StockDocuments;

/// <summary>What one line of a draft says; checked against the article's tracking type before it reaches the domain.</summary>
public sealed record StockLineInput(
    Guid ArticleId,
    decimal Quantity,
    decimal? UnitCost,
    string? LotNumber,
    DateOnly? LotExpiryDate,
    IReadOnlyList<string> SerialNumbers);

public sealed class StockDocumentLine : Entity<Guid>
{
    // Not readonly: EF Core sets it when it loads the line (column serial_numbers, text[]).
    private List<string> _serialNumbers = [];

    private StockDocumentLine(Guid id, CompanyId companyId, Guid documentId, int lineNumber)
        : base(id, companyId)
    {
        DocumentId = documentId;
        LineNumber = lineNumber;
    }

    private StockDocumentLine()
    {
    }

    public Guid DocumentId { get; private set; }

    /// <summary>1-based position on the document.</summary>
    public int LineNumber { get; private set; }

    public Guid ArticleId { get; private set; }

    /// <summary>Always positive; the document type says which way the goods move.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>KM per unit; receipts only.</summary>
    public decimal? UnitCost { get; private set; }

    /// <summary>
    /// The line's value in KM, set when the document is posted: quantity × unit cost for a receipt, the weighted-average
    /// cost for an issue or transfer, the reversed line's value for a storno.
    /// </summary>
    public decimal? Value { get; private set; }

    /// <summary>The part of <see cref="Value"/> that did not change the stock value (storno only; see StockItem).</summary>
    public decimal PriceDifference { get; private set; }

    public string? LotNumber { get; private set; }

    /// <summary>Given on receipts of a new LOT.</summary>
    public DateOnly? LotExpiryDate { get; private set; }

    /// <summary>One per piece, for serial tracked articles; empty otherwise.</summary>
    public IReadOnlyList<string> SerialNumbers => _serialNumbers.AsReadOnly();

    /// <summary>Quantity × unit cost, rounded to fening (receipts only).</summary>
    public decimal ReceiptValue =>
        UnitCost is { } unitCost
            ? (Money.Km(unitCost) * Quantity).Round().Amount
            : throw new InvalidOperationException("Only receipt lines have a unit cost.");

    internal static StockDocumentLine Create(CompanyId companyId, Guid documentId, int lineNumber, StockDocumentType type, StockLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ArticleId == Guid.Empty)
        {
            throw new ArgumentException("A line needs an article.", nameof(input));
        }

        if (input.Quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.Quantity, "Quantity must be positive.");
        }

        if ((type == StockDocumentType.Receipt) != (input.UnitCost is not null) || input.UnitCost < 0m)
        {
            throw new ArgumentException("Receipt lines need a non-negative unit cost; other lines have none.", nameof(input));
        }

        var serialNumbers = input.SerialNumbers?.Select(serial => Text.Required(serial)).ToList() ?? [];
        if (serialNumbers.Count > 0 && serialNumbers.Count != input.Quantity)
        {
            throw new ArgumentException("A serial tracked line needs one serial number per piece.", nameof(input));
        }

        var line = new StockDocumentLine(Guid.CreateVersion7(), companyId, documentId, lineNumber)
        {
            ArticleId = input.ArticleId,
            Quantity = input.Quantity,
            UnitCost = input.UnitCost,
            LotNumber = Text.Optional(input.LotNumber),
            LotExpiryDate = type == StockDocumentType.Receipt ? input.LotExpiryDate : null,
        };
        line._serialNumbers.AddRange(serialNumbers);
        return line;
    }

    /// <summary>The same goods with the value they were posted at; posting the storno reverses them.</summary>
    internal StockDocumentLine CopyForStorno(Guid stornoId)
    {
        var copy = new StockDocumentLine(Guid.CreateVersion7(), CompanyId, stornoId, LineNumber)
        {
            ArticleId = ArticleId,
            Quantity = Quantity,
            UnitCost = UnitCost,
            LotNumber = LotNumber,
            LotExpiryDate = LotExpiryDate,
            OriginalValue = Value ?? throw new InvalidOperationException("The reversed line was never posted."),
        };
        copy._serialNumbers.AddRange(_serialNumbers);
        return copy;
    }

    /// <summary>On a storno line: the value the reversed line was posted at. Not stored; posting copies it into <see cref="Value"/>.</summary>
    internal decimal? OriginalValue { get; private init; }

    internal void RecordPosting(decimal value, decimal priceDifference)
    {
        if (Value is not null)
        {
            throw new InvalidOperationException("The line is already posted.");
        }

        Value = value;
        PriceDifference = priceDifference;
    }
}
