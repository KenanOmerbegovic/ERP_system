using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Application.Numbering;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Orders;

internal static class OrderConfirmation
{
    public static async Task<Result> ConfirmAsync(IPurchasingDbContext db, IPurchasingLocks locks, IClock clock, PurchaseOrder order, CancellationToken cancellationToken)
    {
        var number = await DocumentNumbers.NextAsync(db, locks, order.CompanyId, PurchasingDocumentType.PurchaseOrder, order.OrderDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error;
        }

        order.Confirm(number.Value, clock.UtcNow);
        db.AddIntegrationEvent(PurchasingEvents.OrderConfirmed(order, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
