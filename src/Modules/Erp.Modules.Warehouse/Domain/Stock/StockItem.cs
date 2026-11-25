using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Monetary;

namespace Erp.Modules.Warehouse.Domain.Stock;

/// <summary>
/// Stock of one article in one warehouse: quantity and value in KM. The weighted-average cost is value ÷ quantity
/// (one average per article and warehouse, shared by all its LOTs). Values are kept in fening, so the value of the
/// stock is always the sum of its movements; taking the last piece takes the remaining value, so no rounding residue
/// is left behind.
/// </summary>
public sealed class StockItem : Entity<Guid>
{
    private StockItem(Guid id, CompanyId companyId, Guid storehouseId, Guid articleId)
        : base(id, companyId)
    {
        StorehouseId = storehouseId;
        ArticleId = articleId;
    }

    private StockItem()
    {
    }

    public Guid StorehouseId { get; private set; }

    public Guid ArticleId { get; private set; }

    /// <summary>Never negative (also a database check).</summary>
    public decimal Quantity { get; private set; }

    /// <summary>KM; never negative, and zero whenever the quantity is zero.</summary>
    public decimal Value { get; private set; }

    /// <summary>KM per unit, unrounded; zero without stock.</summary>
    public decimal AverageCost => Quantity == 0m ? 0m : Value / Quantity;

    public static StockItem Create(CompanyId companyId, Guid storehouseId, Guid articleId) =>
        new(Guid.CreateVersion7(), companyId, storehouseId, articleId);

    /// <summary>Goods come in at <paramref name="value"/> (KM, rounded): a receipt, the target of a transfer, or a reversed issue.</summary>
    public void Receive(decimal quantity, decimal value)
    {
        EnsurePositive(quantity);
        if (value < 0m || value != RoundToFening(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be non-negative and rounded to fening.");
        }

        Quantity += quantity;
        Value += value;
    }

    /// <summary>Goods go out at the weighted-average cost; returns the value taken (KM).</summary>
    public decimal Issue(decimal quantity)
    {
        EnsureAvailable(quantity);
        var value = quantity == Quantity ? Value : RoundToFening(Value * quantity / Quantity);
        Quantity -= quantity;
        Value -= value;
        return value;
    }

    /// <summary>
    /// Reverses goods that came in at <paramref name="value"/>; returns the value actually taken from stock (KM).
    /// That is <paramref name="value"/> unless the average moved since: the stock never keeps a negative value or
    /// a value without quantity, so the rest (value − returned) is a price difference for Finance.
    /// </summary>
    public decimal RemoveAtValue(decimal quantity, decimal value)
    {
        EnsureAvailable(quantity);
        var removed = quantity == Quantity ? Value : Math.Min(value, Value);
        Quantity -= quantity;
        Value -= removed;
        return removed;
    }

    public bool HasAvailable(decimal quantity) => quantity <= Quantity;

    private static decimal RoundToFening(decimal value) => Money.Km(value).Round().Amount;

    private static void EnsurePositive(decimal quantity)
    {
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
        }
    }

    private void EnsureAvailable(decimal quantity)
    {
        EnsurePositive(quantity);
        if (quantity > Quantity)
        {
            throw new InvalidOperationException($"Only {Quantity} in stock; {quantity} requested. Check availability first.");
        }
    }
}
