using System.Text.Json.Serialization;
using Erp.Localization.BiH.Core;
using Erp.Modules.Core;
using Erp.Modules.Core.Application.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<ITaxIdentifierRules, BiHTaxIdentifierRules>();
builder.Services.AddCoreModule(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.Services.MigrateCoreDatabaseAsync(app.Lifetime.ApplicationStopping);
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

app.MapCoreEndpoints();

app.Run();
