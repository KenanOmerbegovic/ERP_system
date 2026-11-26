using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Orders;

/// <summary>
/// Confirms a draft order: re-checks the customer, articles and VAT codes, numbers it and makes it immutable (FR-SAL-05)
/// and publishes SalesOrderConfirmed.
/// </summary>
public sealed record ConfirmSalesOrder(CompanyId CompanyId, Guid OrderId);

public sealed class ConfirmSalesOrderHandler(ISalesDbContext db, ISalesLocks locks, IVatRateLookup vatRates, IClock clock)
{
    public async Task<Result> HandleAsync(ConfirmSalesOrder command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockOrderAsync(command.CompanyId, command.OrderId, cancellationToken))
        {
            return SalesErrors.OrderNotFound;
        }

        var order = await db.SalesOrders.Include(o => o.Lines).FirstAsync(o => o.Id == command.OrderId, cancellationToken);
        if (!order.IsDraft)
        {
            return SalesErrors.OrderNotDraft;
        }

        var draftLines = order.Lines.OrderBy(l => l.LineNumber).Select(l => new SalesDraftLine(l.ArticleId, l.Quantity, l.UnitPrice, l.VatCode)).ToList();
        var lines = await SalesLineResolution.ResolveAsync(db, vatRates, order.CompanyId, order.CustomerId, order.OrderDate, draftLines, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        var result = await OrderConfirmation.ConfirmAsync(db, locks, clock, order, cancellationToken);
        if (result.IsFailure)
        {
            return result;
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
