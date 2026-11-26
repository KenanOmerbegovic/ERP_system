namespace Erp.Modules.Core.Contracts.Articles;

/// <summary>Body of create requests. <see cref="Tracking"/> is fixed once the article exists.</summary>
public sealed record CreateArticleRequest(
    string Code,
    string Name,
    ArticleKind Type,
    ArticleTracking Tracking,
    Guid UnitOfMeasureId,
    string? DefaultVatCode,
    bool IsActive);

/// <summary>Body of update requests; the tracking type cannot change.</summary>
public sealed record UpdateArticleRequest(
    string Code,
    string Name,
    ArticleKind Type,
    Guid UnitOfMeasureId,
    string? DefaultVatCode,
    bool IsActive);

public sealed record ArticleResponse(
    Guid Id,
    string Code,
    string Name,
    ArticleKind Type,
    ArticleTracking Tracking,
    Guid UnitOfMeasureId,
    string? DefaultVatCode,
    bool IsActive);

public enum ArticleKind
{
    Goods,
    Service,
}

/// <summary>How stock of an article is identified: not at all, by LOT (batch), or by serial number per piece.</summary>
public enum ArticleTracking
{
    None,
    Lot,
    Serial,
}
