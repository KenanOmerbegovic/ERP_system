using System.Security.Cryptography;
using System.Text;

namespace Erp.Modules.Identity.Application.Common;

/// <summary>Random secrets handed to clients, and the hashes stored in their place.</summary>
public static class Secrets
{
    // Crockford base32: no I, L, O or U, so keys survive being read out or typed by hand.
    private const string KeyAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int KeyGroups = 5;
    private const int KeyGroupLength = 5;

    /// <summary>256 random bits, base64url.</summary>
    public static string NewRefreshToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>125 random bits as five groups of five characters, e.g. <c>7Q2MX-...</c>.</summary>
    public static string NewRegistrationKey()
    {
        var groups = Enumerable.Range(0, KeyGroups)
            .Select(_ => RandomNumberGenerator.GetString(KeyAlphabet, KeyGroupLength));
        return string.Join('-', groups);
    }

    /// <summary>Ignores case, dashes and spaces so a key can be typed loosely.</summary>
    public static string NormalizeRegistrationKey(string key) =>
        new(key.Where(character => character is not ('-' or ' ')).Select(char.ToUpperInvariant).ToArray());

    /// <summary>Lower-case hex SHA-256. Fine for high-entropy secrets; passwords use <see cref="Abstractions.IPasswordHasher"/>.</summary>
    public static string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
