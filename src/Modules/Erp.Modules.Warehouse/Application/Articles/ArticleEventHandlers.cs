using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Domain.Articles;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.Articles;

/// <summary>Keeps Warehouse's copy of Core's articles. Delivery is at least once, so both handlers are idempotent.</summary>
public sealed class ArticleCreatedHandler(IWarehouseDbContext db) : IIntegrationEventHandler<ArticleCreated>
{
    public async Task HandleAsync(ArticleCreated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (await db.Articles.AnyAsync(article => article.Id == integrationEvent.ArticleId, cancellationToken))
        {
            return; // Already delivered.
        }

        db.Articles.Add(ArticleReference.Create(
            integrationEvent.ArticleId,
            new CompanyId(integrationEvent.CompanyId),
            integrationEvent.Code,
            integrationEvent.Name,
            integrationEvent.Kind == ArticleKind.Goods,
            ArticleReferenceMapping.ToTracking(integrationEvent.Tracking),
            integrationEvent.UnitOfMeasureId,
            integrationEvent.IsActive));
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>The event carries the article's full state, so an article whose creation was not seen yet is created from it.</summary>
public sealed class ArticleUpdatedHandler(IWarehouseDbContext db) : IIntegrationEventHandler<ArticleUpdated>
{
    public async Task HandleAsync(ArticleUpdated integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == integrationEvent.ArticleId, cancellationToken);
        var isGoods = integrationEvent.Kind == ArticleKind.Goods;
        if (article is null)
        {
            db.Articles.Add(ArticleReference.Create(
                integrationEvent.ArticleId,
                new CompanyId(integrationEvent.CompanyId),
                integrationEvent.Code,
                integrationEvent.Name,
                isGoods,
                ArticleReferenceMapping.ToTracking(integrationEvent.Tracking),
                integrationEvent.UnitOfMeasureId,
                integrationEvent.IsActive));
        }
        else
        {
            article.Update(integrationEvent.Code, integrationEvent.Name, isGoods, integrationEvent.UnitOfMeasureId, integrationEvent.IsActive);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

internal static class ArticleReferenceMapping
{
    public static TrackingType ToTracking(ArticleTracking tracking) => tracking switch
    {
        ArticleTracking.None => TrackingType.None,
        ArticleTracking.Lot => TrackingType.Lot,
        ArticleTracking.Serial => TrackingType.Serial,
        _ => throw new ArgumentOutOfRangeException(nameof(tracking), tracking, null),
    };
}
