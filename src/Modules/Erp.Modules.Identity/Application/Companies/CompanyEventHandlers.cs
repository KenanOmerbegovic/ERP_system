using Erp.Modules.Core.Contracts.Companies;
using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Domain.Companies;
using Erp.Modules.Identity.Domain.Memberships;
using Erp.Modules.Identity.Domain.Roles;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Identity.Application.Companies;

/// <summary>
/// Step 2 of company registration completes here: the company gets its administrator role,
/// and the user who registered it with their key becomes its administrator.
/// </summary>
public sealed partial class CompanyCreatedHandler(IIdentityDbContext db, ILogger<CompanyCreatedHandler> logger)
    : IIntegrationEventHandler<CompanyCreated>
{
    public async Task HandleAsync(CompanyCreated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var companyId = new CompanyId(integrationEvent.CompanyId);
        if (await db.Companies.AnyAsync(company => company.Id == companyId, cancellationToken))
        {
            return; // Already delivered.
        }

        db.Companies.Add(CompanyReference.Create(companyId, integrationEvent.Name));
        var administrator = Role.CreateAdministrator(companyId);
        db.Roles.Add(administrator);

        var key = await db.RegistrationKeys
            .Where(k => k.RedeemedByUserId == integrationEvent.CreatedByUserId && k.UsedForCompanyId == null)
            .OrderBy(k => k.RedeemedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (key is null)
        {
            // Two companies were created concurrently with one key; only the first gets the key's user as administrator.
            LogNoRegistrationKey(logger, companyId.Value, integrationEvent.CreatedByUserId);
        }
        else
        {
            key.UseFor(companyId);
            db.Memberships.Add(Membership.Create(companyId, integrationEvent.CreatedByUserId, [administrator.Id]));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Company {CompanyId} was created by user {UserId}, who holds no unused registration key; no administrator was assigned")]
    private static partial void LogNoRegistrationKey(ILogger logger, Guid companyId, Guid userId);
}

public sealed class CompanyUpdatedHandler(IIdentityDbContext db) : IIntegrationEventHandler<CompanyUpdated>
{
    public async Task HandleAsync(CompanyUpdated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var companyId = new CompanyId(integrationEvent.CompanyId);
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null)
        {
            // CompanyCreated is delivered first; a missing company is a bug, and throwing makes the outbox retry.
            throw new InvalidOperationException($"Company {companyId} is unknown to Identity.");
        }

        company.Rename(integrationEvent.Name);
        await db.SaveChangesAsync(cancellationToken);
    }
}
