using System.Net;
using System.Net.Http.Json;
using Erp.Integration.Tests.TestSupport;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Contracts.UnitsOfMeasure;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Identity.Contracts.Roles;
using Erp.Modules.Identity.Contracts.Users;
using Erp.Modules.Purchasing.Contracts;
using Erp.Modules.Purchasing.Contracts.Invoices;
using Erp.Modules.Purchasing.Contracts.Numbering;
using Erp.Modules.Purchasing.Contracts.Orders;
using Erp.Modules.Purchasing.Contracts.Receipts;
using Erp.Modules.Warehouse.Contracts.Numbering;
using Erp.Modules.Warehouse.Contracts.Stock;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Contracts.Warehouses;

namespace Erp.Integration.Tests;

/// <summary>Purchasing through the API: suppliers and articles arrive from Core's outbox, receipts move stock in Warehouse.</summary>
[Collection(ApiCollection.Name)]
public sealed class PurchasingTests(ApiFactory factory)
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(15);
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task FR_PUR_01_FR_PUR_02_order_receipt_and_invoice_through_the_api()
    {
        var (admin, companyId, _) = await factory.RegisterCompanyAsync("4200000000801");
        var setup = await SetUpAsync(admin, companyId);

        var order = await CreateAsync(admin, $"/api/companies/{companyId}/purchase-orders", new PurchaseOrderRequest(
            setup.SupplierId, Today, null, setup.WarehouseId, null, [new PurchaseOrderLineRequest(setup.ArticleId, 10m, 2m, null)]));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/companies/{companyId}/purchase-orders/{order}/confirm", null)).StatusCode);
        var orderLine = (await GetAsync<PurchaseOrderResponse>(admin, $"/api/companies/{companyId}/purchase-orders/{order}")).Lines[0];
        Assert.Equal("PDV17", orderLine.VatCode);

        var receipt = await CreateAsync(admin, $"/api/companies/{companyId}/goods-receipts", new GoodsReceiptRequest(
            order, setup.WarehouseId, Today, null, [new GoodsReceiptLineRequest(orderLine.Id, 6m, null, null, null)]));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/companies/{companyId}/goods-receipts/{receipt}/confirm", null)).StatusCode);

        var stock = await GetAsync<PagedResponse<StockBalanceResponse>>(admin, $"/api/companies/{companyId}/stock");
        Assert.Equal((6m, 12.00m), (stock.Items[0].Quantity, stock.Items[0].Value));
        var receiptLine = (await GetAsync<GoodsReceiptResponse>(admin, $"/api/companies/{companyId}/goods-receipts/{receipt}")).Lines[0];

        var invoice = await CreateAsync(admin, $"/api/companies/{companyId}/supplier-invoices", new SupplierInvoiceRequest(
            order, "F-2026-77", Today, Today.AddDays(30), null, [new SupplierInvoiceLineRequest(receiptLine.Id, 6m, 2.1m, null)]));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/companies/{companyId}/supplier-invoices/{invoice}/confirm", null)).StatusCode);
        var registered = await GetAsync<SupplierInvoiceResponse>(admin, $"/api/companies/{companyId}/supplier-invoices/{invoice}");
        Assert.Equal((12.60m, 2.14m, 14.74m, 0.60m), (registered.NetAmount, registered.VatAmount, registered.GrossAmount, registered.PriceDifference));
    }

    [Fact]
    public async Task FR_PUR_01_warehouse_refusal_is_passed_on_as_409_and_the_receipt_stays_a_draft()
    {
        var (admin, companyId, _) = await factory.RegisterCompanyAsync("4200000000802");
        var setup = await SetUpAsync(admin, companyId);
        var order = await CreateAsync(admin, $"/api/companies/{companyId}/purchase-orders", new PurchaseOrderRequest(
            setup.SupplierId, Today, null, setup.WarehouseId, null, [new PurchaseOrderLineRequest(setup.ArticleId, 10m, 2m, null)]));
        await admin.PostAsync($"/api/companies/{companyId}/purchase-orders/{order}/confirm", null);
        var orderLine = (await GetAsync<PurchaseOrderResponse>(admin, $"/api/companies/{companyId}/purchase-orders/{order}")).Lines[0];
        var receipt = await CreateAsync(admin, $"/api/companies/{companyId}/goods-receipts", new GoodsReceiptRequest(
            order, setup.WarehouseId, Today, null, [new GoodsReceiptLineRequest(orderLine.Id, 4m, null, null, null)]));
        await admin.PostAsync($"/api/companies/{companyId}/goods-receipts/{receipt}/confirm", null);

        // Issue the goods in Warehouse, then try to reverse the receipt.
        var issue = await CreateAsync(admin, $"/api/companies/{companyId}/stock-documents", new StockDocumentRequest(
            StockDocumentKind.Issue, Today, setup.WarehouseId, null, null, [new StockDocumentLineRequest(setup.ArticleId, 3m, null, null, null, null)]));
        await admin.PostAsync($"/api/companies/{companyId}/stock-documents/{issue}/confirm", null);
        var storno = await admin.PostAsync($"/api/companies/{companyId}/goods-receipts/{receipt}/storno", null);

        Assert.Equal(HttpStatusCode.Conflict, storno.StatusCode);
        var problem = (await storno.Content.ReadFromJsonAsync<StockProblemResponse>(ApiClient.Json))!;
        Assert.Equal(("Stock.Insufficient", 4m, 1m), (problem.Code, problem.Shortages![0].Requested, problem.Shortages[0].Available));

        // The warehouse receipt belongs to Purchasing: Warehouse refuses to reverse it directly.
        var stockDocumentId = (await GetAsync<GoodsReceiptResponse>(admin, $"/api/companies/{companyId}/goods-receipts/{receipt}")).StockDocumentId;
        var direct = await admin.PostAsync($"/api/companies/{companyId}/stock-documents/{stockDocumentId}/storno", null);
        Assert.Equal(HttpStatusCode.Conflict, direct.StatusCode);
        Assert.Contains("StockDocument.OwnedByAnotherModule", await direct.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Purchasing_endpoints_require_purchasing_permissions()
    {
        var (admin, companyId, _) = await factory.RegisterCompanyAsync("4200000000803");
        var role = await (await admin.PostJsonAsync(
            $"/api/companies/{companyId}/roles", new RoleRequest("Pregled narudžbi", [PurchasingPermissions.OrdersRead]))).ReadAsync<CreatedResponse>();
        await admin.PostJsonAsync($"/api/companies/{companyId}/users",
            new CreateUserRequest("narudzbe.4200000000803@firma.ba", "Narudžbe", ApiClient.Password, [role.Id]));
        var viewer = factory.CreateBrowserClient();
        viewer.SignIn(await viewer.LoginAsync("narudzbe.4200000000803@firma.ba"));

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/companies/{companyId}/purchase-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/companies/{companyId}/goods-receipts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/companies/{companyId}/supplier-invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/companies/{companyId}/purchase-orders/{Guid.NewGuid()}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/companies/{companyId}/vat-rates")).StatusCode);
    }

    private sealed record Setup(Guid SupplierId, Guid ArticleId, Guid WarehouseId);

    /// <summary>VAT rate, number series, a warehouse, and a supplier and an article created in Core that reach the other modules.</summary>
    private static async Task<Setup> SetUpAsync(HttpClient admin, Guid companyId)
    {
        await CreateAsync(admin, $"/api/companies/{companyId}/vat-rates", new VatRateRequest("PDV17", "Opća stopa", 0.17m, new DateOnly(2006, 1, 1)));
        var validFrom = new DateOnly(Today.Year, 1, 1);
        foreach (var kind in Enum.GetValues<PurchasingDocumentKind>())
        {
            await CreateAsync(admin, $"/api/companies/{companyId}/purchasing-number-series", new PurchasingNumberSeriesRequest(kind, validFrom, kind + "-", 4));
        }

        foreach (var kind in Enum.GetValues<StockDocumentKind>())
        {
            await CreateAsync(admin, $"/api/companies/{companyId}/warehouse-number-series", new NumberSeriesRequest(kind, validFrom, kind + "-", 4));
        }

        var warehouseId = await CreateAsync(admin, $"/api/companies/{companyId}/warehouses", new WarehouseRequest("MP1", "Magacin", true));
        var unitId = await CreateAsync(admin, $"/api/companies/{companyId}/units-of-measure", new UnitOfMeasureRequest("kom", "Komad"));
        var articleId = await CreateAsync(admin, $"/api/companies/{companyId}/articles",
            new CreateArticleRequest("A1", "Vijak M8", ArticleKind.Goods, ArticleTracking.None, unitId, "PDV17", true));
        var supplierId = await CreateAsync(admin, $"/api/companies/{companyId}/business-partners",
            new BusinessPartnerRequest("D001", "Dobavljač d.o.o.", "4200000000999", null, "Titova 1", "71000", "Sarajevo", "BA", false, true, true));

        // Wait until Core's outbox delivered both to Purchasing (and the article to Warehouse).
        var deadline = DateTimeOffset.UtcNow + EventTimeout;
        while (true)
        {
            var probe = await admin.PostAsJsonAsync($"/api/companies/{companyId}/purchase-orders", new PurchaseOrderRequest(
                supplierId, Today, null, warehouseId, "probe", [new PurchaseOrderLineRequest(articleId, 1m, 1m, null)]), ApiClient.Json);
            if (probe.StatusCode == HttpStatusCode.Created)
            {
                var probeId = (await probe.ReadAsync<CreatedResponse>()).Id;
                await admin.DeleteAsync($"/api/companies/{companyId}/purchase-orders/{probeId}");
                break;
            }

            Assert.True(DateTimeOffset.UtcNow < deadline, "Core's partner and article never reached Purchasing: " + await probe.Content.ReadAsStringAsync());
            await Task.Delay(100);
        }

        while (true)
        {
            var probe = await admin.PostAsJsonAsync($"/api/companies/{companyId}/stock-documents", new StockDocumentRequest(
                StockDocumentKind.Issue, Today, warehouseId, null, null, [new StockDocumentLineRequest(articleId, 1m, null, null, null, null)]), ApiClient.Json);
            if (probe.StatusCode == HttpStatusCode.Created)
            {
                var probeId = (await probe.ReadAsync<CreatedResponse>()).Id;
                await admin.DeleteAsync($"/api/companies/{companyId}/stock-documents/{probeId}");
                break;
            }

            Assert.True(DateTimeOffset.UtcNow < deadline, "The article never reached Warehouse: " + await probe.Content.ReadAsStringAsync());
            await Task.Delay(100);
        }

        return new Setup(supplierId, articleId, warehouseId);
    }

    private static async Task<Guid> CreateAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PostJsonAsync(url, body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"POST {url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.ReadAsync<CreatedResponse>()).Id;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url) => await (await client.GetAsync(url)).ReadAsync<T>();
}
