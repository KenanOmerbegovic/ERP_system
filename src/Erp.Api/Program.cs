using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Erp.Api.OpenApi;
using Erp.Localization.BiH;
using Erp.Localization.BiH.Core;
using Erp.Localization.BiH.Vat;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Core;
using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Identity;
using Erp.Modules.Purchasing;
using Erp.Modules.Purchasing.Contracts;
using Erp.Modules.Sales;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Warehouse;
using Erp.Modules.Warehouse.Contracts;
using Erp.SharedKernel.Time;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Build-time OpenAPI generation (Erp.Api.csproj) starts the app in the Production environment only to
// read its endpoints. Placeholders satisfy configuration checks; nothing connects or delivers messages.
if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        [$"ConnectionStrings:{CoreModule.ConnectionStringName}"] = "Host=openapi-generation-only",
        ["Identity:Jwt:SigningKey"] = RandomNumberGenerator.GetHexString(64),
        ["Messaging:Outbox:Enabled"] = "false",
    });
}

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecurityTransformer>();
    options.AddOperationTransformer<BearerSecurityTransformer>();
    options.AddDocumentTransformer<PermissionCodesTransformer>();
});
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Browser clients send the refresh cookie cross-origin, so origins are listed explicitly (no wildcard).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddSingleton<IClock>(new SystemClock(BiHLocale.TimeZone));
builder.Services.AddSingleton<ITaxIdentifierRules, BiHTaxIdentifierRules>();
builder.Services.AddScoped<IVatCalculator, BiHVatCalculator>();
builder.Services.AddIdentityModule(builder.Configuration, [.. CorePermissions.All, .. WarehousePermissions.All, .. PurchasingPermissions.All, .. SalesPermissions.All]);
builder.Services.AddCoreModule(builder.Configuration);
builder.Services.AddWarehouseModule(builder.Configuration);
builder.Services.AddPurchasingModule(builder.Configuration);
builder.Services.AddSalesModule(builder.Configuration);

var app = builder.Build();

// Check configuration (e.g. the JWT signing key) before migrating or running a command, not only when the host starts.
app.Services.GetRequiredService<IStartupValidator>().Validate();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateCoreDatabaseAsync(app.Lifetime.ApplicationStopping);
    await app.Services.MigrateIdentityDatabaseAsync(app.Lifetime.ApplicationStopping);
    await app.Services.MigrateWarehouseDatabaseAsync(app.Lifetime.ApplicationStopping);
    await app.Services.MigratePurchasingDatabaseAsync(app.Lifetime.ApplicationStopping);
    await app.Services.MigrateSalesDatabaseAsync(app.Lifetime.ApplicationStopping);
}

if (await app.Services.TryRunCommandAsync(args, Console.Out, app.Lifetime.ApplicationStopping))
{
    return;
}

// Malformed requests (e.g. unreadable JSON) throw BadHttpRequestException in Development; keep their 400.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapIdentityEndpoints();
app.MapCoreEndpoints();
app.MapWarehouseEndpoints();
app.MapPurchasingEndpoints();
app.MapSalesEndpoints();

await app.RunAsync();

/// <summary>Entry point; public so integration tests can host the API.</summary>
public partial class Program;
