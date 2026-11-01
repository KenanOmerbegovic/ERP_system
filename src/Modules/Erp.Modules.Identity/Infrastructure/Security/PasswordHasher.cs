using Erp.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Erp.Modules.Identity.Infrastructure.Security;

/// <summary>ASP.NET Core Identity's hasher (PBKDF2, salted, versioned format).</summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object NoUser = new();
    private static readonly Lazy<string> DummyHash = new(() => Hasher.HashPassword(NoUser, Guid.NewGuid().ToString()));

    public string Hash(string password) => Hasher.HashPassword(NoUser, password);

    public bool Verify(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(NoUser, passwordHash, password) != PasswordVerificationResult.Failed;

    public void VerifyWithoutUser(string password) => Hasher.VerifyHashedPassword(NoUser, DummyHash.Value, password);
}
