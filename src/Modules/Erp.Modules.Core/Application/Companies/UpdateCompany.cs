using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Companies;

public sealed record UpdateCompany(
    CompanyId CompanyId,
    string Name,
    string TaxId,
    string? VatNumber,
    string Address,
    string PostalCode,
    string City) : ICompanyFields;

internal sealed class UpdateCompanyValidator : AbstractValidator<UpdateCompany>
{
    public UpdateCompanyValidator(ITaxIdentifierRules taxRules)
    {
        Include(new CompanyFieldsValidator(taxRules));
    }
}

public sealed class UpdateCompanyHandler(ICoreDbContext db, IValidator<UpdateCompany> validator)
{
    public async Task<Result> HandleAsync(UpdateCompany command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == command.CompanyId, cancellationToken);
        if (company is null)
        {
            return CoreErrors.CompanyNotFound;
        }

        var taxId = command.TaxId.Trim();
        if (await db.Companies.AnyAsync(c => c.TaxId == taxId && c.Id != command.CompanyId, cancellationToken))
        {
            return CoreErrors.DuplicateCompanyTaxId;
        }

        company.Update(command.Name, taxId, command.VatNumber, command.Address, command.PostalCode, command.City);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
