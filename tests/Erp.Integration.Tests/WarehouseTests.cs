using System.Net;
using System.Net.Http.Json;
using Erp.Integration.Tests.TestSupport;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Contracts.UnitsOfMeasure;
using Erp.Modules.Identity.Contracts.Roles;
using Erp.Modules.Identity.Contracts.Users;
using Erp.Modules.Warehouse.Contracts;
using Erp.Modules.Warehouse.Contracts.Numbering;
using Erp.Modules.Warehouse.Contracts.Stock;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Contracts.Warehouses;

namespace Erp.Integration.Tests;

/// <summary>The Warehouse module through the API: Core's articles arrive through the outbox, per-module permissions apply.</summary>
[Collection(ApiCollection.Name)]
public sealed class WarehouseTests(ApiFactory factory)
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task FR_WH_03_issue_beyond_stock_returns_409_with_shortages()
    {
        var (admin, companyId, _) = await factory.RegisterCompanyAsync("4200000000701");
        var warehouseId = await CreateAsync(admin, $"/api/companies/{companyId}/warehouses", new WarehouseRequest("MP1", "Magacin", true));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var kind in Enum.GetValues<StockDocumentKind>())
        {
            await CreateAsync(admin, $"/api/companies/{companyId}/warehouse-number-series", new NumberSeriesRequest(kind, new DateOnly(today.Year - 1, 1, 1), kind + "-", 4));
        }

        var unitId = await CreateAsync(admin, $"/api/companies/{companyId}/units-of-measure", new UnitOfMeasureRequest("kg", "Kilogram"));
        var articleId = await CreateAsync(admin, $"/api/companies/{companyId}/articles",
            new CreateArticleRequest("BR-1", "Brašno T-500", ArticleKind.Goods, ArticleTracking.Lot, unitId, true));

        // The article reaches Warehouse through Core's outbox moments later.
        var receipt = new StockDocumentRequest(StockDocumentKind.Receipt, today, null, warehouseId, null,
            [new StockDocumentLineRequest(articleId, 10m, 1.2m, "L-2026-01", null, null)]);
        var receiptId = await CreateWhenArticleArrivesAsync(admin, $"/api/companies/{companyId}/stock-documents", receipt);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/companies/{companyId}/stock-documents/{receiptId}/confirm", null)).StatusCode);

        var issueId = await CreateAsync(admin, $"/api/companies/{companyId}/stock-documents", new StockDocumentRequest(
            StockDocumentKind.Issue, today, warehouseId, null, null, [new StockDocumentLineRequest(articleId, 12m, null, "L-2026-01", null, null)]));
        var confirm = await admin.PostAsync($"/api/companies/{companyId}/stock-documents/{issueId}/confirm", null);

        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        var problem = (await confirm.Content.ReadFromJsonAsync<StockProblemResponse>(ApiClient.Json))!;
        Assert.Equal("Stock.Insufficient", problem.Code);
        Assert.Equal(2, problem.Shortages!.Count); // the article and its LOT
        Assert.All(problem.Shortages, shortage => Assert.Equal((12m, 10m, "BR-1"), (shortage.Requested, shortage.Available, shortage.ArticleCode)));

        var stock = await (await admin.GetAsync($"/api/companies/{companyId}/stock")).ReadAsync<PagedResponse<StockBalanceResponse>>();
        var balance = Assert.Single(stock.Items);
        Assert.Equal((10m, 12.00m, StockTrackingKind.Lot), (balance.Quantity, balance.Value, balance.Tracking));
        var document = await (await admin.GetAsync($"/api/companies/{companyId}/stock-documents/{receiptId}")).ReadAsync<StockDocumentResponse>();
        Assert.Equal($"Receipt-{today.Year}-0001", document.Number);
    }

    [Fact]
    public async Task Warehouse_endpoints_require_warehouse_permissions()
    {
        var (admin, companyId, _) = await factory.RegisterCompanyAsync("4200000000702");
        var role = await (await admin.PostJsonAsync(
            $"/api/companies/{companyId}/roles",
            new RoleRequest("Pregled zaliha", [WarehousePermissions.StockRead, WarehousePermissions.StockDocumentsRead, CorePermissions.ArticlesRead]))).ReadAsync<CreatedResponse>();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostJsonAsync(
            $"/api/companies/{companyId}/users",
            new CreateUserRequest("zalihe.4200000000702@firma.ba", "Zalihe", ApiClient.Password, [role.Id]))).StatusCode);
        var viewer = factory.CreateBrowserClient();
        viewer.SignIn(await viewer.LoginAsync("zalihe.4200000000702@firma.ba"));

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/companies/{companyId}/stock")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/companies/{companyId}/stock-documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/companies/{companyId}/warehouses")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostJsonAsync(
            $"/api/companies/{companyId}/warehouses", new WarehouseRequest("MP1", "Magacin", true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync(
            $"/api/companies/{companyId}/stock-documents/{Guid.NewGuid()}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostJsonAsync(
            $"/api/companies/{companyId}/warehouse-number-series",
            new NumberSeriesRequest(StockDocumentKind.Receipt, new DateOnly(2026, 1, 1), "P-", 4))).StatusCode);
    }

    [Fact]
    public async Task The_permission_catalog_offers_warehouse_permissions()
    {
        var (admin, _, _) = await factory.RegisterCompanyAsync("4200000000703");

        var permissions = await (await admin.GetAsync("/api/permissions")).ReadAsync<List<string>>();

        Assert.Superset(WarehousePermissions.All.ToHashSet(), permissions.ToHashSet());
    }

    private static async Task<Guid> CreateAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PostJsonAsync(url, body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"POST {url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.ReadAsync<CreatedResponse>()).Id;
    }

    private static async Task<Guid> CreateWhenArticleArrivesAsync(HttpClient client, string url, StockDocumentRequest body)
    {
        var deadline = DateTimeOffset.UtcNow + EventTimeout;
        while (true)
        {
            var response = await client.PostAsJsonAsync(url, body, ApiClient.Json);
            if (response.StatusCode == HttpStatusCode.Created)
            {
                return (await response.ReadAsync<CreatedResponse>()).Id;
            }

            var content = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest && content.Contains("Article.NotFound", StringComparison.Ordinal), content);
            Assert.True(DateTimeOffset.UtcNow < deadline, "The article never reached Warehouse; is the outbox processor running?");
            await Task.Delay(100);
        }
    }
}
