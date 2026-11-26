using Erp.SharedKernel.Domain;

namespace Erp.Modules.Purchasing.Domain.Invoices;

/// <summary>An invoice line as the handler resolved it from a confirmed receipt line.</summary>
public sealed record SupplierInvoiceLineInput(
    Guid ReceiptId,
    Guid ReceiptLineId,
    Guid OrderLineId,
    Guid ArticleId,
    decimal ReceivedUnitCost,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode);

/// <summary>PDV of one line or one rate, as the country's calculator computed it.</summary>
public sealed record VatAmounts(string VatCode, decimal Rate, decimal Base, decimal VatAmount);

/// <summary>
/// A supplier's invoice (FR-PUR-02) for goods received against one purchase order, with input PDV. Confirming
/// ("registering") numbers it and makes it immutable; a storno reverses it and releases its invoiced quantities. A price
/// other than the received cost is allowed: the difference is recorded per line and reported to Finance.
/// </summary>
public sealed class SupplierInvoice : AggregateRoot<Guid>
{
    private readonly List<SupplierInvoiceLine> _lines = [];
    private readonly List<SupplierInvoiceVatTotal> _vatTotals = [];

    private SupplierInvoice(Guid id, CompanyId companyId, Guid orderId, Guid supplierId, DateTimeOffset createdAt)
        : base(id, companyId)
    {
        PurchaseOrderId = orderId;
        SupplierId = supplierId;
        Status = DocumentStatus.Draft;
        CreatedAt = createdAt;
    }

    private SupplierInvoice()
    {
    }

    public DocumentStatus Status { get; private set; }

    /// <summary>Our number, from the number series, once confirmed.</summary>
    public string? Number { get; private set; }

    public Guid PurchaseOrderId { get; private set; }

    public Guid SupplierId { get; private set; }

    /// <summary>The number the supplier printed; unique per supplier among registered invoices.</summary>
    public string SupplierInvoiceNumber { get; private set; } = null!;

    /// <summary>Also the date the PDV rates are taken for.</summary>
    public DateOnly InvoiceDate { get; private set; }

    public DateOnly DueDate { get; private set; }

    public string? Note { get; private set; }

    public Guid? ReversesInvoiceId { get; private set; }

    public Guid? ReversedByInvoiceId { get; private set; }

    public decimal NetAmount { get; private set; }

    public decimal VatAmount { get; private set; }

    public decimal GrossAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public IReadOnlyList<SupplierInvoiceLine> Lines => _lines.AsReadOnly();

    public IReadOnlyList<SupplierInvoiceVatTotal> VatTotals => _vatTotals.AsReadOnly();

    public bool IsDraft => Status == DocumentStatus.Draft;

    public bool IsStorno => ReversesInvoiceId is not null;

    public decimal PriceDifference => _lines.Sum(line => line.PriceDifference);

    public bool CanBeReversed => Status == DocumentStatus.Confirmed && !IsStorno && ReversedByInvoiceId is null;

    public static SupplierInvoice CreateDraft(
        CompanyId companyId,
        Guid orderId,
        Guid supplierId,
        string supplierInvoiceNumber,
        DateOnly invoiceDate,
        DateOnly dueDate,
        string? note,
        IReadOnlyList<SupplierInvoiceLineInput> lines,
        DateTimeOffset createdAt)
    {
        var invoice = new SupplierInvoice(Guid.CreateVersion7(), companyId, orderId, supplierId, createdAt);
        invoice.SetContent(supplierInvoiceNumber, invoiceDate, dueDate, note, lines);
        return invoice;
    }

    public void UpdateDraft(string supplierInvoiceNumber, DateOnly invoiceDate, DateOnly dueDate, string? note, IReadOnlyList<SupplierInvoiceLineInput> lines)
    {
        EnsureDraft();
        SetContent(supplierInvoiceNumber, invoiceDate, dueDate, note, lines);
    }

    /// <summary>Records the PDV per line (by line number) and per rate. Done on every save and again on confirmation.</summary>
    public void ApplyVat(IReadOnlyDictionary<int, VatAmounts> lineVat, IReadOnlyList<VatAmounts> totals)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(lineVat);
        ArgumentNullException.ThrowIfNull(totals);
        foreach (var line in _lines)
        {
            line.RecordVat(lineVat[line.LineNumber]);
        }

        _vatTotals.Clear();
        _vatTotals.AddRange(totals.Select(total => SupplierInvoiceVatTotal.Create(CompanyId, Id, total)));
        NetAmount = _lines.Sum(line => line.NetAmount);
        VatAmount = _lines.Sum(line => line.VatAmount);
        GrossAmount = NetAmount + VatAmount;
    }

    public void Confirm(string number, DateTimeOffset confirmedAt)
    {
        EnsureDraft();
        Number = Text.Required(number);
        Status = DocumentStatus.Confirmed;
        ConfirmedAt = confirmedAt;
    }

    /// <summary>A storno with the same lines and amounts, confirmed by the caller. Marks this invoice reversed.</summary>
    public SupplierInvoice CreateStorno(DateOnly date, DateTimeOffset createdAt)
    {
        if (!CanBeReversed)
        {
            throw new InvalidOperationException("Only a registered invoice that is not a storno and not yet reversed can be reversed.");
        }

        var storno = new SupplierInvoice(Guid.CreateVersion7(), CompanyId, PurchaseOrderId, SupplierId, createdAt)
        {
            SupplierInvoiceNumber = SupplierInvoiceNumber,
            InvoiceDate = date < InvoiceDate ? InvoiceDate : date,
            DueDate = DueDate,
            Note = Number is null ? null : $"Storno {Number}",
            ReversesInvoiceId = Id,
            NetAmount = NetAmount,
            VatAmount = VatAmount,
            GrossAmount = GrossAmount,
        };
        foreach (var line in _lines)
        {
            storno._lines.Add(line.CopyForStorno(storno.Id));
        }

        storno._vatTotals.AddRange(_vatTotals.Select(total => SupplierInvoiceVatTotal.Create(CompanyId, storno.Id,
            new VatAmounts(total.VatCode, total.Rate, total.Base, total.VatAmount))));
        ReversedByInvoiceId = storno.Id;
        return storno;
    }

    private void SetContent(string supplierInvoiceNumber, DateOnly invoiceDate, DateOnly dueDate, string? note, IReadOnlyList<SupplierInvoiceLineInput> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ArgumentException("An invoice needs at least one line.", nameof(lines));
        }

        if (dueDate < invoiceDate)
        {
            throw new ArgumentException("The due date cannot be before the invoice date.", nameof(dueDate));
        }

        SupplierInvoiceNumber = Text.Required(supplierInvoiceNumber);
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        Note = Text.Optional(note);
        _lines.Clear();
        _vatTotals.Clear();
        for (var index = 0; index < lines.Count; index++)
        {
            _lines.Add(SupplierInvoiceLine.Create(CompanyId, Id, index + 1, lines[index]));
        }
    }

    private void EnsureDraft()
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("A registered invoice is immutable; reverse it with a storno.");
        }
    }
}

public sealed class SupplierInvoiceLine : Entity<Guid>
{
    private SupplierInvoiceLine(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private SupplierInvoiceLine()
    {
    }

    public Guid InvoiceId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid ReceiptId { get; private set; }

    public Guid ReceiptLineId { get; private set; }

    public Guid OrderLineId { get; private set; }

    public Guid ArticleId { get; private set; }

    public decimal Quantity { get; private set; }

    /// <summary>KM per unit without PDV, as the supplier invoiced it.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>The receipt line's unit cost: what the goods entered stock at.</summary>
    public decimal ReceivedUnitCost { get; private set; }

    public string VatCode { get; private set; } = null!;

    public decimal VatRate { get; private set; }

    public decimal VatAmount { get; private set; }

    public decimal NetAmount => Amounts.LineAmount(Quantity, UnitPrice);

    /// <summary>The stock value of the invoiced quantity at the received cost.</summary>
    public decimal ReceivedValue => Amounts.LineAmount(Quantity, ReceivedUnitCost);

    /// <summary>Invoiced above (positive) or below (negative) the received cost, in KM.</summary>
    public decimal PriceDifference => NetAmount - ReceivedValue;

    internal static SupplierInvoiceLine Create(CompanyId companyId, Guid invoiceId, int lineNumber, SupplierInvoiceLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Quantity <= 0m || input.UnitPrice < 0m)
        {
            throw new ArgumentException("A line needs a positive quantity and a non-negative price.", nameof(input));
        }

        return new SupplierInvoiceLine(Guid.CreateVersion7(), companyId)
        {
            InvoiceId = invoiceId,
            LineNumber = lineNumber,
            ReceiptId = input.ReceiptId,
            ReceiptLineId = input.ReceiptLineId,
            OrderLineId = input.OrderLineId,
            ArticleId = input.ArticleId,
            Quantity = input.Quantity,
            UnitPrice = input.UnitPrice,
            ReceivedUnitCost = input.ReceivedUnitCost,
            VatCode = Text.Required(input.VatCode),
        };
    }

    internal SupplierInvoiceLine CopyForStorno(Guid stornoId) =>
        new(Guid.CreateVersion7(), CompanyId)
        {
            InvoiceId = stornoId,
            LineNumber = LineNumber,
            ReceiptId = ReceiptId,
            ReceiptLineId = ReceiptLineId,
            OrderLineId = OrderLineId,
            ArticleId = ArticleId,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            ReceivedUnitCost = ReceivedUnitCost,
            VatCode = VatCode,
            VatRate = VatRate,
            VatAmount = VatAmount,
        };

    internal void RecordVat(VatAmounts vat)
    {
        if (vat.VatCode != VatCode || vat.Base != NetAmount)
        {
            throw new ArgumentException("The PDV was computed for another line.", nameof(vat));
        }

        VatRate = vat.Rate;
        VatAmount = vat.VatAmount;
    }
}

public sealed class SupplierInvoiceVatTotal : Entity<Guid>
{
    private SupplierInvoiceVatTotal(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private SupplierInvoiceVatTotal()
    {
    }

    public Guid InvoiceId { get; private set; }

    public string VatCode { get; private set; } = null!;

    public decimal Rate { get; private set; }

    public decimal Base { get; private set; }

    public decimal VatAmount { get; private set; }

    internal static SupplierInvoiceVatTotal Create(CompanyId companyId, Guid invoiceId, VatAmounts total) =>
        new(Guid.CreateVersion7(), companyId)
        {
            InvoiceId = invoiceId,
            VatCode = total.VatCode,
            Rate = total.Rate,
            Base = total.Base,
            VatAmount = total.VatAmount,
        };
}
