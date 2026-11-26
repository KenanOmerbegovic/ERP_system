using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Orders;

public interface ISalesOrderDraft
{
    CompanyId CompanyId { get; }

    Guid CustomerId { get; }

    DateOnly OrderDate { get; }

    Guid? WarehouseId { get; }

    string? Note { get; }

    IReadOnlyList<SalesDraftLine> Lines { get; }
}

internal sealed class SalesOrderDraftValidator : AbstractValidator<ISalesOrderDraft>
{
    public SalesOrderDraftValidator(IClock clock)
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.OrderDate).Must(date => date <= clock.Today).WithErrorCode(SalesValidationCodes.DateInFuture);
        RuleFor(x => x.WarehouseId).NotEqual(Guid.Empty).When(x => x.WarehouseId is not null);
        RuleFor(x => x.Note).MaximumLength(FieldLengths.Note);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= Limits.MaxLines).WithErrorCode("Lines.TooMany");
        RuleForEach(x => x.Lines).SetValidator(new SalesDraftLineValidator());
    }
}

internal static class OrderLookup
{
    /// <summary>The company's order, for a delivery note or invoice draft: a validation error if missing, a conflict if not open.</summary>
    public static async Task<Result<SalesOrder>> OpenOrderAsync(
        ISalesDbContext db, CompanyId companyId, Guid orderId, bool tracked, CancellationToken cancellationToken)
    {
        var orders = tracked ? db.SalesOrders : db.SalesOrders.AsNoTracking();
        var order = await orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.CompanyId == companyId && o.Id == orderId, cancellationToken);
        if (order is null)
        {
            var errors = new ValidationErrors();
            errors.Add("SalesOrderId", "SalesOrder.NotFound");
            return errors.ToError();
        }

        return order.IsOpen ? order : SalesErrors.OrderNotOpen;
    }
}
