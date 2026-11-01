using Erp.SharedKernel.Time;

namespace Erp.Modules.Core.Tests.TestSupport;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
}
