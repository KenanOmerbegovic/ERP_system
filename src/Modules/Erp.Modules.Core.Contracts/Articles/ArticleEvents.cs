using Erp.SharedKernel.Messaging;

namespace Erp.Modules.Core.Contracts.Articles;

/// <summary>An article was created. Modules that keep their own copy of articles (e.g. Warehouse) start from this.</summary>
public sealed record ArticleCreated(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid ArticleId,
    string Code,
    string Name,
    ArticleKind Kind,
    ArticleTracking Tracking,
    Guid UnitOfMeasureId,
    bool IsActive) : IIntegrationEvent;

/// <summary>An article's data changed; it carries the full current state. <see cref="Tracking"/> never changes.</summary>
public sealed record ArticleUpdated(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid ArticleId,
    string Code,
    string Name,
    ArticleKind Kind,
    ArticleTracking Tracking,
    Guid UnitOfMeasureId,
    bool IsActive) : IIntegrationEvent;
