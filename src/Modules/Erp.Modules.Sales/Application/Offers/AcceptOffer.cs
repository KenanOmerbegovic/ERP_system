using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Domain.Offers;
using Erp.Modules.Sales.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Offers;

/// <summary>
/// The customer accepted a sent offer (FR-SAL-02): it becomes a draft sales order with the offer's customer, lines and
/// prices, dated <see cref="OrderDate"/>; the user reviews and confirms the order. An offer gives at most one order.
/// Returns the order's id.
/// </summary>
public sealed record AcceptOffer(CompanyId CompanyId, Guid OfferId, DateOnly OrderDate, Guid? WarehouseId);

internal sealed class AcceptOfferValidator : AbstractValidator<AcceptOffer>
{
    public AcceptOfferValidator(IClock clock)
    {
        RuleFor(x => x.OrderDate).Must(date => date <= clock.Today).WithErrorCode(SalesValidationCodes.DateInFuture);
        RuleFor(x => x.WarehouseId).NotEqual(Guid.Empty).When(x => x.WarehouseId is not null);
    }
}

public sealed class AcceptOfferHandler(ISalesDbContext db, ISalesLocks locks, IVatRateLookup vatRates, IValidator<AcceptOffer> validator, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(AcceptOffer command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockOfferAsync(command.CompanyId, command.OfferId, cancellationToken))
        {
            return SalesErrors.OfferNotFound;
        }

        var offer = await db.Offers.Include(o => o.Lines).FirstAsync(o => o.Id == command.OfferId, cancellationToken);
        if (offer.Status != OfferStatus.Sent)
        {
            return SalesErrors.OfferNotSent;
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (command.OrderDate < offer.OfferDate)
        {
            var errors = new ValidationErrors();
            errors.Add(nameof(command.OrderDate), SalesValidationCodes.BeforeOfferDate);
            return errors.ToError();
        }

        // The customer and the VAT codes must still be valid on the order's date.
        var draftLines = offer.Lines.OrderBy(l => l.LineNumber).Select(l => new SalesDraftLine(l.ArticleId, l.Quantity, l.UnitPrice, l.VatCode)).ToList();
        var lines = await SalesLineResolution.ResolveAsync(db, vatRates, offer.CompanyId, offer.CustomerId, command.OrderDate, draftLines, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        var order = SalesOrder.CreateFromOffer(offer, command.OrderDate, command.WarehouseId, clock.UtcNow);
        db.SalesOrders.Add(order);
        offer.Accept(order.Id, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return order.Id;
    }
}
