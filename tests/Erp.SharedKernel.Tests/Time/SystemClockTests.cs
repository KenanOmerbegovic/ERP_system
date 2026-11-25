using Erp.SharedKernel.Time;

namespace Erp.SharedKernel.Tests.Time;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_is_current_utc_time()
    {
        IClock clock = new SystemClock(TimeZoneInfo.Utc);

        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(TimeSpan.Zero, now.Offset);
        Assert.InRange(now, before, after);
    }

    [Fact]
    public void Today_is_the_date_in_the_business_time_zone()
    {
        // UTC+14: the business date is never behind the UTC date.
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus-14", TimeSpan.FromHours(14), "plus-14", "plus-14");
        IClock clock = new SystemClock(zone);

        var expected = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(14)).DateTime);

        Assert.Equal(expected, clock.Today);
    }
}
