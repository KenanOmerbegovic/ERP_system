using Erp.SharedKernel.Messaging;

namespace Erp.Modules.Core.Contracts.Companies;

/// <summary>A company was registered by <paramref name="CreatedByUserId"/>, who becomes its administrator.</summary>
public sealed record CompanyCreated(Guid Id, DateTimeOffset OccurredAt, Guid CompanyId, string Name, Guid CreatedByUserId)
    : IIntegrationEvent;

public sealed record CompanyUpdated(Guid Id, DateTimeOffset OccurredAt, Guid CompanyId, string Name) : IIntegrationEvent;
