using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Time;
using FluentValidation;

namespace Erp.Modules.Sales.Application.Offers;

public interface IOfferDraft
{
    CompanyId CompanyId { get; }

    Guid CustomerId { get; }

    DateOnly OfferDate { get; }

    DateOnly? ValidUntil { get; }

    string? Note { get; }

    IReadOnlyList<SalesDraftLine> Lines { get; }
}

internal sealed class OfferDraftValidator : AbstractValidator<IOfferDraft>
{
    public OfferDraftValidator(IClock clock)
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.OfferDate).Must(date => date <= clock.Today).WithErrorCode(SalesValidationCodes.DateInFuture);
        RuleFor(x => x.ValidUntil).GreaterThanOrEqualTo(x => x.OfferDate).When(x => x.ValidUntil is not null)
            .WithErrorCode(SalesValidationCodes.ValidUntilBeforeOfferDate);
        RuleFor(x => x.Note).MaximumLength(FieldLengths.Note);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= Limits.MaxLines).WithErrorCode("Lines.TooMany");
        RuleForEach(x => x.Lines).SetValidator(new SalesDraftLineValidator());
    }
}
