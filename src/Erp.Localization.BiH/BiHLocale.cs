namespace Erp.Localization.BiH;

/// <summary>Country settings for Bosnia and Herzegovina that are not rules of a single module.</summary>
public static class BiHLocale
{
    /// <summary>IANA id of the time zone the business date is read in.</summary>
    public const string TimeZoneId = "Europe/Sarajevo";

    public static TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}
