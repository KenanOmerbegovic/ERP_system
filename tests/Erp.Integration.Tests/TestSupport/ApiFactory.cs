using Erp.Modules.Core;
using Erp.Modules.Identity;
using Erp.Modules.Identity.Application.Registration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Erp.Integration.Tests.TestSupport;

/// <summary>
/// The real API on a throwaway PostgreSQL (same image as docker-compose.yml), shared by the
/// tests in <see cref="ApiCollection"/>. Tests isolate themselves with their own companies and emails.
/// Needs Docker running.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AllowedOrigin = "https://client.test";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await Services.MigrateCoreDatabaseAsync(CancellationToken.None);
        await Services.MigrateIdentityDatabaseAsync(CancellationToken.None);
    }

    /// <summary>A client on https so the Secure refresh cookie round-trips; it keeps cookies like a browser.</summary>
    public HttpClient CreateBrowserClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    /// <summary>A server variant sharing this database, e.g. with stricter rate limits.</summary>
    public WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings) =>
        WithWebHostBuilder(builder => Override(builder, settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value)));

    /// <summary>What the operator does with the issue-registration-key command.</summary>
    public async Task<string> IssueRegistrationKeyAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IssueRegistrationKeyHandler>()
            .HandleAsync(new IssueRegistrationKey(), CancellationToken.None);
        return result.Value.Key;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Read while Program registers services, so they must be host settings.
        builder.UseSetting($"ConnectionStrings:{CoreModule.ConnectionStringName}", _database.GetConnectionString());
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);

        Override(builder, new Dictionary<string, string?>
        {
            ["Identity:Jwt:SigningKey"] = "integration-test-signing-key-thirty-two-bytes-plus",
            ["Identity:RateLimiting:PermitLimit"] = "1000",
            ["Messaging:Outbox:PollInterval"] = "00:00:00.100",
        });
    }

    /// <summary>Added last, so these win over appsettings.json; read through options at startup or per request.</summary>
    private static void Override(IWebHostBuilder builder, Dictionary<string, string?> settings) =>
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(settings));
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "API";
}
