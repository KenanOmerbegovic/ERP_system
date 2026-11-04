using System.Collections.Frozen;
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

    /// <summary>
    /// Keys that are public (in this repository or its history), so anyone could sign tokens with them.
    /// Add any key that gets committed by mistake.
    /// </summary>
    private static readonly FrozenSet<string> KnownPlaceholderKeys = FrozenSet.Create(
        StringComparer.Ordinal,
        "dev-only-signing-key-change-outside-development-0123456789",
        "openapi-generation-only-not-a-real-signing-key",
        "integration-test-signing-key-thirty-two-bytes-plus",
        "test-signing-key-at-least-thirty-two-bytes-long",
        "exactly-thirty-two-bytes-long-ok");

    public static bool HasValidSigningKey(JwtOptions options) =>
        options.SigningKey is not null && Encoding.UTF8.GetByteCount(options.SigningKey) >= MinSigningKeyBytes;

    public static bool IsKnownPlaceholder(JwtOptions options) =>
        options.SigningKey is not null && KnownPlaceholderKeys.Contains(options.SigningKey.Trim());
}
