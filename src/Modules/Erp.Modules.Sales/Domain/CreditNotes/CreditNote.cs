using Erp.SharedKernel.Domain;

namespace Erp.Modules.Sales.Domain.CreditNotes;

public enum CreditKind
{
    /// <summary>Goods come back into stock at the cost they left at; the customer is credited at the invoice price.</summary>
    Return,

    /// <summary>Only the value goes down (a complaint, an agreed discount after the fact); no stock moves.</summary>
    ValueReduction,
}

/// <summary>
/// A credit note line as the handler resolved it from an invoice line. For a return, <see cref="NetAmount"/> is
/// quantity × the invoice price and <see cref="ReturnUnitCost"/> the delivery's cost per unit.
/// </summary>
public sealed record CreditNoteLineInput(
    Guid InvoiceLineId,
    Guid ArticleId,
    CreditKind Kind,
    decimal Quantity,
    decimal UnitPrice,
    decimal NetAmount,
    string VatCode,
    string? LotNumber,
    IReadOnlyList<string> SerialNumbers,
    decimal ReturnUnitCost);

/// <summary>
/// A credit note to a customer (FR-SAL-05) that corrects one confirmed invoice: lines return goods or reduce the value.
/// PDV is computed at the rates of the invoice's date (<see cref="VatDate"/>), since it corrects that invoice. A credit
/// note without returns is confirmed in one step; one with returns goes through Confirming around the Warehouse
/// receipt, like a delivery note around its issue. A storno reverses it (and takes returned goods out again).
/// </summary>
public sealed class CreditNote : AggregateRoot<Guid>
{
    private readonly List<CreditNoteLine> _lines = [];
    private readonly List<CreditNoteVatTotal> _vatTotals = [];

    private CreditNote(Guid id, CompanyId companyId, Guid invoiceId, Guid orderId, Guid customerId, DateOnly vatDate, DateTimeOffset createdAt)
        : base(id, companyId)
    {
        InvoiceId = invoiceId;
        SalesOrderId = orderId;
        CustomerId = customerId;
        VatDate = vatDate;
        Status = DocumentStatus.Draft;
        CreatedAt = createdAt;
    }

    private CreditNote()
    {
    }

    public DocumentStatus Status { get; private set; }

    public string? Number { get; private set; }

    public Guid InvoiceId { get; private set; }

    public Guid SalesOrderId { get; private set; }

    public Guid CustomerId { get; private set; }

    public DateOnly CreditNoteDate { get; private set; }

    /// <summary>The invoice's date: the PDV rates of the corrected invoice apply.</summary>
    public DateOnly VatDate { get; private set; }

    /// <summary>Where returned goods come back into; null when nothing is returned.</summary>
    public Guid? WarehouseId { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The warehouse receipt of the returned goods (or, for a storno, its reversal).</summary>
    public Guid? StockDocumentId { get; private set; }

    public string? StockDocumentNumber { get; private set; }

    public Guid? ReversesCreditNoteId { get; private set; }

    public Guid? ReversedByCreditNoteId { get; private set; }

    public decimal NetAmount { get; private set; }

    public decimal VatAmount { get; private set; }

    public decimal GrossAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public IReadOnlyList<CreditNoteLine> Lines => _lines.AsReadOnly();

    public IReadOnlyList<CreditNoteVatTotal> VatTotals => _vatTotals.AsReadOnly();

    public bool IsDraft => Status == DocumentStatus.Draft;

    public bool IsStorno => ReversesCreditNoteId is not null;

    /// <summary>Goods come back (or, for a storno, go out again) through the warehouse.</summary>
    public bool HasReturns => _lines.Any(line => line.Kind == CreditKind.Return);

    /// <summary>What the returned goods re-entered stock at, once confirmed.</summary>
    public decimal? ReturnedCostValue => Status == DocumentStatus.Confirmed ? _lines.Sum(line => line.ReturnedCostValue ?? 0m) : null;

    public bool CanBeReversed => Status == DocumentStatus.Confirmed && !IsStorno && ReversedByCreditNoteId is null;

    /// <summary>The lines that return goods, in line order: the warehouse receipt's lines.</summary>
    public IReadOnlyList<CreditNoteLine> ReturnLines => _lines.Where(line => line.Kind == CreditKind.Return).OrderBy(line => line.LineNumber).ToList();

    public static CreditNote CreateDraft(
        CompanyId companyId,
        Guid invoiceId,
        Guid orderId,
        Guid customerId,
        DateOnly vatDate,
        DateOnly creditNoteDate,
        Guid? warehouseId,
        string? note,
        IReadOnlyList<CreditNoteLineInput> lines,
        DateTimeOffset createdAt)
    {
        var creditNote = new CreditNote(Guid.CreateVersion7(), companyId, invoiceId, orderId, customerId, vatDate, createdAt);
        creditNote.SetContent(creditNoteDate, warehouseId, note, lines);
        return creditNote;
    }

    public void UpdateDraft(DateOnly creditNoteDate, Guid? warehouseId, string? note, IReadOnlyList<CreditNoteLineInput> lines)
    {
        EnsureStatus(DocumentStatus.Draft);
        SetContent(creditNoteDate, warehouseId, note, lines);
    }

    public void ApplyVat(IReadOnlyDictionary<int, VatAmounts> lineVat, IReadOnlyList<VatAmounts> totals)
    {
        EnsureStatus(DocumentStatus.Draft);
        ArgumentNullException.ThrowIfNull(lineVat);
        ArgumentNullException.ThrowIfNull(totals);
        foreach (var line in _lines)
        {
            line.RecordVat(lineVat[line.LineNumber]);
        }

        _vatTotals.Clear();
        _vatTotals.AddRange(totals.Select(total => CreditNoteVatTotal.Create(CompanyId, Id, total)));
        NetAmount = _lines.Sum(line => line.NetAmount);
        VatAmount = _lines.Sum(line => line.VatAmount);
        GrossAmount = NetAmount + VatAmount;
    }

    /// <summary>Confirms a credit note without returns in one step.</summary>
    public void Confirm(string number, DateTimeOffset confirmedAt)
    {
        EnsureStatus(DocumentStatus.Draft);
        if (HasReturns)
        {
            throw new InvalidOperationException("A credit note with returns is confirmed through the warehouse.");
        }

        Number = Text.Required(number);
        Status = DocumentStatus.Confirmed;
        ConfirmedAt = confirmedAt;
    }

    public void BeginConfirmation()
    {
        EnsureStatus(DocumentStatus.Draft);
        if (!HasReturns)
        {
            throw new InvalidOperationException("Only a credit note with returns waits for the warehouse.");
        }

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

    /// <summary>The warehouse took the returns (<paramref name="returnValues"/>: one per return line, in line order).</summary>
    public void CompleteConfirmation(string number, Guid stockDocumentId, string stockDocumentNumber, IReadOnlyList<decimal> returnValues, DateTimeOffset confirmedAt)
    {
        EnsureStatus(DocumentStatus.Confirming);
        ArgumentNullException.ThrowIfNull(returnValues);
        var returns = ReturnLines;
        if (returnValues.Count != returns.Count)
        {
            throw new ArgumentException("One value per return line.", nameof(returnValues));
        }

        for (var index = 0; index < returns.Count; index++)
        {
            returns[index].RecordReturnedCost(returnValues[index]);
        }

        Number = Text.Required(number);
        StockDocumentId = stockDocumentId;
        StockDocumentNumber = Text.Required(stockDocumentNumber);
        Status = DocumentStatus.Confirmed;
        ConfirmedAt = confirmedAt;
    }

    /// <summary>
    /// A storno with the same lines and amounts. With returns it starts in the Confirming state (the warehouse takes the
    /// goods out again); without, it is a draft the caller confirms at once. Marks this credit note reversed.
    /// </summary>
    public CreditNote CreateStorno(DateOnly date, DateTimeOffset createdAt)
    {
        if (!CanBeReversed)
        {
            throw new InvalidOperationException("Only a confirmed credit note that is not a storno and not yet reversed can be reversed.");
        }

        var storno = new CreditNote(Guid.CreateVersion7(), CompanyId, InvoiceId, SalesOrderId, CustomerId, VatDate, createdAt)
        {
            CreditNoteDate = date < CreditNoteDate ? CreditNoteDate : date,
            WarehouseId = WarehouseId,
            Note = Number is null ? null : $"Storno {Number}",
            ReversesCreditNoteId = Id,
            NetAmount = NetAmount,
            VatAmount = VatAmount,
            GrossAmount = GrossAmount,
        };
        foreach (var line in _lines)
        {
            storno._lines.Add(line.CopyForStorno(storno.Id));
        }

        storno._vatTotals.AddRange(_vatTotals.Select(total => CreditNoteVatTotal.Create(CompanyId, storno.Id,
            new VatAmounts(total.VatCode, total.Rate, total.Base, total.VatAmount))));
        if (storno.HasReturns)
        {
            storno.Status = DocumentStatus.Confirming;
        }

        ReversedByCreditNoteId = storno.Id;
        return storno;
    }

    /// <summary>The warehouse refused the reversal; the storno is removed and this credit note is no longer reversed.</summary>
    public void CancelReversal(Guid stornoId)
    {
        if (ReversedByCreditNoteId != stornoId)
        {
            throw new InvalidOperationException("That storno does not reverse this credit note.");
        }

        ReversedByCreditNoteId = null;
    }

    private void SetContent(DateOnly creditNoteDate, Guid? warehouseId, string? note, IReadOnlyList<CreditNoteLineInput> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ArgumentException("A credit note needs at least one line.", nameof(lines));
        }

        var hasReturns = lines.Any(line => line.Kind == CreditKind.Return);
        if (hasReturns && (warehouseId is null || warehouseId == Guid.Empty))
        {
            throw new ArgumentException("Returned goods need a warehouse.", nameof(warehouseId));
        }

        CreditNoteDate = creditNoteDate;
        WarehouseId = hasReturns ? warehouseId : null;
        Note = Text.Optional(note);
        _lines.Clear();
        _vatTotals.Clear();
        for (var index = 0; index < lines.Count; index++)
        {
            _lines.Add(CreditNoteLine.Create(CompanyId, Id, index + 1, lines[index]));
        }
    }

    private void EnsureStatus(DocumentStatus status)
    {
        if (Status != status)
        {
            throw new InvalidOperationException($"The credit note is {Status}, not {status}.");
        }
    }
}

public sealed class CreditNoteLine : Entity<Guid>
{
    // Not readonly: EF Core sets it when it loads the line (column serial_numbers, text[]).
    private List<string> _serialNumbers = [];

    private CreditNoteLine(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private CreditNoteLine()
    {
    }

    public Guid CreditNoteId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid InvoiceLineId { get; private set; }

    public Guid ArticleId { get; private set; }

    public CreditKind Kind { get; private set; }

    /// <summary>Pieces returned; 0 for a value reduction.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>The invoice price for a return; 0 for a value reduction.</summary>
    public decimal UnitPrice { get; private set; }

    public decimal NetAmount { get; private set; }

    public string VatCode { get; private set; } = null!;

    public decimal VatRate { get; private set; }

    public decimal VatAmount { get; private set; }

    /// <summary>The delivered LOT, for returned LOT-tracked goods.</summary>
    public string? LotNumber { get; private set; }

    public IReadOnlyList<string> SerialNumbers => _serialNumbers.AsReadOnly();

    /// <summary>The delivery's cost per unit: what returned goods come back at.</summary>
    public decimal ReturnUnitCost { get; private set; }

    /// <summary>What the warehouse took the returned goods back at (KM), once confirmed.</summary>
    public decimal? ReturnedCostValue { get; private set; }

    internal static CreditNoteLine Create(CompanyId companyId, Guid creditNoteId, int lineNumber, CreditNoteLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var isReturn = input.Kind == CreditKind.Return;
        if (isReturn ? input.Quantity <= 0m || input.NetAmount != Amounts.LineAmount(input.Quantity, input.UnitPrice) : input.Quantity != 0m || input.NetAmount <= 0m)
        {
            throw new ArgumentException("A return needs a positive quantity at the invoice price; a value reduction a positive amount.", nameof(input));
        }

        var line = new CreditNoteLine(Guid.CreateVersion7(), companyId)
        {
            CreditNoteId = creditNoteId,
            LineNumber = lineNumber,
            InvoiceLineId = input.InvoiceLineId,
            ArticleId = input.ArticleId,
            Kind = input.Kind,
            Quantity = input.Quantity,
            UnitPrice = isReturn ? input.UnitPrice : 0m,
            NetAmount = Amounts.Km(input.NetAmount),
            VatCode = Text.Required(input.VatCode),
            LotNumber = isReturn ? Text.Optional(input.LotNumber) : null,
            ReturnUnitCost = isReturn ? input.ReturnUnitCost : 0m,
        };
        if (isReturn)
        {
            line._serialNumbers.AddRange(input.SerialNumbers.Select(serial => Text.Required(serial)));
        }

        return line;
    }

    internal CreditNoteLine CopyForStorno(Guid stornoId)
    {
        var copy = new CreditNoteLine(Guid.CreateVersion7(), CompanyId)
        {
            CreditNoteId = stornoId,
            LineNumber = LineNumber,
            InvoiceLineId = InvoiceLineId,
            ArticleId = ArticleId,
            Kind = Kind,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            NetAmount = NetAmount,
            VatCode = VatCode,
            VatRate = VatRate,
            VatAmount = VatAmount,
            LotNumber = LotNumber,
            ReturnUnitCost = ReturnUnitCost,
        };
        copy._serialNumbers.AddRange(_serialNumbers);
        return copy;
    }

    internal void RecordVat(VatAmounts vat)
    {
        if (vat.VatCode != VatCode || vat.Base != NetAmount)
        {
            throw new ArgumentException("The PDV was computed for another line.", nameof(vat));
        }

        VatRate = vat.Rate;
        VatAmount = vat.VatAmount;
    }

    internal void RecordReturnedCost(decimal value) => ReturnedCostValue = value;
}

public sealed class CreditNoteVatTotal : Entity<Guid>
{
    private CreditNoteVatTotal(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private CreditNoteVatTotal()
    {
    }

    public Guid CreditNoteId { get; private set; }

    public string VatCode { get; private set; } = null!;

    public decimal Rate { get; private set; }

    public decimal Base { get; private set; }

    public decimal VatAmount { get; private set; }

    internal static CreditNoteVatTotal Create(CompanyId companyId, Guid creditNoteId, VatAmounts total) =>
        new(Guid.CreateVersion7(), companyId)
        {
            CreditNoteId = creditNoteId,
            VatCode = total.VatCode,
            Rate = total.Rate,
            Base = total.Base,
            VatAmount = total.VatAmount,
        };
}
