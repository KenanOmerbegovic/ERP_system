using Erp.SharedKernel.Domain;

namespace Erp.Modules.Sales.Domain.Offers;

public enum OfferStatus
{
    Draft,
    Sent,
    Accepted,
    Rejected,
}

/// <summary>A goods or service line of an offer or order, as the handler resolved it (VAT code, article kind).</summary>
public sealed record SalesLineInput(Guid ArticleId, bool IsService, decimal Quantity, decimal UnitPrice, string VatCode);

/// <summary>
/// An offer to a customer (FR-SAL-01). A draft can be edited or deleted; sending numbers it and freezes it, since the
/// customer has seen it. A sent offer is accepted (FR-SAL-02: it becomes a draft sales order, at most one) or rejected.
/// An offer is not a posting document, so it has no storno.
/// </summary>
public sealed class Offer : AggregateRoot<Guid>
{
    private readonly List<OfferLine> _lines = [];

    private Offer(Guid id, CompanyId companyId, DateTimeOffset createdAt)
        : base(id, companyId)
    {
        Status = OfferStatus.Draft;
        CreatedAt = createdAt;
    }

    private Offer()
    {
    }

    public OfferStatus Status { get; private set; }

    public string? Number { get; private set; }

    public Guid CustomerId { get; private set; }

    public DateOnly OfferDate { get; private set; }

    /// <summary>Informational: printed on the offer; accepting later is still allowed.</summary>
    public DateOnly? ValidUntil { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The sales order an accepted offer became.</summary>
    public Guid? SalesOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public IReadOnlyList<OfferLine> Lines => _lines.AsReadOnly();

    public bool IsDraft => Status == OfferStatus.Draft;

    public decimal NetAmount => _lines.Sum(line => line.NetAmount);

    public static Offer CreateDraft(
        CompanyId companyId, Guid customerId, DateOnly offerDate, DateOnly? validUntil, string? note, IReadOnlyList<SalesLineInput> lines, DateTimeOffset createdAt)
    {
        var offer = new Offer(Guid.CreateVersion7(), companyId, createdAt);
        offer.SetContent(customerId, offerDate, validUntil, note, lines);
        return offer;
    }

    public void UpdateDraft(Guid customerId, DateOnly offerDate, DateOnly? validUntil, string? note, IReadOnlyList<SalesLineInput> lines)
    {
        EnsureStatus(OfferStatus.Draft);
        SetContent(customerId, offerDate, validUntil, note, lines);
    }

    public void Send(string number, DateTimeOffset sentAt)
    {
        EnsureStatus(OfferStatus.Draft);
        Number = Text.Required(number);
        Status = OfferStatus.Sent;
        SentAt = sentAt;
    }

    public void Accept(Guid salesOrderId, DateTimeOffset decidedAt)
    {
        EnsureStatus(OfferStatus.Sent);
        if (salesOrderId == Guid.Empty)
        {
            throw new ArgumentException("An accepted offer becomes a sales order.", nameof(salesOrderId));
        }

        SalesOrderId = salesOrderId;
        Status = OfferStatus.Accepted;
        DecidedAt = decidedAt;
    }

    /// <summary>The draft order the acceptance created was deleted: the offer is open (sent) again.</summary>
    public void CancelAcceptance(Guid salesOrderId)
    {
        EnsureStatus(OfferStatus.Accepted);
        if (SalesOrderId != salesOrderId)
        {
            throw new InvalidOperationException("That order was not made from this offer.");
        }

        SalesOrderId = null;
        Status = OfferStatus.Sent;
        DecidedAt = null;
    }

    public void Reject(DateTimeOffset decidedAt)
    {
        EnsureStatus(OfferStatus.Sent);
        Status = OfferStatus.Rejected;
        DecidedAt = decidedAt;
    }

    private void SetContent(Guid customerId, DateOnly offerDate, DateOnly? validUntil, string? note, IReadOnlyList<SalesLineInput> lines)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("An offer needs a customer.", nameof(customerId));
        }

        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ArgumentException("An offer needs at least one line.", nameof(lines));
        }

        if (validUntil < offerDate)
        {
            throw new ArgumentException("An offer cannot expire before its date.", nameof(validUntil));
        }

        CustomerId = customerId;
        OfferDate = offerDate;
        ValidUntil = validUntil;
        Note = Text.Optional(note);
        _lines.Clear();
        for (var index = 0; index < lines.Count; index++)
        {
            _lines.Add(OfferLine.Create(CompanyId, Id, index + 1, lines[index]));
        }
    }

    private void EnsureStatus(OfferStatus status)
    {
        if (Status != status)
        {
            throw new InvalidOperationException($"The offer is {Status}, not {status}.");
        }
    }
}

public sealed class OfferLine : Entity<Guid>
{
    private OfferLine(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private OfferLine()
    {
    }

    public Guid OfferId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid ArticleId { get; private set; }

    public bool IsService { get; private set; }

    public decimal Quantity { get; private set; }

    /// <summary>KM per unit, without PDV.</summary>
    public decimal UnitPrice { get; private set; }

    public string VatCode { get; private set; } = null!;

    public decimal NetAmount => Amounts.LineAmount(Quantity, UnitPrice);

    public SalesLineInput ToInput() => new(ArticleId, IsService, Quantity, UnitPrice, VatCode);

    internal static OfferLine Create(CompanyId companyId, Guid offerId, int lineNumber, SalesLineInput input)
    {
        SalesLines.EnsureValid(input);
        return new OfferLine(Guid.CreateVersion7(), companyId)
        {
            OfferId = offerId,
            LineNumber = lineNumber,
            ArticleId = input.ArticleId,
            IsService = input.IsService,
            Quantity = input.Quantity,
            UnitPrice = input.UnitPrice,
            VatCode = Text.Required(input.VatCode),
        };
    }
}

internal static class SalesLines
{
    public static void EnsureValid(SalesLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ArticleId == Guid.Empty || input.Quantity <= 0m || input.UnitPrice < 0m)
        {
            throw new ArgumentException("A line needs an article, a positive quantity and a non-negative price.", nameof(input));
        }
    }
}
