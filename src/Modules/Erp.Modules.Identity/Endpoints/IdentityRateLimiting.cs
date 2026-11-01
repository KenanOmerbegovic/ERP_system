using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Erp.Modules.Identity.Endpoints;

/// <summary>Configuration section <c>Identity:RateLimiting</c>: requests per client IP to login, refresh and registration.</summary>
public sealed class IdentityRateLimitOptions
{
    public const string SectionName = "Identity:RateLimiting";

    [Range(1, 10_000)]
    public int PermitLimit { get; set; }

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Window { get; set; }
}

internal static class IdentityRateLimiting
{
    public const string Policy = "identity-auth";

    /// <summary>
    /// Partitioned by remote IP. Behind a reverse proxy this needs forwarded headers,
    /// or every client shares one partition (docs/open-decisions.md).
    /// </summary>
    public static void AddIdentityRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<IdentityRateLimitOptions>()
            .BindConfiguration(IdentityRateLimitOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Policy, httpContext =>
            {
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<IdentityRateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PermitLimit,
                        Window = limits.Window,
                        QueueLimit = 0,
                    });
            });
        });
    }
}
