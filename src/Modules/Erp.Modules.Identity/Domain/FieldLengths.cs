namespace Erp.Modules.Identity.Domain;

/// <summary>Maximum text lengths, shared by validation and the database schema.</summary>
public static class FieldLengths
{
    public const int Email = 254;
    public const int DisplayName = 200;
    public const int PasswordHash = 500;
    public const int RoleName = 100;
    public const int Permission = 100;
    public const int CompanyName = 200;

    /// <summary>Hex-encoded SHA-256 of a refresh token or registration key.</summary>
    public const int SecretHash = 64;
}
