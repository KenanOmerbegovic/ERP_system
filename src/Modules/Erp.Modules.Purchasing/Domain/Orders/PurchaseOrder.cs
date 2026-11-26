using Erp.SharedKernel.Domain;

namespace Erp.Modules.Purchasing.Domain.Orders;

public sealed record PurchaseOrderLineInput(Guid ArticleId, decimal Quantity, decimal UnitPrice, string VatCode);

/// <summary>
/// An order to a supplier (FR-PUR-01). A draft can be edited or deleted; confirming numbers it and makes it immutable.
/// Afterwards only its received and invoiced quantities change, as receipts and invoices are confirmed. A storno
/// reverses an order nothing was received or invoiced on yet; a reversed order takes no more receipts.
/// </summary>
public sealed class PurchaseOrder : AggregateRoot<Guid>
{
    private readonly List<PurchaseOrderLine> _lines = [];

    private PurchaseOrder(Guid id, CompanyId companyId, DateTimeOffset createdAt)
        : base(id, companyId)
    {
        Status = DocumentStatus.Draft;
        CreatedAt = createdAt;
    }

    private PurchaseOrder()
    {
    }

    public DocumentStatus Status { get; private set; }

    public string? Number { get; private set; }

    public Guid SupplierId { get; private set; }

    public DateOnly OrderDate { get; private set; }

    public DateOnly? ExpectedDate { get; private set; }

    /// <summary>Where receipts go by default.</summary>
    public Guid? WarehouseId { get; private set; }

    public string? Note { get; private set; }

    public Guid? ReversesOrderId { get; private set; }

    public Guid? ReversedByOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public IReadOnlyList<PurchaseOrderLine> Lines => _lines.AsReadOnly();

    public bool IsDraft => Status == DocumentStatus.Draft;

    public bool IsStorno => ReversesOrderId is not null;

    /// <summary>Receipts and invoices can be made against it.</summary>
    public bool IsOpen => Status == DocumentStatus.Confirmed && !IsStorno && ReversedByOrderId is null;

    public decimal NetAmount => _lines.Sum(line => line.NetAmount);

    public bool FullyReceived => _lines.All(line => line.OpenQuantity == 0m);

    public static PurchaseOrder CreateDraft(
        CompanyId companyId,
        Guid supplierId,
        DateOnly orderDate,
        DateOnly? expectedDate,
        Guid? warehouseId,
        string? note,
        IReadOnlyList<PurchaseOrderLineInput> lines,
        DateTimeOffset createdAt)
    {
        var order = new PurchaseOrder(Guid.CreateVersion7(), companyId, createdAt);
        order.SetContent(supplierId, orderDate, expectedDate, warehouseId, note, lines);
        return order;
    }

    public void UpdateDraft(Guid supplierId, DateOnly orderDate, DateOnly? expectedDate, Guid? warehouseId, string? note, IReadOnlyList<PurchaseOrderLineInput> lines)
    {
        EnsureDraft();
        SetContent(supplierId, orderDate, expectedDate, warehouseId, note, lines);
    }

    public void Confirm(string number, DateTimeOffset confirmedAt)
    {
        EnsureDraft();
        Number = Text.Required(number);
        Status = DocumentStatus.Confirmed;
        ConfirmedAt = confirmedAt;
    }

    public bool CanBeReversed => IsOpen && _lines.All(line => line.ReceivedQuantity == 0m && line.InvoicedQuantity == 0m);

    /// <summary>A draft storno with the same lines; confirming it publishes the reversal. Marks this order reversed.</summary>
    public PurchaseOrder CreateStorno(DateOnly date, DateTimeOffset createdAt)
    {
        if (!CanBeReversed)
        {
            throw new InvalidOperationException("Only an open order with nothing received or invoiced can be reversed.");
        }

        var storno = new PurchaseOrder(Guid.CreateVersion7(), CompanyId, createdAt)
        {
            SupplierId = SupplierId,
            OrderDate = date < OrderDate ? OrderDate : date,
            WarehouseId = WarehouseId,
            Note = Number is null ? null : $"Storno {Number}",
            ReversesOrderId = Id,
        };
        foreach (var line in _lines)
        {
            storno._lines.Add(PurchaseOrderLine.Create(CompanyId, storno.Id, line.LineNumber,
                new PurchaseOrderLineInput(line.ArticleId, line.Quantity, line.UnitPrice, line.VatCode)));
        }

        ReversedByOrderId = storno.Id;
        return storno;
    }

    public PurchaseOrderLine Line(Guid lineId) =>
        _lines.SingleOrDefault(line => line.Id == lineId) ?? throw new InvalidOperationException($"Order line {lineId} is not on order {Id}.");

    /// <summary>A confirmed receipt (positive) or its storno (negative) changed what was received on a line.</summary>
    public void RecordReceived(Guid lineId, decimal quantity) => Line(lineId).AddReceived(quantity);

    /// <summary>A confirmed invoice (positive) or its storno (negative) changed what was invoiced on a line.</summary>
    public void RecordInvoiced(Guid lineId, decimal quantity) => Line(lineId).AddInvoiced(quantity);

    private void SetContent(Guid supplierId, DateOnly orderDate, DateOnly? expectedDate, Guid? warehouseId, string? note, IReadOnlyList<PurchaseOrderLineInput> lines)
    {
        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("An order needs a supplier.", nameof(supplierId));
        }

        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ArgumentException("An order needs at least one line.", nameof(lines));
        }

        SupplierId = supplierId;
        OrderDate = orderDate;
        ExpectedDate = expectedDate;
        WarehouseId = warehouseId;
        Note = Text.Optional(note);
        _lines.Clear();
        for (var index = 0; index < lines.Count; index++)
        {
            _lines.Add(PurchaseOrderLine.Create(CompanyId, Id, index + 1, lines[index]));
        }
    }

    private void EnsureDraft()
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("A confirmed order is immutable; reverse it with a storno.");
        }
    }
}

public sealed class PurchaseOrderLine : Entity<Guid>
{
    private PurchaseOrderLine(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private PurchaseOrderLine()
    {
    }

    public Guid OrderId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid ArticleId { get; private set; }

    public decimal Quantity { get; private set; }

    /// <summary>KM per unit, without PDV; the cost the goods enter stock at.</summary>
    public decimal UnitPrice { get; private set; }

    public string VatCode { get; private set; } = null!;

    /// <summary>Sum of confirmed receipts (stornos subtracted).</summary>
    public decimal ReceivedQuantity { get; private set; }

    /// <summary>Sum of confirmed supplier invoices (stornos subtracted).</summary>
    public decimal InvoicedQuantity { get; private set; }

    public decimal NetAmount => Amounts.LineAmount(Quantity, UnitPrice);

    public decimal OpenQuantity => Quantity - ReceivedQuantity;

    internal static PurchaseOrderLine Create(CompanyId companyId, Guid orderId, int lineNumber, PurchaseOrderLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ArticleId == Guid.Empty || input.Quantity <= 0m || input.UnitPrice < 0m)
        {
            throw new ArgumentException("A line needs an article, a positive quantity and a non-negative price.", nameof(input));
        }

        return new PurchaseOrderLine(Guid.CreateVersion7(), companyId)
        {
            OrderId = orderId,
            LineNumber = lineNumber,
            ArticleId = input.ArticleId,
            Quantity = input.Quantity,
            UnitPrice = input.UnitPrice,
            VatCode = Text.Required(input.VatCode),
        };
    }

    internal void AddReceived(decimal quantity)
    {
        var received = ReceivedQuantity + quantity;
        if (received < 0m || received > Quantity)
        {
            throw new InvalidOperationException($"Received {received} of {Quantity} on order line {Id}; check open quantities first.");
        }

        ReceivedQuantity = received;
    }

    internal void AddInvoiced(decimal quantity)
    {
        var invoiced = InvoicedQuantity + quantity;
        if (invoiced < 0m || invoiced > ReceivedQuantity)
        {
            throw new InvalidOperationException($"Invoiced {invoiced} of {ReceivedQuantity} received on order line {Id}.");
        }

        InvoicedQuantity = invoiced;
    }
}
