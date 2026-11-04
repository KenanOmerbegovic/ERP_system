using Erp.Modules.Core;
using Erp.Modules.Identity.Application.Auth;
using Erp.Seeder;
using Erp.Seeder.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Erp.Seeder.Tests;

/// <summary>The seeder end to end on a throwaway PostgreSQL. Needs Docker running.</summary>
public sealed class SeedRunTests : IAsyncLifetime
{
    private const string Password = "seeder test password";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task Seeds_the_company_and_every_user_can_sign_in()
    {
        var (exitCode, output) = await RunAsync(reset: true);

        Assert.True(exitCode == SeedRun.Succeeded, output);
        Assert.Contains("Self-checks passed", output, StringComparison.Ordinal);

        var data = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Small));
        using var host = Host();
        foreach (var user in data.Roles.Select(role => role.User).Prepend(data.Administrator))
        {
            await using var scope = host.Services.CreateAsyncScope();
            var login = await scope.ServiceProvider.GetRequiredService<LoginHandler>()
                .HandleAsync(new Login(user.Email, Password), CancellationToken.None);
            Assert.True(login.IsSuccess, user.Email);
            Assert.Contains(user.Email, output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Running_again_needs_reset()
    {
        Assert.Equal(SeedRun.Succeeded, (await RunAsync(reset: true)).ExitCode);

        var (exitCode, output) = await RunAsync(reset: false);
        Assert.Equal(SeedRun.Failed, exitCode);
        Assert.Contains("already seeded", output, StringComparison.Ordinal);

        Assert.Equal(SeedRun.Succeeded, (await RunAsync(reset: true)).ExitCode);
    }

    [Fact]
    public async Task Reset_creates_a_database_that_does_not_exist_yet()
    {
        var missing = new NpgsqlConnectionStringBuilder(_database.GetConnectionString()) { Database = "erp_not_created_yet" };
        using var host = SeederHost.Build(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{CoreModule.ConnectionStringName}"] = missing.ConnectionString,
        });
        var output = new StringWriter();

        var exitCode = await SeedRun.ExecuteAsync(host, new SeedOptions(SeedSize.Small, Password, Reset: true), output, CancellationToken.None);

        Assert.True(exitCode == SeedRun.Succeeded, output.ToString());
    }

    [Fact]
    public async Task Refuses_a_database_that_is_not_local()
    {
        using var host = SeederHost.Build(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{CoreModule.ConnectionStringName}"] = "Host=db.example.com;Database=erp;Username=erp;Password=x",
        });
        var output = new StringWriter();

        var exitCode = await SeedRun.ExecuteAsync(host, new SeedOptions(SeedSize.Small, Password, Reset: true), output, CancellationToken.None);

        Assert.Equal(SeedRun.Failed, exitCode);
        Assert.Contains("only writes to a local database", output.ToString(), StringComparison.Ordinal);
    }

    private async Task<(int ExitCode, string Output)> RunAsync(bool reset)
    {
        using var host = Host();
        var output = new StringWriter();
        var exitCode = await SeedRun.ExecuteAsync(host, new SeedOptions(SeedSize.Small, Password, reset), output, CancellationToken.None);
        return (exitCode, output.ToString());
    }

    private IHost Host() => SeederHost.Build(new Dictionary<string, string?>
    {
        [$"ConnectionStrings:{CoreModule.ConnectionStringName}"] = _database.GetConnectionString(),
    });
}
