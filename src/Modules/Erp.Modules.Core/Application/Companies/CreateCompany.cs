using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Contracts.Companies;
using Erp.Modules.Core.Domain.Companies;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Companies;

/// <summary>Step 2 of company registration; <paramref name="CreatedByUserId"/> becomes the company's administrator.</summary>
public sealed record CreateCompany(
    Guid CreatedByUserId,
    string Name,
    string TaxId,
    string? VatNumber,
    string Address,
    string PostalCode,
    string City) : ICompanyFields;

internal sealed class CreateCompanyValidator : AbstractValidator<CreateCompany>
{
    public CreateCompanyValidator(ITaxIdentifierRules taxRules)
    {
        Include(new CompanyFieldsValidator(taxRules));
    }
}

public sealed class CreateCompanyHandler(ICoreDbContext db, IValidator<CreateCompany> validator, IClock clock)
{
    public async Task<Result<CompanyId>> HandleAsync(CreateCompany command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var taxId = command.TaxId.Trim();
        if (await db.Companies.AnyAsync(c => c.TaxId == taxId, cancellationToken))
        {
            return CoreErrors.DuplicateCompanyTaxId;
        }

        var company = Company.Create(command.Name, taxId, command.VatNumber, command.Address, command.PostalCode, command.City);
        db.Companies.Add(company);
        db.AddIntegrationEvent(new CompanyCreated(Guid.CreateVersion7(), clock.UtcNow, company.Id.Value, company.Name, command.CreatedByUserId));
        await db.SaveChangesAsync(cancellationToken);
        return company.Id;
    }
}
