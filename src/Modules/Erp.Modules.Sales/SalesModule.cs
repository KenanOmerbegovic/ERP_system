using Erp.Messaging;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.References;
using Erp.Modules.Sales.Endpoints;
using Erp.Modules.Sales.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Sales;

/// <summary>
/// Composition entry points for the host. The host must also register an <see cref="SharedKernel.Time.IClock"/>, the
/// Identity module (with <see cref="Contracts.SalesPermissions"/>), the Core module (partner and article events, VAT
/// rates), the Warehouse module (its stock command contract), and an <c>IVatCalculator</c> (country localization).
/// </summary>
public static class SalesModule
{
    public const string ConnectionStringName = "Erp";

    public static IServiceCollection AddSalesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<SalesDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", SalesDbContext.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<ISalesDbContext>(provider => provider.GetRequiredService<SalesDbContext>());
        services.AddScoped<ISalesLocks, SalesLocks>();
        services.AddOutboxProcessor<SalesDbContext>();

        services.AddValidatorsFromAssembly(typeof(SalesModule).Assembly, includeInternalTypes: true);

        var handlers = typeof(SalesModule).Assembly.GetTypes().Where(type =>
            type is { IsClass: true, IsAbstract: false, IsPublic: true }
            && type.Name.EndsWith("Handler", StringComparison.Ordinal)
            && type.Namespace?.StartsWith("Erp.Modules.Sales.Application", StringComparison.Ordinal) == true);
        foreach (var handler in handlers)
        {
            services.AddScoped(handler);
        }

        services.AddIntegrationEventHandler<BusinessPartnerCreated, SalesBusinessPartnerCreatedHandler>();
        services.AddIntegrationEventHandler<BusinessPartnerUpdated, SalesBusinessPartnerUpdatedHandler>();
        services.AddIntegrationEventHandler<ArticleCreated, SalesArticleCreatedHandler>();
        services.AddIntegrationEventHandler<ArticleUpdated, SalesArticleUpdatedHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapOfferEndpoints();
        app.MapSalesOrderEndpoints();
        app.MapDeliveryNoteEndpoints();
        app.MapSalesInvoiceEndpoints();
        app.MapCreditNoteEndpoints();
        app.MapSalesNumberSeriesEndpoints();
        return app;
    }

    public static async Task MigrateSalesDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SalesDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
