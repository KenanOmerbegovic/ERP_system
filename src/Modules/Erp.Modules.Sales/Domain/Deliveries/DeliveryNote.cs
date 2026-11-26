using Erp.Modules.Sales.Domain.Orders;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Sales.Domain.Deliveries;

public sealed record DeliveryNoteLineInput(Guid OrderLineId, decimal Quantity, string? LotNumber, IReadOnlyList<string> SerialNumbers);

/// <summary>
/// Goods delivered against one sales order (FR-SAL-03), possibly part of it. Confirming happens in two steps around the
/// Warehouse issue: <see cref="BeginConfirmation"/> freezes the draft (its quantities count as pending), and
/// <see cref="CompleteConfirmation"/> records the warehouse issue, the number and the cost of each line. If the
/// warehouse refuses (e.g. not enough stock, FR-WH-03), <see cref="AbortConfirmation"/> makes it a draft again. A storno
/// is created in the confirming state and goes through the same steps with the warehouse reversal.
/// </summary>
public sealed class DeliveryNote : AggregateRoot<Guid>
{
    private readonly List<DeliveryNoteLine> _lines = [];

    private DeliveryNote(Guid id, CompanyId companyId, Guid orderId, Guid customerId, DateTimeOffset createdAt)
        : base(id, companyId)
    {
        SalesOrderId = orderId;
        CustomerId = customerId;
        Status = DocumentStatus.Draft;
        CreatedAt = createdAt;
    }

    private DeliveryNote()
    {
    }

    public DocumentStatus Status { get; private set; }

    public string? Number { get; private set; }

    public Guid SalesOrderId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public DateOnly DeliveryDate { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The warehouse issue (or, for a storno, its reversal) this delivery note created.</summary>
    public Guid? StockDocumentId { get; private set; }

    public string? StockDocumentNumber { get; private set; }

    public Guid? ReversesDeliveryNoteId { get; private set; }

    public Guid? ReversedByDeliveryNoteId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public IReadOnlyList<DeliveryNoteLine> Lines => _lines.AsReadOnly();

    public bool IsDraft => Status == DocumentStatus.Draft;

    public bool IsStorno => ReversesDeliveryNoteId is not null;

    /// <summary>The cost of the goods issued (cost of goods sold), once confirmed.</summary>
    public decimal? CostValue => Status == DocumentStatus.Confirmed ? _lines.Sum(line => line.CostValue ?? 0m) : null;

    public bool CanBeReversed =>
        Status == DocumentStatus.Confirmed && !IsStorno && ReversedByDeliveryNoteId is null && _lines.All(line => line.InvoicedQuantity == 0m);

    public static DeliveryNote CreateDraft(
        CompanyId companyId, SalesOrder order, Guid warehouseId, DateOnly deliveryDate, string? note, IReadOnlyList<DeliveryNoteLineInput> lines, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(order);
        var delivery = new DeliveryNote(Guid.CreateVersion7(), companyId, order.Id, order.CustomerId, createdAt);
        delivery.SetContent(order, warehouseId, deliveryDate, note, lines);
        return delivery;
    }

    public void UpdateDraft(SalesOrder order, Guid warehouseId, DateOnly deliveryDate, string? note, IReadOnlyList<DeliveryNoteLineInput> lines)
    {
        EnsureStatus(DocumentStatus.Draft);
        if (order?.Id != SalesOrderId)
        {
            throw new ArgumentException("A delivery note stays with its order.", nameof(order));
        }

        SetContent(order, warehouseId, deliveryDate, note, lines);
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

    public void CompleteConfirmation(string number, Guid stockDocumentId, string stockDocumentNumber, IReadOnlyList<decimal> lineCosts, DateTimeOffset confirmedAt)
    {
        EnsureStatus(DocumentStatus.Confirming);
        ArgumentNullException.ThrowIfNull(lineCosts);
        if (lineCosts.Count != _lines.Count)
        {
            throw new ArgumentException("One cost per line.", nameof(lineCosts));
        }

        var ordered = _lines.OrderBy(line => line.LineNumber).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].RecordCost(lineCosts[index]);
        }

        Number = Text.Required(number);
        StockDocumentId = stockDocumentId;
        StockDocumentNumber = Text.Required(stockDocumentNumber);
        Status = DocumentStatus.Confirmed;
        ConfirmedAt = confirmedAt;
    }

    /// <summary>A storno in the confirming state: same lines, to be reversed in the warehouse. Marks this note reversed.</summary>
    public DeliveryNote CreateStorno(DateOnly date, DateTimeOffset createdAt)
    {
        if (!CanBeReversed)
        {
            throw new InvalidOperationException("Only a confirmed, not invoiced, not reversed delivery note can be reversed.");
        }

        var storno = new DeliveryNote(Guid.CreateVersion7(), CompanyId, SalesOrderId, CustomerId, createdAt)
        {
            WarehouseId = WarehouseId,
            DeliveryDate = date < DeliveryDate ? DeliveryDate : date,
            Note = Number is null ? null : $"Storno {Number}",
            ReversesDeliveryNoteId = Id,
            Status = DocumentStatus.Confirming,
        };
        foreach (var line in _lines)
        {
            storno._lines.Add(line.CopyForStorno(storno.Id));
        }

        ReversedByDeliveryNoteId = storno.Id;
        return storno;
    }

    /// <summary>The warehouse refused the reversal; the storno is removed and this note is no longer reversed.</summary>
    public void CancelReversal(Guid stornoId)
    {
        if (ReversedByDeliveryNoteId != stornoId)
        {
            throw new InvalidOperationException("That storno does not reverse this delivery note.");
        }

        ReversedByDeliveryNoteId = null;
    }

    public DeliveryNoteLine Line(Guid lineId) =>
        _lines.SingleOrDefault(line => line.Id == lineId) ?? throw new InvalidOperationException($"Delivery line {lineId} is not on delivery note {Id}.");

    private void SetContent(SalesOrder order, Guid warehouseId, DateOnly deliveryDate, string? note, IReadOnlyList<DeliveryNoteLineInput> lines)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("A delivery note needs a warehouse.", nameof(warehouseId));
        }

        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ArgumentException("A delivery note needs at least one line.", nameof(lines));
        }

        WarehouseId = warehouseId;
        DeliveryDate = deliveryDate;
        Note = Text.Optional(note);
        _lines.Clear();
        for (var index = 0; index < lines.Count; index++)
        {
            _lines.Add(DeliveryNoteLine.Create(CompanyId, Id, index + 1, order.Line(lines[index].OrderLineId), lines[index]));
        }
    }

    private void EnsureStatus(DocumentStatus status)
    {
        if (Status != status)
        {
            throw new InvalidOperationException($"The delivery note is {Status}, not {status}.");
        }
    }
}

public sealed class DeliveryNoteLine : Entity<Guid>
{
    // Not readonly: EF Core sets it when it loads the line (column serial_numbers, text[]).
    private List<string> _serialNumbers = [];

    private DeliveryNoteLine(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private DeliveryNoteLine()
    {
    }

    public Guid DeliveryNoteId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid OrderLineId { get; private set; }

    public Guid ArticleId { get; private set; }

    public decimal Quantity { get; private set; }

    /// <summary>What the warehouse issued the line at (KM, weighted average), once confirmed: its cost of goods sold.</summary>
    public decimal? CostValue { get; private set; }

    /// <summary>Sum of confirmed invoices on this line (stornos subtracted).</summary>
    public decimal InvoicedQuantity { get; private set; }

    public string? LotNumber { get; private set; }

    public IReadOnlyList<string> SerialNumbers => _serialNumbers.AsReadOnly();

    public decimal UninvoicedQuantity => Quantity - InvoicedQuantity;

    /// <summary>The cost of one unit, for bringing returned goods back at what they left at.</summary>
    public decimal UnitCost => Quantity == 0m ? 0m : decimal.Round((CostValue ?? 0m) / Quantity, FieldLengths.QuantityScale, MidpointRounding.AwayFromZero);

    internal static DeliveryNoteLine Create(CompanyId companyId, Guid deliveryNoteId, int lineNumber, SalesOrderLine orderLine, DeliveryNoteLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (orderLine.IsService)
        {
            throw new ArgumentException("Services are not delivered.", nameof(orderLine));
        }

        if (input.Quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.Quantity, "Quantity must be positive.");
        }

        var line = new DeliveryNoteLine(Guid.CreateVersion7(), companyId)
        {
            DeliveryNoteId = deliveryNoteId,
            LineNumber = lineNumber,
            OrderLineId = orderLine.Id,
            ArticleId = orderLine.ArticleId,
            Quantity = input.Quantity,
            LotNumber = Text.Optional(input.LotNumber),
        };
        line._serialNumbers.AddRange(input.SerialNumbers.Select(serial => Text.Required(serial)));
        return line;
    }

    internal DeliveryNoteLine CopyForStorno(Guid stornoId)
    {
        var copy = new DeliveryNoteLine(Guid.CreateVersion7(), CompanyId)
        {
            DeliveryNoteId = stornoId,
            LineNumber = LineNumber,
            OrderLineId = OrderLineId,
            ArticleId = ArticleId,
            Quantity = Quantity,
            LotNumber = LotNumber,
        };
        copy._serialNumbers.AddRange(_serialNumbers);
        return copy;
    }

    internal void RecordCost(decimal value) => CostValue = value;

    public void AddInvoiced(decimal quantity)
    {
        var invoiced = InvoicedQuantity + quantity;
        if (invoiced < 0m || invoiced > Quantity)
        {
            throw new InvalidOperationException($"Invoiced {invoiced} of {Quantity} delivered on delivery line {Id}.");
        }

        InvoicedQuantity = invoiced;
    }
}
