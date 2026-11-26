using Erp.SharedKernel.Domain;

namespace Erp.Modules.Sales.Domain.Invoices;

/// <summary>
/// An invoice line as the handler resolved it: goods from a confirmed delivery line, or a service from its order line.
/// The price and VAT code are the order line's.
/// </summary>
public sealed record SalesInvoiceLineInput(
    Guid OrderLineId,
    Guid? DeliveryNoteId,
    Guid? DeliveryLineId,
    Guid ArticleId,
    bool IsService,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode);

/// <summary>
/// An invoice to a customer for one sales order (FR-SAL-03), with output PDV computed by the country's rules for the
/// invoice date (FR-SAL-04). Confirming numbers it and makes it immutable (FR-SAL-05). It is corrected by a storno
/// (all of it, while no credit note was made) or by credit notes (part of it, returns or value reductions).
/// </summary>
public sealed class SalesInvoice : AggregateRoot<Guid>
{
    private readonly List<SalesInvoiceLine> _lines = [];
    private readonly List<SalesInvoiceVatTotal> _vatTotals = [];

    private SalesInvoice(Guid id, CompanyId companyId, Guid orderId, Guid customerId, DateTimeOffset createdAt)
        : base(id, companyId)
    {
        SalesOrderId = orderId;
        CustomerId = customerId;
        Status = DocumentStatus.Draft;
        CreatedAt = createdAt;
    }

    private SalesInvoice()
    {
    }

    public DocumentStatus Status { get; private set; }

    public string? Number { get; private set; }

    public Guid SalesOrderId { get; private set; }

    public Guid CustomerId { get; private set; }

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

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    public IReadOnlyList<SalesInvoiceVatTotal> VatTotals => _vatTotals.AsReadOnly();

    public bool IsDraft => Status == DocumentStatus.Draft;

    public bool IsStorno => ReversesInvoiceId is not null;

    /// <summary>Confirmed, not a storno and not reversed: credit notes can be made against it.</summary>
    public bool IsOpen => Status == DocumentStatus.Confirmed && !IsStorno && ReversedByInvoiceId is null;

    /// <summary>Net amount confirmed credit notes took off.</summary>
    public decimal CreditedAmount => _lines.Sum(line => line.CreditedAmount);

    /// <summary>A storno undoes the whole invoice, so it is only possible before any credit note.</summary>
    public bool CanBeReversed => IsOpen && _lines.All(line => line.CreditedAmount == 0m && line.ReturnedQuantity == 0m);

    public static SalesInvoice CreateDraft(
        CompanyId companyId,
        Guid orderId,
        Guid customerId,
        DateOnly invoiceDate,
        DateOnly dueDate,
        string? note,
        IReadOnlyList<SalesInvoiceLineInput> lines,
        DateTimeOffset createdAt)
    {
        var invoice = new SalesInvoice(Guid.CreateVersion7(), companyId, orderId, customerId, createdAt);
        invoice.SetContent(invoiceDate, dueDate, note, lines);
        return invoice;
    }

    public void UpdateDraft(DateOnly invoiceDate, DateOnly dueDate, string? note, IReadOnlyList<SalesInvoiceLineInput> lines)
    {
        EnsureDraft();
        SetContent(invoiceDate, dueDate, note, lines);
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
        _vatTotals.AddRange(totals.Select(total => SalesInvoiceVatTotal.Create(CompanyId, Id, total)));
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
    public SalesInvoice CreateStorno(DateOnly date, DateTimeOffset createdAt)
    {
        if (!CanBeReversed)
        {
            throw new InvalidOperationException("Only a confirmed invoice without credit notes that is not a storno and not yet reversed can be reversed.");
        }

        var storno = new SalesInvoice(Guid.CreateVersion7(), CompanyId, SalesOrderId, CustomerId, createdAt)
        {
            InvoiceDate = date < InvoiceDate ? InvoiceDate : date,
            DueDate = DueDate < date ? date : DueDate,
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

        storno._vatTotals.AddRange(_vatTotals.Select(total => SalesInvoiceVatTotal.Create(CompanyId, storno.Id,
            new VatAmounts(total.VatCode, total.Rate, total.Base, total.VatAmount))));
        ReversedByInvoiceId = storno.Id;
        return storno;
    }

    public SalesInvoiceLine Line(Guid lineId) =>
        _lines.SingleOrDefault(line => line.Id == lineId) ?? throw new InvalidOperationException($"Invoice line {lineId} is not on invoice {Id}.");

    private void SetContent(DateOnly invoiceDate, DateOnly dueDate, string? note, IReadOnlyList<SalesInvoiceLineInput> lines)
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

        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        Note = Text.Optional(note);
        _lines.Clear();
        _vatTotals.Clear();
        for (var index = 0; index < lines.Count; index++)
        {
            _lines.Add(SalesInvoiceLine.Create(CompanyId, Id, index + 1, lines[index]));
        }
    }

    private void EnsureDraft()
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("A confirmed invoice is immutable; correct it with a storno or a credit note.");
        }
    }
}

public sealed class SalesInvoiceLine : Entity<Guid>
{
    private SalesInvoiceLine(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private SalesInvoiceLine()
    {
    }

    public Guid InvoiceId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid OrderLineId { get; private set; }

    /// <summary>The delivery a goods line invoices; null for a service.</summary>
    public Guid? DeliveryNoteId { get; private set; }

    public Guid? DeliveryLineId { get; private set; }

    public Guid ArticleId { get; private set; }

    public bool IsService { get; private set; }

    public decimal Quantity { get; private set; }

    /// <summary>KM per unit without PDV: the order line's price.</summary>
    public decimal UnitPrice { get; private set; }

    public string VatCode { get; private set; } = null!;

    public decimal VatRate { get; private set; }

    public decimal VatAmount { get; private set; }

    /// <summary>Net amount confirmed credit notes took off this line (stornos of credit notes subtracted).</summary>
    public decimal CreditedAmount { get; private set; }

    /// <summary>Quantity confirmed credit notes returned to stock.</summary>
    public decimal ReturnedQuantity { get; private set; }

    public decimal NetAmount => Amounts.LineAmount(Quantity, UnitPrice);

    /// <summary>What credit notes may still take off, in KM net.</summary>
    public decimal CreditableAmount => NetAmount - CreditedAmount;

    /// <summary>What credit notes may still return.</summary>
    public decimal ReturnableQuantity => IsService ? 0m : Quantity - ReturnedQuantity;

    internal static SalesInvoiceLine Create(CompanyId companyId, Guid invoiceId, int lineNumber, SalesInvoiceLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Quantity <= 0m || input.UnitPrice < 0m)
        {
            throw new ArgumentException("A line needs a positive quantity and a non-negative price.", nameof(input));
        }

        if (input.IsService != (input.DeliveryLineId is null) || (input.DeliveryLineId is null) != (input.DeliveryNoteId is null))
        {
            throw new ArgumentException("A goods line invoices a delivery line; a service line has none.", nameof(input));
        }

        return new SalesInvoiceLine(Guid.CreateVersion7(), companyId)
        {
            InvoiceId = invoiceId,
            LineNumber = lineNumber,
            OrderLineId = input.OrderLineId,
            DeliveryNoteId = input.DeliveryNoteId,
            DeliveryLineId = input.DeliveryLineId,
            ArticleId = input.ArticleId,
            IsService = input.IsService,
            Quantity = input.Quantity,
            UnitPrice = input.UnitPrice,
            VatCode = Text.Required(input.VatCode),
        };
    }

    internal SalesInvoiceLine CopyForStorno(Guid stornoId) =>
        new(Guid.CreateVersion7(), CompanyId)
        {
            InvoiceId = stornoId,
            LineNumber = LineNumber,
            OrderLineId = OrderLineId,
            DeliveryNoteId = DeliveryNoteId,
            DeliveryLineId = DeliveryLineId,
            ArticleId = ArticleId,
            IsService = IsService,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
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

    /// <summary>A confirmed credit note (positive) or its storno (negative) took an amount, and maybe goods, off this line.</summary>
    public void AddCredit(decimal netAmount, decimal returnedQuantity)
    {
        var credited = CreditedAmount + netAmount;
        var returned = ReturnedQuantity + returnedQuantity;
        if (credited < 0m || credited > NetAmount || returned < 0m || returned > Quantity || (IsService && returned != 0m))
        {
            throw new InvalidOperationException($"Credited {credited} of {NetAmount}, returned {returned} of {Quantity} on invoice line {Id}.");
        }

        CreditedAmount = credited;
        ReturnedQuantity = returned;
    }
}

public sealed class SalesInvoiceVatTotal : Entity<Guid>
{
    private SalesInvoiceVatTotal(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private SalesInvoiceVatTotal()
    {
    }

    public Guid InvoiceId { get; private set; }

    public string VatCode { get; private set; } = null!;

    public decimal Rate { get; private set; }

    public decimal Base { get; private set; }

    public decimal VatAmount { get; private set; }

    internal static SalesInvoiceVatTotal Create(CompanyId companyId, Guid invoiceId, VatAmounts total) =>
        new(Guid.CreateVersion7(), companyId)
        {
            InvoiceId = invoiceId,
            VatCode = total.VatCode,
            Rate = total.Rate,
            Base = total.Base,
            VatAmount = total.VatAmount,
        };
}
