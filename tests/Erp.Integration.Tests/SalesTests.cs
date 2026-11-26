using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Integration.Tests.TestSupport;
using Erp.Messaging.Outbox;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Contracts.UnitsOfMeasure;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Identity.Contracts.Roles;
using Erp.Modules.Identity.Contracts.Users;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Contracts.CreditNotes;
using Erp.Modules.Sales.Contracts.Deliveries;
using Erp.Modules.Sales.Contracts.Invoices;
using Erp.Modules.Sales.Contracts.Numbering;
using Erp.Modules.Sales.Contracts.Offers;
using Erp.Modules.Sales.Contracts.Orders;
using Erp.Modules.Sales.Infrastructure;
using Erp.Modules.Warehouse.Contracts.Numbering;
using Erp.Modules.Warehouse.Contracts.Stock;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Contracts.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Integration.Tests;

/// <summary>Sales through the API: customers and articles arrive from Core's outbox, deliveries and returns move stock in Warehouse.</summary>
[Collection(ApiCollection.Name)]
public sealed class SalesTests(ApiFactory factory)
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(15);
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task FR_SAL_full_flow_offer_order_delivery_invoice_credit_note()
    {
        var (admin, companyId, _) = await factory.RegisterCompanyAsync("4200000000901");
        var setup = await SetUpAsync(admin, companyId);
        var api = $"/api/companies/{companyId}";
        await ReceiveStockAsync(admin, companyId, setup, 5m, 8m);

        // FR-SAL-01, FR-SAL-02: offer → sent → accepted → draft order → confirmed.
        var offer = await CreateAsync(admin, $"{api}/offers", new OfferRequest(
            setup.CustomerId, Today, Today.AddDays(10), null, [new SalesLineRequest(setup.ArticleId, 4m, 15m, null), new SalesLineRequest(setup.ServiceId, 1m, 50m, null)]));
        await PostAsync(admin, $"{api}/offers/{offer}/send");
        var order = await CreateAsync(admin, $"{api}/offers/{offer}/accept", new AcceptOfferRequest(Today, setup.WarehouseId));
        await PostAsync(admin, $"{api}/sales-orders/{order}/confirm");
        var orderLines = (await GetAsync<SalesOrderResponse>(admin, $"{api}/sales-orders/{order}")).Lines;

        // FR-WH-03: delivering more than is in stock is refused with the shortages; the note stays a draft.
        // Two of the five pieces leave the warehouse otherwise first, so three are left.
        var tooMuch = await CreateAsync(admin, $"{api}/delivery-notes", new DeliveryNoteRequest(
            order, setup.WarehouseId, Today, null, [new DeliveryNoteLineRequest(orderLines[0].Id, 4m, null, null)]));
        var issueOther = await CreateAsync(admin, $"{api}/stock-documents", new StockDocumentRequest(
            StockDocumentKind.Issue, Today, setup.WarehouseId, null, null, [new StockDocumentLineRequest(setup.ArticleId, 2m, null, null, null, null)]));
        await PostAsync(admin, $"{api}/stock-documents/{issueOther}/confirm");
        var refused = await admin.PostAsync($"{api}/delivery-notes/{tooMuch}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var problem = (await refused.Content.ReadFromJsonAsync<StockProblemResponse>(ApiClient.Json))!;
        Assert.Equal(("Stock.Insufficient", 4m, 3m), (problem.Code, problem.Shortages![0].Requested, problem.Shortages[0].Available));
        Assert.Equal(SalesDocumentState.Draft, (await GetAsync<DeliveryNoteResponse>(admin, $"{api}/delivery-notes/{tooMuch}")).Status);

        // FR-SAL-03: a partial delivery of what is there.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{api}/delivery-notes/{tooMuch}")).StatusCode);
        var delivery = await CreateAsync(admin, $"{api}/delivery-notes", new DeliveryNoteRequest(
            order, setup.WarehouseId, Today, null, [new DeliveryNoteLineRequest(orderLines[0].Id, 3m, null, null)]));
        await PostAsync(admin, $"{api}/delivery-notes/{delivery}/confirm");
        var delivered = await GetAsync<DeliveryNoteResponse>(admin, $"{api}/delivery-notes/{delivery}");
        Assert.Equal((SalesDocumentState.Confirmed, 24m), (delivered.Status, delivered.CostValue));

        // FR-SAL-03, FR-SAL-04: invoice the delivered goods and the service, with PDV.
        var invoiceable = await GetAsync<List<InvoiceableLineResponse>>(admin, $"{api}/sales-invoices/invoiceable-lines?salesOrderId={order}");
        Assert.Equal([(ArticleKind.Goods, 3m), (ArticleKind.Service, 1m)], invoiceable.Select(l => (l.Kind, l.Quantity)));
        var invoice = await CreateAsync(admin, $"{api}/sales-invoices", new SalesInvoiceRequest(
            order, Today, Today.AddDays(15), null,
            [new SalesInvoiceLineRequest(delivered.Lines[0].Id, null, 3m), new SalesInvoiceLineRequest(null, orderLines[1].Id, 1m)]));
        await PostAsync(admin, $"{api}/sales-invoices/{invoice}/confirm");
        var issued = await GetAsync<SalesInvoiceResponse>(admin, $"{api}/sales-invoices/{invoice}");
        Assert.Equal((95m, 16.15m, 111.15m), (issued.NetAmount, issued.VatAmount, issued.GrossAmount));

        // FR-SAL-05: a confirmed invoice cannot be changed; a credit note returns one piece to stock.
        var edit = await admin.PutAsJsonAsync($"{api}/sales-invoices/{invoice}", new SalesInvoiceRequest(
            order, Today, Today.AddDays(15), null, [new SalesInvoiceLineRequest(delivered.Lines[0].Id, null, 1m)]), ApiClient.Json);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        var creditNote = await CreateAsync(admin, $"{api}/credit-notes", new CreditNoteRequest(
            invoice, Today, setup.WarehouseId, "Reklamacija",
            [new CreditNoteLineRequest(issued.Lines[0].Id, CreditNoteLineKind.Return, 1m, null, null), new CreditNoteLineRequest(issued.Lines[1].Id, CreditNoteLineKind.ValueReduction, null, 10m, null)]));
        await PostAsync(admin, $"{api}/credit-notes/{creditNote}/confirm");
        var credited = await GetAsync<CreditNoteResponse>(admin, $"{api}/credit-notes/{creditNote}");
        Assert.Equal((SalesDocumentState.Confirmed, 25m, 4.25m, 8m), (credited.Status, credited.NetAmount, credited.VatAmount, credited.ReturnedCostValue));

        var stock = await GetAsync<PagedResponse<StockBalanceResponse>>(admin, $"{api}/stock");
        Assert.Equal((1m, 8m), (stock.Items[0].Quantity, stock.Items[0].Value));

        // Events for Finance, with the cost of goods sold.
        var goodsDelivered = Assert.Single(await EventsAsync<GoodsDelivered>(companyId));
        Assert.Equal((delivery, 24m), (goodsDelivered.DeliveryNoteId, goodsDelivered.CostValue));
        Assert.Equal(111.15m, Assert.Single(await EventsAsync<InvoiceIssued>(companyId)).GrossAmount);
        var creditNoteIssued = Assert.Single(await EventsAsync<CreditNoteIssued>(companyId));
        Assert.Equal((29.25m, 8m), (creditNoteIssued.GrossAmount, creditNoteIssued.ReturnedCostValue));
        Assert.Single(await EventsAsync<SalesOrderConfirmed>(companyId));
    }

    [Fact]
    public async Task Sales_endpoints_require_sales_permissions()
    {
        var (admin, companyId, _) = await factory.RegisterCompanyAsync("4200000000903");
        var role = await (await admin.PostJsonAsync(
            $"/api/companies/{companyId}/roles", new RoleRequest("Pregled ponuda", [SalesPermissions.OffersRead]))).ReadAsync<CreatedResponse>();
        await admin.PostJsonAsync($"/api/companies/{companyId}/users",
            new CreateUserRequest("ponude.4200000000903@firma.ba", "Ponude", ApiClient.Password, [role.Id]));
        var viewer = factory.CreateBrowserClient();
        viewer.SignIn(await viewer.LoginAsync("ponude.4200000000903@firma.ba"));

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/companies/{companyId}/offers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/companies/{companyId}/offers/{Guid.NewGuid()}/send", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/companies/{companyId}/sales-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/companies/{companyId}/delivery-notes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/companies/{companyId}/sales-invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/companies/{companyId}/credit-notes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/companies/{companyId}/delivery-notes/{Guid.NewGuid()}/confirm", null)).StatusCode);
    }

    private sealed record Setup(Guid CustomerId, Guid ArticleId, Guid ServiceId, Guid WarehouseId);

    /// <summary>VAT rate, number series, a warehouse, and a customer, goods and a service created in Core that reach the other modules.</summary>
    private static async Task<Setup> SetUpAsync(HttpClient admin, Guid companyId)
    {
        var api = $"/api/companies/{companyId}";
        await CreateAsync(admin, $"{api}/vat-rates", new VatRateRequest("PDV17", "Opća stopa", 0.17m, new DateOnly(2006, 1, 1)));
        var validFrom = new DateOnly(Today.Year, 1, 1);
        foreach (var kind in Enum.GetValues<SalesDocumentKind>())
        {
            await CreateAsync(admin, $"{api}/sales-number-series", new SalesNumberSeriesRequest(kind, validFrom, kind + "-", 4));
        }

        foreach (var kind in Enum.GetValues<StockDocumentKind>())
        {
            await CreateAsync(admin, $"{api}/warehouse-number-series", new NumberSeriesRequest(kind, validFrom, kind + "-", 4));
        }

        var warehouseId = await CreateAsync(admin, $"{api}/warehouses", new WarehouseRequest("MP1", "Magacin", true));
        var unitId = await CreateAsync(admin, $"{api}/units-of-measure", new UnitOfMeasureRequest("kom", "Komad"));
        var articleId = await CreateAsync(admin, $"{api}/articles", new CreateArticleRequest("A1", "Vijak M8", ArticleKind.Goods, ArticleTracking.None, unitId, "PDV17", true));
        var serviceId = await CreateAsync(admin, $"{api}/articles", new CreateArticleRequest("U1", "Montaža", ArticleKind.Service, ArticleTracking.None, unitId, "PDV17", true));
        var customerId = await CreateAsync(admin, $"{api}/business-partners",
            new BusinessPartnerRequest("K001", "Kupac d.o.o.", "4200000000998", null, "Titova 2", "71000", "Sarajevo", "BA", true, false, true));

        // Wait until Core's outbox delivered the customer and both articles to Sales (and the goods to Warehouse).
        var deadline = DateTimeOffset.UtcNow + EventTimeout;
        while (true)
        {
            var probe = await admin.PostAsJsonAsync($"{api}/offers", new OfferRequest(
                customerId, Today, null, "probe", [new SalesLineRequest(articleId, 1m, 1m, null), new SalesLineRequest(serviceId, 1m, 1m, null)]), ApiClient.Json);
            if (probe.StatusCode == HttpStatusCode.Created)
            {
                await admin.DeleteAsync($"{api}/offers/{(await probe.ReadAsync<CreatedResponse>()).Id}");
                break;
            }

            Assert.True(DateTimeOffset.UtcNow < deadline, "Core's partner and articles never reached Sales: " + await probe.Content.ReadAsStringAsync());
            await Task.Delay(100);
        }

        while (true)
        {
            var probe = await admin.PostAsJsonAsync($"{api}/stock-documents", new StockDocumentRequest(
                StockDocumentKind.Receipt, Today, null, warehouseId, null, [new StockDocumentLineRequest(articleId, 1m, 1m, null, null, null)]), ApiClient.Json);
            if (probe.StatusCode == HttpStatusCode.Created)
            {
                await admin.DeleteAsync($"{api}/stock-documents/{(await probe.ReadAsync<CreatedResponse>()).Id}");
                break;
            }

            Assert.True(DateTimeOffset.UtcNow < deadline, "The article never reached Warehouse: " + await probe.Content.ReadAsStringAsync());
            await Task.Delay(100);
        }

        return new Setup(customerId, articleId, serviceId, warehouseId);
    }

    private static async Task ReceiveStockAsync(HttpClient admin, Guid companyId, Setup setup, decimal quantity, decimal unitCost)
    {
        var api = $"/api/companies/{companyId}";
        var receipt = await CreateAsync(admin, $"{api}/stock-documents", new StockDocumentRequest(
            StockDocumentKind.Receipt, Today, null, setup.WarehouseId, null, [new StockDocumentLineRequest(setup.ArticleId, quantity, unitCost, null, null, null)]));
        await PostAsync(admin, $"{api}/stock-documents/{receipt}/confirm");
    }

    private async Task<List<T>> EventsAsync<T>(Guid companyId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var type = typeof(T).FullName;
        var messages = await scope.ServiceProvider.GetRequiredService<SalesDbContext>().Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.Type == type).ToListAsync();
        return messages.Select(m => JsonSerializer.Deserialize<T>(m.Content, JsonSerializerOptions.Web)!)
            .Where(e => (Guid)typeof(T).GetProperty("CompanyId")!.GetValue(e)! == companyId)
            .ToList();
    }

    private static async Task PostAsync(HttpClient client, string url)
    {
        var response = await client.PostAsync(url, null);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"POST {url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<Guid> CreateAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PostJsonAsync(url, body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"POST {url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.ReadAsync<CreatedResponse>()).Id;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url) => await (await client.GetAsync(url)).ReadAsync<T>();
}
