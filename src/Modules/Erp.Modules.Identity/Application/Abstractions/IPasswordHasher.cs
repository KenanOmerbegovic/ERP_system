namespace Erp.Modules.Identity.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);

    /// <summary>
    /// Spends the time of a real verification without a user, so a login for an unknown
    /// email takes as long as one with a wrong password.
    /// </summary>
    void VerifyWithoutUser(string password);
}
