using System.Globalization;
using Erp.Messaging;
using Erp.Modules.Core.Contracts.Companies;
using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Auth;
using Erp.Modules.Identity.Application.Authorization;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Application.Companies;
using Erp.Modules.Identity.Application.Registration;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Identity.Endpoints;
using Erp.Modules.Identity.Endpoints.Authorization;
using Erp.Modules.Identity.Infrastructure;
using Erp.Modules.Identity.Infrastructure.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Modules.Identity;

/// <summary>
/// Composition entry points for the host: authentication (JWT bearer), authorization
/// (deny by default; permission policies), users, roles and company registration.
/// The host must also register an <see cref="SharedKernel.Time.IClock"/>.
/// </summary>
public static class IdentityModule
{
    public const string ConnectionStringName = "Erp";

    public const string IssueRegistrationKeyCommand = "issue-registration-key";

    /// <param name="modulePermissions">Permissions other modules check, e.g. CorePermissions.All.</param>
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<string> modulePermissions)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<IdentityDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IIdentityDbContext>(provider => provider.GetRequiredService<IdentityDbContext>());

        services.AddValidatorsFromAssembly(typeof(IdentityModule).Assembly, includeInternalTypes: true);
        AddOptions<SessionOptions>(services, SessionOptions.SectionName);
        AddOptions<LockoutOptions>(services, LockoutOptions.SectionName);
        AddOptions<RegistrationOptions>(services, RegistrationOptions.SectionName);
        AddOptions<JwtOptions>(services, JwtOptions.SectionName)
            .Validate(JwtOptions.HasValidSigningKey, $"{JwtOptions.SectionName}:SigningKey must be at least {JwtOptions.MinSigningKeyBytes} bytes.");

        services.AddSingleton(new PermissionCatalog([.. IdentityPermissions.All, .. modulePermissions]));
        services.AddScoped<MemberPermissions>();
        services.AddScoped<SessionIssuer>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        var handlers = typeof(IdentityModule).Assembly.GetTypes().Where(type =>
            type is { IsClass: true, IsAbstract: false, IsPublic: true }
            && type.Name.EndsWith("Handler", StringComparison.Ordinal)
            && type.Namespace?.StartsWith("Erp.Modules.Identity.Application", StringComparison.Ordinal) == true);
        foreach (var handler in handlers)
        {
            services.AddScoped(handler);
        }

        services.AddIntegrationEventHandler<CompanyCreated, CompanyCreatedHandler>();
        services.AddIntegrationEventHandler<CompanyUpdated, CompanyUpdatedHandler>();

        AddAuthentication(services);
        services.AddIdentityRateLimiting();
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapRegistrationEndpoints();
        app.MapMeEndpoints();
        app.MapUserEndpoints();
        app.MapRoleEndpoints();
        return app;
    }

    public static async Task MigrateIdentityDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Runs <c>issue-registration-key [--days N]</c> when it is the first argument: prints a new
    /// key once and returns true. The operator gives the key to the company that registers with it.
    /// </summary>
    public static async Task<bool> TryRunCommandAsync(this IServiceProvider services, string[] args, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        if (args.Length == 0 || args[0] != IssueRegistrationKeyCommand)
        {
            return false;
        }

        TimeSpan? lifetime = null;
        if (args is [_, "--days", var days] && int.TryParse(days, NumberStyles.None, CultureInfo.InvariantCulture, out var dayCount))
        {
            lifetime = TimeSpan.FromDays(dayCount);
        }
        else if (args.Length > 1)
        {
            await output.WriteLineAsync($"Usage: {IssueRegistrationKeyCommand} [--days N]");
            return true;
        }

        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IssueRegistrationKeyHandler>();
        var result = await handler.HandleAsync(new IssueRegistrationKey(lifetime), cancellationToken);
        await output.WriteLineAsync(result.IsSuccess
            ? $"Registration key: {result.Value.Key}{Environment.NewLine}Valid until: {result.Value.ExpiresAt:yyyy-MM-dd HH:mm} UTC. It is shown only once."
            : $"No key issued: {result.Error.Message}");
        return true;
    }

    private static OptionsBuilder<TOptions> AddOptions<TOptions>(IServiceCollection services, string sectionName)
        where TOptions : class =>
        services.AddOptions<TOptions>().BindConfiguration(sectionName).ValidateDataAnnotations().ValidateOnStart();

    private static void AddAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = jwt.Value.SecurityKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimsPrincipalExtensions.UserIdClaim,
                };
            });

        // Deny by default: every endpoint requires a signed-in user unless it opts out with AllowAnonymous.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(IdentityPolicies.CompanyRegistration, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new CompanyRegistrationRequirement()));
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<IAuthorizationHandler, CompanyRegistrationHandler>();
    }
}
