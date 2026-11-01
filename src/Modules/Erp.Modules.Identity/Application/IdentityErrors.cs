using Erp.SharedKernel.Results;

namespace Erp.Modules.Identity.Application;

public static class IdentityErrors
{
    /// <summary>Same error for an unknown email and a wrong password, so logins don't reveal which accounts exist.</summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

    public static readonly Error LockedOut =
        Error.Unauthorized("Auth.LockedOut", "Too many failed logins; the account is temporarily locked.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Auth.InvalidRefreshToken", "The session has expired or was revoked.");

    /// <summary>Unknown, expired and already redeemed keys look the same.</summary>
    public static readonly Error InvalidRegistrationKey =
        new("Registration.InvalidKey", "The registration key is invalid, expired or already used.");

    public static readonly Error UserNotFound =
        Error.NotFound("User.NotFound", "User not found.");

    public static readonly Error DuplicateEmail =
        Error.Conflict("User.DuplicateEmail", "An account with this email already exists.");

    public static readonly Error CompanyNotFound =
        Error.NotFound("Company.NotFound", "Company not found.");

    public static readonly Error MemberNotFound =
        Error.NotFound("Member.NotFound", "The user is not a member of this company.");

    public static readonly Error LastAdministrator =
        new("Member.LastAdministrator", "The company must keep at least one active administrator.");

    public static readonly Error RoleNotFound =
        Error.NotFound("Role.NotFound", "Role not found.");

    public static readonly Error DuplicateRoleName =
        Error.Conflict("Role.DuplicateName", "A role with this name already exists.");

    public static readonly Error AdministratorRoleIsReadOnly =
        new("Role.AdministratorReadOnly", "The administrator role cannot be changed.");

    /// <summary>Validation error codes reported on a property.</summary>
    public static class Codes
    {
        public const string WrongPassword = "Password.Incorrect";
        public const string UnknownRole = "Role.NotFound";
        public const string UnknownPermission = "Permission.Unknown";
    }
}
