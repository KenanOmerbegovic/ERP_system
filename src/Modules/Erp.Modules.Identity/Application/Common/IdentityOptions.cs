using System.ComponentModel.DataAnnotations;

namespace Erp.Modules.Identity.Application.Common;

/// <summary>Configuration section <c>Identity:Sessions</c>.</summary>
public sealed class SessionOptions
{
    public const string SectionName = "Identity:Sessions";

    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; }

    /// <summary>Sliding: each refresh issues a token valid this long.</summary>
    [Range(typeof(TimeSpan), "00:10:00", "90.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; set; }
}

/// <summary>Configuration section <c>Identity:Lockout</c>.</summary>
public sealed class LockoutOptions
{
    public const string SectionName = "Identity:Lockout";

    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; }

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Duration { get; set; }
}

/// <summary>Configuration section <c>Identity:Registration</c>.</summary>
public sealed class RegistrationOptions
{
    public const string SectionName = "Identity:Registration";

    /// <summary>How long a newly issued registration key can be redeemed, unless the issuer chooses otherwise.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
    public TimeSpan KeyLifetime { get; set; }
}
