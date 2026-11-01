using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Modules.Identity.Infrastructure.Security;

/// <summary>Configuration section <c>Identity:Jwt</c>. The signing key is a secret: never commit one outside Development.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Identity:Jwt";

    /// <summary>HMAC-SHA256 needs at least 256 bits.</summary>
    public const int MinSigningKeyBytes = 32;

    [Required]
    public string Issuer { get; set; } = null!;

    [Required]
    public string Audience { get; set; } = null!;

    [Required]
    public string SigningKey { get; set; } = null!;

    public SymmetricSecurityKey SecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    public static bool HasValidSigningKey(JwtOptions options) =>
        options.SigningKey is not null && Encoding.UTF8.GetByteCount(options.SigningKey) >= MinSigningKeyBytes;
}
