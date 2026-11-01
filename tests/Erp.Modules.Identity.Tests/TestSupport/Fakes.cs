using Erp.Modules.Identity.Application.Abstractions;
using Erp.SharedKernel.Time;

namespace Erp.Modules.Identity.Tests.TestSupport;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>Readable, fast stand-in; the real hasher has its own tests.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public int VerificationsWithoutUser { get; private set; }

    public string Hash(string password) => "hashed:" + password;

    public bool Verify(string passwordHash, string password) => passwordHash == Hash(password);

    public void VerifyWithoutUser(string password) => VerificationsWithoutUser++;
}
