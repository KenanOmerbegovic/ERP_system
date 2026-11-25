namespace Erp.SharedKernel.Time;

/// <param name="businessTimeZone">The time zone <see cref="Today"/> is read in; the host takes it from the country localization.</param>
public sealed class SystemClock(TimeZoneInfo businessTimeZone) : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, businessTimeZone).DateTime);
}
