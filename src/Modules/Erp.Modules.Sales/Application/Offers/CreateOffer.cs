using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Domain.Offers;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;

namespace Erp.Modules.Sales.Application.Offers;

/// <summary>Saves a draft offer to a customer (FR-SAL-01).</summary>
public sealed record CreateOffer(
    CompanyId CompanyId,
    Guid CustomerId,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    string? Note,
    IReadOnlyList<SalesDraftLine> Lines) : IOfferDraft;

internal sealed class CreateOfferValidator : AbstractValidator<CreateOffer>
{
    public CreateOfferValidator(IClock clock)
    {
        Include(new OfferDraftValidator(clock));
    }
}

public sealed class CreateOfferHandler(ISalesDbContext db, IVatRateLookup vatRates, IValidator<CreateOffer> validator, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreateOffer command, CancellationToken cancellationToken)
    {
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

        var offer = Offer.CreateDraft(command.CompanyId, command.CustomerId, command.OfferDate, command.ValidUntil, command.Note, lines.Value, clock.UtcNow);
        db.Offers.Add(offer);
        await db.SaveChangesAsync(cancellationToken);
        return offer.Id;
    }
}
