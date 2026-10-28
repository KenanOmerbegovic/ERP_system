namespace Erp.Modules.Core.Contracts.Common;

public sealed record CreatedResponse(Guid Id);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
