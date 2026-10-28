using Erp.SharedKernel.Time;

namespace Erp.SharedKernel.Tests.Time;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_is_current_utc_time()
    {
        IClock clock = new SystemClock();

        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(TimeSpan.Zero, now.Offset);
        Assert.InRange(now, before, after);
    }
}
