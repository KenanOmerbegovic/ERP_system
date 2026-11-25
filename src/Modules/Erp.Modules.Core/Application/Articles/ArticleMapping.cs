using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Domain.Articles;

namespace Erp.Modules.Core.Application.Articles;

/// <summary>Domain enums to their contract counterparts (responses and events).</summary>
internal static class ArticleMapping
{
    public static ArticleKind ToKind(this ArticleType type) => type switch
    {
        ArticleType.Goods => ArticleKind.Goods,
        ArticleType.Service => ArticleKind.Service,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static ArticleTracking ToTracking(this TrackingType tracking) => tracking switch
    {
        TrackingType.None => ArticleTracking.None,
        TrackingType.Lot => ArticleTracking.Lot,
        TrackingType.Serial => ArticleTracking.Serial,
        _ => throw new ArgumentOutOfRangeException(nameof(tracking), tracking, null),
    };

    public static ArticleCreated ToCreatedEvent(this Article article, DateTimeOffset now) => new(
        Guid.CreateVersion7(), now, article.CompanyId.Value, article.Id, article.Code, article.Name,
        article.Type.ToKind(), article.TrackingType.ToTracking(), article.UnitOfMeasureId, article.IsActive);

    public static ArticleUpdated ToUpdatedEvent(this Article article, DateTimeOffset now) => new(
        Guid.CreateVersion7(), now, article.CompanyId.Value, article.Id, article.Code, article.Name,
        article.Type.ToKind(), article.TrackingType.ToTracking(), article.UnitOfMeasureId, article.IsActive);
}
