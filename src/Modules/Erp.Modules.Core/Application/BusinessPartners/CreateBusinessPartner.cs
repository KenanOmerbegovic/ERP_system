using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Domain.BusinessPartners;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.BusinessPartners;

public sealed record CreateBusinessPartner(
    CompanyId CompanyId,
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

internal sealed class CreateBusinessPartnerValidator : AbstractValidator<CreateBusinessPartner>
{
    public CreateBusinessPartnerValidator(ITaxIdentifierRules taxRules)
    {
        Include(new BusinessPartnerFieldsValidator(taxRules));
    }
}

public sealed class CreateBusinessPartnerHandler(
    ICoreDbContext db,
    IValidator<CreateBusinessPartner> validator,
    ITaxIdentifierRules taxRules)
{
    public async Task<Result<Guid>> HandleAsync(CreateBusinessPartner command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (!await db.CompanyExistsAsync(command.CompanyId, cancellationToken))
        {
            return CoreErrors.CompanyNotFound;
        }

        var code = command.Code.Trim();
        if (await db.BusinessPartners.AnyAsync(p => p.CompanyId == command.CompanyId && p.Code == code, cancellationToken))
        {
            return CoreErrors.DuplicateBusinessPartnerCode;
        }

        var partner = BusinessPartner.Create(command.CompanyId, command.ToDetails(taxRules));
        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync(cancellationToken);
        return partner.Id;
    }
}
