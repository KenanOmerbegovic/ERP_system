using Erp.Modules.Purchasing.Domain.Orders;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Purchasing.Domain.Receipts;

public sealed record GoodsReceiptLineInput(Guid OrderLineId, decimal Quantity, string? LotNumber, DateOnly? LotExpiryDate, IReadOnlyList<string> SerialNumbers);

/// <summary>
/// Goods received against one purchase order (FR-PUR-01), possibly part of it. Confirming happens in two steps around
/// the Warehouse call: <see cref="BeginConfirmation"/> freezes the draft (its quantities count as pending), and
/// <see cref="CompleteConfirmation"/> records the warehouse receipt, the number and the line values. If the warehouse
/// refuses, <see cref="AbortConfirmation"/> makes it a draft again. A storno is created in the confirming state and
/// goes through the same steps with the warehouse reversal.
/// </summary>
public sealed class GoodsReceipt : AggregateRoot<Guid>
{
    private readonly List<GoodsReceiptLine> _lines = [];

    private GoodsReceipt(Guid id, CompanyId companyId, Guid orderId, Guid supplierId, DateTimeOffset createdAt)
        : base(id, companyId)
    {
        PurchaseOrderId = orderId;
        SupplierId = supplierId;
        Status = DocumentStatus.Draft;
        CreatedAt = createdAt;
    }

    private GoodsReceipt()
    {
    }

    public DocumentStatus Status { get; private set; }

    public string? Number { get; private set; }

    public Guid PurchaseOrderId { get; private set; }

    public Guid SupplierId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public DateOnly ReceiptDate { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The warehouse receipt (or its storno) this receipt created.</summary>
    public Guid? StockDocumentId { get; private set; }

    public string? StockDocumentNumber { get; private set; }

    public Guid? ReversesReceiptId { get; private set; }

    public Guid? ReversedByReceiptId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public IReadOnlyList<GoodsReceiptLine> Lines => _lines.AsReadOnly();

    public bool IsDraft => Status == DocumentStatus.Draft;

    public bool IsStorno => ReversesReceiptId is not null;

    public decimal? TotalValue => Status == DocumentStatus.Confirmed ? _lines.Sum(line => line.Value ?? 0m) : null;

    public bool CanBeReversed =>
        Status == DocumentStatus.Confirmed && !IsStorno && ReversedByReceiptId is null && _lines.All(line => line.InvoicedQuantity == 0m);

    public static GoodsReceipt CreateDraft(
        CompanyId companyId, PurchaseOrder order, Guid warehouseId, DateOnly receiptDate, string? note, IReadOnlyList<GoodsReceiptLineInput> lines, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(order);
        var receipt = new GoodsReceipt(Guid.CreateVersion7(), companyId, order.Id, order.SupplierId, createdAt);
        receipt.SetContent(order, warehouseId, receiptDate, note, lines);
        return receipt;
    }

    public void UpdateDraft(PurchaseOrder order, Guid warehouseId, DateOnly receiptDate, string? note, IReadOnlyList<GoodsReceiptLineInput> lines)
    {
        EnsureStatus(DocumentStatus.Draft);
        if (order?.Id != PurchaseOrderId)
        {
            throw new ArgumentException("A receipt stays with its order.", nameof(order));
        }

        SetContent(order, warehouseId, receiptDate, note, lines);
    }

    public void BeginConfirmation()
    {
        EnsureStatus(DocumentStatus.Draft);
        Status = DocumentStatus.Confirming;
    }

    public void AbortConfirmation()
    {
        EnsureStatus(DocumentStatus.Confirming);
        if (IsStorno)
        {
            throw new InvalidOperationException("A storno that failed is removed, not returned to draft.");
        }

        Status = DocumentStatus.Draft;
    }

    public void CompleteConfirmation(string number, Guid stockDocumentId, string stockDocumentNumber, IReadOnlyList<decimal> lineValues, DateTimeOffset confirmedAt)
    {
        EnsureStatus(DocumentStatus.Confirming);
        ArgumentNullException.ThrowIfNull(lineValues);
        if (lineValues.Count != _lines.Count)
        {
            throw new ArgumentException("One value per line.", nameof(lineValues));
        }

        var ordered = _lines.OrderBy(line => line.LineNumber).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].RecordValue(lineValues[index]);
        }

        Number = Text.Required(number);
        StockDocumentId = stockDocumentId;
        StockDocumentNumber = Text.Required(stockDocumentNumber);
        Status = DocumentStatus.Confirmed;
        ConfirmedAt = confirmedAt;
    }

    /// <summary>A storno in the confirming state: same lines, to be reversed in the warehouse. Marks this receipt reversed.</summary>
    public GoodsReceipt CreateStorno(DateOnly date, DateTimeOffset createdAt)
    {
        if (!CanBeReversed)
        {
            throw new InvalidOperationException("Only a confirmed, not invoiced, not reversed receipt can be reversed.");
        }

        var storno = new GoodsReceipt(Guid.CreateVersion7(), CompanyId, PurchaseOrderId, SupplierId, createdAt)
        {
            WarehouseId = WarehouseId,
            ReceiptDate = date < ReceiptDate ? ReceiptDate : date,
            Note = Number is null ? null : $"Storno {Number}",
            ReversesReceiptId = Id,
            Status = DocumentStatus.Confirming,
        };
        foreach (var line in _lines)
        {
            storno._lines.Add(line.CopyForStorno(storno.Id));
        }

        ReversedByReceiptId = storno.Id;
        return storno;
    }

    /// <summary>The warehouse refused the reversal; the storno is removed and this receipt is no longer reversed.</summary>
    public void CancelReversal(Guid stornoId)
    {
        if (ReversedByReceiptId != stornoId)
        {
            throw new InvalidOperationException("That storno does not reverse this receipt.");
        }

        ReversedByReceiptId = null;
    }

    public GoodsReceiptLine Line(Guid lineId) =>
        _lines.SingleOrDefault(line => line.Id == lineId) ?? throw new InvalidOperationException($"Receipt line {lineId} is not on receipt {Id}.");

    private void SetContent(PurchaseOrder order, Guid warehouseId, DateOnly receiptDate, string? note, IReadOnlyList<GoodsReceiptLineInput> lines)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("A receipt needs a warehouse.", nameof(warehouseId));
        }

        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ArgumentException("A receipt needs at least one line.", nameof(lines));
        }

        WarehouseId = warehouseId;
        ReceiptDate = receiptDate;
        Note = Text.Optional(note);
        _lines.Clear();
        for (var index = 0; index < lines.Count; index++)
        {
            _lines.Add(GoodsReceiptLine.Create(CompanyId, Id, index + 1, order.Line(lines[index].OrderLineId), lines[index]));
        }
    }

    private void EnsureStatus(DocumentStatus status)
    {
        if (Status != status)
        {
            throw new InvalidOperationException($"The receipt is {Status}, not {status}.");
        }
    }
}

public sealed class GoodsReceiptLine : Entity<Guid>
{
    // Not readonly: EF Core sets it when it loads the line (column serial_numbers, text[]).
    private List<string> _serialNumbers = [];

    private GoodsReceiptLine(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private GoodsReceiptLine()
    {
    }

    public Guid ReceiptId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid OrderLineId { get; private set; }

    public Guid ArticleId { get; private set; }

    public decimal Quantity { get; private set; }

    /// <summary>The order line's price: what the goods enter stock at.</summary>
    public decimal UnitCost { get; private set; }

    /// <summary>The warehouse's value of the line (KM), once confirmed.</summary>
    public decimal? Value { get; private set; }

    /// <summary>Sum of confirmed supplier invoices on this line (stornos subtracted).</summary>
    public decimal InvoicedQuantity { get; private set; }

    public string? LotNumber { get; private set; }

    public DateOnly? LotExpiryDate { get; private set; }

    public IReadOnlyList<string> SerialNumbers => _serialNumbers.AsReadOnly();

    public decimal UninvoicedQuantity => Quantity - InvoicedQuantity;

    internal static GoodsReceiptLine Create(CompanyId companyId, Guid receiptId, int lineNumber, PurchaseOrderLine orderLine, GoodsReceiptLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.Quantity, "Quantity must be positive.");
        }

        var line = new GoodsReceiptLine(Guid.CreateVersion7(), companyId)
        {
            ReceiptId = receiptId,
            LineNumber = lineNumber,
            OrderLineId = orderLine.Id,
            ArticleId = orderLine.ArticleId,
            Quantity = input.Quantity,
            UnitCost = orderLine.UnitPrice,
            LotNumber = Text.Optional(input.LotNumber),
            LotExpiryDate = input.LotExpiryDate,
        };
        line._serialNumbers.AddRange(input.SerialNumbers.Select(serial => Text.Required(serial)));
        return line;
    }

    internal GoodsReceiptLine CopyForStorno(Guid stornoId)
    {
        var copy = new GoodsReceiptLine(Guid.CreateVersion7(), CompanyId)
        {
            ReceiptId = stornoId,
            LineNumber = LineNumber,
            OrderLineId = OrderLineId,
            ArticleId = ArticleId,
            Quantity = Quantity,
            UnitCost = UnitCost,
            LotNumber = LotNumber,
            LotExpiryDate = LotExpiryDate,
        };
        copy._serialNumbers.AddRange(_serialNumbers);
        return copy;
    }

    internal void RecordValue(decimal value) => Value = value;

    public void AddInvoiced(decimal quantity)
    {
        var invoiced = InvoicedQuantity + quantity;
        if (invoiced < 0m || invoiced > Quantity)
        {
            throw new InvalidOperationException($"Invoiced {invoiced} of {Quantity} received on receipt line {Id}.");
        }

        InvoicedQuantity = invoiced;
    }
}
