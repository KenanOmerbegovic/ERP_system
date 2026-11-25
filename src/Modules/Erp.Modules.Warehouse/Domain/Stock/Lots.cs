using Erp.SharedKernel.Domain;

namespace Erp.Modules.Warehouse.Domain.Stock;

/// <summary>A LOT (batch) of a LOT tracked article. The number is unique per article; the expiry date is set by its first receipt.</summary>
public sealed class Lot : Entity<Guid>
{
    private Lot(Guid id, CompanyId companyId, Guid articleId, string number, DateOnly? expiryDate)
        : base(id, companyId)
    {
        ArticleId = articleId;
        Number = Text.Required(number);
        ExpiryDate = expiryDate;
    }

    private Lot()
    {
    }

    public Guid ArticleId { get; private set; }

    public string Number { get; private set; } = null!;

    public DateOnly? ExpiryDate { get; private set; }

    public static Lot Create(CompanyId companyId, Guid articleId, string number, DateOnly? expiryDate) =>
        new(Guid.CreateVersion7(), companyId, articleId, number, expiryDate);
}

/// <summary>Quantity of one LOT in one warehouse; its value is part of the warehouse's <see cref="StockItem"/>.</summary>
public sealed class LotBalance : Entity<Guid>
{
    private LotBalance(Guid id, CompanyId companyId, Guid storehouseId, Guid articleId, Guid lotId)
        : base(id, companyId)
    {
        StorehouseId = storehouseId;
        ArticleId = articleId;
        LotId = lotId;
    }

    private LotBalance()
    {
    }

    public Guid StorehouseId { get; private set; }

    public Guid ArticleId { get; private set; }

    public Guid LotId { get; private set; }

    /// <summary>Never negative (also a database check).</summary>
    public decimal Quantity { get; private set; }

    public static LotBalance Create(CompanyId companyId, Guid storehouseId, Guid articleId, Guid lotId) =>
        new(Guid.CreateVersion7(), companyId, storehouseId, articleId, lotId);

    public void Add(decimal quantity) => Quantity += quantity > 0m
        ? quantity
        : throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");

    public void Remove(decimal quantity)
    {
        if (quantity <= 0m || quantity > Quantity)
        {
            throw new InvalidOperationException($"Only {Quantity} of the LOT in stock; {quantity} requested.");
        }

        Quantity -= quantity;
    }
}

/// <summary>One piece of a serial tracked article. The number is unique per article; a piece is in at most one warehouse.</summary>
public sealed class SerialNumber : Entity<Guid>
{
    private SerialNumber(Guid id, CompanyId companyId, Guid articleId, string number)
        : base(id, companyId)
    {
        ArticleId = articleId;
        Number = Text.Required(number);
    }

    private SerialNumber()
    {
    }

    public Guid ArticleId { get; private set; }

    public string Number { get; private set; } = null!;

    /// <summary>The warehouse holding the piece; null when it is not in stock (never received, or issued).</summary>
    public Guid? StorehouseId { get; private set; }

    public bool InStock => StorehouseId is not null;

    public static SerialNumber Create(CompanyId companyId, Guid articleId, string number) =>
        new(Guid.CreateVersion7(), companyId, articleId, number);

    public bool IsIn(Guid storehouseId) => StorehouseId == storehouseId;

    public void MoveIn(Guid storehouseId)
    {
        if (InStock)
        {
            throw new InvalidOperationException($"Serial number {Number} is already in stock.");
        }

        StorehouseId = storehouseId;
    }

    public void MoveOut(Guid storehouseId)
    {
        if (!IsIn(storehouseId))
        {
            throw new InvalidOperationException($"Serial number {Number} is not in this warehouse.");
        }

        StorehouseId = null;
    }
}

/// <summary>
/// The stock ledger: one row per warehouse a document line moved goods in or out of, signed (+ in, − out).
/// Append-only. A <see cref="StockItem"/> always equals the sum of its movements.
/// </summary>
public sealed class StockMovement : Entity<Guid>
{
    private StockMovement(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private StockMovement()
    {
    }

    public Guid DocumentId { get; private set; }

    public Guid DocumentLineId { get; private set; }

    public DateOnly DocumentDate { get; private set; }

    public Guid StorehouseId { get; private set; }

    public Guid ArticleId { get; private set; }

    public Guid? LotId { get; private set; }

    public decimal Quantity { get; private set; }

    /// <summary>Change of the stock value in KM.</summary>
    public decimal Value { get; private set; }

    public DateTimeOffset PostedAt { get; private set; }

    public static StockMovement Create(
        CompanyId companyId,
        Guid documentId,
        Guid documentLineId,
        DateOnly documentDate,
        Guid storehouseId,
        Guid articleId,
        Guid? lotId,
        decimal quantity,
        decimal value,
        DateTimeOffset postedAt) =>
        new(Guid.CreateVersion7(), companyId)
        {
            DocumentId = documentId,
            DocumentLineId = documentLineId,
            DocumentDate = documentDate,
            StorehouseId = storehouseId,
            ArticleId = articleId,
            LotId = lotId,
            Quantity = quantity,
            Value = value,
            PostedAt = postedAt,
        };
}
