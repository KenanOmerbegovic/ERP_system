using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Offers;

/// <summary>Replaces a draft offer's content. A sent offer cannot be changed: the customer has seen it.</summary>
public sealed record UpdateOffer(
    CompanyId CompanyId,
    Guid OfferId,
    Guid CustomerId,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    string? Note,
    IReadOnlyList<SalesDraftLine> Lines) : IOfferDraft;

internal sealed class UpdateOfferValidator : AbstractValidator<UpdateOffer>
{
    public UpdateOfferValidator(IClock clock)
    {
        Include(new OfferDraftValidator(clock));
    }
}

public sealed class UpdateOfferHandler(ISalesDbContext db, ISalesLocks locks, IVatRateLookup vatRates, IValidator<UpdateOffer> validator)
{
    public async Task<Result> HandleAsync(UpdateOffer command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockOfferAsync(command.CompanyId, command.OfferId, cancellationToken))
        {
            return SalesErrors.OfferNotFound;
        }

        var offer = await db.Offers.Include(o => o.Lines).FirstAsync(o => o.Id == command.OfferId, cancellationToken);
        if (!offer.IsDraft)
        {
            return SalesErrors.OfferNotDraft;
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var lines = await SalesLineResolution.ResolveAsync(db, vatRates, command.CompanyId, command.CustomerId, command.OfferDate, command.Lines, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        offer.UpdateDraft(command.CustomerId, command.OfferDate, command.ValidUntil, command.Note, lines.Value);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
