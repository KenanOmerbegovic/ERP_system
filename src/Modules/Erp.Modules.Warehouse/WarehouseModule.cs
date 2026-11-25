using Erp.Messaging;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Articles;
using Erp.Modules.Warehouse.Endpoints;
using Erp.Modules.Warehouse.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Warehouse;

/// <summary>
/// Composition entry points for the host. The host must also register an <see cref="SharedKernel.Time.IClock"/>,
/// the Identity module (which authorizes Warehouse's endpoints, with <see cref="Contracts.WarehousePermissions"/>),
/// and the Core module, whose article events Warehouse keeps its copy of articles from.
/// </summary>
public static class WarehouseModule
{
    public const string ConnectionStringName = "Erp";

    public static IServiceCollection AddWarehouseModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<WarehouseDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", WarehouseDbContext.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IWarehouseDbContext>(provider => provider.GetRequiredService<WarehouseDbContext>());
        services.AddScoped<IStockLocks, StockLocks>();
        services.AddOutboxProcessor<WarehouseDbContext>();

        services.AddValidatorsFromAssembly(typeof(WarehouseModule).Assembly, includeInternalTypes: true);

        var handlers = typeof(WarehouseModule).Assembly.GetTypes().Where(type =>
            type is { IsClass: true, IsAbstract: false, IsPublic: true }
            && type.Name.EndsWith("Handler", StringComparison.Ordinal)
            && type.Namespace?.StartsWith("Erp.Modules.Warehouse.Application", StringComparison.Ordinal) == true);
        foreach (var handler in handlers)
        {
            services.AddScoped(handler);
        }

        services.AddIntegrationEventHandler<ArticleCreated, ArticleCreatedHandler>();
        services.AddIntegrationEventHandler<ArticleUpdated, ArticleUpdatedHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapWarehouseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapWarehouseLocationEndpoints();
        app.MapStockDocumentEndpoints();
        app.MapStockEndpoints();
        app.MapNumberSeriesEndpoints();
        return app;
    }

    public static async Task MigrateWarehouseDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<WarehouseDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
