using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Domain.References;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.References;

// Sales' copies of Core's business partners and articles. Delivery is at least once, and an update carries the full
// state, so every handler is idempotent and an update for an unseen record creates it.

public sealed class SalesBusinessPartnerCreatedHandler(ISalesDbContext db) : IIntegrationEventHandler<BusinessPartnerCreated>
{
    public Task HandleAsync(BusinessPartnerCreated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return db.UpsertCustomerAsync(
            integrationEvent.BusinessPartnerId, integrationEvent.CompanyId, integrationEvent.Code, integrationEvent.Name, integrationEvent.TaxId,
            integrationEvent.VatNumber, integrationEvent.IsCustomer, integrationEvent.IsActive, onlyIfNew: true, cancellationToken);
    }
}

public sealed class SalesBusinessPartnerUpdatedHandler(ISalesDbContext db) : IIntegrationEventHandler<BusinessPartnerUpdated>
{
    public Task HandleAsync(BusinessPartnerUpdated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return db.UpsertCustomerAsync(
            integrationEvent.BusinessPartnerId, integrationEvent.CompanyId, integrationEvent.Code, integrationEvent.Name, integrationEvent.TaxId,
            integrationEvent.VatNumber, integrationEvent.IsCustomer, integrationEvent.IsActive, onlyIfNew: false, cancellationToken);
    }
}

public sealed class SalesArticleCreatedHandler(ISalesDbContext db) : IIntegrationEventHandler<ArticleCreated>
{
    public Task HandleAsync(ArticleCreated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return db.UpsertArticleAsync(
            integrationEvent.ArticleId, integrationEvent.CompanyId, integrationEvent.Code, integrationEvent.Name, integrationEvent.Kind,
            integrationEvent.Tracking, integrationEvent.DefaultVatCode, integrationEvent.IsActive, onlyIfNew: true, cancellationToken);
    }
}

public sealed class SalesArticleUpdatedHandler(ISalesDbContext db) : IIntegrationEventHandler<ArticleUpdated>
{
    public Task HandleAsync(ArticleUpdated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return db.UpsertArticleAsync(
            integrationEvent.ArticleId, integrationEvent.CompanyId, integrationEvent.Code, integrationEvent.Name, integrationEvent.Kind,
            integrationEvent.Tracking, integrationEvent.DefaultVatCode, integrationEvent.IsActive, onlyIfNew: false, cancellationToken);
    }
}

internal static class ReferenceUpserts
{
    public static async Task UpsertCustomerAsync(
        this ISalesDbContext db, Guid id, Guid companyId, string code, string name, string? taxId, string? vatNumber,
        bool isCustomer, bool isActive, bool onlyIfNew, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            db.Customers.Add(CustomerReference.Create(id, new CompanyId(companyId), code, name, taxId, vatNumber, isCustomer, isActive));
        }
        else if (!onlyIfNew)
        {
            customer.Update(code, name, taxId, vatNumber, isCustomer, isActive);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task UpsertArticleAsync(
        this ISalesDbContext db, Guid id, Guid companyId, string code, string name, ArticleKind kind, ArticleTracking tracking,
        string? defaultVatCode, bool isActive, bool onlyIfNew, CancellationToken cancellationToken)
    {
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        var isService = kind == ArticleKind.Service;
        if (article is null)
        {
            db.Articles.Add(ArticleReference.Create(id, new CompanyId(companyId), code, name, isService, tracking.ToTracking(), defaultVatCode, isActive));
        }
        else if (!onlyIfNew)
        {
            article.Update(code, name, isService, defaultVatCode, isActive);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
