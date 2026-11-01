namespace Erp.Modules.Identity.Contracts.Users;

/// <summary>Creates a login and makes it a member of the company. The password is the initial one the administrator sets.</summary>
public sealed record CreateUserRequest(string Email, string DisplayName, string Password, IReadOnlyList<Guid> RoleIds);

public sealed record UpdateMemberRequest(IReadOnlyList<Guid> RoleIds, bool IsActive);

public sealed record MemberResponse(Guid UserId, string Email, string DisplayName, IReadOnlyList<Guid> RoleIds, bool IsActive);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<CurrentUserCompanyResponse> Companies,
    bool CanRegisterCompany);

/// <summary>A company the user is an active member of, with the permissions their roles grant there.</summary>
public sealed record CurrentUserCompanyResponse(Guid CompanyId, string Name, bool IsAdministrator, IReadOnlyList<string> Permissions);
