using Erp.Messaging;
using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Endpoints;
using Erp.Modules.Core.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Core;

/// <summary>
/// Composition entry points for the host. The host must also register an
/// <see cref="ITaxIdentifierRules"/> implementation (country localization), an
/// <see cref="SharedKernel.Time.IClock"/>, and the Identity module, which authorizes Core's endpoints.
/// </summary>
public static class CoreModule
{
    public const string ConnectionStringName = "Erp";

    public static IServiceCollection AddCoreModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<CoreDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CoreDbContext.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<ICoreDbContext>(provider => provider.GetRequiredService<CoreDbContext>());
        services.AddOutboxProcessor<CoreDbContext>();

        services.AddValidatorsFromAssembly(typeof(CoreModule).Assembly, includeInternalTypes: true);

        var handlers = typeof(CoreModule).Assembly.GetTypes().Where(type =>
            type is { IsClass: true, IsAbstract: false, IsPublic: true }
            && type.Name.EndsWith("Handler", StringComparison.Ordinal)
            && type.Namespace?.StartsWith("Erp.Modules.Core.Application", StringComparison.Ordinal) == true);
        foreach (var handler in handlers)
        {
            services.AddScoped(handler);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapCoreEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCompanyEndpoints();
        app.MapBusinessPartnerEndpoints();
        app.MapUnitOfMeasureEndpoints();
        app.MapArticleEndpoints();
        return app;
    }

    public static async Task MigrateCoreDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CoreDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
