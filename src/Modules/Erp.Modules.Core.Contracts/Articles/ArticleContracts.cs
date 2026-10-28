namespace Erp.Modules.Core.Contracts.Articles;

/// <summary>Body of create and update requests.</summary>
public sealed record ArticleRequest(
    string Code,
    string Name,
    ArticleKind Type,
    Guid UnitOfMeasureId,
    bool IsActive);

public sealed record ArticleResponse(
    Guid Id,
    string Code,
    string Name,
    ArticleKind Type,
    Guid UnitOfMeasureId,
    bool IsActive);

public enum ArticleKind
{
    Goods,
    Service,
}
