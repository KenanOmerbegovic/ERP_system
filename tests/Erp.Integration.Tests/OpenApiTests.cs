using System.Net;
using System.Text.Json;
using Erp.Integration.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;

namespace Erp.Integration.Tests;

/// <summary>The document the client generates its API code from (also written to /openapi on build).</summary>
[Collection(ApiCollection.Name)]
public sealed class OpenApiTests(ApiFactory factory)
{
    private static readonly string[] Methods = ["get", "put", "post", "delete", "patch"];

    [Fact]
    public async Task Document_describes_every_operation_for_client_generation()
    {
        using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        var response = await development.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        var paths = root.GetProperty("paths").EnumerateObject().Select(path => path.Name).ToList();
        Assert.Contains("/api/auth/login", paths);
        Assert.Contains("/api/auth/refresh", paths);
        Assert.Contains("/api/registration", paths);
        Assert.Contains("/api/me", paths);
        Assert.Contains("/api/companies/{companyId}/articles", paths);

        var operations = root.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Where(member => Methods.Contains(member.Name)))
            .Select(member => member.Value)
            .ToList();
        Assert.NotEmpty(operations);
        Assert.All(operations, operation => Assert.False(string.IsNullOrEmpty(operation.GetProperty("operationId").GetString())));

        var components = root.GetProperty("components");
        Assert.Equal("bearer", components.GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        var delivery = components.GetProperty("schemas").GetProperty("RefreshTokenDelivery").GetProperty("enum");
        Assert.Equal(["Cookie", "Body"], delivery.EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task Document_is_not_served_outside_development()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
