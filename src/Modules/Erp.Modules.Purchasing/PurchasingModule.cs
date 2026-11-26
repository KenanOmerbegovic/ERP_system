using Erp.Messaging;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.References;
using Erp.Modules.Purchasing.Endpoints;
using Erp.Modules.Purchasing.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Purchasing;

/// <summary>
/// Composition entry points for the host. The host must also register an <see cref="SharedKernel.Time.IClock"/>, the
/// Identity module (with <see cref="Contracts.PurchasingPermissions"/>), the Core module (partner and article events, VAT
/// rates), the Warehouse module (its stock command contract), and an <c>IVatCalculator</c> (country localization).
/// </summary>
public static class PurchasingModule
{
    public const string ConnectionStringName = "Erp";

    public static IServiceCollection AddPurchasingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<PurchasingDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", PurchasingDbContext.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IPurchasingDbContext>(provider => provider.GetRequiredService<PurchasingDbContext>());
        services.AddScoped<IPurchasingLocks, PurchasingLocks>();
        services.AddOutboxProcessor<PurchasingDbContext>();

        services.AddValidatorsFromAssembly(typeof(PurchasingModule).Assembly, includeInternalTypes: true);

        var handlers = typeof(PurchasingModule).Assembly.GetTypes().Where(type =>
            type is { IsClass: true, IsAbstract: false, IsPublic: true }
            && type.Name.EndsWith("Handler", StringComparison.Ordinal)
            && type.Namespace?.StartsWith("Erp.Modules.Purchasing.Application", StringComparison.Ordinal) == true);
        foreach (var handler in handlers)
        {
            services.AddScoped(handler);
        }

        services.AddIntegrationEventHandler<BusinessPartnerCreated, BusinessPartnerCreatedHandler>();
        services.AddIntegrationEventHandler<BusinessPartnerUpdated, BusinessPartnerUpdatedHandler>();
        services.AddIntegrationEventHandler<ArticleCreated, PurchasingArticleCreatedHandler>();
        services.AddIntegrationEventHandler<ArticleUpdated, PurchasingArticleUpdatedHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapPurchasingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPurchaseOrderEndpoints();
        app.MapGoodsReceiptEndpoints();
        app.MapSupplierInvoiceEndpoints();
        app.MapPurchasingNumberSeriesEndpoints();
        return app;
    }

    public static async Task MigratePurchasingDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PurchasingDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
