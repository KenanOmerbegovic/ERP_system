using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.BusinessPartners;

public sealed record UpdateBusinessPartner(
    CompanyId CompanyId,
    Guid BusinessPartnerId,
    string Code,
    string Name,
    string? TaxId,
    string? VatNumber,
    string? Address,
    string? PostalCode,
    string? City,
    string? CountryCode,
    bool IsCustomer,
    bool IsSupplier,
    bool IsActive) : IBusinessPartnerFields;

internal sealed class UpdateBusinessPartnerValidator : AbstractValidator<UpdateBusinessPartner>
{
    public UpdateBusinessPartnerValidator(ITaxIdentifierRules taxRules)
    {
        Include(new BusinessPartnerFieldsValidator(taxRules));
    }
}

public sealed class UpdateBusinessPartnerHandler(
    ICoreDbContext db,
    IValidator<UpdateBusinessPartner> validator,
    ITaxIdentifierRules taxRules)
{
    public async Task<Result> HandleAsync(UpdateBusinessPartner command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var partner = await db.BusinessPartners.FirstOrDefaultAsync(
            p => p.CompanyId == command.CompanyId && p.Id == command.BusinessPartnerId,
            cancellationToken);
        if (partner is null)
        {
            return CoreErrors.BusinessPartnerNotFound;
        }

        var code = command.Code.Trim();
        if (await db.BusinessPartners.AnyAsync(
                p => p.CompanyId == command.CompanyId && p.Code == code && p.Id != command.BusinessPartnerId,
                cancellationToken))
        {
            return CoreErrors.DuplicateBusinessPartnerCode;
        }

        partner.Update(command.ToDetails(taxRules));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
