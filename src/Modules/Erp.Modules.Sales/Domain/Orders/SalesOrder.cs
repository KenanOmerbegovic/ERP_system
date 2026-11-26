using Erp.Modules.Sales.Domain.Offers;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Sales.Domain.Orders;

/// <summary>
/// A customer's order (FR-SAL-02, FR-SAL-03). A draft can be edited or deleted; confirming numbers it and makes it
/// immutable. Afterwards only its delivered and invoiced quantities change. Goods are delivered by delivery notes and
/// invoiced up to what was delivered; services are invoiced up to what was ordered, without a delivery. A storno
/// reverses an order nothing was delivered or invoiced on yet; a reversed order takes no more deliveries or invoices.
/// </summary>
public sealed class SalesOrder : AggregateRoot<Guid>
{
    private readonly List<SalesOrderLine> _lines = [];

    private SalesOrder(Guid id, CompanyId companyId, DateTimeOffset createdAt)
        : base(id, companyId)
    {
        Status = DocumentStatus.Draft;
        CreatedAt = createdAt;
    }

    private SalesOrder()
    {
    }

    public DocumentStatus Status { get; private set; }

    public string? Number { get; private set; }

    public Guid CustomerId { get; private set; }

    public DateOnly OrderDate { get; private set; }

    /// <summary>Where delivery notes go out from by default.</summary>
    public Guid? WarehouseId { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The accepted offer this order came from.</summary>
    public Guid? OfferId { get; private set; }

    public Guid? ReversesOrderId { get; private set; }

    public Guid? ReversedByOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public IReadOnlyList<SalesOrderLine> Lines => _lines.AsReadOnly();

    public bool IsDraft => Status == DocumentStatus.Draft;

    public bool IsStorno => ReversesOrderId is not null;

    /// <summary>Deliveries and invoices can be made against it.</summary>
    public bool IsOpen => Status == DocumentStatus.Confirmed && !IsStorno && ReversedByOrderId is null;

    public decimal NetAmount => _lines.Sum(line => line.NetAmount);

    public bool FullyDelivered => _lines.All(line => line.OpenQuantity == 0m);

    public bool FullyInvoiced => _lines.All(line => line.InvoicedQuantity == line.Quantity);

    public bool CanBeReversed => IsOpen && _lines.All(line => line.DeliveredQuantity == 0m && line.InvoicedQuantity == 0m);

    public static SalesOrder CreateDraft(
        CompanyId companyId, Guid customerId, DateOnly orderDate, Guid? warehouseId, string? note, IReadOnlyList<SalesLineInput> lines, DateTimeOffset createdAt)
    {
        var order = new SalesOrder(Guid.CreateVersion7(), companyId, createdAt);
        order.SetContent(customerId, orderDate, warehouseId, note, lines);
        return order;
    }

    /// <summary>FR-SAL-02: a draft order with the accepted offer's customer and lines.</summary>
    public static SalesOrder CreateFromOffer(Offer offer, DateOnly orderDate, Guid? warehouseId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (offer.Status != OfferStatus.Sent)
        {
            throw new InvalidOperationException("Only a sent offer can be accepted.");
        }

        var order = new SalesOrder(Guid.CreateVersion7(), offer.CompanyId, createdAt) { OfferId = offer.Id };
        order.SetContent(
            offer.CustomerId,
            orderDate,
            warehouseId,
            offer.Number is null ? offer.Note : $"Ponuda {offer.Number}",
            offer.Lines.OrderBy(line => line.LineNumber).Select(line => line.ToInput()).ToList());
        return order;
    }

    public void UpdateDraft(Guid customerId, DateOnly orderDate, Guid? warehouseId, string? note, IReadOnlyList<SalesLineInput> lines)
    {
        EnsureDraft();
        SetContent(customerId, orderDate, warehouseId, note, lines);
    }

    public void Confirm(string number, DateTimeOffset confirmedAt)
    {
        EnsureDraft();
        Number = Text.Required(number);
        Status = DocumentStatus.Confirmed;
        ConfirmedAt = confirmedAt;
    }

    /// <summary>A draft storno with the same lines; confirming it publishes the reversal. Marks this order reversed.</summary>
    public SalesOrder CreateStorno(DateOnly date, DateTimeOffset createdAt)
    {
        if (!CanBeReversed)
        {
            throw new InvalidOperationException("Only an open order with nothing delivered or invoiced can be reversed.");
        }

        var storno = new SalesOrder(Guid.CreateVersion7(), CompanyId, createdAt)
        {
            CustomerId = CustomerId,
            OrderDate = date < OrderDate ? OrderDate : date,
            WarehouseId = WarehouseId,
            Note = Number is null ? null : $"Storno {Number}",
            OfferId = OfferId,
            ReversesOrderId = Id,
        };
        foreach (var line in _lines)
        {
            storno._lines.Add(SalesOrderLine.Create(CompanyId, storno.Id, line.LineNumber, line.ToInput()));
        }

        ReversedByOrderId = storno.Id;
        return storno;
    }

    public SalesOrderLine Line(Guid lineId) =>
        _lines.SingleOrDefault(line => line.Id == lineId) ?? throw new InvalidOperationException($"Order line {lineId} is not on order {Id}.");

    /// <summary>A confirmed delivery note (positive) or its storno (negative) changed what was delivered on a line.</summary>
    public void RecordDelivered(Guid lineId, decimal quantity) => Line(lineId).AddDelivered(quantity);

    /// <summary>A confirmed invoice (positive) or its storno (negative) changed what was invoiced on a line.</summary>
    public void RecordInvoiced(Guid lineId, decimal quantity) => Line(lineId).AddInvoiced(quantity);

    private void SetContent(Guid customerId, DateOnly orderDate, Guid? warehouseId, string? note, IReadOnlyList<SalesLineInput> lines)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("An order needs a customer.", nameof(customerId));
        }

        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ArgumentException("An order needs at least one line.", nameof(lines));
        }

        CustomerId = customerId;
        OrderDate = orderDate;
        WarehouseId = warehouseId;
        Note = Text.Optional(note);
        _lines.Clear();
        for (var index = 0; index < lines.Count; index++)
        {
            _lines.Add(SalesOrderLine.Create(CompanyId, Id, index + 1, lines[index]));
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

public sealed class SalesOrderLine : Entity<Guid>
{
    private SalesOrderLine(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private SalesOrderLine()
    {
    }

    public Guid OrderId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid ArticleId { get; private set; }

    /// <summary>The article's kind when the line was saved: services are invoiced without delivery.</summary>
    public bool IsService { get; private set; }

    public decimal Quantity { get; private set; }

    /// <summary>KM per unit, without PDV; the price every invoice of this line charges.</summary>
    public decimal UnitPrice { get; private set; }

    public string VatCode { get; private set; } = null!;

    /// <summary>Sum of confirmed delivery notes (stornos subtracted); always 0 for a service.</summary>
    public decimal DeliveredQuantity { get; private set; }

    /// <summary>Sum of confirmed invoices (stornos subtracted).</summary>
    public decimal InvoicedQuantity { get; private set; }

    public decimal NetAmount => Amounts.LineAmount(Quantity, UnitPrice);

    /// <summary>What delivery notes may still take; nothing for a service.</summary>
    public decimal OpenQuantity => IsService ? 0m : Quantity - DeliveredQuantity;

    /// <summary>What invoices may still take: delivered − invoiced for goods, ordered − invoiced for services.</summary>
    public decimal InvoiceableQuantity => (IsService ? Quantity : DeliveredQuantity) - InvoicedQuantity;

    public SalesLineInput ToInput() => new(ArticleId, IsService, Quantity, UnitPrice, VatCode);

    internal static SalesOrderLine Create(CompanyId companyId, Guid orderId, int lineNumber, SalesLineInput input)
    {
        SalesLines.EnsureValid(input);
        return new SalesOrderLine(Guid.CreateVersion7(), companyId)
        {
            OrderId = orderId,
            LineNumber = lineNumber,
            ArticleId = input.ArticleId,
            IsService = input.IsService,
            Quantity = input.Quantity,
            UnitPrice = input.UnitPrice,
            VatCode = Text.Required(input.VatCode),
        };
    }

    internal void AddDelivered(decimal quantity)
    {
        var delivered = DeliveredQuantity + quantity;
        if (IsService || delivered < 0m || delivered > Quantity || delivered < InvoicedQuantity)
        {
            throw new InvalidOperationException($"Delivered {delivered} of {Quantity} (invoiced {InvoicedQuantity}) on order line {Id}.");
        }

        DeliveredQuantity = delivered;
    }

    internal void AddInvoiced(decimal quantity)
    {
        var invoiced = InvoicedQuantity + quantity;
        var limit = IsService ? Quantity : DeliveredQuantity;
        if (invoiced < 0m || invoiced > limit)
        {
            throw new InvalidOperationException($"Invoiced {invoiced} of {limit} on order line {Id}; check invoiceable quantities first.");
        }

        InvoicedQuantity = invoiced;
    }
}
