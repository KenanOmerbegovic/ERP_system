using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Modules.Sales.Domain.Orders;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;

namespace Erp.Modules.Sales.Application.Orders;

internal static class OrderConfirmation
{
    public static async Task<Result> ConfirmAsync(ISalesDbContext db, ISalesLocks locks, IClock clock, SalesOrder order, CancellationToken cancellationToken)
    {
        var number = await DocumentNumbers.NextAsync(db, locks, order.CompanyId, SalesDocumentType.SalesOrder, order.OrderDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error;
        }

        order.Confirm(number.Value, clock.UtcNow);
        db.AddIntegrationEvent(SalesEvents.OrderConfirmed(order, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
