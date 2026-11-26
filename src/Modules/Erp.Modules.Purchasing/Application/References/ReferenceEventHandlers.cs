using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Domain.References;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.References;

// Purchasing's copies of Core's business partners and articles. Delivery is at least once, and an update carries the
// full state, so every handler is idempotent and an update for an unseen record creates it.

public sealed class BusinessPartnerCreatedHandler(IPurchasingDbContext db) : IIntegrationEventHandler<BusinessPartnerCreated>
{
    public Task HandleAsync(BusinessPartnerCreated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return db.UpsertSupplierAsync(
            integrationEvent.BusinessPartnerId, integrationEvent.CompanyId, integrationEvent.Code, integrationEvent.Name, integrationEvent.TaxId,
            integrationEvent.VatNumber, integrationEvent.IsSupplier, integrationEvent.IsActive, onlyIfNew: true, cancellationToken);
    }
}

public sealed class BusinessPartnerUpdatedHandler(IPurchasingDbContext db) : IIntegrationEventHandler<BusinessPartnerUpdated>
{
    public Task HandleAsync(BusinessPartnerUpdated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return db.UpsertSupplierAsync(
            integrationEvent.BusinessPartnerId, integrationEvent.CompanyId, integrationEvent.Code, integrationEvent.Name, integrationEvent.TaxId,
            integrationEvent.VatNumber, integrationEvent.IsSupplier, integrationEvent.IsActive, onlyIfNew: false, cancellationToken);
    }
}

public sealed class PurchasingArticleCreatedHandler(IPurchasingDbContext db) : IIntegrationEventHandler<ArticleCreated>
{
    public Task HandleAsync(ArticleCreated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return db.UpsertArticleAsync(
            integrationEvent.ArticleId, integrationEvent.CompanyId, integrationEvent.Code, integrationEvent.Name, integrationEvent.Kind,
            integrationEvent.Tracking, integrationEvent.DefaultVatCode, integrationEvent.IsActive, onlyIfNew: true, cancellationToken);
    }
}

public sealed class PurchasingArticleUpdatedHandler(IPurchasingDbContext db) : IIntegrationEventHandler<ArticleUpdated>
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
    public static async Task UpsertSupplierAsync(
        this IPurchasingDbContext db, Guid id, Guid companyId, string code, string name, string? taxId, string? vatNumber,
        bool isSupplier, bool isActive, bool onlyIfNew, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier is null)
        {
            db.Suppliers.Add(SupplierReference.Create(id, new CompanyId(companyId), code, name, taxId, vatNumber, isSupplier, isActive));
        }
        else if (!onlyIfNew)
        {
            supplier.Update(code, name, taxId, vatNumber, isSupplier, isActive);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task UpsertArticleAsync(
        this IPurchasingDbContext db, Guid id, Guid companyId, string code, string name, ArticleKind kind, ArticleTracking tracking,
        string? defaultVatCode, bool isActive, bool onlyIfNew, CancellationToken cancellationToken)
    {
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        var isGoods = kind == ArticleKind.Goods;
        if (article is null)
        {
            db.Articles.Add(ArticleReference.Create(id, new CompanyId(companyId), code, name, isGoods, tracking.ToTracking(), defaultVatCode, isActive));
        }
        else if (!onlyIfNew)
        {
            article.Update(code, name, isGoods, defaultVatCode, isActive);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
