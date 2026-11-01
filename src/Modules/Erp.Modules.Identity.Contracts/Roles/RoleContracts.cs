namespace Erp.Modules.Identity.Contracts.Roles;

public sealed record RoleRequest(string Name, IReadOnlyList<string> Permissions);

/// <summary>The built-in administrator role grants every permission and cannot be changed.</summary>
public sealed record RoleResponse(Guid Id, string Name, bool IsAdministrator, IReadOnlyList<string> Permissions);
